# Voron Integration Analysis for VAYRON

> Engineering analysis of key integration points for embedding Voron-style storage in the VAYRON runtime.

---

## 1. Integration Context

VAYRON aims to create managed objects whose bodies reside in persistent storage (Voron), with lightweight handles in the GC heap. This document identifies the key Voron components and interfaces relevant to this integration.

### 1.1 Target Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        CLR / VAYRON Runtime                         │
│                                                                     │
│  ┌─────────────────┐                                                │
│  │  VayronHandle   │ ← GC-managed, lightweight                     │
│  │  ┌───────────┐  │                                                │
│  │  │ OID       │──┼─────────┐                                      │
│  │  │ Epoch     │  │         │                                      │
│  │  │ HotPtr    │  │         │  Materialization / Fault-In         │
│  │  └───────────┘  │         │                                      │
│  └─────────────────┘         │                                      │
│                              ▼                                      │
├──────────────────────────────┼──────────────────────────────────────┤
│                              │                                      │
│  ┌───────────────────────────┴─────────────────────────────────────┐│
│  │              Voron Storage Layer (Adapted)                      ││
│  │  ┌─────────────────────────────────────────────────────────────┐││
│  │  │  OID Lookup (FixedSizeTree: OID → Location)                │││
│  │  └─────────────────────────────────────────────────────────────┘││
│  │  ┌─────────────────────────────────────────────────────────────┐││
│  │  │  Object Bodies (Containers / RawDataSections)              │││
│  │  └─────────────────────────────────────────────────────────────┘││
│  │  ┌─────────────────────────────────────────────────────────────┐││
│  │  │  Relation Indexes (Trees / PostingLists)                   │││
│  │  └─────────────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │              Memory-Mapped Files (Pager)                        ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. Key Integration Points

### 2.1 AbstractPager - The Memory Interface

**Location**: `Impl/Paging/AbstractPager.cs`

**Why It Matters**: This is the boundary between Voron and the operating system's virtual memory. VAYRON needs to hook at this level to:
- Receive notifications when pages are accessed
- Implement custom materialization logic
- Potentially integrate with runtime memory management

**Key Methods to Adapt**:

```csharp
public abstract class AbstractPager
{
    // CRITICAL: Returns pointer to page data
    // VAYRON hook: Trigger handle materialization
    public abstract byte* AcquirePagePointer(
        IPagerLevelTransactionState tx,
        long pageNumber,
        PagerState pagerState = null);

    // For overflow pages (large objects)
    public abstract byte* AcquirePagePointerWithOverflowHandling(...);

    // CRITICAL: Growing the storage file
    // VAYRON hook: Track allocations for body storage
    public abstract void AllocateMorePages(long newLength);
}
```

**Integration Strategy**:

```csharp
// Hypothetical VAYRON extension
public class VayronPager : AbstractPager
{
    // Registry of handles interested in pages
    private Dictionary<long, WeakReference<VayronHandle>> _handleRegistry;

    public override byte* AcquirePagePointer(...)
    {
        var ptr = base.AcquirePagePointer(tx, pageNumber, pagerState);

        // Notify any handles watching this page
        if (_handleRegistry.TryGetValue(pageNumber, out var handleRef))
        {
            if (handleRef.TryGetTarget(out var handle))
            {
                handle.OnPageMaterialized(ptr);
            }
        }

        return ptr;
    }
}
```

### 2.2 LowLevelTransaction - Page Operations

**Location**: `Impl/LowLevelTransaction.cs`

**Why It Matters**: All page-level operations flow through here. VAYRON needs to:
- Get page pointers for object bodies
- Allocate pages for new objects
- Free pages when objects are collected
- Participate in transaction commit/rollback

**Key Methods**:

