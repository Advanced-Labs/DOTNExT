using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;
using Orleans.Metadata;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Manages dynamic loading of grain assemblies at runtime.
/// </summary>
internal sealed class DynamicAssemblyLoader
{
    private readonly AssemblyValidator _validator;
    private readonly ILogger<DynamicAssemblyLoader> _logger;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new();
    private readonly ConcurrentDictionary<string, AssemblyLoadMetadata> _assemblyMetadata = new();
    private readonly ConcurrentDictionary<string, DynamicPluginAssemblySet> _pluginSets = new();
    private readonly ConcurrentDictionary<string, PluginLoader> _pluginLoaders = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Type[] _cachedSharedTypes;

    public DynamicAssemblyLoader(
        AssemblyValidator validator,
        ILogger<DynamicAssemblyLoader> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads an assembly from the specified path.
    /// </summary>
    public async Task<(Assembly Assembly, AssemblyLoadMetadata Metadata, List<string> Errors)> LoadAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentNullException(nameof(assemblyPath));
        }

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}", assemblyPath);
        }

        // Normalize path
        assemblyPath = Path.GetFullPath(assemblyPath);

        // Check if already loaded
        if (_loadedAssemblies.TryGetValue(assemblyPath, out var existing))
        {
            _logger.LogWarning("Assembly {AssemblyPath} is already loaded", assemblyPath);
            var validationResult = _validator.Validate(existing);
            return (existing, validationResult.Metadata, new List<string> { "Assembly already loaded" });
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_loadedAssemblies.TryGetValue(assemblyPath, out existing))
            {
                var validationResult = _validator.Validate(existing);
                return (existing, validationResult.Metadata, new List<string> { "Assembly already loaded" });
            }

            _logger.LogInformation("Loading grain assembly from {AssemblyPath}", assemblyPath);

            Assembly assembly;
            PluginLoader pluginLoader;
            try
            {
                // Get or initialize shared types for plugin isolation
                var sharedTypes = GetOrCreateSharedTypes();

                // Load the assembly using McMaster.NETCore.Plugins for proper isolation and unloading
                // This creates a collectible AssemblyLoadContext that enables runtime unloading
                pluginLoader = PluginLoader.CreateFromAssemblyFile(
                    assemblyPath,
                    config =>
                    {
                        // Share Orleans types between host and plugin to avoid type identity issues
                        config.PreferSharedTypes = true;

                        // Enable unloading support - critical for runtime grain replacement
                        config.IsUnloadable = true;

                        // Configure explicit shared types from Orleans runtime
                        foreach (var sharedType in sharedTypes)
                        {
                            config.SharedAssemblies.Add(sharedType.Assembly.GetName());
                        }
                    });

                assembly = pluginLoader.LoadDefaultAssembly();

                _logger.LogDebug(
                    "Loaded assembly {AssemblyName} using MDCP PluginLoader (IsCollectible: {IsCollectible})",
                    assembly.GetName().Name,
                    AssemblyLoadContext.GetLoadContext(assembly)?.IsCollectible ?? false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load assembly from {AssemblyPath}", assemblyPath);
                return (null, null, new List<string> { $"Failed to load assembly: {ex.Message}" });
            }

            // Get the AssemblyLoadContext for the loaded assembly
            var loadContext = AssemblyLoadContext.GetLoadContext(assembly);

            // Create plugin assembly set - discover all related assemblies in the same ALC
            var pluginSet = DynamicPluginAssemblySet.FromAssemblyLoadContext(
                assembly,
                loadContext,
                assemblyPath);

            _logger.LogInformation(
                "Discovered plugin assembly set for {RootAssembly}:\n" +
                "  Total assemblies: {TotalAssemblies} [{AllAssemblyNames}]\n" +
                "  Interface assemblies: {InterfaceAssemblies} [{InterfaceAssemblyNames}]\n" +
                "  Implementation assemblies: {ImplementationAssemblies} [{ImplementationAssemblyNames}]\n" +
                "  Codegen assemblies: {CodegenAssemblies} [{CodegenAssemblyNames}]",
                assembly.GetName().Name,
                pluginSet.AllAssemblies.Count,
                string.Join(", ", pluginSet.AllAssemblies.Select(a => a.GetName().Name)),
                pluginSet.InterfaceAssemblies.Count,
                string.Join(", ", pluginSet.InterfaceAssemblies.Select(a => a.GetName().Name)),
                pluginSet.ImplementationAssemblies.Count,
                string.Join(", ", pluginSet.ImplementationAssemblies.Select(a => a.GetName().Name)),
                pluginSet.CodegenAssemblies.Count,
                string.Join(", ", pluginSet.CodegenAssemblies.Select(a => a.GetName().Name)));

            // Validate the plugin set (multiple assemblies)
            var validation = _validator.ValidatePluginSet(pluginSet);
            if (!validation.IsValid)
            {
                _logger.LogError("Plugin assembly set {AssemblyPath} failed validation: {Errors}",
                    assemblyPath, string.Join("; ", validation.Errors));
                return (null, null, validation.Errors.ToList());
            }

            // Log warnings
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Plugin assembly set {AssemblyPath}: {Warning}", assemblyPath, warning);
            }

            // Track loaded assembly, metadata, plugin set, and MDCP loader
            _loadedAssemblies[assemblyPath] = assembly;
            _assemblyMetadata[assemblyPath] = validation.Metadata;
            _pluginSets[assemblyPath] = pluginSet;
            _pluginLoaders[assemblyPath] = pluginLoader;

            _logger.LogInformation(
                "Successfully loaded assembly {AssemblyName} with {GrainCount} grain classes and {InterfaceCount} interfaces",
                assembly.GetName().Name,
                validation.Metadata.GrainClasses.Count,
                validation.Metadata.GrainInterfaces.Count);

            return (assembly, validation.Metadata, new List<string>());
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Loads an assembly and returns the complete plugin assembly set.
    /// This method discovers all Orleans-relevant assemblies in the same AssemblyLoadContext,
    /// supporting the split grain pattern where interfaces, implementations, and codegen
    /// can be in separate assemblies.
    /// </summary>
    public async Task<(DynamicPluginAssemblySet PluginSet, AssemblyLoadMetadata Metadata, List<string> Errors)> LoadPluginAssemblySetAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        // First, load the assembly using existing method
        var (assembly, metadata, errors) = await LoadAssemblyAsync(assemblyPath, cancellationToken);

        if (errors.Count > 0 || assembly == null)
        {
            return (null, metadata, errors);
        }

        // Get the plugin set that was created during loading
        if (_pluginSets.TryGetValue(Path.GetFullPath(assemblyPath), out var pluginSet))
        {
            return (pluginSet, metadata, errors);
        }

        // Fallback: create a single-assembly plugin set
        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
        var fallbackSet = DynamicPluginAssemblySet.ForSingleAssembly(assembly, loadContext, assemblyPath);
        return (fallbackSet, metadata, errors);
    }

    /// <summary>
    /// Gets the plugin assembly set for a loaded assembly.
    /// </summary>
    public DynamicPluginAssemblySet GetPluginAssemblySet(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return _pluginSets.TryGetValue(assemblyPath, out var pluginSet) ? pluginSet : null;
    }

    /// <summary>
    /// Unloads an assembly and releases its collectible AssemblyLoadContext via MDCP PluginLoader.Dispose().
    /// </summary>
    public async Task<bool> UnloadAssemblyAsync(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentNullException(nameof(assemblyPath));
        }

        assemblyPath = Path.GetFullPath(assemblyPath);

        await _loadLock.WaitAsync();
        try
        {
            // First check if we have the MDCP loader for this assembly
            if (!_pluginLoaders.TryRemove(assemblyPath, out var pluginLoader))
            {
                _logger.LogWarning("Assembly {AssemblyPath} not found or not loaded via MDCP", assemblyPath);
                return false;
            }

            // Remove from all tracking dictionaries
            _pluginSets.TryRemove(assemblyPath, out _);
            _loadedAssemblies.TryRemove(assemblyPath, out _);
            _assemblyMetadata.TryRemove(assemblyPath, out _);

            _logger.LogInformation("Unloading assembly from {AssemblyPath} via MDCP PluginLoader.Dispose()", assemblyPath);

            // Dispose the MDCP PluginLoader - this triggers AssemblyLoadContext.Unload()
            // MDCP handles the collectible ALC lifecycle internally
            pluginLoader.Dispose();

            // Force garbage collection to reclaim memory
            // Multiple cycles increase likelihood of collection
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
            }

            _logger.LogInformation("Assembly {AssemblyPath} unloaded via MDCP and memory collection triggered", assemblyPath);
            return true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets information about a loaded assembly.
    /// </summary>
    public (Assembly Assembly, AssemblyLoadMetadata Metadata) GetLoadedAssemblyInfo(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);

        if (_loadedAssemblies.TryGetValue(assemblyPath, out var assembly) &&
            _assemblyMetadata.TryGetValue(assemblyPath, out var metadata))
        {
            return (assembly, metadata);
        }

        return (null, null);
    }

    /// <summary>
    /// Checks if an assembly is currently loaded.
    /// </summary>
    public bool IsAssemblyLoaded(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return _loadedAssemblies.ContainsKey(assemblyPath);
    }

    /// <summary>
    /// Checks if an assembly was loaded via MDCP PluginLoader and can be unloaded.
    /// Returns false for statically loaded assemblies that cannot be unloaded.
    /// </summary>
    public bool IsAssemblyUnloadable(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return _pluginLoaders.ContainsKey(assemblyPath);
    }

    /// <summary>
    /// Rescans all loaded assemblies in the current AppDomain.
    /// </summary>
    public IEnumerable<Assembly> RescanLoadedAssemblies()
    {
        var newAssemblies = new List<Assembly>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Skip system assemblies
            if (assembly.IsDynamic || assembly.ReflectionOnly)
            {
                continue;
            }

            try
            {
                // Check if it has ApplicationPartAttribute
                if (!assembly.IsDefined(typeof(ApplicationPartAttribute)))
                {
                    continue;
                }

                // Check if we've already processed this assembly
                var location = assembly.Location;
                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                if (_loadedAssemblies.ContainsKey(location))
                {
                    continue;
                }

                // Validate
                var validation = _validator.Validate(assembly);
                if (validation.IsValid && validation.Metadata.HasGeneratedCode)
                {
                    _loadedAssemblies[location] = assembly;
                    newAssemblies.Add(assembly);

                    _logger.LogInformation(
                        "Discovered new grain assembly {AssemblyName} during rescan",
                        assembly.GetName().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning assembly {AssemblyName}", assembly.GetName().Name);
            }
        }

        return newAssemblies;
    }

    /// <summary>
    /// Gets all loaded assemblies that have been validated.
    /// </summary>
    public IEnumerable<Assembly> GetLoadedAssemblies()
    {
        return _loadedAssemblies.Values;
    }

    /// <summary>
    /// Gets all loaded assembly paths.
    /// </summary>
    public IEnumerable<string> GetLoadedAssemblyPaths()
    {
        return _loadedAssemblies.Keys;
    }

    /// <summary>
    /// Gets or creates the cached list of shared types for MDCP plugin loading.
    /// Caching prevents expensive reflection on every assembly load.
    /// </summary>
    private Type[] GetOrCreateSharedTypes()
    {
        if (_cachedSharedTypes != null)
        {
            return _cachedSharedTypes;
        }

        _cachedSharedTypes = GetOrleansSharedTypes();
        return _cachedSharedTypes;
    }

    /// <summary>
    /// Returns the comprehensive list of Orleans types that should be shared across plugin boundaries.
    /// Uses reflection to automatically discover all Orleans types from loaded assemblies,
    /// eliminating the need for manual maintenance and avoiding circular dependency issues.
    /// </summary>
    private Type[] GetOrleansSharedTypes()
    {
        var sharedTypes = new List<Type>();

        // Scan all currently loaded assemblies that belong to Orleans
        var orleansAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name != null &&
                       (name.StartsWith("Orleans", StringComparison.Ordinal) ||
                        name.StartsWith("Microsoft.Orleans", StringComparison.Ordinal));
            })
            .ToList();

        _logger.LogDebug(
            "Scanning {AssemblyCount} Orleans assemblies for shared types",
            orleansAssemblies.Count);

        foreach (var assembly in orleansAssemblies)
        {
            try
            {
                // Get all exported (public) types from Orleans namespaces
                var types = assembly.GetExportedTypes()
                    .Where(t => t.Namespace?.StartsWith("Orleans", StringComparison.Ordinal) == true)
                    .Where(t =>
                        // Include interfaces (IGrain, IGrainFactory, IRemindable, etc.)
                        t.IsInterface ||
                        // Include abstract classes (Grain base class, etc.)
                        (t.IsClass && t.IsAbstract) ||
                        // Include attributes
                        typeof(Attribute).IsAssignableFrom(t) ||
                        // Include value types/structs (GrainId, SiloAddress, etc.)
                        t.IsValueType ||
                        // Include enums (DeactivationReasonCode, etc.)
                        t.IsEnum
                    )
                    .ToList();

                sharedTypes.AddRange(types);

                _logger.LogTrace(
                    "Found {TypeCount} shared types in assembly {AssemblyName}",
                    types.Count,
                    assembly.GetName().Name);
            }
            catch (Exception ex)
            {
                // Log but continue - some assemblies might fail to load types
                _logger.LogWarning(ex,
                    "Failed to get types from assembly {AssemblyName}",
                    assembly.GetName().Name);
            }
        }

        // Also add common .NET types that grains typically use
        var commonNetTypes = new[]
        {
            typeof(Task),
            typeof(Task<>),
            typeof(ValueTask),
            typeof(ValueTask<>),
            typeof(CancellationToken),
            typeof(IServiceProvider),
            typeof(IAsyncEnumerable<>),
            typeof(IAsyncEnumerator<>),
            typeof(IAsyncDisposable),
            typeof(IEnumerable<>),
            typeof(ICollection<>),
            typeof(IList<>),
            typeof(List<>),
            typeof(Dictionary<,>),
            typeof(IReadOnlyCollection<>),
            typeof(IReadOnlyList<>),
            typeof(IReadOnlyDictionary<,>),
            typeof(Nullable<>),
            typeof(Attribute),
            typeof(Exception),
        };

        sharedTypes.AddRange(commonNetTypes);

        var distinctTypes = sharedTypes.Distinct().ToArray();

        _logger.LogInformation(
            "Discovered {TypeCount} distinct shared types for plugin loading",
            distinctTypes.Length);

        return distinctTypes;
    }
}
