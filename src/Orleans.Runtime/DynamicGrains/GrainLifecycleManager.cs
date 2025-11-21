using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Manages lifecycle of grain activations for dynamic type management.
/// Coordinates deactivation of grain instances before type unloading.
/// </summary>
internal sealed class GrainLifecycleManager : IGrainLifecycleManager
{
    private readonly Catalog _catalog;
    private readonly ILogger<GrainLifecycleManager> _logger;

    public GrainLifecycleManager(
        Catalog catalog,
        ILogger<GrainLifecycleManager> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GrainDeactivationResult> DeactivateGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var grainTypeSet = grainTypes.ToHashSet();
        var deactivatedPerType = new Dictionary<GrainType, int>();
        var errors = new List<string>();
        var forcedCount = 0;

        _logger.LogInformation(
            "Starting deactivation of grain types: {Types} with {Timeout}ms timeout",
            string.Join(", ", grainTypeSet),
            timeout.TotalMilliseconds);

        // Find all active grains of these types
        var activationsToDeactivate = new List<IGrainContext>();

        foreach (var activation in GetAllActivations())
        {
            if (grainTypeSet.Contains(activation.GrainId.Type))
            {
                activationsToDeactivate.Add(activation);

                if (!deactivatedPerType.ContainsKey(activation.GrainId.Type))
                    deactivatedPerType[activation.GrainId.Type] = 0;

                deactivatedPerType[activation.GrainId.Type]++;
            }
        }

        if (activationsToDeactivate.Count == 0)
        {
            _logger.LogInformation("No active grain instances found for specified types");
            stopwatch.Stop();

            return new GrainDeactivationResult
            {
                Success = true,
                TotalGrainsDeactivated = 0,
                DeactivatedPerType = new Dictionary<GrainType, int>(),
                Errors = new List<string>(),
                Duration = stopwatch.Elapsed,
                ForcedDeactivations = 0
            };
        }

        _logger.LogInformation(
            "Found {ActivationCount} active grain instances to deactivate across {TypeCount} types",
            activationsToDeactivate.Count,
            deactivatedPerType.Count);

        // Deactivate with timeout using Catalog's existing method
        var reason = new DeactivationReason(
            DeactivationReasonCode.TypeUnloading,
            "Grain type being dynamically unloaded");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            // Use Catalog's existing DeactivateActivations method
            await _catalog.DeactivateActivations(reason, activationsToDeactivate, cts.Token);

            _logger.LogInformation(
                "Successfully deactivated {Count} grain instances in {Duration}ms",
                activationsToDeactivate.Count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            _logger.LogWarning(
                "Deactivation timeout reached after {Timeout}ms, some grains may still be active",
                timeout.TotalMilliseconds);

            // Count remaining active grains as "forced"
            var stillActive = GetActiveGrainCounts(grainTypeSet);
            forcedCount = stillActive.Values.Sum();

            if (forcedCount > 0)
            {
                errors.Add($"{forcedCount} grains did not complete graceful deactivation within timeout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during grain deactivation");
            errors.Add($"Deactivation error: {ex.Message}");
            stopwatch.Stop();

            return new GrainDeactivationResult
            {
                Success = false,
                TotalGrainsDeactivated = 0,
                DeactivatedPerType = deactivatedPerType,
                Errors = errors,
                Duration = stopwatch.Elapsed,
                ForcedDeactivations = 0
            };
        }

        stopwatch.Stop();

        return new GrainDeactivationResult
        {
            Success = true,
            TotalGrainsDeactivated = activationsToDeactivate.Count,
            DeactivatedPerType = deactivatedPerType,
            Errors = errors,
            Duration = stopwatch.Elapsed,
            ForcedDeactivations = forcedCount
        };
    }

    public bool HasActiveGrains(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = grainTypes.ToHashSet();

        foreach (var activation in GetAllActivations())
        {
            if (grainTypeSet.Contains(activation.GrainId.Type))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyDictionary<GrainType, int> GetActiveGrainCounts(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = grainTypes.ToHashSet();
        var counts = new Dictionary<GrainType, int>();

        foreach (var activation in GetAllActivations())
        {
            if (grainTypeSet.Contains(activation.GrainId.Type))
            {
                if (!counts.ContainsKey(activation.GrainId.Type))
                    counts[activation.GrainId.Type] = 0;

                counts[activation.GrainId.Type]++;
            }
        }

        return counts;
    }

    /// <summary>
    /// Gets all active grain contexts from the Catalog.
    /// </summary>
    private IEnumerable<IGrainContext> GetAllActivations()
    {
        return _catalog.GetAllActivations();
    }
}
