using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scynapse.Runtime.DynamicGrains;

/// <summary>
/// Default implementation of IGrainLifecycleManager that manages grain activations for dynamic type management.
/// </summary>
internal sealed class GrainLifecycleManager : IGrainLifecycleManager
{
    private readonly ActivationDirectory _activationDirectory;
    private readonly ILogger<GrainLifecycleManager> _logger;

    public GrainLifecycleManager(
        ActivationDirectory activationDirectory,
        ILogger<GrainLifecycleManager> logger)
    {
        _activationDirectory = activationDirectory ?? throw new ArgumentNullException(nameof(activationDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<GrainDeactivationResult> DeactivateGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var grainTypeSet = new HashSet<GrainType>(grainTypes);
        var errors = new List<string>();
        var deactivatedPerType = new Dictionary<GrainType, int>();
        var forcedDeactivations = 0;

        try
        {
            // Find all activations matching the grain types
            var matchingActivations = new List<(IGrainContext Context, GrainType Type)>();

            foreach (var kvp in _activationDirectory)
            {
                var grainId = kvp.Key;
                var grainContext = kvp.Value;

                if (grainTypeSet.Contains(grainId.Type))
                {
                    matchingActivations.Add((grainContext, grainId.Type));
                }
            }

            if (matchingActivations.Count == 0)
            {
                _logger.LogDebug("No active grains found for the specified types");
                return new GrainDeactivationResult
                {
                    Success = true,
                    TotalGrainsDeactivated = 0,
                    DeactivatedPerType = deactivatedPerType,
                    Errors = Array.Empty<string>(),
                    Duration = stopwatch.Elapsed,
                    ForcedDeactivations = 0
                };
            }

            _logger.LogInformation(
                "Deactivating {Count} grain activations across {TypeCount} types",
                matchingActivations.Count,
                grainTypeSet.Count);

            // Create deactivation tasks
            var deactivationTasks = new List<Task<(GrainType Type, bool Forced)>>();
            var reason = new DeactivationReason(
                DeactivationReasonCode.ApplicationRequested,
                "Grain type being unloaded");

            foreach (var (context, grainType) in matchingActivations)
            {
                // Initialize count for this type
                if (!deactivatedPerType.ContainsKey(grainType))
                {
                    deactivatedPerType[grainType] = 0;
                }

                var deactivationTask = DeactivateGrainAsync(context, grainType, reason, timeout, cancellationToken);
                deactivationTasks.Add(deactivationTask);
            }

            // Wait for all deactivations with combined timeout
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var results = await Task.WhenAll(deactivationTasks);

                foreach (var (type, forced) in results)
                {
                    deactivatedPerType[type]++;
                    if (forced)
                    {
                        forcedDeactivations++;
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Timeout waiting for grain deactivations. Some grains may have been force-deactivated.");
                errors.Add($"Deactivation timeout after {timeout.TotalSeconds}s - some grains may have been force-deactivated");
            }

            stopwatch.Stop();

            var totalDeactivated = deactivatedPerType.Values.Sum();

            _logger.LogInformation(
                "Deactivated {Count} grains in {Duration}ms ({Forced} forced)",
                totalDeactivated,
                stopwatch.ElapsedMilliseconds,
                forcedDeactivations);

            return new GrainDeactivationResult
            {
                Success = true,
                TotalGrainsDeactivated = totalDeactivated,
                DeactivatedPerType = deactivatedPerType,
                Errors = errors,
                Duration = stopwatch.Elapsed,
                ForcedDeactivations = forcedDeactivations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during grain deactivation");
            errors.Add($"Deactivation error: {ex.Message}");

            return new GrainDeactivationResult
            {
                Success = false,
                TotalGrainsDeactivated = deactivatedPerType.Values.Sum(),
                DeactivatedPerType = deactivatedPerType,
                Errors = errors,
                Duration = stopwatch.Elapsed,
                ForcedDeactivations = forcedDeactivations
            };
        }
    }

    private async Task<(GrainType Type, bool Forced)> DeactivateGrainAsync(
        IGrainContext context,
        GrainType grainType,
        DeactivationReason reason,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var forced = false;

        try
        {
            // Initiate deactivation
            context.Deactivate(reason, cancellationToken);

            // Wait for deactivation to complete with timeout
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await context.Deactivated.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout - grain was force-deactivated
                forced = true;
                _logger.LogWarning(
                    "Grain {GrainId} deactivation timed out after {Timeout}s",
                    context.GrainId,
                    timeout.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deactivating grain {GrainId}", context.GrainId);
        }

        return (grainType, forced);
    }

    /// <inheritdoc/>
    public bool HasActiveGrains(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = new HashSet<GrainType>(grainTypes);

        foreach (var kvp in _activationDirectory)
        {
            if (grainTypeSet.Contains(kvp.Key.Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<GrainType, int> GetActiveGrainCounts(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = new HashSet<GrainType>(grainTypes);
        var counts = new Dictionary<GrainType, int>();

        // Initialize counts for all requested types
        foreach (var grainType in grainTypeSet)
        {
            counts[grainType] = 0;
        }

        // Count active grains
        foreach (var kvp in _activationDirectory)
        {
            var grainType = kvp.Key.Type;
            if (grainTypeSet.Contains(grainType))
            {
                counts[grainType]++;
            }
        }

        return counts;
    }
}
