using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Metadata;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Service for loading plugin grain assemblies at runtime.
/// </summary>
public interface IPluginGrainLoader
{
    /// <summary>
    /// Loads a pre-compiled grain assembly with Orleans-generated code.
    /// The assembly must be compiled with Orleans.Sdk to include generated serializers,
    /// proxies, and metadata providers.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing loaded grain types and metadata</returns>
    /// <exception cref="ArgumentNullException">When assemblyPath is null</exception>
    /// <exception cref="InvalidOperationException">When assembly lacks required Orleans-generated code</exception>
    Task<GrainLoadResult> LoadGrainAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads grain types that were loaded via an isolated AssemblyLoadContext.
    /// This operation is only supported for assemblies loaded with isolation enabled.
    /// </summary>
    /// <param name="grainTypes">The grain types to unload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async unload operation</returns>
    Task UnloadGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an async stream of grain assembly load events across the cluster.
    /// </summary>
    IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents { get; }
}

/// <summary>
/// Result of a grain assembly load operation.
/// </summary>
public sealed class GrainLoadResult
{
    /// <summary>
    /// The loaded assembly.
    /// </summary>
    public Assembly Assembly { get; init; }

    /// <summary>
    /// List of grain types discovered in the assembly.
    /// </summary>
    public IReadOnlyList<GrainType> GrainTypes { get; init; }

    /// <summary>
    /// Time taken to complete the load operation.
    /// </summary>
    public TimeSpan LoadDuration { get; init; }

    /// <summary>
    /// The new cluster manifest version after the update.
    /// </summary>
    public MajorMinorVersion NewManifestVersion { get; init; }

    /// <summary>
    /// Whether the load operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// List of errors encountered during loading (empty if successful).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Metadata about the loaded grain types.
    /// </summary>
    public AssemblyLoadMetadata Metadata { get; init; }
}

/// <summary>
/// Metadata about a loaded grain assembly.
/// </summary>
public sealed class AssemblyLoadMetadata
{
    /// <summary>
    /// Grain interface types found in the assembly.
    /// </summary>
    public IReadOnlyList<Type> GrainInterfaces { get; init; }

    /// <summary>
    /// Grain implementation types found in the assembly.
    /// </summary>
    public IReadOnlyList<Type> GrainClasses { get; init; }

    /// <summary>
    /// Generated serializer types.
    /// </summary>
    public IReadOnlyList<Type> Serializers { get; init; }

    /// <summary>
    /// Generated copier types.
    /// </summary>
    public IReadOnlyList<Type> Copiers { get; init; }

    /// <summary>
    /// Generated proxy types.
    /// </summary>
    public IReadOnlyList<Type> Proxies { get; init; }

    /// <summary>
    /// Whether the assembly has Orleans-generated code.
    /// </summary>
    public bool HasGeneratedCode { get; init; }
}

/// <summary>
/// Event raised when a grain assembly is loaded.
/// </summary>
public sealed class GrainAssemblyLoadedEvent
{
    /// <summary>
    /// The loaded assembly.
    /// </summary>
    public Assembly Assembly { get; init; }

    /// <summary>
    /// The silo that loaded the assembly.
    /// </summary>
    public SiloAddress LoadedBy { get; init; }

    /// <summary>
    /// Timestamp of the load operation.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// New grain types added by this load.
    /// </summary>
    public IReadOnlyList<GrainType> NewGrainTypes { get; init; }

    /// <summary>
    /// The manifest version after this load.
    /// </summary>
    public MajorMinorVersion ManifestVersion { get; init; }
}
