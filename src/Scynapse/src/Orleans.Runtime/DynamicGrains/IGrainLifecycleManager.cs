using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Manages lifecycle of grain activations for dynamic type management.
/// </summary>
internal interface IGrainLifecycleManager
{
    /// <summary>
    /// Deactivates all active grain instances of the specified types.
    /// </summary>
    /// <param name="grainTypes">The grain types to deactivate.</param>
    /// <param name="timeout">Maximum time to wait for graceful deactivation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and deactivation details.</returns>
    Task<GrainDeactivationResult> DeactivateGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any grains of the specified types are currently active.
    /// </summary>
    bool HasActiveGrains(IEnumerable<GrainType> grainTypes);

    /// <summary>
    /// Gets count of active grains for each specified type.
    /// </summary>
    IReadOnlyDictionary<GrainType, int> GetActiveGrainCounts(IEnumerable<GrainType> grainTypes);
}

/// <summary>
/// Result of a grain deactivation operation.
/// </summary>
public sealed class GrainDeactivationResult
{
    public bool Success { get; init; }
    public int TotalGrainsDeactivated { get; init; }
    public IReadOnlyDictionary<GrainType, int> DeactivatedPerType { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public TimeSpan Duration { get; init; }
    public int ForcedDeactivations { get; init; }  // Timed out, force-deactivated
}
