// VAYRON - Runtime-Integrated Persistent Storage
// Side table for handle metadata
//
// Phase 3: Enhanced with statistics, enumeration, native access support, and lifecycle management

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vayron;

/// <summary>
/// Side table storing metadata for VAYRON handles.
/// Uses ConditionalWeakTable for GC-friendly weak keying.
/// </summary>
/// <remarks>
/// <para><b>Phase 3 Enhancements:</b></para>
/// <list type="bullet">
/// <item><description>Statistics and diagnostics</description></item>
/// <item><description>Enumeration support for lifecycle management</description></item>
/// <item><description>Native interop (FCalls for runtime access)</description></item>
/// <item><description>Memory pressure callbacks</description></item>
/// <item><description>Tracking for eviction candidates</description></item>
/// </list>
///
/// <para>This pattern is proven in the CLR (e.g., DependentHandle).</para>
/// <para>When a handle object is collected, its metadata is automatically removed.</para>
/// </remarks>
public static class VayronMetaTable
{
    // =====================================================================
    // Primary Storage
    // =====================================================================

    /// <summary>
    /// The underlying weak table: handle object -> metadata.
    /// </summary>
    private static readonly ConditionalWeakTable<object, VayronMeta> _table = new();

    /// <summary>
    /// Secondary index: OID -> WeakReference to handle object.
    /// Allows looking up handles by OID for lifecycle management.
    /// </summary>
    private static readonly ConcurrentDictionary<VayronOid, WeakReference<object>> _oidIndex = new();

    /// <summary>
    /// Registered eviction callbacks for memory pressure.
    /// </summary>
    private static readonly List<Action<EvictionRequestEventArgs>> _evictionCallbacks = new();

    /// <summary>
    /// Lock for eviction callback registration.
    /// </summary>
    private static readonly object _callbackLock = new();

    // =====================================================================
    // Statistics (Phase 3)
    // =====================================================================

    private static long _getCount;
    private static long _setCount;
    private static long _removeCount;
    private static long _missCount;
    private static long _totalBytesTracked;
    private static long _peakBytesTracked;
    private static int _activeCount;

    /// <summary>
    /// Gets the number of Get operations.
    /// </summary>
    public static long GetCount => Volatile.Read(ref _getCount);

    /// <summary>
    /// Gets the number of Set operations.
    /// </summary>
    public static long SetCount => Volatile.Read(ref _setCount);

    /// <summary>
    /// Gets the number of Remove operations.
    /// </summary>
    public static long RemoveCount => Volatile.Read(ref _removeCount);

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public static long MissCount => Volatile.Read(ref _missCount);

    /// <summary>
    /// Gets the total bytes tracked across all metadata entries.
    /// </summary>
    public static long TotalBytesTracked => Volatile.Read(ref _totalBytesTracked);

    /// <summary>
    /// Gets the peak bytes tracked.
    /// </summary>
    public static long PeakBytesTracked => Volatile.Read(ref _peakBytesTracked);

    /// <summary>
    /// Gets the approximate active entry count.
    /// </summary>
    public static int ActiveCount => Volatile.Read(ref _activeCount);

    // =====================================================================
    // Core Operations
    // =====================================================================

    /// <summary>
    /// Gets the metadata for a handle, or null if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VayronMeta? Get(object handle)
    {
        Interlocked.Increment(ref _getCount);

        if (_table.TryGetValue(handle, out var meta))
        {
            return meta;
        }

        Interlocked.Increment(ref _missCount);
        return null;
    }

    /// <summary>
    /// Gets existing metadata or creates new metadata for the handle.
    /// </summary>
    public static VayronMeta GetOrCreate(object handle, VayronOid oid)
    {
        return _table.GetValue(handle, h =>
        {
            Interlocked.Increment(ref _setCount);
            Interlocked.Increment(ref _activeCount);

            var meta = new VayronMeta(oid);

            // Track in OID index
            _oidIndex[oid] = new WeakReference<object>(handle);

            return meta;
        });
    }

    /// <summary>
    /// Sets the metadata for a handle.
    /// </summary>
    public static void Set(object handle, VayronMeta meta)
    {
        Interlocked.Increment(ref _setCount);

        _table.AddOrUpdate(handle, meta);

        // Track in OID index
        _oidIndex[meta.Oid] = new WeakReference<object>(handle);

        // Update statistics
        var oldActive = Interlocked.Increment(ref _activeCount);
        UpdateBytesTracked(meta.CachedBodySize);
    }

