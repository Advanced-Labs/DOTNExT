using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.DynamicGrains;
using Orleans.Metadata;

namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// Implementation of <see cref="IDynamicGrainClient"/> for dynamic grain access.
    /// Works for both external clients and silos (grain-to-grain calls).
    /// </summary>
    public class DynamicGrainClient : IDynamicGrainClient
    {
        private readonly IGrainFactory _grainFactory;
        private readonly IGrainPackageStore _packageStore;
        private readonly IGrainPackageCache _packageCache;
        private readonly ILogger<DynamicGrainClient> _logger;
        private readonly ConcurrentDictionary<string, GrainPackageHandle> _loadedPackages = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicGrainClient"/> class.
        /// </summary>
        /// <param name="grainFactory">The grain factory.</param>
        /// <param name="packageStore">The package store.</param>
        /// <param name="packageCache">The package cache.</param>
        /// <param name="logger">The logger.</param>
        public DynamicGrainClient(
            IGrainFactory grainFactory,
            IGrainPackageStore packageStore,
            IGrainPackageCache packageCache,
            ILogger<DynamicGrainClient> logger)
        {
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _packageStore = packageStore ?? throw new ArgumentNullException(nameof(packageStore));
            _packageCache = packageCache ?? throw new ArgumentNullException(nameof(packageCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IGrainFactory GrainFactory => _grainFactory;

        /// <inheritdoc />
        public IReadOnlyList<GrainPackageHandle> LoadedPackages =>
            _loadedPackages.Values.Where(h => h.IsLoaded).ToList();

        // =============================================
        // Package Management
        // =============================================

        /// <inheritdoc />
        public async Task<GrainPackageHandle> LoadPackageAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(packageId);

            var key = MakeKey(packageId, version ?? "latest");

            // Check if already loaded
            if (_loadedPackages.TryGetValue(key, out var existingHandle) && existingHandle.IsLoaded)
            {
                _logger.LogDebug("Package {PackageId} v{Version} already loaded", packageId, version ?? "latest");
                return existingHandle;
            }

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_loadedPackages.TryGetValue(key, out existingHandle) && existingHandle.IsLoaded)
                {
                    return existingHandle;
                }

                // Try to get from cache first
                var content = await _packageCache.GetAsync(packageId, version, cancellationToken);

                if (content == null)
                {
                    // Fetch from store
                    _logger.LogInformation("Fetching package {PackageId} v{Version} from store", packageId, version ?? "latest");
                    content = await _packageStore.GetPackageAsync(packageId, version, cancellationToken);

                    if (content == null)
                    {
                        throw new InvalidOperationException(
                            $"Package '{packageId}' version '{version ?? "latest"}' not found in any package source.");
                    }

                    // Cache for future use
                    await _packageCache.PutAsync(content, cancellationToken);
                }

                // Create handle
                var handle = new GrainPackageHandle(
                    content.Package,
                    content,
                    _grainFactory,
                    loadContext: null,
                    unloadCallback: async h => await OnPackageUnloaded(h));

                // Update key with actual version
                var actualKey = MakeKey(packageId, content.Package.Version);
                _loadedPackages[actualKey] = handle;

                // Also store under "latest" key if no version specified
                if (version == null)
                {
                    _loadedPackages[key] = handle;
                }

                _logger.LogInformation(
                    "Loaded package {PackageId} v{Version} with {TypeCount} grain types",
                    packageId, content.Package.Version, content.Package.GrainTypes.Count);

                return handle;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task UnloadPackageAsync(
            GrainPackageHandle handle,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            await handle.DisposeAsync();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GrainPackageInfo>> ListAvailablePackagesAsync(
            CancellationToken cancellationToken = default)
        {
            return await _packageStore.ListPackagesAsync(cancellationToken);
        }

        // =============================================
        // Grain Access
        // =============================================

        /// <inheritdoc />
        public async Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            string primaryKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grainTypeName);
            ArgumentNullException.ThrowIfNull(primaryKey);

            // Try to find in already loaded packages
            var handle = await FindOrLoadPackageForType(grainTypeName, cancellationToken);
            if (handle != null)
            {
                return handle.GetGrain(grainTypeName, primaryKey);
            }

            // Fall back to direct factory extension
            return _grainFactory.GetGrainDynamic(grainTypeName, primaryKey);
        }

        /// <inheritdoc />
        public async Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            Guid primaryKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grainTypeName);

            var handle = await FindOrLoadPackageForType(grainTypeName, cancellationToken);
            if (handle != null)
            {
                return handle.GetGrain(grainTypeName, primaryKey);
            }

            return _grainFactory.GetGrainDynamic(grainTypeName, primaryKey);
        }

        /// <inheritdoc />
        public async Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            long primaryKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grainTypeName);

            var handle = await FindOrLoadPackageForType(grainTypeName, cancellationToken);
            if (handle != null)
            {
                return handle.GetGrain(grainTypeName, primaryKey);
            }

            return _grainFactory.GetGrainDynamic(grainTypeName, primaryKey);
        }

        /// <inheritdoc />
        public dynamic GetGrain(GrainTypeMeta grainType, string primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainType);
            ArgumentNullException.ThrowIfNull(primaryKey);

            return _grainFactory.GetGrain(grainType, primaryKey);
        }

        /// <inheritdoc />
        public dynamic GetGrain(GrainTypeMeta grainType, Guid primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainType);

            return _grainFactory.GetGrain(grainType, primaryKey);
        }

        /// <inheritdoc />
        public dynamic GetGrain(GrainTypeMeta grainType, long primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainType);

            return _grainFactory.GetGrain(grainType, primaryKey);
        }

        /// <inheritdoc />
        public async Task<object?> InvokeMethodAsync(
            string grainTypeName,
            string primaryKey,
            string methodName,
            object?[]? args = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grainTypeName);
            ArgumentNullException.ThrowIfNull(primaryKey);
            ArgumentNullException.ThrowIfNull(methodName);

            dynamic grain = await GetGrainDynamicAsync(grainTypeName, primaryKey, cancellationToken);

            // Use explicit invocation if available
            if (grain is DynamicGrainReference dynRef)
            {
                return await dynRef.InvokeAsync(methodName, args ?? Array.Empty<object?>());
            }

            // Fall back to DLR
            throw new InvalidOperationException(
                $"Could not invoke method '{methodName}' on grain '{grainTypeName}'. " +
                "The grain reference does not support explicit method invocation.");
        }

        // =============================================
        // GTD Queries
        // =============================================

        /// <inheritdoc />
        public async Task<IReadOnlyList<GrainTypeMeta>> QueryGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null,
            CancellationToken cancellationToken = default)
        {
            var gtd = _grainFactory.GetGrain<IGrainTypeDirectoryGrain>("gtd");
            var types = await gtd.FindGrainTypesAsync(namespaceFilter, namePattern);
            return types;
        }

        /// <inheritdoc />
        public async Task<GrainTypeMeta?> GetGrainTypeMetaAsync(
            string grainTypeName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grainTypeName);

            var gtd = _grainFactory.GetGrain<IGrainTypeDirectoryGrain>("gtd");
            return await gtd.GetGrainTypeAsync(grainTypeName);
        }

        // =============================================
        // Private Helpers
        // =============================================

        private static string MakeKey(string packageId, string version) => $"{packageId}:{version}";

        private async Task OnPackageUnloaded(GrainPackageHandle handle)
        {
            var key = MakeKey(handle.PackageId, handle.Version);
            _loadedPackages.TryRemove(key, out _);

            // Also remove "latest" key if it points to this handle
            var latestKey = MakeKey(handle.PackageId, "latest");
            if (_loadedPackages.TryGetValue(latestKey, out var latestHandle) &&
                latestHandle == handle)
            {
                _loadedPackages.TryRemove(latestKey, out _);
            }

            _logger.LogInformation("Unloaded package {PackageId} v{Version}", handle.PackageId, handle.Version);
        }

        private async Task<GrainPackageHandle?> FindOrLoadPackageForType(
            string grainTypeName,
            CancellationToken cancellationToken)
        {
            // First check loaded packages
            foreach (var handle in _loadedPackages.Values)
            {
                if (!handle.IsLoaded) continue;

                var grainType = handle.GetGrainType(grainTypeName);
                if (grainType != null)
                {
                    return handle;
                }
            }

            // Try to find via GTD
            try
            {
                var meta = await GetGrainTypeMetaAsync(grainTypeName, cancellationToken);
                if (meta?.SourcePackage != null)
                {
                    // Load the package
                    return await LoadPackageAsync(
                        meta.SourcePackage.PackageId,
                        meta.SourcePackage.Version,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to find package for grain type {GrainTypeName} via GTD",
                    grainTypeName);
            }

            return null;
        }
    }
}
