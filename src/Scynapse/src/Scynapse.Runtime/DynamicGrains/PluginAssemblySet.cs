using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Scynapse.Serialization.Configuration;

namespace Scynapse.Runtime.DynamicGrains;

/// <summary>
/// Represents a grain plugin composed of multiple assemblies within a dedicated AssemblyLoadContext.
/// Supports the split grain pattern where grain interfaces, implementations, and codegen
/// can be distributed across multiple assemblies.
/// </summary>
internal sealed class PluginAssemblySet
{
    /// <summary>
    /// The root assembly that was explicitly loaded (entry point to the plugin).
    /// </summary>
    public Assembly RootAssembly { get; init; }

    /// <summary>
    /// All assemblies in the plugin's AssemblyLoadContext that are Scynapse-relevant.
    /// </summary>
    public IReadOnlyList<Assembly> AllAssemblies { get; init; }

    /// <summary>
    /// Assemblies containing grain interfaces (types implementing IGrain that are interfaces).
    /// </summary>
    public IReadOnlyList<Assembly> InterfaceAssemblies { get; init; }

    /// <summary>
    /// Assemblies containing grain implementations (concrete classes extending Grain).
    /// </summary>
    public IReadOnlyList<Assembly> ImplementationAssemblies { get; init; }

    /// <summary>
    /// Assemblies containing Scynapse-generated code (serializers, copiers, proxies, manifests).
    /// </summary>
    public IReadOnlyList<Assembly> CodegenAssemblies { get; init; }

    /// <summary>
    /// The AssemblyLoadContext that hosts this plugin.
    /// </summary>
    public AssemblyLoadContext LoadContext { get; init; }

    /// <summary>
    /// The original assembly path provided to the loader.
    /// </summary>
    public string AssemblyPath { get; init; }

    /// <summary>
    /// Creates a plugin assembly set for a single assembly (backward compatibility).
    /// </summary>
    public static PluginAssemblySet ForSingleAssembly(Assembly assembly, AssemblyLoadContext loadContext, string assemblyPath)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var isInterfaceAssembly = HasGrainInterfaces(assembly);
        var isImplementationAssembly = HasGrainImplementations(assembly);
        var isCodegenAssembly = HasScynapseCodegen(assembly);