```csharp
public sealed class LowLevelTransaction
{
    // Get a page (read or write depending on tx type)
    public Page GetPage(long pageNumber);

    // Allocate new pages
    public Page AllocatePage(int numberOfPages);

    // Copy-on-write modification
    public Page ModifyPage(long pageNumber);

    // Free a page (deferred until commit)
    public void FreePage(long pageNumber);

    // Transaction ID (for MVCC)
    public long Id { get; }
}
```

**Integration Strategy**:

```csharp
// Object body allocation
public class VayronStorage
{
    private StorageEnvironment _env;
    private FixedSizeTree _oidIndex;

    public long AllocateBody(int size)
    {
        using var tx = _env.WriteTransaction();

        // Allocate in container (handles overflow automatically)
        var storageId = Container.Allocate(tx.LowLevelTransaction,
            _containerId, size, out byte* ptr);

        // Generate OID
        var oid = GenerateOid();

        // Register in index
        _oidIndex.Add(oid, storageId);

        tx.Commit();
        return oid;
    }
}
```

### 2.3 StorageEnvironmentState - MVCC Snapshot

**Location**: `Impl/StorageEnvironmentState.cs`

**Why It Matters**: This immutable state object represents a point-in-time snapshot. VAYRON handles can cache which state they're viewing.

```csharp
public readonly struct StorageEnvironmentState
{
    public readonly long NextPageNumber;
    public readonly TreeRootHeader Root;
    public readonly long TransactionId;  // CRITICAL for VAYRON epoch
}
```

**Integration Strategy**:

```csharp
struct VayronHandle
{
    ulong OID;
    long LastSeenTxId;  // Maps to StorageEnvironmentState.TransactionId

    bool IsStale(LowLevelTransaction currentTx)
    {
        return LastSeenTxId < currentTx.Id;
    }
}
```

### 2.4 Container - Body Storage

**Location**: `Data/Containers/Container.cs`

**Why It Matters**: Containers provide allocation/deallocation of variable-sized blobs. Perfect for object bodies.

```csharp
public static class Container
{
    // Allocate space for object body
    public static long Allocate(
        LowLevelTransaction tx,
        ContainerId container,
        int size,
        out byte* ptr);

    // Read object body
    public static (byte* Ptr, int Size) Get(
        LowLevelTransaction tx,
        long storageId);

    // Free object body
    public static void Delete(
        LowLevelTransaction tx,
        long storageId);
}
```

**Storage ID Encoding**:
The `storageId` returned encodes both page number and offset, providing a stable reference.

### 2.5 FixedSizeTree - OID Lookup

**Location**: `Data/Fixed/FixedSizeTree.cs`

**Why It Matters**: Efficient lookup from OID (long) to storage location (long).

```csharp
// OID → StorageLocation mapping
var oidIndex = new FixedSizeTree(tx, rootTree, "oid-index", sizeof(long));

// Add mapping
oidIndex.Add(oid, storageLocation);

// Lookup
if (oidIndex.Read(oid, out Slice value))
{
    var storageLocation = *(long*)value.Content.Ptr;
}
```

---

## 3. Handle Materialization Flow

### 3.1 Cold Path (First Access)

```
VayronHandle.AccessField()
     │
     ├── Check HotPtr != null? ─── Yes ──► Return cached pointer
     │                │
     │               No
     │                │
     │                ▼
     ├── Acquire read transaction (or use ambient)
     │                │
     │                ▼
     ├── Lookup OID in FixedSizeTree
     │                │
     │                ▼
     │         StorageLocation
     │                │
     │                ▼
     ├── Container.Get(storageLocation) → (ptr, size)
     │                │
     │                ▼
     ├── Cache in handle: HotPtr = ptr, Epoch = tx.Id
     │                │
     │                ▼
     └── Return ptr to field access
```

### 3.2 Hot Path (Cached)

```
VayronHandle.AccessField()
     │
     ├── Check HotPtr != null && !IsStale(currentTx)?
     │                │
     │              Yes
     │                │
     │                ▼
     └── Direct pointer dereference (near zero overhead)
```

### 3.3 Modification Path

