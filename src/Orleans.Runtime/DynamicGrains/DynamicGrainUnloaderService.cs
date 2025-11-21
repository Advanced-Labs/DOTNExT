using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.GrainReferences;
using Orleans.Metadata;
using Orleans.Runtime.GrainDirectory;
using Orleans.Runtime.Metadata;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Service that orchestrates dynamic grain assembly unloading.
/// Coordinates all phases: deactivation, cache cleanup, manifest updates, and memory reclamation.
/// </summary>
internal sealed class DynamicGrainUnloaderService : IDynamicGrainUnloader, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly DynamicAssemblyLoader _assemblyLoader;
    private readonly IGrainLifecycleManager _lifecycleManager;
    private readonly SiloManifestProvider _manifestProvider;
    private readonly ClusterManifestProvider _clusterManifestProvider;
    private readonly GrainContextActivator _grainContextActivator;
    private readonly GrainTypeSharedContextResolver _sharedContextResolver;
    private readonly GrainReferenceActivator _grainReferenceActivator;
    private readonly ILogger<DynamicGrainUnloaderService> _logger;
    private readonly SiloAddress _siloAddress;
    private readonly Channel<GrainAssemblyUnloadedEvent> _unloadEventsChannel;
    private readonly SemaphoreSlim _unloadSemaphore = new(1, 1);

    private static readonly TimeSpan DefaultDeactivationTimeout = TimeSpan.FromSeconds(30);

    public DynamicGrainUnloaderService(
        DynamicAssemblyLoader assemblyLoader,
        IGrainLifecycleManager lifecycleManager,
        SiloManifestProvider manifestProvider,
        ClusterManifestProvider clusterManifestProvider,
        GrainContextActivator grainContextActivator,
        GrainTypeSharedContextResolver sharedContextResolver,
        GrainReferenceActivator grainReferenceActivator,
        ILocalSiloDetails siloDetails,
        ILogger<DynamicGrainUnloaderService> logger)
    {
        _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _clusterManifestProvider = clusterManifestProvider ?? throw new ArgumentNullException(nameof(clusterManifestProvider));
        _grainContextActivator = grainContextActivator ?? throw new ArgumentNullException(nameof(grainContextActivator));
        _sharedContextResolver = sharedContextResolver ?? throw new ArgumentNullException(nameof(sharedContextResolver));
        _grainReferenceActivator = grainReferenceActivator ?? throw new ArgumentNullException(nameof(grainReferenceActivator));
        _siloAddress = siloDetails?.SiloAddress ?? throw new ArgumentNullException(nameof(siloDetails));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unloadEventsChannel = Channel.CreateUnbounded<GrainAssemblyUnloadedEvent>();
    }

    public IAsyncEnumerable<GrainAssemblyUnloadedEvent> UnloadEvents =>
        _unloadEventsChannel.Reader.ReadAllAsync();

    public async Task<GrainUnloadResult> UnloadGrainAssemblyAsync(
        string assemblyPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= DefaultDeactivationTimeout;

        // Only one unload at a time to prevent race conditions
        await _unloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await UnloadGrainAssemblyInternalAsync(assemblyPath, timeout.Value, cancellationToken);
        }
        finally
        {
            _unloadSemaphore.Release();
        }
    }

    private async Task<GrainUnloadResult> UnloadGrainAssemblyInternalAsync(
        string assemblyPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        GrainDeactivationResult deactivationResult = null;
        var errors = new List<string>();

        try
        {
            _logger.LogInformation("Starting dynamic unload of grain assembly: {AssemblyPath}", assemblyPath);

            // ===================================================================
            // Phase 1: Validate & Prepare
            // ===================================================================
            _logger.LogDebug("Phase 1: Validating and preparing for unload");

            var (assembly, metadata) = _assemblyLoader.GetLoadedAssemblyInfo(assemblyPath);
            if (assembly == null)
            {
                var error = "Assembly not loaded or not found";
                _logger.LogWarning("{Error}: {AssemblyPath}", error, assemblyPath);

                return new GrainUnloadResult
                {
                    Success = false,
                    Errors = new[] { error },
                    UnloadDuration = stopwatch.Elapsed
                };
            }

            // Validate that the assembly was loaded via PluginLoader and can be unloaded
            if (!_assemblyLoader.IsAssemblyUnloadable(assemblyPath))
            {
                var error = "Assembly was not loaded via dynamic loader (PluginLoader) and cannot be unloaded. " +
                           "Only assemblies loaded dynamically at runtime can be unloaded.";
                _logger.LogError("{Error}: {AssemblyPath}", error, assemblyPath);

                return new GrainUnloadResult
                {
                    Success = false,
                    Errors = new[] { error },
                    UnloadDuration = stopwatch.Elapsed
                };
            }

            // Build list of grain types being unloaded
            var grainTypes = new List<GrainType>();
            foreach (var grainClass in metadata.GrainClasses)
            {
                var grainType = GrainType.Create(grainClass.FullName);
                grainTypes.Add(grainType);
            }

            _logger.LogInformation(
                "Unloading assembly {AssemblyName} with {TypeCount} grain types",
                assembly.GetName().Name,
                grainTypes.Count);

            // ===================================================================
            // Phase 2: Deactivate Active Grains
            // ===================================================================
            _logger.LogDebug("Phase 2: Deactivating active grains");

            var activeCount = _lifecycleManager.GetActiveGrainCounts(grainTypes);
            var totalActive = activeCount.Values.Sum();

            if (totalActive > 0)
            {
                _logger.LogInformation(
                    "Deactivating {ActiveCount} active grain instances across {TypeCount} types",
                    totalActive,
                    activeCount.Count);

                deactivationResult = await _lifecycleManager.DeactivateGrainTypesAsync(
                    grainTypes,
                    timeout,
                    cancellationToken);

                if (!deactivationResult.Success)
                {
                    _logger.LogError(
                        "Failed to deactivate grains: {Errors}",
                        string.Join("; ", deactivationResult.Errors));

                    errors.AddRange(deactivationResult.Errors);

                    return new GrainUnloadResult
                    {
                        Assembly = assembly,
                        UnloadedGrainTypes = grainTypes,
                        Success = false,
                        Errors = errors,
                        DeactivationResult = deactivationResult,
                        ActiveGrainsDeactivated = 0,
                        UnloadDuration = stopwatch.Elapsed
                    };
                }

                _logger.LogInformation(
                    "Successfully deactivated {Count} grains ({Forced} forced)",
                    deactivationResult.TotalGrainsDeactivated,
                    deactivationResult.ForcedDeactivations);

                if (deactivationResult.ForcedDeactivations > 0)
                {
                    errors.Add($"{deactivationResult.ForcedDeactivations} grains forced deactivation after timeout");
                }
            }
            else
            {
                _logger.LogInformation("No active grains to deactivate");
            }

            // ===================================================================
            // Phase 3: Update Silo Manifest
            // ===================================================================
            // CRITICAL: Manifest must be updated BEFORE clearing caches to prevent race conditions
            // This ensures new requests won't be routed here before we clear local state
            _logger.LogDebug("Phase 3: Updating silo manifest");

            var (updatedManifest, removedGrainTypes) = _manifestProvider.RemoveFromManifest(
                metadata.GrainClasses,
                metadata.GrainInterfaces);

            _logger.LogInformation(
                "Updated silo manifest, removed {TypeCount} types",
                removedGrainTypes.Count());

            // ===================================================================
            // Phase 4: Propagate to Cluster
            // ===================================================================
            _logger.LogDebug("Phase 4: Propagating manifest to cluster");

            // Update the local manifest in the cluster manifest provider
            // This triggers propagation to other silos
            _clusterManifestProvider.LocalGrainManifest = updatedManifest;

            var newVersion = _clusterManifestProvider.Current.Version;

            _logger.LogInformation(
                "Propagated manifest removal to cluster. New version: {Version}",
                newVersion);

            // Small delay to allow manifest propagation before clearing caches
            // This prevents race conditions where requests arrive for types being unloaded
            await Task.Delay(100, cancellationToken);

            // ===================================================================
            // Phase 5: Remove from Caches
            // ===================================================================
            // Safe to clear caches now - manifest has been updated and propagated
            // No new requests will be routed to this silo for these types
            _logger.LogDebug("Phase 5: Removing from caches");

            foreach (var grainType in grainTypes)
            {
                // Invalidate activator cache (removes from immutable dictionary)
                _grainContextActivator.InvalidateActivator(grainType);

                // Invalidate shared context cache
                _sharedContextResolver.InvalidateGrainType(grainType);
            }

            // Invalidate grain reference activator cache (for proxies)
            _grainReferenceActivator.InvalidateCache();

            _logger.LogInformation("Removed {TypeCount} grain types from caches", grainTypes.Count);

            // ===================================================================
            // Phase 6: Unload Assembly
            // ===================================================================
            _logger.LogDebug("Phase 6: Unloading assembly");

            var unloaded = await _assemblyLoader.UnloadAssemblyAsync(assemblyPath);

            if (!unloaded)
            {
                var error = "Failed to unload assembly - may still have references";
                _logger.LogWarning("{Error}: {AssemblyPath}", error, assemblyPath);
                errors.Add(error);

                return new GrainUnloadResult
                {
                    Assembly = assembly,
                    UnloadedGrainTypes = grainTypes,
                    Success = false,
                    Errors = errors,
                    DeactivationResult = deactivationResult,
                    ActiveGrainsDeactivated = totalActive,
                    MemoryReclaimed = false,
                    UnloadDuration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Assembly unloaded and memory reclamation triggered");

            // ===================================================================
            // Phase 7: Publish Event
            // ===================================================================
            _logger.LogDebug("Phase 7: Publishing unload event");

            var unloadEvent = new GrainAssemblyUnloadedEvent
            {
                Assembly = assembly,
                UnloadedBy = _siloAddress,
                Timestamp = DateTimeOffset.UtcNow,
                UnloadedGrainTypes = grainTypes,
                ManifestVersion = newVersion,
                GrainsDeactivated = totalActive
            };

            await _unloadEventsChannel.Writer.WriteAsync(unloadEvent, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully completed dynamic unload of assembly {AssemblyName} in {Duration}ms. " +
                "Removed {TypeCount} types, deactivated {GrainCount} grains.",
                assembly.GetName().Name,
                stopwatch.ElapsedMilliseconds,
                grainTypes.Count,
                totalActive);

            return new GrainUnloadResult
            {
                Assembly = assembly,
                UnloadedGrainTypes = grainTypes,
                UnloadDuration = stopwatch.Elapsed,
                Success = true,
                Errors = errors,
                DeactivationResult = deactivationResult,
                ActiveGrainsDeactivated = totalActive,
                MemoryReclaimed = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during dynamic grain unloading from {AssemblyPath}", assemblyPath);

            stopwatch.Stop();

            return new GrainUnloadResult
            {
                Success = false,
                Errors = new[] { $"Unload failed: {ex.Message}" },
                UnloadDuration = stopwatch.Elapsed
            };
        }
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        // Register with silo lifecycle if needed for cleanup during shutdown
        // Currently no lifecycle actions needed
    }
}
