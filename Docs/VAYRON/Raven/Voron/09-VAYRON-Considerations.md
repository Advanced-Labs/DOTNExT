# VAYRON Architectural Considerations

> Synthesis of Voron analysis for VAYRON handle/body architecture decisions.

---

## 1. Core Question

How do we design managed objects where:
- **Handle**: Lightweight, GC-managed, contains identity and control
- **Body**: Persistent, lives in Voron storage, survives process restart

---

## 2. Handle Design Options

### 2.1 Minimal Handle (Recommended for Phase 1)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct VayronHandleData
{
    public ulong OID;              // 8 bytes - stable identity
    public long Epoch;             // 8 bytes - transaction ID when cached
    public IntPtr CachedBodyPtr;   // 8 bytes - pointer into Voron mapping
    // Total: 24 bytes + object header
}
```

**Rationale**:
- Matches LMDB/Voron's transaction-based caching model
- Epoch allows cheap staleness check
- CachedBodyPtr avoids re-lookup for hot paths

### 2.2 Extended Handle (Phase 2+)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct VayronHandleDataEx
{
    public ulong OID;              // Stable identity
    public long Epoch;             // Version/transaction
    public IntPtr CachedBodyPtr;   // Hot pointer

    // Extended fields
    public uint Flags;             // Loaded/Loading/Unloading, etc.
    public uint TypeToken;         // Runtime type identifier
    public long ShardInfo;         // Federation/tenant routing
    public IntPtr RelationPtr;     // Offset into relation indexes
    // Total: 48+ bytes
}
```

---

## 3. Body Layout Options

### 3.1 Blob Storage (Simple)

Store body as opaque blob in Container:

```
Container Entry
┌─────────────────────────────────────────────────────────────────────┐
│ Size Header (4 bytes)                                               │
├─────────────────────────────────────────────────────────────────────┤
│ TypeToken (4 bytes)                                                 │
├─────────────────────────────────────────────────────────────────────┤
│ Field Data (variable)                                               │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ Field 0 data                                                    ││
│  │ Field 1 data                                                    ││
│  │ ...                                                             ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

**Pros**: Simple, works with existing Container API
**Cons**: Entire body must be loaded for any field access

### 3.2 Segmented Storage (Advanced)

Separate hot fields from cold fields:

```
Hot Segment (Container, frequently accessed)
┌─────────────────────────────────────────────────────────────────────┐
│ HotFieldCount | ColdSegmentRef | Field0 | Field1 | ...              │
└─────────────────────────────────────────────────────────────────────┘

Cold Segment (Container, rarely accessed)
┌─────────────────────────────────────────────────────────────────────┐
│ FieldN | FieldN+1 | ... (less frequently accessed data)             │
└─────────────────────────────────────────────────────────────────────┘
```

**Pros**: Only load what's needed
**Cons**: More complex, needs heuristics for hot/cold

### 3.3 Table Storage (Structured)

Use Table with schema for typed objects:

```csharp
var schema = new TableSchema()
    .DefineKey(OidColumn, isGlobal: false)
    .AddIndex(TypeColumn)
    .AddIndex(ModifiedTimeColumn);

