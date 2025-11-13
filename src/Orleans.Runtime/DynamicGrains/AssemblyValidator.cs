using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orleans.Serialization.Configuration;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Validates that an assembly contains required Orleans-generated code.
/// </summary>
internal sealed class AssemblyValidator
{
    /// <summary>
    /// Validates that an assembly has the required Orleans-generated code and metadata.
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
                      "The assembly must be compiled with Orleans.Sdk.");
        }

        // Check for TypeManifestProviderAttribute
        var manifestProviderAttrs = assembly.GetCustomAttributes<TypeManifestProviderAttribute>().ToList();
        if (manifestProviderAttrs.Count == 0)
        {
            errors.Add($"Assembly '{assembly.GetName().Name}' is missing [TypeManifestProvider] attribute. " +
                      "No Orleans code generation was detected.");
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
                if (type.Namespace?.Contains("OrleansCodeGen") == true ||
                    type.GetCustomAttribute<GeneratedCodeAttribute>()?.Tool?.Contains("Orleans") == true)
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
                      "Ensure the assembly was compiled with Orleans.Sdk and code generation succeeded.");
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
