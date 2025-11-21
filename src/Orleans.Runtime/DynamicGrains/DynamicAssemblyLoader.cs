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
    private readonly SemaphoreSlim _loadLock = new(1, 1);

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
            try
            {
                // Load the assembly
                // Note: This loads into the default AssemblyLoadContext
                // For isolation and unloading support, we would need to create a custom AssemblyLoadContext
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load assembly from {AssemblyPath}", assemblyPath);
                return (null, null, new List<string> { $"Failed to load assembly: {ex.Message}" });
            }

            // Validate the assembly
            var validation = _validator.Validate(assembly);
            if (!validation.IsValid)
            {
                _logger.LogError("Assembly {AssemblyPath} failed validation: {Errors}",
                    assemblyPath, string.Join("; ", validation.Errors));
                return (null, null, validation.Errors.ToList());
            }

            // Log warnings
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Assembly {AssemblyPath}: {Warning}", assemblyPath, warning);
            }

            // Track loaded assembly
            _loadedAssemblies[assemblyPath] = assembly;

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
}
