using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Service for unloading grain assemblies at runtime.
/// Orchestrates deactivation, cache cleanup, manifest updates, and memory reclamation.
/// </summary>
public interface IDynamicGrainUnloader
{
    /// <summary>
    /// Unloads a dynamically loaded grain assembly from this silo.
    /// This is a multi-phase operation that:
    /// 1. Validates assembly is loaded
    /// 2. Deactivates all active grain instances
    /// 3. Removes from all caches
    /// 4. Updates silo manifest
    /// 5. Propagates to cluster
    /// 6. Unloads assembly and reclaims memory
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to unload.</param>
    /// <param name="timeout">Maximum time to wait for graceful grain deactivation. Defaults to 30 seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing unload status and details.</returns>
    Task<GrainUnloadResult> UnloadGrainAssemblyAsync(
        string assemblyPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a stream of grain assembly unload events from this silo.
    /// Useful for monitoring and logging unload operations.
    /// </summary>
    IAsyncEnumerable<GrainAssemblyUnloadedEvent> UnloadEvents { get; }
}

/// <summary>
/// Result of a grain assembly unload operation.
/// </summary>
public sealed class GrainUnloadResult
{
    public Assembly Assembly { get; init; }
    public IReadOnlyList<GrainType> UnloadedGrainTypes { get; init; }
    public TimeSpan UnloadDuration { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public GrainDeactivationResult DeactivationResult { get; init; }
    public int ActiveGrainsDeactivated { get; init; }
    public bool MemoryReclaimed { get; init; }
}

/// <summary>
/// Event published when a grain assembly is unloaded.
/// </summary>
public sealed class GrainAssemblyUnloadedEvent
{
    public Assembly Assembly { get; init; }
    public SiloAddress UnloadedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<GrainType> UnloadedGrainTypes { get; init; }
    public MajorMinorVersion ManifestVersion { get; init; }
    public int GrainsDeactivated { get; init; }
}
