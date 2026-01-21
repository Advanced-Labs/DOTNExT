// VAYRON - Runtime-Integrated Persistent Storage
// Lifecycle Manager for handle cleanup and memory pressure
//
// Phase 3: Background cleanup, eviction policies, and memory pressure response

using System.Collections.Concurrent;

namespace Vayron;

/// <summary>
/// Manages the lifecycle of VAYRON handles including background cleanup,
/// memory pressure response, and eviction policies.
/// </summary>
/// <remarks>
/// <para><b>Features:</b></para>
/// <list type="bullet">
/// <item><description>Background cleanup of stale handles</description></item>
/// <item><description>Memory pressure detection and response</description></item>
/// <item><description>LRU/LFU eviction policies</description></item>
/// <item><description>Finalization tracking</description></item>
/// <item><description>Periodic weak reference cleanup</description></item>
/// </list>
/// </remarks>
public sealed class VayronLifecycleManager : IDisposable
{
    // =====================================================================
    // Configuration
    // =====================================================================

    /// <summary>
    /// Configuration options for the lifecycle manager.
    /// </summary>
    public sealed class Options
    {
        /// <summary>
        /// Whether to enable background cleanup.
        /// </summary>
        public bool EnableBackgroundCleanup { get; init; } = true;

        /// <summary>
        /// Interval between cleanup cycles in milliseconds.
        /// </summary>
        public int CleanupIntervalMs { get; init; } = 30_000; // 30 seconds

        /// <summary>
        /// Maximum cached body age before eviction (in milliseconds).
        /// </summary>
        public long MaxBodyAgeMs { get; init; } = 60_000; // 60 seconds

        /// <summary>
        /// Target memory pressure threshold (0.0 to 1.0).
        /// When memory pressure exceeds this, eviction begins.
        /// </summary>
        public double MemoryPressureThreshold { get; init; } = 0.75;

        /// <summary>
        /// Maximum total bytes to allow before proactive eviction.
        /// </summary>
        public long MaxTotalBytes { get; init; } = 100 * 1024 * 1024; // 100 MB

        /// <summary>
        /// Number of entries to evict per cleanup cycle.
        /// </summary>
        public int MaxEvictionsPerCycle { get; init; } = 100;

        /// <summary>
        /// Whether to pin frequently accessed bodies.
        /// </summary>
        public bool AutoPinHotBodies { get; init; } = false;

        /// <summary>
        /// Access count threshold for auto-pinning.
        /// </summary>
        public int HotBodyAccessThreshold { get; init; } = 100;
    }

    // =====================================================================
    // Singleton Instance
    // =====================================================================

    private static VayronLifecycleManager? _instance;
    private static readonly object _instanceLock = new();

    /// <summary>
    /// Gets the singleton instance of the lifecycle manager.
    /// </summary>
    public static VayronLifecycleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new VayronLifecycleManager(new Options());
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initializes the lifecycle manager with custom options.
    /// Must be called before accessing Instance if custom options are needed.
    /// </summary>
    public static void Initialize(Options options)
    {
        lock (_instanceLock)
        {
            _instance?.Dispose();
            _instance = new VayronLifecycleManager(options);
        }
    }

    // =====================================================================
    // Instance Fields
    // =====================================================================

    private readonly Options _options;
    private readonly CancellationTokenSource _cts;
    private readonly Task? _cleanupTask;
    private readonly ConcurrentQueue<FinalizationRecord> _finalizationQueue;
    private readonly object _statsLock = new();
    private bool _disposed;

    // Statistics
    private long _totalEvictions;
    private long _totalBytesEvicted;
    private long _cleanupCycles;
    private long _memoryPressureEvents;
    private DateTimeOffset _lastCleanup;

    // =====================================================================
    // Constructor
    // =====================================================================

    /// <summary>
    /// Creates a new lifecycle manager with the specified options.
    /// </summary>
    public VayronLifecycleManager(Options options)
    {
        _options = options;
        _cts = new CancellationTokenSource();
        _finalizationQueue = new ConcurrentQueue<FinalizationRecord>();
        _lastCleanup = DateTimeOffset.UtcNow;

        // Register for memory pressure notifications
        RegisterMemoryPressureHandler();

        // Register eviction callback with the meta table
        VayronMetaTable.RegisterEvictionCallback(OnEvictionRequest);

        // Start background cleanup if enabled
        if (options.EnableBackgroundCleanup)
        {
            _cleanupTask = Task.Run(CleanupLoop, _cts.Token);
        }
    }

    // =====================================================================
    // Memory Pressure Handling
    // =====================================================================

    private void RegisterMemoryPressureHandler()
    {
        // Register with GC for memory pressure notifications
        Gen2GcCallback.Register(OnGen2Gc);
    }

    private void OnGen2Gc()
    {
        // Check if we should trigger eviction
        var stats = VayronMetaTable.GetStatistics();
        if (stats.TotalBytesTracked > _options.MaxTotalBytes)
        {
            Interlocked.Increment(ref _memoryPressureEvents);
            EvictForMemoryPressure();
        }
    }

