# Engram System - Design Specification v0.1

> **Status:** Initial Draft
> **Date:** 2025-12-05
> **Purpose:** Define the architecture for semantic memory packages in DOTNExT

---

## 1. Vision Recap

**Engram** = A self-contained memory package that:
- Captures an object graph with all relationships preserved
- Uses UUID-based identity (not memory addresses)
- Can be extracted from one VM, persisted, and loaded into another
- Carries semantic metadata beyond raw data
- Enables distributed computing, persistence, and boundary-crossing

---

## 2. Core Concepts

### 2.1 Thing (Engram Entity)

Every object in an Engram-aware runtime is a "Thing" with:

| Property | Description |
|----------|-------------|
| **UUID** | Globally unique identifier, assigned at creation |
| **Type** | Runtime type reference (versioned) |
| **Data** | Instance field values |
| **Relations** | Outgoing references to other Things (by UUID) |
| **Origin** | VM/Node that created this Thing |
| **Version** | Mutation counter / lineage info |

### 2.2 Engram (Memory Package)

An Engram contains:

```
┌─────────────────────────────────────────────────┐
│ ENGRAM HEADER                                   │
│  - Engram UUID                                  │
│  - Origin Node UUID                             │
│  - Creation timestamp                           │
│  - Version/checksum                             │
│  - Root Thing UUID(s)                           │
├─────────────────────────────────────────────────┤
│ TYPE TABLE                                      │
│  - Type definitions used in this Engram         │
│  - Full names, version info, field layouts      │
│  - Schema for forward/backward compatibility    │
├─────────────────────────────────────────────────┤
│ THING TABLE                                     │
│  - Array of Things with their data              │
│  - Internal references use local indices        │
│  - External references use full UUIDs           │
├─────────────────────────────────────────────────┤
│ RELATION TABLE                                  │
│  - Explicit relationship metadata               │
│  - Field name, source Thing, target UUID        │
│  - Optional: semantic annotations               │
├─────────────────────────────────────────────────┤
│ EXTERNAL REFERENCE TABLE                        │
│  - UUIDs of Things referenced but not included  │
│  - Type hints for each                          │
│  - Origin hints (which Node might have them)    │
└─────────────────────────────────────────────────┘
```

### 2.3 Reference Types in Engram

| Type | Symbol | Description |
|------|--------|-------------|
| Internal | `@` | Points to Thing within this Engram (local index) |
| External | `#` | Points to Thing outside this Engram (full UUID) |
| Null | `∅` | Explicit null reference |
| Lazy | `?` | Unresolved, will trigger resolution on access |

---

## 3. Runtime Integration Points

### 3.1 Object Creation Hook

**Where:** Object allocation in GC heap
**How:** JIT helper or profiler API intercept
**Action:**
1. Check if type is Engram-enabled (MethodTable flag)
2. If yes, generate UUID
3. Store UUID in SyncBlock extension or side table
4. Set BIT_SBLK_UNUSED (bit 31) as "has engram data" marker

```cpp
// Pseudo-code for allocation hook
void* AllocateEngramAware(MethodTable* pMT, size_t size) {
    void* obj = GCHeap::Alloc(size);

    if (pMT->IsEngramEnabled()) {
        GUID uuid = GenerateUUID();
        EngramSideTable::Register(obj, uuid);
        obj->GetHeader()->SetEngramMarker();
    }

    return obj;
}
```

### 3.2 Reference Assignment Hook

**Where:** Reference field writes
**How:** Write barrier extension or JIT helper
**Action:**
1. Check if source object has engram marker
2. If yes, record relationship (source UUID, field, target UUID)

```cpp
// Pseudo-code for write barrier extension
void WriteBarrierEngram(Object* src, Object** fieldAddr, Object* target) {
    // Standard write barrier
    WriteBarrier(fieldAddr, target);

    // Engram extension
    if (src->HasEngramMarker()) {
        EngramRelationTracker::RecordRelation(
            EngramSideTable::GetUUID(src),
            GetFieldOffset(src, fieldAddr),
            target ? EngramSideTable::GetUUID(target) : GUID_NULL
        );
    }
}
```

### 3.3 Engram Extraction

**Input:** Root object(s)
**Process:**
1. Walk object graph using CGCDesc (GC already knows reference fields!)
2. For each object visited:
   - Get UUID from side table
   - Serialize field values
   - Record all reference relationships
3. Classify references as Internal (in extraction set) or External
4. Build Engram structure

```cpp
Engram ExtractEngram(Object* root, ExtractionOptions options) {
    Engram result;
    Queue<Object*> toProcess;
    HashSet<Object*> visited;

    toProcess.Enqueue(root);

    while (!toProcess.IsEmpty()) {
        Object* obj = toProcess.Dequeue();
        if (visited.Contains(obj)) continue;
        visited.Add(obj);

        Thing thing = CreateThing(obj);
        result.AddThing(thing);

        // Use CGCDesc to find reference fields
        CGCDesc* gcDesc = CGCDesc::GetCGCDescFromMT(obj->GetMethodTable());
        for (each reference field in gcDesc) {
            Object* ref = GetReferenceField(obj, field);
            if (ref != null) {
                if (ShouldInclude(ref, options)) {
                    toProcess.Enqueue(ref);
                    result.AddInternalRelation(thing.UUID, field, GetUUID(ref));
                } else {
                    result.AddExternalRelation(thing.UUID, field, GetUUID(ref));
                }
            }
        }
    }

    return result;
}
```