        return new PluginAssemblySet
        {
            RootAssembly = assembly,
            AllAssemblies = new[] { assembly },
            InterfaceAssemblies = isInterfaceAssembly ? new[] { assembly } : Array.Empty<Assembly>(),
            ImplementationAssemblies = isImplementationAssembly ? new[] { assembly } : Array.Empty<Assembly>(),
            CodegenAssemblies = isCodegenAssembly ? new[] { assembly } : Array.Empty<Assembly>(),
            LoadContext = loadContext,
            AssemblyPath = assemblyPath
        };
    }

    /// <summary>
    /// Creates a plugin assembly set from all Scynapse-relevant assemblies in an AssemblyLoadContext.
    /// </summary>
    public static PluginAssemblySet FromAssemblyLoadContext(
        Assembly rootAssembly,
        AssemblyLoadContext loadContext,
        string assemblyPath)
    {
        if (rootAssembly == null)
        {
            throw new ArgumentNullException(nameof(rootAssembly));
        }

        if (loadContext == null)
        {
            throw new ArgumentNullException(nameof(loadContext));
        }

        // Force load all referenced assemblies to ensure dependencies are loaded
        // This is critical for split-assembly pattern where interfaces are in a dependency
        LoadReferencedAssemblies(rootAssembly, loadContext);

        // Get all assemblies loaded in this context
        var candidateAssemblies = loadContext.Assemblies
            .Where(IsPluginCandidateAssembly)
            .ToList();

        // Ensure root assembly is included even if it doesn't pass the candidate filter
        if (!candidateAssemblies.Contains(rootAssembly))
        {
            candidateAssemblies.Add(rootAssembly);
        }

        // Classify assemblies by their role
        var interfaceAssemblies = candidateAssemblies.Where(HasGrainInterfaces).ToList();
        var implementationAssemblies = candidateAssemblies.Where(HasGrainImplementations).ToList();
        var codegenAssemblies = candidateAssemblies.Where(HasScynapseCodegen).ToList();

        return new PluginAssemblySet
        {
            RootAssembly = rootAssembly,
            AllAssemblies = candidateAssemblies,
            InterfaceAssemblies = interfaceAssemblies,
            ImplementationAssemblies = implementationAssemblies,
            CodegenAssemblies = codegenAssemblies,
            LoadContext = loadContext,
            AssemblyPath = assemblyPath
        };
    }

    /// <summary>
    /// Checks if an assembly is a candidate for the plugin (not a system/framework assembly).
    /// </summary>
    private static bool IsPluginCandidateAssembly(Assembly assembly)
    {
        if (assembly == null || assembly.IsDynamic)
        {
            return false;
        }

        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Exclude common system/framework assemblies
        if (name.StartsWith("System.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            name.Equals("System", StringComparison.Ordinal) ||
            name.Equals("netstandard", StringComparison.Ordinal) ||
            name.Equals("mscorlib", StringComparison.Ordinal))
        {
            return false;
        }

        // Include if it references Scynapse assemblies or contains Scynapse types
        return ReferencesScynapse(assembly) || HasScynapseTypes(assembly);
    }

    /// <summary>
    /// Checks if an assembly references Scynapse assemblies.
    /// </summary>
    private static bool ReferencesScynapse(Assembly assembly)
    {
        try
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            return referencedAssemblies.Any(a =>
                a.Name?.StartsWith("Scynapse", StringComparison.Ordinal) == true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an assembly contains any Scynapse types (grains, interfaces, generated code).
    /// </summary>
    private static bool HasScynapseTypes(Assembly assembly)
    {
        return HasGrainInterfaces(assembly) ||
               HasGrainImplementations(assembly) ||
               HasScynapseCodegen(assembly);
    }

    /// <summary>
    /// Checks if an assembly contains grain interfaces.
    /// </summary>
    private static bool HasGrainInterfaces(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Any(t => t.IsInterface && typeof(IGrain).IsAssignableFrom(t));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an assembly contains grain implementations.
    /// </summary>
    private static bool HasGrainImplementations(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Any(t => t.IsClass &&
                         !t.IsAbstract &&
                         typeof(Grain).IsAssignableFrom(t) &&
                         typeof(IGrain).IsAssignableFrom(t));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an assembly contains Scynapse-generated code.
    /// </summary>
    private static bool HasScynapseCodegen(Assembly assembly)
    {
        try
        {
            // Check for TypeManifestProvider attribute (indicates codegen)
            if (assembly.GetCustomAttributes<TypeManifestProviderAttribute>().Any())
            {
                return true;
            }

            // Check for types in ScynapseCodeGen namespace
            return assembly.GetTypes()
                .Any(t => t.Namespace?.Contains("ScynapseCodeGen", StringComparison.Ordinal) == true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively loads all referenced assemblies to ensure dependencies are loaded into the context.
    /// This is critical for split-assembly patterns where interfaces may be in dependency assemblies.
    /// </summary>
    private static void LoadReferencedAssemblies(Assembly assembly, AssemblyLoadContext loadContext)
    {
        try
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var referencedAssembly in referencedAssemblies)
            {
                try
                {
                    // Skip if already loaded
                    if (loadContext.Assemblies.Any(a => a.GetName().Name == referencedAssembly.Name))
                    {
                        continue;
                    }

                    // Try to load the referenced assembly into this context
                    var loaded = loadContext.LoadFromAssemblyName(referencedAssembly);

                    // Recursively load its dependencies
                    LoadReferencedAssemblies(loaded, loadContext);
                }
                catch
                {
                    // Ignore load failures for individual references
                    // They might be system assemblies or unavailable dependencies
                }
            }
        }
        catch
        {
            // Ignore failures to enumerate references
        }
    }
}