    private void EvictForMemoryPressure()
    {
        var stats = VayronMetaTable.GetStatistics();
        var targetBytes = stats.TotalBytesTracked - (long)(_options.MaxTotalBytes * 0.8); // Evict to 80%

        if (targetBytes > 0)
        {
            var freed = VayronMetaTable.RequestEviction(targetBytes);
            Interlocked.Add(ref _totalBytesEvicted, freed);
            Interlocked.Increment(ref _totalEvictions);
        }
    }

    private void OnEvictionRequest(EvictionRequestEventArgs args)
    {
        // This is called from VayronMetaTable when eviction is needed
        // We can add custom logic here, like prioritizing certain handles
    }

    // =====================================================================
    // Background Cleanup Loop
    // =====================================================================

    private async Task CleanupLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupIntervalMs, _cts.Token);

                if (_cts.Token.IsCancellationRequested)
                    break;

                PerformCleanupCycle();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue
                System.Diagnostics.Debug.WriteLine($"VAYRON cleanup error: {ex.Message}");
            }
        }
    }

    private void PerformCleanupCycle()
    {
        Interlocked.Increment(ref _cleanupCycles);
        _lastCleanup = DateTimeOffset.UtcNow;

        // 1. Process finalization queue
        ProcessFinalizationQueue();

        // 2. Clean up expired weak references
        var cleaned = VayronMetaTable.CleanupExpiredReferences();

        // 3. Evict old cached bodies
        var evictionAge = _options.MaxBodyAgeMs * TimeSpan.TicksPerMillisecond / TimeSpan.TicksPerSecond;
        var evicted = 0L;

        foreach (var (oid, meta) in VayronMetaTable.GetEvictionCandidates(evictionAge, _options.MaxEvictionsPerCycle))
        {
            var freed = meta.Evict(EvictionReason.LruEviction);
            evicted += freed;
        }

        if (evicted > 0)
        {
            Interlocked.Add(ref _totalBytesEvicted, evicted);
            Interlocked.Increment(ref _totalEvictions);
        }

        // 4. Auto-pin hot bodies if enabled
        if (_options.AutoPinHotBodies)
        {
            AutoPinHotBodies();
        }

        // 5. Check memory pressure
        var stats = VayronMetaTable.GetStatistics();
        if (stats.TotalBytesTracked > _options.MaxTotalBytes)
        {
            EvictForMemoryPressure();
        }
    }

    private void AutoPinHotBodies()
    {
        foreach (var oid in VayronMetaTable.GetAllOids())
        {
            if (VayronMetaTable.TryGetByOid(oid, out var meta) && meta != null)
            {
                if (meta.AccessCount >= _options.HotBodyAccessThreshold &&
                    !meta.IsPinned &&
                    meta.State == MaterializationState.Materialized)
                {
                    // Mark as prefer pinned for next materialization
                    meta.Flags |= VayronMetaFlags.PreferPinned;
                }
            }
        }
    }

    // =====================================================================
    // Finalization Tracking
    // =====================================================================

    /// <summary>
    /// Records that a handle has been finalized.
    /// </summary>
    public void RecordFinalization(VayronOid oid, int bodySize)
    {
        _finalizationQueue.Enqueue(new FinalizationRecord(oid, bodySize, DateTimeOffset.UtcNow));
    }

    private void ProcessFinalizationQueue()
    {
        var processed = 0;
        while (_finalizationQueue.TryDequeue(out var record) && processed < 1000)
        {
            // Clean up any remaining resources for this OID
            // The actual cleanup is handled by VayronMeta.Dispose
            processed++;
        }
    }

    // =====================================================================
    // Manual Operations
    // =====================================================================

    /// <summary>
    /// Forces an immediate cleanup cycle.
    /// </summary>
    public void ForceCleanup()
    {
        PerformCleanupCycle();
    }

    /// <summary>
    /// Evicts all evictable cached bodies.
    /// </summary>
    public long EvictAll()
    {
        long totalEvicted = 0;

        foreach (var (_, meta) in VayronMetaTable.GetLruCandidates(int.MaxValue))
        {
            totalEvicted += meta.Evict(EvictionReason.Explicit);
        }

        Interlocked.Add(ref _totalBytesEvicted, totalEvicted);
        if (totalEvicted > 0)
        {
            Interlocked.Increment(ref _totalEvictions);
        }

        return totalEvicted;
    }

    /// <summary>
    /// Evicts cached bodies until target bytes are freed.
    /// </summary>
    public long Evict(long targetBytes)
    {
        var freed = VayronMetaTable.RequestEviction(targetBytes);
        Interlocked.Add(ref _totalBytesEvicted, freed);
        if (freed > 0)
        {
            Interlocked.Increment(ref _totalEvictions);
        }
        return freed;
    }

    /// <summary>
    /// Flushes all dirty handles to storage.
    /// </summary>
    /// <param name="scope">The transaction scope to use for persistence.</param>
    public void FlushDirty(VayronTransactionScope scope)
    {
        foreach (var (oid, meta) in VayronMetaTable.GetDirtyEntries())
        {
            if (VayronMetaTable.TryGetHandleByOid(oid, out var handle) && handle is IVayronHandle vayronHandle)
            {
                vayronHandle.Persist(scope);
            }
        }
    }

    // =====================================================================
    // Statistics
    // =====================================================================

    /// <summary>
    /// Gets lifecycle manager statistics.
    /// </summary>
    public LifecycleStatistics GetStatistics()
    {
        return new LifecycleStatistics
        {
            TotalEvictions = Volatile.Read(ref _totalEvictions),
            TotalBytesEvicted = Volatile.Read(ref _totalBytesEvicted),
            CleanupCycles = Volatile.Read(ref _cleanupCycles),
            MemoryPressureEvents = Volatile.Read(ref _memoryPressureEvents),
            LastCleanup = _lastCleanup,
            FinalizationQueueSize = _finalizationQueue.Count,
            IsBackgroundCleanupEnabled = _options.EnableBackgroundCleanup,
            MaxTotalBytes = _options.MaxTotalBytes,
        };
    }

    /// <summary>
    /// Resets statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalEvictions, 0);
        Interlocked.Exchange(ref _totalBytesEvicted, 0);
        Interlocked.Exchange(ref _cleanupCycles, 0);
        Interlocked.Exchange(ref _memoryPressureEvents, 0);
    }

    // =====================================================================
    // Disposal
    // =====================================================================

    /// <summary>
    /// Disposes the lifecycle manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop background task
        _cts.Cancel();

        try
        {
            _cleanupTask?.Wait(5000);
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        _cts.Dispose();

        // Unregister callbacks
        VayronMetaTable.UnregisterEvictionCallback(OnEvictionRequest);

        // Clear singleton
        lock (_instanceLock)
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
    }
}

