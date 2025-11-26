using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans.DynamicGrains;
using Orleans.Metadata;

#nullable enable

namespace Orleans.DynamicGrains
{
    /// <summary>
    /// Local cache for grain packages. Used by both silos and clients
    /// to avoid repeated downloads from package sources.
    /// </summary>
    public interface IGrainPackageCache
    {
        /// <summary>
        /// Gets a cached package, or null if not cached.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">Optional version. If null, returns the latest cached version.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The cached package content, or null if not found.</returns>
        Task<GrainPackageContent?> GetAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a package to the cache.
        /// </summary>
        /// <param name="content">The package content to cache.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if cached successfully.</returns>
        Task<bool> PutAsync(
            GrainPackageContent content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a package from the cache.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">Optional version. If null, removes all versions.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if removed.</returns>
        Task<bool> EvictAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached packages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        GrainPackageCacheStats GetStats();

        /// <summary>
        /// Checks if a package is cached.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">The package version.</param>
        /// <returns>True if the package is cached.</returns>
        bool Contains(string packageId, string version);
    }

    /// <summary>
    /// Statistics about the package cache.
    /// </summary>
    public sealed class GrainPackageCacheStats
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageCacheStats"/> class.
        /// </summary>
        public GrainPackageCacheStats(
            int packageCount,
            long totalSizeBytes,
            long hitCount,
            long missCount,
            long evictionCount)
        {
            PackageCount = packageCount;
            TotalSizeBytes = totalSizeBytes;
            HitCount = hitCount;
            MissCount = missCount;
            EvictionCount = evictionCount;
        }

        /// <summary>
        /// Gets the number of packages in the cache.
        /// </summary>
        public int PackageCount { get; }

        /// <summary>
        /// Gets the total size of cached packages in bytes.
        /// </summary>
        public long TotalSizeBytes { get; }

        /// <summary>
        /// Gets the number of cache hits.
        /// </summary>
        public long HitCount { get; }

        /// <summary>
        /// Gets the number of cache misses.
        /// </summary>
        public long MissCount { get; }

        /// <summary>
        /// Gets the number of evictions.
        /// </summary>
        public long EvictionCount { get; }

        /// <summary>
        /// Gets the hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate => HitCount + MissCount > 0
            ? (double)HitCount / (HitCount + MissCount)
            : 0.0;

        /// <summary>
        /// Gets the total size in megabytes.
        /// </summary>
        public double TotalSizeMB => TotalSizeBytes / (1024.0 * 1024.0);
    }

    /// <summary>
    /// Configuration options for the package cache.
    /// </summary>
    public sealed class GrainPackageCacheOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of packages to cache.
        /// Default: 100
        /// </summary>
        public int MaxPackageCount { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum total cache size in bytes.
        /// Default: 500 MB
        /// </summary>
        public long MaxTotalSizeBytes { get; set; } = 500 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the cache directory path for file-based caches.
        /// </summary>
        public string? CacheDirectory { get; set; }

        /// <summary>
        /// Gets or sets the time after which a cached package expires.
        /// Default: 24 hours. Set to null for no expiration.
        /// </summary>
        public TimeSpan? ExpirationTime { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the eviction policy.
        /// Default: LRU (Least Recently Used)
        /// </summary>
        public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LeastRecentlyUsed;
    }

    /// <summary>
    /// Cache eviction policies.
    /// </summary>
    public enum CacheEvictionPolicy
    {
        /// <summary>
        /// Evict least recently used packages first.
        /// </summary>
        LeastRecentlyUsed,

        /// <summary>
        /// Evict least frequently used packages first.
        /// </summary>
        LeastFrequentlyUsed,

        /// <summary>
        /// Evict oldest packages first (FIFO).
        /// </summary>
        FirstInFirstOut,

        /// <summary>
        /// Evict largest packages first.
        /// </summary>
        LargestFirst
    }
}
