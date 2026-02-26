using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Metadata;

#nullable enable

namespace Orleans.DynamicGrains
{
    /// <summary>
    /// File system-based implementation of <see cref="IGrainPackageCache"/>.
    /// Caches packages on local disk with configurable eviction policies.
    /// </summary>
    public class FileSystemPackageCache : IGrainPackageCache, IDisposable
    {
        private readonly string _cacheDir;
        private readonly GrainPackageCacheOptions _options;
        private readonly ILogger<FileSystemPackageCache> _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
        private readonly SemaphoreSlim _evictionLock = new(1, 1);

        private long _hitCount;
        private long _missCount;
        private long _evictionCount;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemPackageCache"/> class.
        /// </summary>
        /// <param name="options">Cache options.</param>
        /// <param name="logger">The logger.</param>
        public FileSystemPackageCache(
            IOptions<GrainPackageCacheOptions> options,
            ILogger<FileSystemPackageCache> logger)
            : this(options.Value, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemPackageCache"/> class.
        /// </summary>
        /// <param name="options">Cache options.</param>
        /// <param name="logger">The logger.</param>
        public FileSystemPackageCache(
            GrainPackageCacheOptions options,
            ILogger<FileSystemPackageCache> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _cacheDir = options.CacheDirectory
                ?? Path.Combine(Path.GetTempPath(), "orleans-package-cache");

            Directory.CreateDirectory(_cacheDir);

            // Load existing cache entries
            LoadExistingEntries();

            _logger.LogInformation(
                "Package cache initialized at {Path} with {Count} existing entries",
                _cacheDir, _entries.Count);
        }

        /// <inheritdoc />
        public async Task<LoadedGrainPackage?> GetAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            var key = version != null
                ? MakeKey(packageId, version)
                : _entries.Keys
                    .Where(k => k.StartsWith($"{packageId}:"))
                    .OrderByDescending(k => k)
                    .FirstOrDefault();

            if (key == null || !_entries.TryGetValue(key, out var entry))
            {
                Interlocked.Increment(ref _missCount);
                return null;
            }

            // Check expiration
            if (_options.ExpirationTime.HasValue &&
                DateTime.UtcNow - entry.CachedAt > _options.ExpirationTime.Value)
            {
                _logger.LogDebug("Cache entry expired: {Key}", key);
                await EvictAsync(packageId, version, cancellationToken);
                Interlocked.Increment(ref _missCount);
                return null;
            }

            // Try to load from disk
            var content = await LoadFromDiskAsync(entry, cancellationToken);
            if (content == null)
            {
                // File missing, remove entry
                _entries.TryRemove(key, out _);
                Interlocked.Increment(ref _missCount);
                return null;
            }

            // Update access info
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;

            Interlocked.Increment(ref _hitCount);
            _logger.LogDebug("Cache hit: {Key}", key);
            return content;
        }

        /// <inheritdoc />
        public async Task<bool> PutAsync(
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            var key = MakeKey(content.Package.PackageId, content.Package.Version);

            // Check if we need to evict
            await EnsureCapacityAsync(content.TotalSize, cancellationToken);

            // Save to disk
            var packageDir = GetPackageDir(content.Package.PackageId, content.Package.Version);
            Directory.CreateDirectory(packageDir);

            try
            {
                // Write assemblies
                foreach (var (fileName, bytes) in content.Assemblies)
                {
                    var filePath = Path.Combine(packageDir, fileName);
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                }

                // Write metadata
                var metadataPath = Path.Combine(packageDir, "cache-metadata.json");
                var metadata = new CacheMetadata
                {
                    PackageId = content.Package.PackageId,
                    Version = content.Package.Version,
                    ContentHash = content.Package.ContentHash,
                    CachedAt = DateTime.UtcNow,
                    TotalSize = content.TotalSize,
                    AssemblyFiles = content.Assemblies.Keys.ToList()
                };
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

                // Add to index
                var entry = new CacheEntry
                {
                    PackageId = content.Package.PackageId,
                    Version = content.Package.Version,
                    CachedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    Size = content.TotalSize,
                    Path = packageDir,
                    AccessCount = 0
                };
                _entries[key] = entry;

                _logger.LogInformation(
                    "Cached package {PackageId} v{Version} ({Size:N0} bytes)",
                    content.Package.PackageId, content.Package.Version, content.TotalSize);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache package {PackageId} v{Version}",
                    content.Package.PackageId, content.Package.Version);

                // Clean up partial write
                try { Directory.Delete(packageDir, recursive: true); } catch { }
                return false;
            }
        }

