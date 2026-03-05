namespace Scynapse.Security;

/// <summary>
/// Utilities for deriving Scynapse resource URIs from grain types and methods.
/// Used by both outgoing and incoming call filters for consistent resource naming.
/// </summary>
public static class GrainResourceInference
{
    /// <summary>
    /// Derives a resource URI from a grain interface type.
    /// Example: IMyGrain -> "scynapse:grain/IMyGrain"
    /// </summary>
    public static string FromGrainInterface(Type grainInterfaceType)
    {
        return $"scynapse:grain/{grainInterfaceType.Name}";
    }

    /// <summary>
    /// Derives a resource URI from a grain interface type and method.
    /// Example: IMyGrain.DoWork -> "scynapse:grain/IMyGrain/DoWork"
    /// </summary>
    public static string FromGrainMethod(Type grainInterfaceType, string methodName)
    {
        return $"scynapse:grain/{grainInterfaceType.Name}/{methodName}";
    }

    /// <summary>
    /// Returns a wildcard resource pattern covering all methods of a grain type.
    /// Example: IMyGrain -> "scynapse:grain/IMyGrain/*"
    /// </summary>
    public static string WildcardForGrain(Type grainInterfaceType)
    {
        return $"scynapse:grain/{grainInterfaceType.Name}/*";
    }

    /// <summary>
    /// Returns a wildcard resource pattern covering all grains.
    /// </summary>
    public static string WildcardAll => "scynapse:grain/*";
}