    /// <summary>
    /// Removes metadata for a handle.
    /// </summary>
    public static bool Remove(object handle)
    {
        Interlocked.Increment(ref _removeCount);

        // Get metadata first for cleanup
        if (_table.TryGetValue(handle, out var meta))
        {
            // Remove from OID index
            _oidIndex.TryRemove(meta.Oid, out _);

            // Update statistics
            Interlocked.Decrement(ref _activeCount);
            Interlocked.Add(ref _totalBytesTracked, -meta.CachedBodySize);

            // Dispose metadata to clean up native resources
            meta.Dispose();
        }

        return _table.Remove(handle);
    }

    /// <summary>
    /// Tries to get metadata for a handle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(object handle, out VayronMeta? meta)
    {
        Interlocked.Increment(ref _getCount);

        if (_table.TryGetValue(handle, out meta))
        {
            return true;
        }

        Interlocked.Increment(ref _missCount);
        meta = null;
        return false;
    }

    // =====================================================================
    // OID-Based Lookup (Phase 3)
    // =====================================================================

    /// <summary>
    /// Tries to get the handle object for an OID.
    /// </summary>
    /// <param name="oid">The OID to look up.</param>
    /// <param name="handle">The handle object if found.</param>
    /// <returns>True if the handle was found and is still alive.</returns>
    public static bool TryGetHandleByOid(VayronOid oid, out object? handle)
    {
        handle = null;

        if (_oidIndex.TryGetValue(oid, out var weakRef))
        {
            if (weakRef.TryGetTarget(out handle))
            {
                return true;
            }

            // Weak reference expired, clean up
            _oidIndex.TryRemove(oid, out _);
        }

        return false;
    }

    /// <summary>
    /// Tries to get metadata by OID.
    /// </summary>
    public static bool TryGetByOid(VayronOid oid, out VayronMeta? meta)
    {
        meta = null;

        if (TryGetHandleByOid(oid, out var handle) && handle != null)
        {
            return TryGet(handle, out meta);
        }

        return false;
    }

    // =====================================================================
    // Enumeration (Phase 3) - For Lifecycle Management
    // =====================================================================

    /// <summary>
    /// Gets all OIDs currently tracked.
    /// </summary>
    /// <remarks>
    /// Some OIDs may have been collected by the GC since tracking began.
    /// </remarks>
    public static IEnumerable<VayronOid> GetAllOids()
    {
        // Clean up dead references while iterating
        var deadOids = new List<VayronOid>();

        foreach (var kvp in _oidIndex)
        {
            if (kvp.Value.TryGetTarget(out _))
            {
                yield return kvp.Key;
            }
            else
            {
                deadOids.Add(kvp.Key);
            }
        }

        // Clean up dead references
        foreach (var oid in deadOids)
        {
            _oidIndex.TryRemove(oid, out _);
        }
    }

