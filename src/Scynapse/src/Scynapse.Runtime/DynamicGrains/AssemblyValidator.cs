using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Scynapse.Serialization.Configuration;

namespace Scynapse.Runtime.DynamicGrains;

/// <summary>
/// Validates that an assembly contains required Scynapse-generated code.
/// </summary>
internal sealed class AssemblyValidator
{
    /// <summary>
    /// Validates a plugin assembly set (multiple assemblies) for required Scynapse code.
    /// Supports the split grain pattern where interfaces, implementations, and codegen
    /// can be distributed across multiple assemblies.
    /// </summary>
    /// <param name="pluginSet">The plugin assembly set to validate</param>
    /// <returns>Validation result with aggregated metadata from all assemblies</returns>
    public ValidationResult ValidatePluginSet(PluginAssemblySet pluginSet)
    {
        if (pluginSet == null)
        {
            throw new ArgumentNullException(nameof(pluginSet));
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        var allGrainInterfaces = new List<Type>();
        var allGrainClasses = new List<Type>();
        var allSerializers = new List<Type>();
        var allCopiers = new List<Type>();
        var allProxies = new List<Type>();

        var hasApplicationPart = false;
        var hasManifestProvider = false;

        // Validate each assembly in the plugin set
        foreach (var assembly in pluginSet.AllAssemblies)
        {
            try
            {
                // Check for ApplicationPartAttribute
                if (assembly.IsDefined(typeof(ApplicationPartAttribute)))
                {
                    hasApplicationPart = true;
                }

                // Check for TypeManifestProviderAttribute
                if (assembly.GetCustomAttributes<TypeManifestProviderAttribute>().Any())
                {
                    hasManifestProvider = true;
                }

                // Find grain types
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IGrain).IsAssignableFrom(type))
                    {
                        if (type.IsInterface)
                        {
                            allGrainInterfaces.Add(type);
                        }
                        else if (type.IsClass && !type.IsAbstract)
                        {
                            allGrainClasses.Add(type);
                        }
                    }
                }

                // Find generated types
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Namespace?.Contains("ScynapseCodeGen") == true ||
                        type.GetCustomAttribute<GeneratedCodeAttribute>()?.Tool?.Contains("Scynapse") == true)
                    {
                        // Heuristics to identify generated types
                        if (type.Name.StartsWith("Codec_") ||
                            type.GetInterfaces().Any(i => i.Name.Contains("IFieldCodec") || i.Name.Contains("IBaseCodec")))
                        {
                            allSerializers.Add(type);
                        }
                        else if (type.Name.StartsWith("Copier_") ||
                                 type.GetInterfaces().Any(i => i.Name.Contains("IDeepCopier")))
                        {
                            allCopiers.Add(type);
                        }
                        else if (type.Name.StartsWith("Proxy_") ||
                                 typeof(GrainReference).IsAssignableFrom(type))
                        {
                            allProxies.Add(type);
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                errors.Add($"Failed to load types from assembly '{assembly.GetName().Name}': {ex.Message}");
                foreach (var loaderEx in ex.LoaderExceptions.Where(e => e != null).Take(5))
                {
                    errors.Add($"  Loader exception: {loaderEx.Message}");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Error scanning assembly '{assembly.GetName().Name}': {ex.Message}");
            }
        }

        // Validation: must have ApplicationPart attribute in at least one assembly
        if (!hasApplicationPart)
        {
            var errorMessage = $"Plugin assembly set is missing [ApplicationPart] attribute.\n" +
                              $"  Assemblies checked: {string.Join(", ", pluginSet.AllAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  At least one assembly must be compiled with Scynapse.Sdk.";
            errors.Add(errorMessage);
        }

        // Validation: must have TypeManifestProvider in at least one assembly
        if (!hasManifestProvider)
        {
            var errorMessage = $"Plugin assembly set is missing [TypeManifestProvider] attribute. No Scynapse code generation was detected.\n" +
                              $"  Assemblies checked: {string.Join(", ", pluginSet.AllAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  This indicates that none of the assemblies in this plugin were built with Scynapse code generation enabled.";
            errors.Add(errorMessage);
        }

        // Warning: no grain types found
        if (allGrainClasses.Count == 0 && allGrainInterfaces.Count == 0)
        {
            warnings.Add($"Plugin assembly set contains no grain types or interfaces.");
        }

        var hasGeneratedCode = allSerializers.Count > 0 || allCopiers.Count > 0 || allProxies.Count > 0;

        // Validation: if we have grain classes, we must have generated code somewhere
        if (allGrainClasses.Count > 0 && !hasGeneratedCode)
        {
            var errorMessage = $"Plugin assembly set contains {allGrainClasses.Count} grain type(s) but no Scynapse generated code was found.\n" +
                              $"  Assemblies checked: {string.Join(", ", pluginSet.AllAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  Grain classes found: {string.Join(", ", allGrainClasses.Select(t => $"{t.FullName} (in {t.Assembly.GetName().Name})"))}\n" +
                              $"  Grain interfaces found: {string.Join(", ", allGrainInterfaces.Select(t => $"{t.FullName} (in {t.Assembly.GetName().Name})"))}\n" +
                              $"  Interface assemblies: {string.Join(", ", pluginSet.InterfaceAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  Implementation assemblies: {string.Join(", ", pluginSet.ImplementationAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  Codegen assemblies: {string.Join(", ", pluginSet.CodegenAssemblies.Select(a => a.GetName().Name))}\n" +
                              $"  Serializers: {allSerializers.Count}, Copiers: {allCopiers.Count}, Proxies: {allProxies.Count}\n" +
                              $"Ensure at least one assembly in the plugin was compiled with Scynapse.Sdk and code generation succeeded.";
            errors.Add(errorMessage);
        }

        var metadata = new AssemblyLoadMetadata
        {
            GrainInterfaces = allGrainInterfaces,
            GrainClasses = allGrainClasses,
            Serializers = allSerializers,
            Copiers = allCopiers,
            Proxies = allProxies,
            HasGeneratedCode = hasGeneratedCode
        };

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Validates that an assembly has the required Scynapse-generated code and metadata.
    /// </summary>
    /// <param name="assembly">The assembly to validate</param>
    /// <returns>Validation result</returns>
    public ValidationResult Validate(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for ApplicationPartAttribute
        var hasApplicationPart = assembly.IsDefined(typeof(ApplicationPartAttribute));
        if (!hasApplicationPart)
        {
            errors.Add($"Assembly '{assembly.GetName().Name}' is missing [ApplicationPart] attribute. " +
                      "The assembly must be compiled with Scynapse.Sdk.");
        }

        // Check for TypeManifestProviderAttribute
        var manifestProviderAttrs = assembly.GetCustomAttributes<TypeManifestProviderAttribute>().ToList();
        if (manifestProviderAttrs.Count == 0)
        {
            errors.Add($"Assembly '{assembly.GetName().Name}' is missing [TypeManifestProvider] attribute. " +
                      "No Scynapse code generation was detected.");
        }

        // Find grain types
        var grainTypes = new List<Type>();
        var grainInterfaces = new List<Type>();

        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IGrain).IsAssignableFrom(type))
                {
                    if (type.IsInterface)
                    {
                        grainInterfaces.Add(type);
                    }
                    else if (type.IsClass && !type.IsAbstract)
                    {
                        grainTypes.Add(type);
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            errors.Add($"Failed to load types from assembly '{assembly.GetName().Name}': {ex.Message}");
            // Add loader exceptions
            foreach (var loaderEx in ex.LoaderExceptions.Where(e => e != null).Take(5))
            {
                errors.Add($"  Loader exception: {loaderEx.Message}");
            }
        }

        if (grainTypes.Count == 0 && grainInterfaces.Count == 0)
        {
            warnings.Add($"Assembly '{assembly.GetName().Name}' contains no grain types or interfaces.");
        }

        // Find generated types
        var serializers = new List<Type>();
        var copiers = new List<Type>();
        var proxies = new List<Type>();

        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.Namespace?.Contains("ScynapseCodeGen") == true ||
                    type.GetCustomAttribute<GeneratedCodeAttribute>()?.Tool?.Contains("Scynapse") == true)
                {
                    // Heuristics to identify generated types
                    if (type.Name.StartsWith("Codec_") ||
                        type.GetInterfaces().Any(i => i.Name.Contains("IFieldCodec") || i.Name.Contains("IBaseCodec")))
                    {
                        serializers.Add(type);
                    }
                    else if (type.Name.StartsWith("Copier_") ||
                             type.GetInterfaces().Any(i => i.Name.Contains("IDeepCopier")))
                    {
                        copiers.Add(type);
                    }
                    else if (type.Name.StartsWith("Proxy_") ||
                             typeof(GrainReference).IsAssignableFrom(type))
                    {
                        proxies.Add(type);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Error scanning for generated types: {ex.Message}");
        }

        var hasGeneratedCode = serializers.Count > 0 || copiers.Count > 0 || proxies.Count > 0;

        if (grainTypes.Count > 0 && !hasGeneratedCode)
        {
            errors.Add($"Assembly '{assembly.GetName().Name}' contains grain types but no generated code was found. " +
                      "Ensure the assembly was compiled with Scynapse.Sdk and code generation succeeded.");
        }

        var metadata = new AssemblyLoadMetadata
        {
            GrainInterfaces = grainInterfaces,
            GrainClasses = grainTypes,
            Serializers = serializers,
            Copiers = copiers,
            Proxies = proxies,
            HasGeneratedCode = hasGeneratedCode
        };

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Metadata = metadata
        };
    }
}

/// <summary>
/// Result of assembly validation.
/// </summary>
internal sealed class ValidationResult
{
    /// <summary>
    /// Whether the assembly passed validation.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors (empty if valid).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Metadata extracted from the assembly.
    /// </summary>
    public AssemblyLoadMetadata Metadata { get; init; }
}
