namespace Scynapse.Security;

/// <summary>
/// Utilities for deriving Scynapse resource URIs from grain types and methods.
/// Uses dot-separated hierarchical names (NATS-style Subject Namespace).
///
/// Format: scynapse.app.{grainInterface}.{method}
/// System:  scynapse.system.{subsystem}
///
/// Wildcard rules:
///   * matches one segment
///   > matches one or more trailing segments
/// </summary>
public static class GrainResourceInference
{
    /// <summary>
    /// Derives a resource URI from a grain interface type.
    /// Example: IMyGrain -> "scynapse.app.IMyGrain"
    /// </summary>
    public static string FromGrainInterface(Type grainInterfaceType)
    {
        return $"scynapse.app.{grainInterfaceType.Name}";
    }

    /// <summary>
    /// Derives a resource URI from a grain interface type and method.
    /// Example: IMyGrain.DoWork -> "scynapse.app.IMyGrain.DoWork"
    /// </summary>
    public static string FromGrainMethod(Type grainInterfaceType, string methodName)
    {
        return $"scynapse.app.{grainInterfaceType.Name}.{methodName}";
    }

    /// <summary>
    /// Returns a wildcard resource pattern covering all methods of a grain type.
    /// Example: IMyGrain -> "scynapse.app.IMyGrain.>"
    /// </summary>
    public static string WildcardForGrain(Type grainInterfaceType)
    {
        return $"scynapse.app.{grainInterfaceType.Name}.>";
    }

    /// <summary>
    /// Returns a wildcard resource pattern covering all application grains.
    /// </summary>
    public static string WildcardAllApp => "scynapse.app.>";

    /// <summary>
    /// Returns a wildcard resource pattern covering everything in Scynapse.
    /// </summary>
    public static string WildcardAll => "scynapse.>";

    /// <summary>
    /// Returns a system namespace resource URI.
    /// Example: "security.gateway" -> "scynapse.system.security.gateway"
    /// </summary>
    public static string SystemResource(string path)
    {
        return $"scynapse.system.{path}";
    }
}
