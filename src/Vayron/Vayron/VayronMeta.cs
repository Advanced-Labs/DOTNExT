// VAYRON - Runtime-Integrated Persistent Storage
// Handle metadata stored in side table
//
// Phase 3: Enhanced with native pointer caching, GCHandle pinning, and proper lifecycle management

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Voron.Data.Containers;

namespace Vayron;

/// <summary>
/// Metadata associated with a VAYRON handle, stored in a side table.
/// This keeps the handle objects small while allowing rich metadata.
/// </summary>
/// <remarks>
/// <para><b>Phase 3 Enhancements:</b></para>
/// <list type="bullet">
/// <item><description>Native pointer caching with GCHandle pinning</description></item>
/// <item><description>Proper state transition validation</description></item>
/// <item><description>Memory pressure awareness</description></item>
/// <item><description>Event hooks for lifecycle management</description></item>
/// </list>
///
/// <para><b>Memory Model:</b></para>
/// <para>
/// Cached body data can be stored in two modes:
/// 1. Managed byte array (default): Safe, GC-managed
/// 2. Pinned native pointer: Fast, requires explicit unpinning
/// </para>
///
/// <para>Using a side table (via ConditionalWeakTable) allows:</para>
/// <list type="bullet">
/// <item><description>Minimal memory overhead for handle objects</description></item>
/// <item><description>GC-friendly weak keying</description></item>
/// <item><description>Runtime-accessible metadata without header pressure</description></item>
/// </list>
/// </remarks>
public sealed class VayronMeta : IDisposable
{
    // =====================================================================
    // State Change Events (Phase 3)
    // =====================================================================

    /// <summary>
    /// Event raised when materialization state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when the cached body is about to be evicted.
    /// </summary>
    public event EventHandler<EvictionEventArgs>? Evicting;

    // =====================================================================
    // Core Identity
    // =====================================================================

    /// <summary>
    /// The stable Object Identifier.
    /// </summary>
    public VayronOid Oid { get; }

    // =====================================================================
    // State Management
    // =====================================================================

    /// <summary>
    /// Current materialization state.
    /// </summary>
    private volatile MaterializationState _state;