```
VayronHandle.ModifyField()
     │
     ├── Require write transaction
     │                │
     │                ▼
     ├── Get current body location
     │                │
     │                ▼
     ├── Container page → ModifyPage (COW)
     │                │
     │                ▼
     ├── Update field in scratch copy
     │                │
     │                ▼
     ├── On commit: Journal write, then visible
     │
     └── Handle.HotPtr may need update (new scratch location)
```

---

## 4. GC Integration Points

### 4.1 Handle Finalization

When a VayronHandle is collected:

```csharp
~VayronHandle()
{
    // Queue body for potential cleanup
    // (Can't do immediate Voron operation in finalizer)
    VayronGC.QueueForCleanup(this.OID);
}
```

### 4.2 Background Cleanup

```csharp
// Background thread processes cleanup queue
void ProcessCleanupQueue()
{
    using var tx = _env.WriteTransaction();

    while (_cleanupQueue.TryDequeue(out var oid))
    {
        // Check no live handles reference this OID
        if (!_handleRegistry.HasLiveHandles(oid))
        {
            // Get storage location
            if (_oidIndex.Read(oid, out var location))
            {
                // Free body
                Container.Delete(tx.LowLevelTransaction, location);
                _oidIndex.Delete(oid);
            }
        }
    }

    tx.Commit();
}
```

### 4.3 Handle Table (Side Metadata)

```csharp
// Runtime-maintained metadata
class VayronMetaTable
{
    // ObjectRef → Metadata
    // Using ConditionalWeakTable or similar
    private ConditionalWeakTable<object, VayronMeta> _table;

    public struct VayronMeta
    {
        public ulong OID;
        public long Epoch;
        public byte* CachedPtr;
        public VoronLocation Location;
        public MaterializationState State;
    }
}
```

---

## 5. Transaction Integration

### 5.1 Ambient Transactions

VAYRON could use thread-local ambient transactions:

```csharp
static class VayronTransaction
{
    [ThreadStatic]
    private static Transaction _ambient;

    public static Transaction Current => _ambient;

    public static IDisposable BeginRead()
    {
        _ambient = _env.ReadTransaction();
        return new TxScope(() => { _ambient.Dispose(); _ambient = null; });
    }

    public static IDisposable BeginWrite()
    {
        _ambient = _env.WriteTransaction();
        return new WriteTxScope(_ambient);
    }
}
```

### 5.2 Async/Await Considerations

Transaction state needs to flow with async:

```csharp
// AsyncLocal for transaction context
private static AsyncLocal<Transaction> _asyncTransaction = new();

// Or: ExecutionContext-based flow
```

---

## 6. Memory Pressure Handling

### 6.1 Cache Eviction

Under memory pressure, evict cached pointers:

```csharp
void OnLowMemory()
{
    foreach (var handle in _handleRegistry.AllHandles())
    {
        // Clear cached pointer, force re-materialization
        handle.HotPtr = null;
    }
}
```

### 6.2 Integration with Voron's Memory Management

```csharp
// Voron already registers for low memory notifications
LowMemoryNotification.Instance.RegisterLowMemoryHandler(this);

// VAYRON can piggyback on this
public void LowMemory(LowMemorySeverity severity)
{
    if (severity >= LowMemorySeverity.Medium)
    {
        EvictCachedPointers();
    }
}
```

---

## 7. Runtime Hook Points (CLR Side)

### 7.1 Object Header Tag

Using `BIT_SBLK_UNUSED` as VAYRON marker:

```cpp
// In object.h or equivalent
#define BIT_SBLK_IS_VAYRON_HANDLE 0x80000000

inline bool IsVayronHandle(Object* obj)
{
    return (obj->GetHeader()->GetBits() & BIT_SBLK_IS_VAYRON_HANDLE) != 0;
}
```

### 7.2 Field Access Interception

JIT could emit checks for VAYRON objects:

```cpp
// Pseudo-code for field access
void* GetFieldAddress(Object* obj, int fieldOffset)
{
    if (IsVayronHandle(obj))
    {
        VayronHandle* handle = (VayronHandle*)obj;
        return handle->MaterializeAndGetField(fieldOffset);
    }
    return (byte*)obj + fieldOffset;
}
```

### 7.3 GC Integration

GC needs special handling:

```cpp
// During GC mark phase
void MarkObject(Object* obj)
{
    if (IsVayronHandle(obj))
    {
        // Only mark the handle, not the body
        // Body lives in Voron, not GC heap
        MarkVayronHandle((VayronHandle*)obj);
    }
    else
    {
        StandardMark(obj);
    }
}
```

---

## 8. Performance Considerations

### 8.1 Hot Pointer Validity

Challenge: How long is a cached pointer valid?

Options:
1. **Per-transaction**: Valid only within one transaction
2. **Epoch-based**: Valid until next write commits
3. **Permanent for reads**: Memory-mapped pointers don't move

Voron's model: Memory-mapped pointers are stable for the duration of a read transaction. Write transactions use scratch buffers.

### 8.2 Read Path Optimization

For maximum performance:
1. Keep read transaction open during hot operations
2. Cache pointers per transaction
3. Use intrinsics for fast-path checks

### 8.3 Write Path Optimization

1. Batch modifications within transaction
2. Use async commit where possible
3. Consider compression for large bodies

---

## 9. Recommended Integration Architecture

### 9.1 Layer Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    VAYRON Runtime Layer                             │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │ VayronHandle    │  │ VayronTransaction│  │ VayronGC          │  │
│  │ (managed obj)   │  │ (ambient mgmt)   │  │ (cleanup coord)   │  │
│  └────────┬────────┘  └────────┬─────────┘  └─────────┬─────────┘  │
│           │                    │                      │            │
│           └────────────────────┼──────────────────────┘            │
│                                │                                    │
├────────────────────────────────┼────────────────────────────────────┤
│                                ▼                                    │
│                    VayronStorage Adapter                            │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ - OID generation & indexing                                     ││
│  │ - Body allocation/deallocation                                  ││
│  │ - Transaction lifecycle                                         ││
│  │ - Materialization coordination                                  ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                │                                    │
├────────────────────────────────┼────────────────────────────────────┤
│                                ▼                                    │
│                    Voron (minimally modified)                       │
│  ┌───────────────────┐  ┌───────────────┐  ┌──────────────────────┐│
│  │ StorageEnvironment│  │ Container     │  │ FixedSizeTree       ││
│  │ (environment)     │  │ (body storage)│  │ (OID index)         ││
│  └───────────────────┘  └───────────────┘  └──────────────────────┘│
│                                │                                    │
│  ┌─────────────────────────────▼───────────────────────────────────┐│
│  │                    AbstractPager                                ││
│  │             (memory mapping abstraction)                        ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

### 9.2 Minimal Voron Modifications

To keep Voron clean, prefer extension over modification:

1. **VayronPager**: Extend AbstractPager with runtime hooks
2. **VayronEnvironment**: Thin wrapper around StorageEnvironment
3. **Event notifications**: Add hooks for page access, commit, etc.

### 9.3 Key Data Structures

```csharp
// In Voron storage
Tree "vayron:oid-index"         // OID → StorageLocation
Container "vayron:bodies"        // Object body storage
Tree "vayron:type-registry"      // TypeToken → Schema
Tree "vayron:relations:{type}"   // Per-type relationship indexes
```

---

## 10. Next Steps

1. **Prototype VayronPager**: Extend AbstractPager with basic hooks
2. **Implement OID Index**: FixedSizeTree for OID→Location
3. **Create VayronHandle struct**: Define handle layout
4. **Test materialization**: Cold path with basic object
5. **Measure overhead**: Compare with regular managed objects

See [09-VAYRON-Considerations](./09-VAYRON-Considerations.md) for architectural decisions.