/// <summary>
/// Record of a finalized handle.
/// </summary>
internal readonly struct FinalizationRecord
{
    public VayronOid Oid { get; }
    public int BodySize { get; }
    public DateTimeOffset Timestamp { get; }

    public FinalizationRecord(VayronOid oid, int bodySize, DateTimeOffset timestamp)
    {
        Oid = oid;
        BodySize = bodySize;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Statistics from the lifecycle manager.
/// </summary>
public readonly struct LifecycleStatistics
{
    /// <summary>Total number of eviction operations.</summary>
    public long TotalEvictions { get; init; }

    /// <summary>Total bytes evicted.</summary>
    public long TotalBytesEvicted { get; init; }

    /// <summary>Number of cleanup cycles performed.</summary>
    public long CleanupCycles { get; init; }

    /// <summary>Number of memory pressure events handled.</summary>
    public long MemoryPressureEvents { get; init; }

    /// <summary>When the last cleanup was performed.</summary>
    public DateTimeOffset LastCleanup { get; init; }

    /// <summary>Current size of the finalization queue.</summary>
    public int FinalizationQueueSize { get; init; }

    /// <summary>Whether background cleanup is enabled.</summary>
    public bool IsBackgroundCleanupEnabled { get; init; }

    /// <summary>Maximum total bytes configuration.</summary>
    public long MaxTotalBytes { get; init; }

    public override string ToString()
    {
        return $"Evictions={TotalEvictions} ({TotalBytesEvicted:N0} bytes) | Cycles={CleanupCycles} | MemPressure={MemoryPressureEvents}";
    }
}

/// <summary>
/// Helper class for registering Gen2 GC callbacks.
/// </summary>
internal static class Gen2GcCallback
{
    private static readonly List<Action> _callbacks = new();
    private static readonly object _lock = new();
    private static bool _registered;

    /// <summary>
    /// Registers a callback to be invoked after Gen2 garbage collection.
    /// </summary>
    public static void Register(Action callback)
    {
        lock (_lock)
        {
            _callbacks.Add(callback);

            if (!_registered)
            {
                _registered = true;
                // Register a weak finalizer object to detect Gen2 collections
                new Gen2Notifier();
            }
        }
    }

    /// <summary>
    /// Invokes all registered callbacks.
    /// </summary>
    private static void InvokeCallbacks()
    {
        List<Action> callbacks;
        lock (_lock)
        {
            callbacks = new List<Action>(_callbacks);
        }

        foreach (var callback in callbacks)
        {
            try
            {
                callback();
            }
            catch
            {
                // Ignore exceptions in callbacks
            }
        }
    }

    /// <summary>
    /// Helper class that detects Gen2 collections via finalization.
    /// </summary>
    private sealed class Gen2Notifier
    {
        ~Gen2Notifier()
        {
            if (!Environment.HasShutdownStarted && !AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                // Re-register for next Gen2
                InvokeCallbacks();
                GC.ReRegisterForFinalize(this);
            }
        }
    }
}
