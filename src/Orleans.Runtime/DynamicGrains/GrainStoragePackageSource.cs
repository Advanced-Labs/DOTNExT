using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.DynamicGrains;
using Orleans.Metadata;
using Orleans.Providers;

#nullable enable

namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// A package source that stores grain packages using Orleans grain storage.
    /// No external dependencies required - uses the cluster's own storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Packages are stored in chunks via <see cref="IPackageStorageGrain"/> to handle
    /// large assemblies that may exceed grain state size limits.
    /// </para>
    /// </remarks>
    public class GrainStoragePackageSource : IGrainPackageSource
    {
        private const int ChunkSize = 256 * 1024; // 256 KB chunks

        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<GrainStoragePackageSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainStoragePackageSource"/> class.
        /// </summary>
        /// <param name="grainFactory">The grain factory.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="priority">The source priority.</param>
        public GrainStoragePackageSource(
            IGrainFactory grainFactory,
            ILogger<GrainStoragePackageSource> logger,
            int priority = 200)
        {
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Priority = priority;
        }

        /// <inheritdoc />
        public string Name => "GrainStorage";

        /// <inheritdoc />
        public int Priority { get; }

        /// <inheritdoc />
        public bool IsWritable => true;

        /// <inheritdoc />
        public async Task<LoadedGrainPackage?> FetchAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get the index grain to find available versions
                var indexGrain = _grainFactory.GetGrain<IPackageIndexGrain>("package-index");
                var packages = await indexGrain.GetPackagesAsync();

                // Find matching package
                PackageIndexEntry? entry;
                if (version != null)
                {
                    entry = packages.FirstOrDefault(p =>
                        p.PackageId == packageId && p.Version == version);
                }
                else
                {
                    entry = packages
                        .Where(p => p.PackageId == packageId)
                        .OrderByDescending(p => p.Version)
                        .FirstOrDefault();
                }

                if (entry == null)
                {
                    _logger.LogDebug("Package {PackageId} v{Version} not found in grain storage", packageId, version ?? "latest");
                    return null;
                }

                // Get the storage grain for this package
                var storageGrain = _grainFactory.GetGrain<IPackageStorageGrain>(entry.StorageKey);
                var metadata = await storageGrain.GetMetadataAsync();

                if (metadata == null)
                {
                    _logger.LogWarning("Package metadata missing for {PackageId} v{Version}", packageId, entry.Version);
                    return null;
                }

                // Download all chunks
                var assemblies = new Dictionary<string, byte[]>();
                foreach (var asmInfo in metadata.Assemblies)
                {
                    var chunks = new List<byte[]>();
                    for (int i = 0; i < asmInfo.ChunkCount; i++)
                    {
                        var chunk = await storageGrain.DownloadChunkAsync(asmInfo.FileName, i);
                        if (chunk == null)
                        {
                            _logger.LogError("Missing chunk {Index} for assembly {FileName}", i, asmInfo.FileName);
                            return null;
                        }
                        chunks.Add(chunk);
                    }

                    // Reassemble
                    var totalLength = chunks.Sum(c => c.Length);
                    var assembled = new byte[totalLength];
                    var offset = 0;
                    foreach (var chunk in chunks)
                    {
                        Buffer.BlockCopy(chunk, 0, assembled, offset, chunk.Length);
                        offset += chunk.Length;
                    }

                    assemblies[asmInfo.FileName] = assembled;
                }

                _logger.LogInformation(
                    "Fetched package {PackageId} v{Version} with {AssemblyCount} assemblies from grain storage",
                    packageId, entry.Version, assemblies.Count);

                return new LoadedGrainPackage(metadata.Package, assemblies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch package {PackageId} v{Version} from grain storage", packageId, version);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GrainPackageInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var indexGrain = _grainFactory.GetGrain<IPackageIndexGrain>("package-index");
                var packages = await indexGrain.GetPackagesAsync();

                return packages.Select(p => new GrainPackageInfo(
                    p.PackageId,
                    p.Version,
                    p.ContentHash,
                    p.GrainTypeCount,
                    p.ContentType,
                    ImmutableList<SiloAddress>.Empty))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list packages from grain storage");
                return Array.Empty<GrainPackageInfo>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> PublishAsync(
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var storageKey = $"{package.PackageId}:{package.Version}";
                var storageGrain = _grainFactory.GetGrain<IPackageStorageGrain>(storageKey);

                // Upload assemblies in chunks
                var assemblyInfos = new List<StoredAssemblyInfo>();
                foreach (var (fileName, bytes) in content.Assemblies)
                {
                    var chunkCount = (int)Math.Ceiling((double)bytes.Length / ChunkSize);

                    for (int i = 0; i < chunkCount; i++)
                    {
                        var offset = i * ChunkSize;
                        var length = Math.Min(ChunkSize, bytes.Length - offset);
                        var chunk = new byte[length];
                        Buffer.BlockCopy(bytes, offset, chunk, 0, length);

                        var success = await storageGrain.UploadChunkAsync(fileName, i, chunk);
                        if (!success)
                        {
                            _logger.LogError("Failed to upload chunk {Index} for assembly {FileName}", i, fileName);
                            return false;
                        }
                    }

                    assemblyInfos.Add(new StoredAssemblyInfo(fileName, bytes.Length, chunkCount));
                }

                // Complete upload with metadata
                var metadata = new StoredPackageMetadata(package, assemblyInfos.ToImmutableList());
                var completed = await storageGrain.CompleteUploadAsync(metadata);

                if (!completed)
                {
                    _logger.LogError("Failed to complete package upload for {PackageId} v{Version}", package.PackageId, package.Version);
                    return false;
                }

                // Update index
                var indexGrain = _grainFactory.GetGrain<IPackageIndexGrain>("package-index");
                await indexGrain.AddPackageAsync(new PackageIndexEntry(
                    package.PackageId,
                    package.Version,
                    package.ContentHash,
                    package.GrainTypes.Count,
                    package.ContentType,
                    storageKey));

                _logger.LogInformation(
                    "Published package {PackageId} v{Version} with {AssemblyCount} assemblies to grain storage",
                    package.PackageId, package.Version, content.Assemblies.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish package {PackageId} v{Version} to grain storage", package.PackageId, package.Version);
                return false;
            }
        }
    }

    // =============================================
    // Supporting grain interfaces and types
    // =============================================

    /// <summary>
    /// Index grain that tracks all stored packages.
    /// </summary>
    public interface IPackageIndexGrain : IGrainWithStringKey
    {
        Task<ImmutableList<PackageIndexEntry>> GetPackagesAsync();
        Task AddPackageAsync(PackageIndexEntry entry);
        Task RemovePackageAsync(string packageId, string version);
    }

    /// <summary>
    /// Storage grain for a single package version.
    /// </summary>
    public interface IPackageStorageGrain : IGrainWithStringKey
    {
        Task<bool> UploadChunkAsync(string fileName, int index, byte[] chunk);
        Task<bool> CompleteUploadAsync(StoredPackageMetadata metadata);
        Task<byte[]?> DownloadChunkAsync(string fileName, int index);
        Task<StoredPackageMetadata?> GetMetadataAsync();
    }

    /// <summary>
    /// Entry in the package index.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class PackageIndexEntry
    {
        public PackageIndexEntry(
            string packageId,
            string version,
            string contentHash,
            int grainTypeCount,
            GrainPackageContent contentType,
            string storageKey)
        {
            PackageId = packageId;
            Version = version;
            ContentHash = contentHash;
            GrainTypeCount = grainTypeCount;
            ContentType = contentType;
            StorageKey = storageKey;
        }

        [Id(0)] public string PackageId { get; }
        [Id(1)] public string Version { get; }
        [Id(2)] public string ContentHash { get; }
        [Id(3)] public int GrainTypeCount { get; }
        [Id(4)] public GrainPackageContent ContentType { get; }
        [Id(5)] public string StorageKey { get; }
    }

    /// <summary>
    /// Metadata stored with a package.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class StoredPackageMetadata
    {
        public StoredPackageMetadata(GrainPackage package, ImmutableList<StoredAssemblyInfo> assemblies)
        {
            Package = package;
            Assemblies = assemblies;
        }

        [Id(0)] public GrainPackage Package { get; }
        [Id(1)] public ImmutableList<StoredAssemblyInfo> Assemblies { get; }
    }

    /// <summary>
    /// Info about a stored assembly.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class StoredAssemblyInfo
    {
        public StoredAssemblyInfo(string fileName, int totalSize, int chunkCount)
        {
            FileName = fileName;
            TotalSize = totalSize;
            ChunkCount = chunkCount;
        }

        [Id(0)] public string FileName { get; }
        [Id(1)] public int TotalSize { get; }
        [Id(2)] public int ChunkCount { get; }
    }

    // =============================================
    // Grain implementations
    // =============================================

    /// <summary>
    /// State for the package index grain.
    /// </summary>
    [GenerateSerializer]
    public sealed class PackageIndexState
    {
        [Id(0)]
        public Dictionary<string, PackageIndexEntry> Packages { get; set; } = new();
    }

    /// <summary>
    /// Implementation of the package index grain.
    /// </summary>
    [StorageProvider(ProviderName = ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME)]
    public class PackageIndexGrain : Grain<PackageIndexState>, IPackageIndexGrain
    {
        public Task<ImmutableList<PackageIndexEntry>> GetPackagesAsync()
        {
            return Task.FromResult(State.Packages.Values.ToImmutableList());
        }

        public async Task AddPackageAsync(PackageIndexEntry entry)
        {
            var key = $"{entry.PackageId}:{entry.Version}";
            State.Packages[key] = entry;
            await WriteStateAsync();
        }

        public async Task RemovePackageAsync(string packageId, string version)
        {
            var key = $"{packageId}:{version}";
            if (State.Packages.Remove(key))
            {
                await WriteStateAsync();
            }
        }
    }

    /// <summary>
    /// State for the package storage grain.
    /// </summary>
    [GenerateSerializer]
    public sealed class PackageStorageState
    {
        [Id(0)]
        public StoredPackageMetadata? Metadata { get; set; }

        [Id(1)]
        public Dictionary<string, byte[]> Chunks { get; set; } = new();
    }

    /// <summary>
    /// Implementation of the package storage grain.
    /// </summary>
    [StorageProvider(ProviderName = ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME)]
    public class PackageStorageGrain : Grain<PackageStorageState>, IPackageStorageGrain
    {
        public async Task<bool> UploadChunkAsync(string fileName, int index, byte[] chunk)
        {
            var key = $"{fileName}:{index}";
            State.Chunks[key] = chunk;
            await WriteStateAsync();
            return true;
        }

        public async Task<bool> CompleteUploadAsync(StoredPackageMetadata metadata)
        {
            State.Metadata = metadata;
            await WriteStateAsync();
            return true;
        }

        public Task<byte[]?> DownloadChunkAsync(string fileName, int index)
        {
            var key = $"{fileName}:{index}";
            State.Chunks.TryGetValue(key, out var chunk);
            return Task.FromResult(chunk);
        }

        public Task<StoredPackageMetadata?> GetMetadataAsync()
        {
            return Task.FromResult(State.Metadata);
        }
    }
}