    /// <summary>
    /// Gets or sets the current materialization state.
    /// Setting this property validates the transition and raises StateChanged event.
    /// </summary>
    public MaterializationState State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state;
        set => TransitionState(value);
    }

    /// <summary>
    /// Lock for concurrent access control (spinlock pattern).
    /// </summary>
    private int _lock;

    /// <summary>
    /// Number of active readers (for read-write locking).
    /// </summary>
    private int _readerCount;

    // =====================================================================
    // Epoch and Staleness
    // =====================================================================

    /// <summary>
    /// Transaction ID when the body was last cached.
    /// Used for staleness detection.
    /// </summary>
    public long Epoch { get; private set; }

    /// <summary>
    /// Timestamp when the body was last accessed.
    /// Used for LRU eviction.
    /// </summary>
    public long LastAccessTicks { get; private set; }

    /// <summary>
    /// Access count for frequency-based eviction policies.
    /// </summary>
    public int AccessCount { get; private set; }

    // =====================================================================
    // Native Pointer Caching (Phase 3)
    // =====================================================================

    /// <summary>
    /// Raw pointer to the cached body data.
    /// IntPtr.Zero when not materialized or using managed array.
    /// </summary>
    private IntPtr _cachedBodyPtr;

    /// <summary>
    /// Size of the cached body in bytes.
    /// </summary>
    private int _cachedBodySize;

    /// <summary>
    /// GCHandle for pinning the managed byte array.
    /// </summary>
    private GCHandle _pinnedHandle;

    /// <summary>
    /// The managed byte array (when not using native pointer).
    /// </summary>
    private byte[]? _managedBody;

    /// <summary>
    /// Whether the body is currently pinned.
    /// </summary>
    private bool _isPinned;

    /// <summary>
    /// Whether native memory was allocated (vs pinning managed array).
    /// </summary>
    private bool _isNativeAllocated;

    /// <summary>
    /// Gets the raw pointer to cached body (pinned or native).
    /// </summary>
    public IntPtr CachedBodyPtr
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cachedBodyPtr;
    }

    /// <summary>
    /// Gets the size of the cached body in bytes.
    /// </summary>
    public int CachedBodySize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cachedBodySize;
    }

    /// <summary>
    /// Gets whether the body is currently pinned in memory.
    /// </summary>
    public bool IsPinned => _isPinned;

    // =====================================================================
    // Storage Location
    // =====================================================================

    /// <summary>
    /// Storage location within Voron (Container entry ID).
    /// </summary>
    public ContainerEntryId StorageLocation { get; set; }

    /// <summary>
    /// Type token for runtime type identification.
    /// </summary>
    public uint TypeToken { get; set; }

    /// <summary>
    /// Schema version of the stored body.
    /// </summary>
    public ushort SchemaVersion { get; set; }

    // =====================================================================
    // Flags
    // =====================================================================

    /// <summary>
    /// Additional flags for handle state.
    /// </summary>
    public VayronMetaFlags Flags { get; set; }

    /// <summary>
    /// Whether this handle has been finalized.
    /// </summary>
    public bool IsFinalized => (Flags & VayronMetaFlags.Finalized) != 0;

    /// <summary>
    /// Whether this handle is marked for background eviction.
    /// </summary>
    public bool IsEvictionCandidate => (Flags & VayronMetaFlags.EvictionCandidate) != 0;

    // =====================================================================
    // Constructors
    // =====================================================================

    /// <summary>
    /// Creates new metadata for a given OID.
    /// </summary>
    public VayronMeta(VayronOid oid)
    {
        Oid = oid;
        _state = MaterializationState.NotMaterialized;
        _cachedBodyPtr = IntPtr.Zero;
        _cachedBodySize = 0;
        Epoch = -1;
        StorageLocation = ContainerEntryId.Invalid;
        LastAccessTicks = Environment.TickCount64;
    }

    // =====================================================================
    // State Transitions (Phase 3)
    // =====================================================================

    /// <summary>
    /// Transitions to a new state with validation.
    /// </summary>
    private void TransitionState(MaterializationState newState)
    {
        var oldState = _state;
        if (oldState == newState)
            return;

        // Validate transition
        if (!VayronStateManager.IsValidTransition(oldState, newState))
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {oldState} to {newState}. " +
                $"Valid transitions: {string.Join(", ", VayronStateManager.GetValidTransitions(oldState))}");
        }

        _state = newState;

        // Raise event
        StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState, Oid));
    }

    /// <summary>
    /// Attempts to transition to a new state, returns false if invalid.
    /// </summary>
    public bool TryTransitionState(MaterializationState newState)
    {
        var oldState = _state;
        if (oldState == newState)
            return true;

        if (!VayronStateManager.IsValidTransition(oldState, newState))
            return false;

        _state = newState;
        StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState, Oid));
        return true;
    }

    // =====================================================================
    // Locking (Spinlock Pattern)
    // =====================================================================

    /// <summary>
    /// Attempts to acquire a spinlock on this metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterLock()
    {
        return Interlocked.CompareExchange(ref _lock, 1, 0) == 0;
    }

    /// <summary>
    /// Acquires the spinlock, spinning if necessary.
    /// </summary>
    public void EnterLock()
    {
        var spinner = new SpinWait();
        while (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
        {
            spinner.SpinOnce();
        }
    }

    /// <summary>
    /// Releases the spinlock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitLock()
    {
        Volatile.Write(ref _lock, 0);
    }

    /// <summary>
    /// Executes an action while holding the lock.
    /// </summary>
    public void WithLock(Action action)
    {
        EnterLock();
        try
        {
            action();
        }
        finally
        {
            ExitLock();
        }
    }

    /// <summary>
    /// Executes a function while holding the lock.
    /// </summary>
    public T WithLock<T>(Func<T> func)
    {
        EnterLock();
        try
        {
            return func();
        }
        finally
        {
            ExitLock();
        }
    }

    /// <summary>
    /// Enters read mode (multiple readers allowed).
    /// </summary>
    public void EnterReadLock()
    {
        var spinner = new SpinWait();
        while (true)
        {
            int current = _readerCount;
            if (current >= 0 && Interlocked.CompareExchange(ref _readerCount, current + 1, current) == current)
                break;
            spinner.SpinOnce();
        }
    }

    /// <summary>
    /// Exits read mode.
    /// </summary>
    public void ExitReadLock()
    {
        Interlocked.Decrement(ref _readerCount);
    }

    // =====================================================================
    // Materialization State Helpers
    // =====================================================================

    /// <summary>
    /// Checks if the cached body is stale relative to the given epoch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStale(long currentEpoch)
    {
        return _state != MaterializationState.Materialized || Epoch < currentEpoch;
    }

    /// <summary>
    /// Records access for LRU tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordAccess()
    {
        LastAccessTicks = Environment.TickCount64;
        Interlocked.Increment(ref AccessCount);
    }

    // =====================================================================
    // Native Pointer Operations (Phase 3)
    // =====================================================================

    /// <summary>
    /// Pins the managed byte array and sets the native pointer.
    /// </summary>
    /// <param name="body">The managed byte array to pin.</param>
    /// <remarks>
    /// Use this for frequently accessed bodies to avoid managed array bounds checks.
    /// Remember to call <see cref="Unpin"/> when done to allow GC to move the array.
    /// </remarks>
    public void PinBody(byte[] body)
    {
        if (_isPinned)
            throw new InvalidOperationException("Body is already pinned.");

        _managedBody = body;
        _pinnedHandle = GCHandle.Alloc(body, GCHandleType.Pinned);
        _cachedBodyPtr = _pinnedHandle.AddrOfPinnedObject();
        _cachedBodySize = body.Length;
        _isPinned = true;
        _isNativeAllocated = false;
    }

    /// <summary>
    /// Allocates native memory and copies the body data.
    /// </summary>
    /// <param name="body">The body data to copy.</param>
    /// <remarks>
    /// Use this for long-lived bodies that should not pressure the GC.
    /// Memory is allocated using NativeMemory.Alloc and must be freed via <see cref="FreeNativeBody"/>.
    /// </remarks>
    public unsafe void AllocateNativeBody(ReadOnlySpan<byte> body)
    {
        if (_cachedBodyPtr != IntPtr.Zero)
            throw new InvalidOperationException("Body is already allocated.");

        _cachedBodyPtr = (IntPtr)NativeMemory.Alloc((nuint)body.Length);
        body.CopyTo(new Span<byte>((void*)_cachedBodyPtr, body.Length));
        _cachedBodySize = body.Length;
        _isNativeAllocated = true;
        _isPinned = false;
    }

    /// <summary>
    /// Unpins a pinned managed byte array.
    /// </summary>
    public void Unpin()
    {
        if (!_isPinned)
            return;

        _pinnedHandle.Free();
        _cachedBodyPtr = IntPtr.Zero;
        _isPinned = false;
    }

    /// <summary>
    /// Frees native memory allocated for the body.
    /// </summary>
    public unsafe void FreeNativeBody()
    {
        if (!_isNativeAllocated || _cachedBodyPtr == IntPtr.Zero)
            return;

        NativeMemory.Free((void*)_cachedBodyPtr);
        _cachedBodyPtr = IntPtr.Zero;
        _cachedBodySize = 0;
        _isNativeAllocated = false;
    }

    /// <summary>
    /// Gets the cached body as a span (works for both pinned and native memory).
    /// </summary>
    public unsafe Span<byte> GetBodySpan()
    {
        if (_cachedBodyPtr == IntPtr.Zero)
        {
            if (_managedBody != null)
                return _managedBody;
            return Span<byte>.Empty;
        }

        return new Span<byte>((void*)_cachedBodyPtr, _cachedBodySize);
    }

    /// <summary>
    /// Gets the cached body as a read-only span.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetBodyReadOnlySpan()
    {
        if (_cachedBodyPtr == IntPtr.Zero)
        {
            if (_managedBody != null)
                return _managedBody;
            return ReadOnlySpan<byte>.Empty;
        }

        return new ReadOnlySpan<byte>((void*)_cachedBodyPtr, _cachedBodySize);
    }

    /// <summary>
    /// Sets the managed body without pinning.
    /// </summary>
    public void SetManagedBody(byte[] body)
    {
        _managedBody = body;
        _cachedBodySize = body.Length;
        // Don't set _cachedBodyPtr - it's only for pinned/native
    }

    /// <summary>
    /// Gets the managed body array (may be null if using native memory).
    /// </summary>
    public byte[]? GetManagedBody() => _managedBody;

    // =====================================================================
    // State Marking Helpers
    // =====================================================================

    /// <summary>
    /// Marks the body as materialized with the given epoch.
    /// </summary>
    public void MarkMaterialized(long epoch, IntPtr bodyPtr, int bodySize)
    {
        _cachedBodyPtr = bodyPtr;
        _cachedBodySize = bodySize;
        Epoch = epoch;
        TryTransitionState(MaterializationState.Materialized);
        RecordAccess();
    }

    /// <summary>
    /// Marks the body as materialized using a managed byte array.
    /// </summary>
    public void MarkMaterialized(long epoch, byte[] body)
    {
        SetManagedBody(body);
        Epoch = epoch;
        TryTransitionState(MaterializationState.Materialized);
        RecordAccess();
    }

    /// <summary>
    /// Marks the body as dirty (modified, needs persistence).
    /// </summary>
    public void MarkDirty()
    {
        TryTransitionState(MaterializationState.Dirty);
    }

    /// <summary>
    /// Clears the cached body and marks as stale.
    /// </summary>
    public void Invalidate()
    {
        // Raise eviction event
        Evicting?.Invoke(this, new EvictionEventArgs(Oid, _cachedBodySize, EvictionReason.Explicit));

        // Clean up native resources
        if (_isPinned)
            Unpin();
        if (_isNativeAllocated)
            FreeNativeBody();

        _cachedBodyPtr = IntPtr.Zero;
        _cachedBodySize = 0;
        _managedBody = null;

        TryTransitionState(MaterializationState.Stale);
    }

    /// <summary>
    /// Evicts the cached body under memory pressure.
    /// </summary>
    /// <param name="reason">The reason for eviction.</param>
    /// <returns>The number of bytes freed.</returns>
    public int Evict(EvictionReason reason)
    {
        if (_state == MaterializationState.NotMaterialized || _state == MaterializationState.Stale)
            return 0;

        // Don't evict dirty bodies
        if (_state == MaterializationState.Dirty)
            return 0;

        var freedBytes = _cachedBodySize;

        Evicting?.Invoke(this, new EvictionEventArgs(Oid, freedBytes, reason));

        // Clean up
        if (_isPinned)
            Unpin();
        if (_isNativeAllocated)
            FreeNativeBody();

        _cachedBodyPtr = IntPtr.Zero;
        _cachedBodySize = 0;
        _managedBody = null;

        TryTransitionState(MaterializationState.Stale);

        return freedBytes;
    }

    // =====================================================================
    // IDisposable
    // =====================================================================

    private bool _disposed;

    /// <summary>
    /// Disposes the metadata and releases native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Clean up native resources
        if (_isPinned)
            Unpin();
        if (_isNativeAllocated)
            FreeNativeBody();

        _managedBody = null;
        Flags |= VayronMetaFlags.Finalized;
    }
}

