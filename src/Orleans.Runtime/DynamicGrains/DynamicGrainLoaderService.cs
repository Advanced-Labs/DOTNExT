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
using Orleans.Metadata;
using Orleans.Runtime.Placement;
using Orleans.Serialization.Configuration;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Main service for dynamic grain loading functionality.
/// Coordinates assembly loading, manifest updates, and cache invalidation.
/// </summary>
internal sealed class DynamicGrainLoaderService : IDynamicGrainLoader, IAsyncDisposable, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly DynamicAssemblyLoader _assemblyLoader;
    private readonly SiloManifestProvider _manifestProvider;
    private readonly IClusterManifestProvider _clusterManifestProvider;
    private readonly ILocalSiloDetails _siloDetails;
    private readonly ILogger<DynamicGrainLoaderService> _logger;
    private readonly Channel<GrainAssemblyLoadedEvent> _loadEventsChannel;
    private readonly CancellationTokenSource _shutdownCts = new();

    public DynamicGrainLoaderService(
        DynamicAssemblyLoader assemblyLoader,
        SiloManifestProvider manifestProvider,
        IClusterManifestProvider clusterManifestProvider,
        ILocalSiloDetails siloDetails,
        ILogger<DynamicGrainLoaderService> logger)
    {
        _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _clusterManifestProvider = clusterManifestProvider ?? throw new ArgumentNullException(nameof(clusterManifestProvider));
        _siloDetails = siloDetails ?? throw new ArgumentNullException(nameof(siloDetails));
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

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting dynamic load of grain assembly: {AssemblyPath}", assemblyPath);

            // Phase 1: Load and validate assembly
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
            var grainTypes = new List<GrainType>();

            if (metadata.GrainClasses.Count > 0 || metadata.GrainInterfaces.Count > 0)
            {
                _logger.LogInformation(
                    "Updating silo manifest with {ClassCount} grain classes and {InterfaceCount} interfaces",
                    metadata.GrainClasses.Count,
                    metadata.GrainInterfaces.Count);

                var (updatedManifest, typeMap) = _manifestProvider.UpdateManifest(
                    metadata.GrainClasses,
                    metadata.GrainInterfaces);

                // Extract grain types that were added
                grainTypes.AddRange(typeMap.Keys);

                _logger.LogInformation("Successfully updated silo manifest with {TypeCount} new grain types", grainTypes.Count);
            }

            // Phase 3: Update serialization system (if we have generated serializers)
            if (metadata.Serializers.Count > 0 || metadata.Copiers.Count > 0)
            {
                _logger.LogInformation(
                    "Registering {SerializerCount} serializers and {CopierCount} copiers",
                    metadata.Serializers.Count,
                    metadata.Copiers.Count);

                // TODO: Update CodecProvider with new serializers/copiers
                // This will be implemented in the serialization update phase
            }

            // Phase 4: Get new cluster manifest version
            var currentManifest = _clusterManifestProvider.Current;
            var newVersion = currentManifest.Version;

            // TODO: Propagate manifest update to cluster
            // This will trigger ClusterManifestProvider to update

            // Phase 5: Publish load event
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
                "Successfully completed dynamic load of assembly {AssemblyName} in {Duration}ms",
                assembly.GetName().Name,
                stopwatch.ElapsedMilliseconds);

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
    public async IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents
    {
        get
        {
            await foreach (var loadEvent in _loadEventsChannel.Reader.ReadAllAsync(_shutdownCts.Token))
            {
                yield return loadEvent;
            }
        }
    }

    /// <summary>
    /// Lifecycle hook for silo startup.
    /// </summary>
    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            observerName: nameof(DynamicGrainLoaderService),
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

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _loadEventsChannel.Writer.Complete();

        // Wait for channel to drain
        await _loadEventsChannel.Reader.Completion.ConfigureAwait(false);
    }
}