// Each row is an object body
```

**Pros**: Secondary indexes, schema evolution, queries
**Cons**: More overhead for simple objects

---

## 4. OID Design

### 4.1 Structure

```
OID (64 bits)
┌───────────────┬──────────────────────┬────────────────────────────┐
│   TypeBits    │    ShardBits         │      SequenceBits          │
│   (8 bits)    │    (8 bits)          │      (48 bits)             │
└───────────────┴──────────────────────┴────────────────────────────┘
```

Or simpler:

```
OID (64 bits)
┌─────────────────────────────────────────────────────────────────────┐
│              Monotonically increasing sequence                       │
│              (like Voron's page numbers)                            │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 Generation

```csharp
// Similar to Voron's NextPageNumber
private long _nextOid;

public ulong GenerateOid()
{
    return (ulong)Interlocked.Increment(ref _nextOid);
}
```

---

## 5. Materialization Strategies

### 5.1 Lazy Materialization (Recommended)

```csharp
// Only materialize when accessed
public T GetField<T>(int fieldOffset)
{
    EnsureMaterialized();
    return *(T*)(CachedBodyPtr + fieldOffset);
}

private void EnsureMaterialized()
{
    if (CachedBodyPtr == IntPtr.Zero || IsStale())
    {
        Materialize();
    }
}
```

### 5.2 Eager Materialization

```csharp
// Materialize on handle creation
public static VayronHandle Load(ulong oid)
{
    var handle = new VayronHandle { OID = oid };
    handle.Materialize();  // Immediate
    return handle;
}
```

### 5.3 Prefetch Materialization

```csharp
// Batch materialize related objects
public void PrefetchRelated(VayronHandle root, int depth)
{
    var toMaterialize = TraverseRelations(root, depth);
    BatchMaterialize(toMaterialize);  // Single transaction
}
```

---

## 6. Transaction Semantics

### 6.1 Implicit Transactions

```csharp
// Each handle access uses ambient transaction
var value = handle.GetField<int>("Value");
// Implicitly uses current read transaction
```

### 6.2 Explicit Transactions

```csharp
using (var tx = Vayron.WriteTransaction())
{
    handle.SetField("Value", 42);
    handle.SetField("Name", "Updated");
    tx.Commit();  // Atomic
}
```

### 6.3 Snapshot Consistency

```csharp
using (var tx = Vayron.ReadTransaction())
{
    // All accesses see consistent snapshot
    var a = handle1.GetField<int>("X");
    var b = handle2.GetField<int>("Y");
    // a and b from same point in time
}
```

---

## 7. GC Coordination

### 7.1 Handle Tracking

```csharp
// GC-rooted but weak table for metadata
static ConditionalWeakTable<object, VayronMeta> MetaTable;

// Or: registry with weak references
static Dictionary<ulong, WeakReference<VayronHandle>> HandlesByOid;
```

### 7.2 Body Cleanup

```csharp
// When no handles reference an OID
class VayronFinalizer
{
    private ConcurrentQueue<ulong> _orphanedOids = new();

    public void OnHandleFinalized(ulong oid)
    {
        _orphanedOids.Enqueue(oid);
    }

    public void ProcessOrphans()
    {
        using var tx = _env.WriteTransaction();
        while (_orphanedOids.TryDequeue(out var oid))
        {
            if (!HasLiveHandles(oid))
            {
                DeleteBody(oid, tx);
            }
        }
        tx.Commit();
    }
}
```

---

## 8. Voron Mapping to VAYRON Concepts

| VAYRON Concept | Voron Implementation |
|----------------|----------------------|
| OID | Long key in FixedSizeTree |
| Body storage | Container allocation |
| Handle cache | Transaction-scoped page pointers |
| Epoch/Version | Transaction ID |
| Materialization | Page pointer acquisition |
| Persistence | Journal commit |
| Relationships | Separate Trees or PostingLists |
| Queries | Tree iteration / Table scans |

---

## 9. Open Questions

### 9.1 Object Header Bit

**Question**: Should we use `BIT_SBLK_UNUSED` or request a dedicated bit?

**Consideration**: Using unused bit is "free" but may conflict with future CLR changes.

**Recommendation**: Use unused bit for prototype, propose dedicated bit for production.

### 9.2 JIT Integration

**Question**: How deep should JIT integration go?

**Options**:
1. **Method interception only**: All access goes through helper methods
2. **Inline null-check**: Fast path for cached pointer
3. **Full intrinsics**: JIT emits specialized code for VAYRON types

**Recommendation**: Start with (1), graduate to (2) after proving value.

### 9.3 Multi-Process Access

**Question**: Can multiple processes share Voron storage?

**Answer**: Yes, Voron supports this. VAYRON handles would need:
- Coordination protocol for OID generation
- Handle invalidation on external writes

### 9.4 Schema Evolution

**Question**: How do we handle type changes?

**Recommendations**:
- Store schema version with body
- Migration on read (lazy upgrade)
- Use Voron's Table schema evolution

---

## 10. Phased Implementation Plan

### Phase 1: Proof of Concept
- Simple handle struct with OID + CachedPtr
- Container-based blob storage
- Manual transaction management
- Single-process only

### Phase 2: Runtime Integration
- Object header tagging
- JIT fast-path optimization
- Ambient transactions
- GC coordination

### Phase 3: Advanced Features
- Segmented storage (hot/cold)
- Relationship indexes
- Multi-process support
- Query capabilities

### Phase 4: Production Hardening
- Performance optimization
- Schema evolution
- Tooling (debugger, profiler)
- Documentation

---

## 11. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Voron perf overhead | Medium | Benchmark early, optimize hot paths |
| GC interaction complexity | High | Conservative approach, extensive testing |
| JIT changes instability | Medium | Abstraction layer, feature flags |
| Memory pressure issues | Medium | Integration with LowMemory handlers |
| Debugging complexity | High | Custom debugger visualizers |

---

## 12. Success Criteria

1. **Performance**: Hot path < 10ns overhead vs regular field access
2. **Memory**: Handle overhead < 48 bytes per object
3. **Durability**: Crash recovery restores all committed state
4. **Compatibility**: Works with existing GC, debugger, profiler
5. **Simplicity**: Minimal CLR changes (< 500 lines core runtime)