        /// <inheritdoc />
        public Task<bool> EvictAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            var keysToRemove = version != null
                ? new[] { MakeKey(packageId, version) }
                : _entries.Keys.Where(k => k.StartsWith($"{packageId}:")).ToArray();

            var removed = false;
            foreach (var key in keysToRemove)
            {
                if (_entries.TryRemove(key, out var entry))
                {
                    try
                    {
                        if (Directory.Exists(entry.Path))
                        {
                            Directory.Delete(entry.Path, recursive: true);
                        }
                        Interlocked.Increment(ref _evictionCount);
                        removed = true;
                        _logger.LogDebug("Evicted package {Key}", key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache directory: {Path}", entry.Path);
                    }
                }
            }

            return Task.FromResult(removed);
        }

        /// <inheritdoc />
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            var keys = _entries.Keys.ToArray();
            foreach (var key in keys)
            {
                if (_entries.TryRemove(key, out var entry))
                {
                    try
                    {
                        if (Directory.Exists(entry.Path))
                        {
                            Directory.Delete(entry.Path, recursive: true);
                        }
                    }
                    catch { }
                }
            }

            _logger.LogInformation("Cleared all {Count} cached packages", keys.Length);
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public GrainPackageCacheStats GetStats()
        {
            var totalSize = _entries.Values.Sum(e => e.Size);
            return new GrainPackageCacheStats(
                _entries.Count,
                totalSize,
                Interlocked.Read(ref _hitCount),
                Interlocked.Read(ref _missCount),
                Interlocked.Read(ref _evictionCount));
        }