/// <summary>
/// Additional flags for VAYRON handle metadata.
/// </summary>
[Flags]
public enum VayronMetaFlags : byte
{
    /// <summary>No flags set.</summary>
    None = 0,

    /// <summary>The handle has been finalized.</summary>
    Finalized = 1 << 0,

    /// <summary>The handle is a candidate for eviction.</summary>
    EvictionCandidate = 1 << 1,

    /// <summary>The handle body should be pinned when materialized.</summary>
    PreferPinned = 1 << 2,

    /// <summary>The handle body should use native memory when materialized.</summary>
    PreferNativeMemory = 1 << 3,

    /// <summary>The handle is currently being accessed (read lock).</summary>
    InUse = 1 << 4,

    /// <summary>The handle is marked for deletion.</summary>
    MarkedForDeletion = 1 << 5,
}

/// <summary>
/// Event arguments for state change events.
/// </summary>
public sealed class StateChangedEventArgs : EventArgs
{
    /// <summary>The previous state.</summary>
    public MaterializationState OldState { get; }

    /// <summary>The new state.</summary>
    public MaterializationState NewState { get; }

    /// <summary>The OID of the affected handle.</summary>
    public VayronOid Oid { get; }

    public StateChangedEventArgs(MaterializationState oldState, MaterializationState newState, VayronOid oid)
    {
        OldState = oldState;
        NewState = newState;
        Oid = oid;
    }
}

/// <summary>
/// Event arguments for eviction events.
/// </summary>
public sealed class EvictionEventArgs : EventArgs
{
    /// <summary>The OID of the evicted handle.</summary>
    public VayronOid Oid { get; }

    /// <summary>The number of bytes being evicted.</summary>
    public int BytesEvicted { get; }

    /// <summary>The reason for eviction.</summary>
    public EvictionReason Reason { get; }

    public EvictionEventArgs(VayronOid oid, int bytesEvicted, EvictionReason reason)
    {
        Oid = oid;
        BytesEvicted = bytesEvicted;
        Reason = reason;
    }
}

/// <summary>
/// Reasons for body eviction.
/// </summary>
public enum EvictionReason
{
    /// <summary>Explicit invalidation.</summary>
    Explicit = 0,

    /// <summary>Memory pressure response.</summary>
    MemoryPressure = 1,

    /// <summary>LRU eviction (least recently used).</summary>
    LruEviction = 2,

    /// <summary>Staleness (transaction epoch changed).</summary>
    Staleness = 3,

    /// <summary>Handle disposal.</summary>
    Disposal = 4,
}
