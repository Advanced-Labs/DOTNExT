using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.GrainReferences;
using Orleans.Metadata;
using Orleans.Runtime.Metadata;
using Orleans.Runtime.Placement;
using Orleans.Serialization.Configuration;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Main service for plugin grain loading functionality.
/// Coordinates assembly loading, manifest updates, cache invalidation, and cluster propagation.
/// </summary>
internal sealed class PluginGrainLoaderService : IPluginGrainLoader, IAsyncDisposable, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly PluginAssemblyLoader _assemblyLoader;
    private readonly SiloManifestProvider _manifestProvider;
    private readonly ClusterManifestProvider _clusterManifestProvider;
    private readonly PluginSerializationManager _serializationManager;
    private readonly GrainContextActivator _grainContextActivator;
    private readonly GrainTypeSharedContextResolver _sharedContextResolver;
    private readonly RpcProvider _rpcProvider;
    private readonly GrainReferenceActivator _grainReferenceActivator;
    private readonly ILocalSiloDetails _siloDetails;
    private readonly IClusterMembershipService _clusterMembershipService;
    private readonly IInternalGrainFactory _grainFactory;
    private readonly ILogger<PluginGrainLoaderService> _logger;
    private readonly Channel<GrainAssemblyLoadedEvent> _loadEventsChannel;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    public PluginGrainLoaderService(
        PluginAssemblyLoader assemblyLoader,
        SiloManifestProvider manifestProvider,
        ClusterManifestProvider clusterManifestProvider,
        PluginSerializationManager serializationManager,
        GrainContextActivator grainContextActivator,
        GrainTypeSharedContextResolver sharedContextResolver,
        RpcProvider rpcProvider,
        GrainReferenceActivator grainReferenceActivator,
        ILocalSiloDetails siloDetails,
        IClusterMembershipService clusterMembershipService,
        IInternalGrainFactory grainFactory,
        ILogger<PluginGrainLoaderService> logger)
    {
        _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _clusterManifestProvider = clusterManifestProvider ?? throw new ArgumentNullException(nameof(clusterManifestProvider));
        _serializationManager = serializationManager ?? throw new ArgumentNullException(nameof(serializationManager));
        _grainContextActivator = grainContextActivator ?? throw new ArgumentNullException(nameof(grainContextActivator));
        _sharedContextResolver = sharedContextResolver ?? throw new ArgumentNullException(nameof(sharedContextResolver));
        _rpcProvider = rpcProvider ?? throw new ArgumentNullException(nameof(rpcProvider));
        _grainReferenceActivator = grainReferenceActivator ?? throw new ArgumentNullException(nameof(grainReferenceActivator));
        _siloDetails = siloDetails ?? throw new ArgumentNullException(nameof(siloDetails));
        _clusterMembershipService = clusterMembershipService ?? throw new ArgumentNullException(nameof(clusterMembershipService));
        _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _loadEventsChannel = Channel.CreateUnbounded<GrainAssemblyLoadedEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <inheritdoc/>
    public async Task<GrainLoadResult> LoadGrainAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentNullException(nameof(assemblyPath));
        }

        // Ensure only one assembly is being loaded at a time
        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await LoadGrainAssemblyInternalAsync(assemblyPath, cancellationToken);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private async Task<GrainLoadResult> LoadGrainAssemblyInternalAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting dynamic load of grain assembly: {AssemblyPath}", assemblyPath);

            // Phase 1: Load and validate assembly
            _logger.LogDebug("Phase 1: Loading and validating assembly");
            var (assembly, metadata, errors) = await _assemblyLoader.LoadAssemblyAsync(assemblyPath, cancellationToken);

            if (errors.Count > 0)
            {
                _logger.LogError("Failed to load assembly {AssemblyPath}: {Errors}",
                    assemblyPath, string.Join("; ", errors));

                return new GrainLoadResult
                {
                    Assembly = assembly,
                    Success = false,
                    Errors = errors,
                    LoadDuration = stopwatch.Elapsed
                };
            }

            // Phase 2: Update local silo manifest
            _logger.LogDebug("Phase 2: Updating local silo manifest");
            var grainTypes = new List<GrainType>();
            GrainManifest updatedManifest = null;

            if (metadata.GrainClasses.Count > 0 || metadata.GrainInterfaces.Count > 0)
            {
                _logger.LogInformation(
                    "Updating silo manifest with {ClassCount} grain classes and {InterfaceCount} interfaces",
                    metadata.GrainClasses.Count,
                    metadata.GrainInterfaces.Count);

                var (manifest, typeMap) = _manifestProvider.UpdateManifest(
                    metadata.GrainClasses,
                    metadata.GrainInterfaces);

                updatedManifest = manifest;
                grainTypes.AddRange(typeMap.Keys);

                _logger.LogInformation("Successfully updated silo manifest with {TypeCount} new grain types", grainTypes.Count);
            }

            // Phase 3: Update serialization system
            _logger.LogDebug("Phase 3: Updating serialization system");
            if (metadata.Serializers.Count > 0 || metadata.Copiers.Count > 0)
            {
                _logger.LogInformation(
                    "Registering {SerializerCount} serializers and {CopierCount} copiers",
                    metadata.Serializers.Count,
                    metadata.Copiers.Count);

                _serializationManager.RegisterSerializers(metadata);

                _logger.LogInformation("Successfully registered serialization types");
            }

            // Phase 3.5: Register grain reference activators (proxy types)
            _logger.LogDebug("Phase 3.5: Registering grain reference activators");
            if (metadata.Proxies.Count > 0)
            {
                _logger.LogInformation(
                    "Registering {ProxyCount} grain reference proxy types",
                    metadata.Proxies.Count);

                _rpcProvider.AddProxyTypes(metadata.Proxies);
                _grainReferenceActivator.InvalidateCache();

                _logger.LogInformation("Successfully registered grain reference activators");
            }

            // Phase 4: Invalidate caches for new grain types
            _logger.LogDebug("Phase 4: Invalidating caches");
            if (grainTypes.Count > 0)
            {
                foreach (var grainType in grainTypes)
                {
                    _grainContextActivator.InvalidateActivator(grainType);
                    _sharedContextResolver.InvalidateGrainType(grainType);
                }

                _logger.LogInformation("Invalidated caches for {TypeCount} grain types", grainTypes.Count);
            }

            // Phase 5: Propagate manifest update to cluster
            _logger.LogDebug("Phase 5: Propagating manifest to cluster");
            var newVersion = _clusterManifestProvider.Current.Version;

            if (updatedManifest != null)
            {
                var propagated = _clusterManifestProvider.UpdateLocalManifest(updatedManifest);

                if (propagated)
                {
                    newVersion = _clusterManifestProvider.Current.Version;
                    _logger.LogInformation(
                        "Successfully propagated manifest update to cluster. New version: {Version}",
                        newVersion);

                    // Notify other silos to refresh their cluster manifest
                    await NotifyOtherSilosOfManifestChangeAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Failed to propagate manifest update to cluster");
                }
            }

            // Phase 6: Publish load event
            _logger.LogDebug("Phase 6: Publishing load event");
            var loadEvent = new GrainAssemblyLoadedEvent
            {
                Assembly = assembly,
                LoadedBy = _siloDetails.SiloAddress,
                Timestamp = DateTimeOffset.UtcNow,
                NewGrainTypes = grainTypes,
                ManifestVersion = newVersion
            };

            await _loadEventsChannel.Writer.WriteAsync(loadEvent, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully completed dynamic load of assembly {AssemblyName} in {Duration}ms with {GrainTypeCount} grain types",
                assembly.GetName().Name,
                stopwatch.ElapsedMilliseconds,
                grainTypes.Count);

            return new GrainLoadResult
            {
                Assembly = assembly,
                GrainTypes = grainTypes,
                LoadDuration = stopwatch.Elapsed,
                NewManifestVersion = newVersion,
                Success = true,
                Errors = Array.Empty<string>(),
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during dynamic grain loading from {AssemblyPath}", assemblyPath);

            return new GrainLoadResult
            {
                Success = false,
                Errors = new[] { $"Unexpected error: {ex.Message}" },
                LoadDuration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc/>
    public Task UnloadGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement unloading support
        // This requires AssemblyLoadContext isolation which is a future enhancement
        throw new NotSupportedException(
            "Grain type unloading is not yet supported. " +
            "To enable unloading, assemblies must be loaded in isolated AssemblyLoadContext instances.");
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents => GetLoadEventsAsync();

    private async IAsyncEnumerable<GrainAssemblyLoadedEvent> GetLoadEventsAsync()
    {
        await foreach (var loadEvent in _loadEventsChannel.Reader.ReadAllAsync(_shutdownCts.Token))
        {
            yield return loadEvent;
        }
    }

    /// <summary>
    /// Lifecycle hook for silo startup.
    /// </summary>
    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            observerName: nameof(PluginGrainLoaderService),
            stage: ServiceLifecycleStage.RuntimeGrainServices,
            onStart: OnStart,
            onStop: OnStop);
    }

    private Task OnStart(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dynamic grain loader service started");
        return Task.CompletedTask;
    }

    private Task OnStop(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dynamic grain loader service stopping");
        _shutdownCts.Cancel();
        _loadEventsChannel.Writer.Complete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Notifies all other silos in the cluster to refresh their manifest to pick up the changes.
    /// </summary>
    private async Task NotifyOtherSilosOfManifestChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var membershipSnapshot = _clusterMembershipService.CurrentSnapshot;
            var localSiloAddress = _siloDetails.SiloAddress;
            var notificationTasks = new List<Task>();

            foreach (var entry in membershipSnapshot.Members)
            {
                var member = entry.Value;

                // Skip local silo and inactive silos
                if (member.SiloAddress.Equals(localSiloAddress) || member.Status != SiloStatus.Active)
                {
                    continue;
                }

                notificationTasks.Add(NotifySiloAsync(member.SiloAddress, cancellationToken));
            }

            if (notificationTasks.Count > 0)
            {
                _logger.LogDebug("Notifying {Count} silos of manifest change", notificationTasks.Count);

                // Wait for all notifications, but don't fail if some don't respond
                await Task.WhenAll(notificationTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

                _logger.LogInformation("Notified {Count} silos of manifest change", notificationTasks.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error notifying silos of manifest change");
        }

        async Task NotifySiloAsync(SiloAddress siloAddress, CancellationToken ct)
        {
            try
            {
                var remoteManifestProvider = _grainFactory.GetSystemTarget<ISiloManifestSystemTarget>(
                    Constants.ManifestProviderType, siloAddress);
                await remoteManifestProvider.NotifyManifestChanged().AsTask().WaitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify silo {SiloAddress} of manifest change", siloAddress);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _loadEventsChannel.Writer.Complete();

        // Wait for channel to drain
        await _loadEventsChannel.Reader.Completion.ConfigureAwait(false);
    }
}
