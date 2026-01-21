// VAYRON - Runtime-Integrated Persistent Storage
// Handle metadata stored in side table

using Voron.Data.Containers;

namespace Vayron;

/// <summary>
/// Metadata associated with a VAYRON handle, stored in a side table.
/// This keeps the handle objects small while allowing rich metadata.
/// </summary>
/// <remarks>
/// Using a side table (via ConditionalWeakTable) allows:
/// - Minimal memory overhead for handle objects
/// - GC-friendly weak keying
/// - Runtime-accessible metadata without header pressure
/// </remarks>
public sealed class VayronMeta
{
    /// <summary>
    /// The stable Object Identifier.
    /// </summary>
    public VayronOid Oid;

    /// <summary>
    /// Transaction ID when the body was last cached.
    /// Used for staleness detection.
    /// </summary>
    public long Epoch;

    /// <summary>
    /// Raw pointer to the cached body data.
    /// IntPtr.Zero when not materialized.
    /// </summary>
    public IntPtr CachedBodyPtr;

    /// <summary>
    /// Size of the cached body in bytes.
    /// </summary>
    public int CachedBodySize;

    /// <summary>
    /// Current materialization state.
    /// </summary>
    public MaterializationState State;

    /// <summary>
    /// Storage location within Voron (Container entry ID).
    /// </summary>
    public ContainerEntryId StorageLocation;

    /// <summary>
    /// Type token for runtime type identification.
    /// </summary>
    public uint TypeToken;

    /// <summary>
    /// Schema version of the stored body.
    /// </summary>
    public ushort SchemaVersion;

    /// <summary>
    /// Lock for concurrent access control.
    /// </summary>
    private int _lock;

    /// <summary>
    /// Creates new metadata for a given OID.
    /// </summary>
    public VayronMeta(VayronOid oid)
    {
        Oid = oid;
        State = MaterializationState.NotMaterialized;
        CachedBodyPtr = IntPtr.Zero;
        CachedBodySize = 0;
        Epoch = -1;
        StorageLocation = ContainerEntryId.Invalid;
    }

    /// <summary>
    /// Attempts to acquire a spinlock on this metadata.
    /// </summary>
    public bool TryEnterLock()
    {
        return Interlocked.CompareExchange(ref _lock, 1, 0) == 0;
    }

    /// <summary>
    /// Releases the spinlock.
    /// </summary>
    public void ExitLock()
    {
        Volatile.Write(ref _lock, 0);
    }

    /// <summary>
    /// Checks if the cached body is stale relative to the given epoch.
    /// </summary>
    public bool IsStale(long currentEpoch)
    {
        return State != MaterializationState.Materialized || Epoch < currentEpoch;
    }

    /// <summary>
    /// Marks the body as materialized with the given epoch.
    /// </summary>
    public void MarkMaterialized(long epoch, IntPtr bodyPtr, int bodySize)
    {
        CachedBodyPtr = bodyPtr;
        CachedBodySize = bodySize;
        Epoch = epoch;
        State = MaterializationState.Materialized;
    }

    /// <summary>
    /// Marks the body as dirty (modified, needs persistence).
    /// </summary>
    public void MarkDirty()
    {
        State = MaterializationState.Dirty;
    }

    /// <summary>
    /// Clears the cached body.
    /// </summary>
    public void Invalidate()
    {
        CachedBodyPtr = IntPtr.Zero;
        CachedBodySize = 0;
        State = MaterializationState.Stale;
    }
}