    /// <summary>
    /// Gets all metadata entries that are candidates for eviction.
    /// </summary>
    /// <param name="maxAge">Maximum age in ticks since last access.</param>
    /// <param name="maxCount">Maximum number of candidates to return.</param>
    public static IEnumerable<(VayronOid Oid, VayronMeta Meta)> GetEvictionCandidates(long maxAge, int maxCount)
    {
        var now = Environment.TickCount64;
        var count = 0;

        foreach (var oid in GetAllOids())
        {
            if (count >= maxCount)
                break;

            if (TryGetByOid(oid, out var meta) && meta != null)
            {
                // Check if evictable
                if (VayronStateManager.CanEvict(meta.State))
                {
                    var age = now - meta.LastAccessTicks;
                    if (age > maxAge)
                    {
                        count++;
                        yield return (oid, meta);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets metadata entries sorted by last access time (oldest first).
    /// </summary>
    /// <param name="maxCount">Maximum number of entries to return.</param>
    public static IEnumerable<(VayronOid Oid, VayronMeta Meta)> GetLruCandidates(int maxCount)
    {
        var candidates = new List<(VayronOid Oid, VayronMeta Meta, long LastAccess)>();

        foreach (var oid in GetAllOids())
        {
            if (TryGetByOid(oid, out var meta) && meta != null && VayronStateManager.CanEvict(meta.State))
            {
                candidates.Add((oid, meta, meta.LastAccessTicks));
            }
        }

        // Sort by last access time (oldest first)
        candidates.Sort((a, b) => a.LastAccess.CompareTo(b.LastAccess));

        return candidates.Take(maxCount).Select(c => (c.Oid, c.Meta));
    }

    /// <summary>
    /// Gets metadata entries in dirty state.
    /// </summary>
    public static IEnumerable<(VayronOid Oid, VayronMeta Meta)> GetDirtyEntries()
    {
        foreach (var oid in GetAllOids())
        {
            if (TryGetByOid(oid, out var meta) && meta != null)
            {
                if (VayronStateManager.HasPendingWrites(meta.State))
                {
                    yield return (oid, meta);
                }
            }
        }
    }

    // =====================================================================
    // Memory Pressure (Phase 3)
    // =====================================================================

    /// <summary>
    /// Updates the total bytes tracked.
    /// </summary>
    private static void UpdateBytesTracked(int delta)
    {
        var newTotal = Interlocked.Add(ref _totalBytesTracked, delta);

        // Update peak
        long currentPeak;
        while (newTotal > (currentPeak = Volatile.Read(ref _peakBytesTracked)))
        {
            if (Interlocked.CompareExchange(ref _peakBytesTracked, newTotal, currentPeak) == currentPeak)
                break;
        }
    }

    /// <summary>
    /// Registers a callback for eviction requests.
    /// </summary>
    public static void RegisterEvictionCallback(Action<EvictionRequestEventArgs> callback)
    {
        lock (_callbackLock)
        {
            _evictionCallbacks.Add(callback);
        }
    }

    /// <summary>
    /// Unregisters an eviction callback.
    /// </summary>
    public static void UnregisterEvictionCallback(Action<EvictionRequestEventArgs> callback)
    {
        lock (_callbackLock)
        {
            _evictionCallbacks.Remove(callback);
        }
    }

    /// <summary>
    /// Requests eviction of cached bodies due to memory pressure.
    /// </summary>
    /// <param name="bytesNeeded">The number of bytes to try to free.</param>
    /// <returns>The actual number of bytes freed.</returns>
    public static long RequestEviction(long bytesNeeded)
    {
        var args = new EvictionRequestEventArgs(bytesNeeded);

        // Notify callbacks
        List<Action<EvictionRequestEventArgs>> callbacks;
        lock (_callbackLock)
        {
            callbacks = new List<Action<EvictionRequestEventArgs>>(_evictionCallbacks);
        }

        foreach (var callback in callbacks)
        {
            try
            {
                callback(args);
            }
            catch
            {
                // Ignore exceptions from callbacks
            }
        }

        // Default eviction: LRU
        if (args.BytesFreed < bytesNeeded)
        {
            var remaining = bytesNeeded - args.BytesFreed;
            var lruFreed = EvictLru(remaining);
            args.BytesFreed += lruFreed;
        }

        return args.BytesFreed;
    }

    /// <summary>
    /// Evicts least recently used entries until target bytes are freed.
    /// </summary>
    /// <param name="targetBytes">Target bytes to free.</param>
    /// <returns>Actual bytes freed.</returns>
    private static long EvictLru(long targetBytes)
    {
        long freedBytes = 0;

        foreach (var (_, meta) in GetLruCandidates(100))
        {
            var evicted = meta.Evict(EvictionReason.MemoryPressure);
            freedBytes += evicted;

            if (freedBytes >= targetBytes)
                break;
        }

        return freedBytes;
    }

    // =====================================================================
    // Native Interop (Phase 3) - For Runtime Access
    // =====================================================================

    /// <summary>
    /// Gets the metadata pointer for a handle (for native interop).
    /// </summary>
    /// <param name="handle">The handle object.</param>
    /// <returns>A GCHandle that pins the metadata, or default if not found.</returns>
    /// <remarks>
    /// The caller must free the GCHandle when done.
    /// </remarks>
    public static GCHandle GetMetadataHandle(object handle)
    {
        if (_table.TryGetValue(handle, out var meta))
        {
            return GCHandle.Alloc(meta, GCHandleType.Normal);
        }

        return default;
    }

    /// <summary>
    /// Gets the cached body pointer for a handle (for native interop).
    /// </summary>
    /// <param name="handle">The handle object.</param>
    /// <param name="bodyPtr">Output: pointer to the cached body.</param>
    /// <param name="bodySize">Output: size of the cached body.</param>
    /// <returns>True if the body is available.</returns>
    public static bool TryGetCachedBodyPtr(object handle, out IntPtr bodyPtr, out int bodySize)
    {
        if (_table.TryGetValue(handle, out var meta) && meta.CachedBodyPtr != IntPtr.Zero)
        {
            bodyPtr = meta.CachedBodyPtr;
            bodySize = meta.CachedBodySize;
            return true;
        }

        bodyPtr = IntPtr.Zero;
        bodySize = 0;
        return false;
    }

    /// <summary>
    /// Gets the OID for a handle (for native interop).
    /// </summary>
    public static bool TryGetOid(object handle, out long oid)
    {
        if (_table.TryGetValue(handle, out var meta))
        {
            oid = meta.Oid.Value;
            return true;
        }

        oid = 0;
        return false;
    }

    /// <summary>
    /// Gets the materialization state for a handle (for native interop).
    /// </summary>
    public static bool TryGetState(object handle, out int state)
    {
        if (_table.TryGetValue(handle, out var meta))
        {
            state = (int)meta.State;
            return true;
        }

        state = 0;
        return false;
    }

    // =====================================================================
    // Diagnostics (Phase 3)
    // =====================================================================

    /// <summary>
    /// Gets comprehensive statistics about the side table.
    /// </summary>
    public static SideTableStatistics GetStatistics()
    {
        return new SideTableStatistics
        {
            GetCount = GetCount,
            SetCount = SetCount,
            RemoveCount = RemoveCount,
            MissCount = MissCount,
            ActiveCount = ActiveCount,
            TotalBytesTracked = TotalBytesTracked,
            PeakBytesTracked = PeakBytesTracked,
            OidIndexCount = _oidIndex.Count,
            HitRate = GetCount > 0 ? (double)(GetCount - MissCount) / GetCount : 0,
        };
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public static void ResetStatistics()
    {
        Interlocked.Exchange(ref _getCount, 0);
        Interlocked.Exchange(ref _setCount, 0);
        Interlocked.Exchange(ref _removeCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
        Interlocked.Exchange(ref _peakBytesTracked, _totalBytesTracked);
    }

    /// <summary>
    /// Cleans up expired weak references in the OID index.
    /// </summary>
    /// <returns>The number of entries cleaned up.</returns>
    public static int CleanupExpiredReferences()
    {
        var cleaned = 0;
        var deadOids = new List<VayronOid>();

        foreach (var kvp in _oidIndex)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                deadOids.Add(kvp.Key);
            }
        }

        foreach (var oid in deadOids)
        {
            if (_oidIndex.TryRemove(oid, out _))
            {
                cleaned++;
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Dumps the side table state for debugging.
    /// </summary>
    [Conditional("DEBUG")]
    public static void DumpState(Action<string> output)
    {
        output("=== VayronMetaTable State ===");
        output($"Active entries: ~{ActiveCount}");
        output($"OID index size: {_oidIndex.Count}");
        output($"Total bytes: {TotalBytesTracked:N0}");
        output($"Peak bytes: {PeakBytesTracked:N0}");
        output($"Get/Miss: {GetCount}/{MissCount} ({GetStatistics().HitRate:P1} hit rate)");
        output("");
        output("Entries by state:");

        var stateGroups = new Dictionary<MaterializationState, int>();
        foreach (var oid in GetAllOids())
        {
            if (TryGetByOid(oid, out var meta) && meta != null)
            {
                stateGroups.TryGetValue(meta.State, out var count);
                stateGroups[meta.State] = count + 1;
            }
        }

        foreach (var (state, count) in stateGroups)
        {
            output($"  {state}: {count}");
        }
    }
}

/// <summary>
/// Statistics about the side table.
/// </summary>
public readonly struct SideTableStatistics
{
    /// <summary>Number of Get operations.</summary>
    public long GetCount { get; init; }

    /// <summary>Number of Set operations.</summary>
    public long SetCount { get; init; }

    /// <summary>Number of Remove operations.</summary>
    public long RemoveCount { get; init; }

    /// <summary>Number of cache misses.</summary>
    public long MissCount { get; init; }

    /// <summary>Approximate active entry count.</summary>
    public int ActiveCount { get; init; }

    /// <summary>Total bytes tracked across all entries.</summary>
    public long TotalBytesTracked { get; init; }

    /// <summary>Peak bytes tracked.</summary>
    public long PeakBytesTracked { get; init; }

    /// <summary>Size of the OID index.</summary>
    public int OidIndexCount { get; init; }

    /// <summary>Cache hit rate (0.0 to 1.0).</summary>
    public double HitRate { get; init; }

    public override string ToString()
    {
        return $"Active={ActiveCount} | Bytes={TotalBytesTracked:N0} | Get={GetCount} Miss={MissCount} ({HitRate:P1})";
    }
}

/// <summary>
/// Event arguments for eviction requests.
/// </summary>
public sealed class EvictionRequestEventArgs : EventArgs
{
    /// <summary>The number of bytes requested to be freed.</summary>
    public long BytesRequested { get; }

    /// <summary>The number of bytes actually freed (updated by handlers).</summary>
    public long BytesFreed { get; set; }

    /// <summary>Whether the eviction request was handled.</summary>
    public bool Handled { get; set; }

    public EvictionRequestEventArgs(long bytesRequested)
    {
        BytesRequested = bytesRequested;
    }
}