### 3.4 Engram Loading

**Input:** Engram binary
**Process:**
1. Read header, validate
2. Load type table, resolve types (handle versioning)
3. For each Thing:
   - Allocate object (RuntimeHelpers.GetUninitializedObject)
   - Assign same UUID (or map old->new if collision)
   - Populate non-reference fields
4. Second pass: wire up internal references
5. Handle external references based on strategy (Lazy, Proxy, Fetch, etc.)

```cpp
LoadResult LoadEngram(Engram engram, LoadOptions options) {
    Dictionary<GUID, Object*> uuidToObject;

    // First pass: create objects
    for (Thing thing in engram.Things) {
        Type type = ResolveType(thing.TypeInfo);
        Object* obj = RuntimeHelpers::GetUninitializedObject(type);

        EngramSideTable::Register(obj, thing.UUID);
        PopulateValueFields(obj, thing.Data);

        uuidToObject[thing.UUID] = obj;
    }

    // Second pass: wire references
    for (Relation rel in engram.Relations) {
        Object* src = uuidToObject[rel.SourceUUID];

        if (rel.IsInternal) {
            Object* target = uuidToObject[rel.TargetUUID];
            SetReferenceField(src, rel.FieldOffset, target);
        } else {
            // External reference handling
            switch (options.ExternalRefStrategy) {
                case Lazy:
                    SetReferenceField(src, rel.FieldOffset,
                        CreateLazyProxy(rel.TargetUUID));
                    break;
                case Fetch:
                    Object* fetched = FetchFromRemote(rel.TargetUUID);
                    SetReferenceField(src, rel.FieldOffset, fetched);
                    break;
                case Null:
                    SetReferenceField(src, rel.FieldOffset, null);
                    break;
            }
        }
    }

    return new LoadResult(uuidToObject);
}
```

---

## 4. Implementation Phases

### Phase 1: Side Table Proof of Concept
- Pure managed code implementation
- Side table (ConcurrentDictionary) for UUID storage
- Manual extraction/loading APIs
- No runtime modification
- **Goal:** Validate the concept works

### Phase 2: Profiler-Based Tracking
- Use ICorProfilerCallback for object creation hooks
- Automatic UUID assignment for marked types
- Relationship tracking via field write notifications
- Still no runtime modification
- **Goal:** Automatic tracking without code changes

### Phase 3: Runtime Integration
- Add Engram marker bit (BIT_SBLK_UNUSED)
- Extend SyncBlock for UUID storage
- Add JIT helper for fast UUID access
- Integrate with CGCDesc for graph walking
- **Goal:** Native performance

### Phase 4: Language Integration
- `[Engram]` attribute for type opt-in
- Roslyn source generator for boilerplate
- C# syntax for engram operations (TBD)
- **Goal:** Developer experience

### Phase 5: Distributed Protocol
- Node discovery and identity
- Engram transfer protocol
- External reference resolution
- Security and ownership
- **Goal:** Cross-VM operation

---

## 5. Key Design Decisions

### 5.1 UUID Generation

**Decision:** Use UUIDv7 (time-ordered)
**Rationale:**
- Sortable by creation time
- Includes timestamp intrinsically
- Good distribution for hash tables
- 128 bits = effectively infinite namespace

### 5.2 Opt-In vs Opt-Out

**Decision:** Opt-in via `[Engram]` attribute or base type
**Rationale:**
- Zero overhead for non-engram code
- Explicit intent
- Gradual adoption possible

### 5.3 Reference Tracking Granularity

**Decision:** Track at field assignment, not just extraction time
**Rationale:**
- Enables incremental updates
- Supports live synchronization scenarios
- More expensive but more powerful

### 5.4 External Reference Default

**Decision:** Default to Lazy with configurable strategies
**Rationale:**
- Doesn't block loading
- Triggers resolution only when needed
- Application can choose appropriate strategy

---

## 6. Open Questions

1. **Type Versioning:** How to handle type changes between extraction and load?
   - Schema evolution strategy needed
   - Orleans-style `[Id(n)]` for field stability?

2. **Circular References with External:** What if A->B->C->A but only extracting A,B?
   - Need clear semantics for partial graphs

3. **Large Object Handling:** Special treatment for LOH objects?
   - Streaming extraction for large arrays?

4. **Security:** How to prevent UUID spoofing in distributed scenarios?
   - Cryptographic signatures on Engrams?
   - Trusted node certificates?

5. **GC Interaction:** Does UUID tracking affect GC performance?
   - Side table entries need cleanup on object collection
   - Weak reference pattern for side table?

---

## 7. Relation to Existing DOTNExT Work

### Orleans Integration
- Grain identity → Thing UUID
- Grain state → Engram
- Activation → Engram loading
- Deactivation → Engram extraction

### Async+ Integration
- State machine state → Engram
- Continuation → Thing with relations to captured variables
- Resume → Load Engram, continue execution

### Future: Semantic Embeddings
- Thing metadata could include vector embeddings
- Relationships could carry semantic weight
- AI-assisted graph operations (find similar, predict relations)

---

## 8. Next Steps

1. [ ] Implement Phase 1 PoC in managed code
2. [ ] Test with simple object graphs
3. [ ] Measure overhead of UUID tracking
4. [ ] Design binary format for Engram serialization
5. [ ] Prototype Orleans integration

---

*Version 0.1 - Initial Design Draft*
*Author: Claude Opus 4.5 in collaboration with Louis*
