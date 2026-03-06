namespace Scynapse.Security.Orleans;

/// <summary>
/// Secured grain interface for plugin loading operations.
/// Wraps IPluginGrainLoader as a grain call to enforce CCap-based access control.
///
/// IPluginGrainLoader is a service interface (not a grain) and cannot carry [SecurityPolicy].
/// This grain provides a security boundary: callers must present an admin CCap to load assemblies.
/// The grain implementation delegates to IPluginGrainLoader internally.
/// </summary>
[SecurityPolicy(RequiresAuthentication = true, RequiresCallerCapability = true)]
public interface ISecuredPluginLoaderGrain : IGrainWithStringKey
{
    /// <summary>
    /// Load a grain assembly through the security boundary.
    /// Requires admin capability.
    /// </summary>
    [RequireCapability(Action = "admin", Resource = "scynapse.system.plugins")]
    Task<bool> LoadGrainAssemblyAsync(string assemblyPath);

    /// <summary>
    /// Unload grain types through the security boundary.
    /// Requires admin capability.
    /// </summary>
    [RequireCapability(Action = "admin", Resource = "scynapse.system.plugins")]
    Task UnloadGrainTypesAsync(string[] grainTypeNames);
}