        /// <inheritdoc />
        public bool Contains(string packageId, string version)
        {
            var key = MakeKey(packageId, version);
            return _entries.ContainsKey(key);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _evictionLock.Dispose();
                _disposed = true;
            }
        }

        private static string MakeKey(string packageId, string version) => $"{packageId}:{version}";

        private string GetPackageDir(string packageId, string version)
            => Path.Combine(_cacheDir, packageId, version);

        private void LoadExistingEntries()
        {
            if (!Directory.Exists(_cacheDir)) return;

            foreach (var packageDir in Directory.GetDirectories(_cacheDir))
            {
                var packageId = Path.GetFileName(packageDir);
                foreach (var versionDir in Directory.GetDirectories(packageDir))
                {
                    var version = Path.GetFileName(versionDir);
                    var metadataPath = Path.Combine(versionDir, "cache-metadata.json");

                    if (File.Exists(metadataPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(metadataPath);
                            var metadata = JsonSerializer.Deserialize<CacheMetadata>(json);
                            if (metadata != null)
                            {
                                var key = MakeKey(packageId, version);
                                _entries[key] = new CacheEntry
                                {
                                    PackageId = packageId,
                                    Version = version,
                                    CachedAt = metadata.CachedAt,
                                    LastAccessedAt = metadata.CachedAt,
                                    Size = metadata.TotalSize,
                                    Path = versionDir,
                                    AccessCount = 0
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load cache metadata from {Path}", metadataPath);
                        }
                    }
                }
            }
        }

        private async Task<LoadedGrainPackage?> LoadFromDiskAsync(
            CacheEntry entry,
            CancellationToken cancellationToken)
        {
            var metadataPath = Path.Combine(entry.Path, "cache-metadata.json");
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<CacheMetadata>(json);
                if (metadata == null) return null;

                // Load assemblies
                var assemblies = new Dictionary<string, byte[]>();
                foreach (var fileName in metadata.AssemblyFiles)
                {
                    var filePath = Path.Combine(entry.Path, fileName);
                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("Missing assembly file: {Path}", filePath);
                        return null;
                    }
                    assemblies[fileName] = await File.ReadAllBytesAsync(filePath, cancellationToken);
                }

                // Create minimal package (we don't store full grain type metadata in cache)
                var package = new GrainPackage(
                    metadata.PackageId,
                    metadata.Version,
                    metadata.ContentHash,
                    ImmutableList<GrainTypeMeta>.Empty,
                    GrainPackageContent.Full,
                    ImmutableList<GrainPackageAssembly>.Empty,
                    ImmutableDictionary<string, string>.Empty);

                return new LoadedGrainPackage(package, assemblies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load package from cache: {Path}", entry.Path);
                return null;
            }
        }

        private async Task EnsureCapacityAsync(long requiredSize, CancellationToken cancellationToken)
        {
            await _evictionLock.WaitAsync(cancellationToken);
            try
            {
                var currentSize = _entries.Values.Sum(e => e.Size);
                var currentCount = _entries.Count;

                // Check if eviction needed
                var needsEviction =
                    currentCount >= _options.MaxPackageCount ||
                    currentSize + requiredSize > _options.MaxTotalSizeBytes;

                if (!needsEviction) return;

                // Get entries sorted by eviction policy
                var entriesToEvict = GetEvictionCandidates()
                    .Take(Math.Max(1, currentCount / 4)) // Evict up to 25%
                    .ToList();

                foreach (var entry in entriesToEvict)
                {
                    var key = MakeKey(entry.PackageId, entry.Version);
                    if (_entries.TryRemove(key, out _))
                    {
                        try
                        {
                            if (Directory.Exists(entry.Path))
                            {
                                Directory.Delete(entry.Path, recursive: true);
                            }
                            Interlocked.Increment(ref _evictionCount);
                            _logger.LogDebug("Evicted {Key} due to capacity", key);
                        }
                        catch { }
                    }

                    // Check if we have enough space now
                    currentSize = _entries.Values.Sum(e => e.Size);
                    if (_entries.Count < _options.MaxPackageCount &&
                        currentSize + requiredSize <= _options.MaxTotalSizeBytes)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _evictionLock.Release();
            }
        }

        private IEnumerable<CacheEntry> GetEvictionCandidates()
        {
            return _options.EvictionPolicy switch
            {
                CacheEvictionPolicy.LeastRecentlyUsed =>
                    _entries.Values.OrderBy(e => e.LastAccessedAt),

                CacheEvictionPolicy.LeastFrequentlyUsed =>
                    _entries.Values.OrderBy(e => e.AccessCount),

                CacheEvictionPolicy.FirstInFirstOut =>
                    _entries.Values.OrderBy(e => e.CachedAt),

                CacheEvictionPolicy.LargestFirst =>
                    _entries.Values.OrderByDescending(e => e.Size),

                _ => _entries.Values.OrderBy(e => e.LastAccessedAt)
            };
        }

        private class CacheEntry
        {
            public required string PackageId { get; init; }
            public required string Version { get; init; }
            public required DateTime CachedAt { get; init; }
            public DateTime LastAccessedAt { get; set; }
            public required long Size { get; init; }
            public required string Path { get; init; }
            public int AccessCount { get; set; }
        }

        private class CacheMetadata
        {
            public string PackageId { get; set; } = "";
            public string Version { get; set; } = "";
            public string ContentHash { get; set; } = "";
            public DateTime CachedAt { get; set; }
            public long TotalSize { get; set; }
            public List<string> AssemblyFiles { get; set; } = new();
        }
    }
}
