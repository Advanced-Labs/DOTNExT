# IMP-004: Custom GC Scanning

> **Status:** Backlog
> **Origin:** Phase 1 Gap Closure - Default Scanning Only Decision
> **Priority:** Medium (Enables advanced object layouts)
> **Target Phase:** Phase 3+

---

## Summary

Phase 1 uses **default GC scanning only** (`ObjectModel_DefaultCLR`), meaning all routed objects must have standard CLR layouts with `CGCDesc`-described reference fields. This improvement enables **custom GC scanning** for non-standard object layouts (external bodies, zero-copy mappings, etc.).

---

## Current State (Phase 1)

```cpp
// Phase 1: DefaultObjectModelDriver uses existing CGCDesc
static void DefaultOM_ScanRefs(VContext* ctx, Object* obj,
    DDSRefEnumCallback callback, ScanContext* sc, void* context)
{
    MethodTable* mt = obj->GetMethodTable();
    if (!mt->ContainsPointers()) return;

    // Standard CGCDesc-based scanning
    CGCDesc* map = CGCDesc::GetCGCDescFromMT(mt);
    // ... enumerate references using CGCDesc ...
}
```

**Limitations:**
- All routed objects must have CLR-standard layout
- Cannot have references in external memory (Voron pages)
- Cannot have custom reference discovery logic

---

## Use Cases for Custom Scanning

### 1. External Body (Voron-Backed)

Object handle is small, body lives in Voron memory:
```
Handle (GC heap):           Body (Voron page):
┌─────────────────┐         ┌─────────────────┐
│ ObjHeader       │         │ Field1: ref ────┼──► Another object
│ MethodTable*    │         │ Field2: value   │
│ VUID            │         │ Field3: ref ────┼──► Another object
│ BodyPtr ────────┼────────►│ ...             │
└─────────────────┘         └─────────────────┘
```

GC must scan references in the external body.

### 2. Lazy Reference Resolution

References stored as VUIDs, resolved on access:
```
Body:
┌─────────────────┐
│ RelatedVUID: 123│  ← Not a pointer, but represents a reference
│ ...             │
└─────────────────┘
```

GC must understand this represents a reachability edge.

### 3. Hybrid Layout

Some fields in GC heap, some in external storage:
```
Handle:                     External:
┌─────────────────┐         ┌─────────────────┐
│ HotField1: ref  │         │ ColdField1: ref │
│ HotField2: value│         │ ColdField2: value│
└─────────────────┘         └─────────────────┘
```

---

## Proposed Solution

### ObjectModelDevice Interface Extension

```cpp
struct IObjectModelOps {
    // ... existing ...

    // Phase 3+: Custom scanning support
    enum ScanMode {
        SCAN_DEFAULT,      // Use CGCDesc
        SCAN_CUSTOM,       // Use ScanRefs callback
        SCAN_HYBRID        // CGCDesc + custom for external
    };

    ScanMode (*GetScanMode)(VContext* ctx, Object* obj);

    // Enhanced ScanRefs for external memory
    void (*ScanExternalRefs)(
        VContext* ctx,
        Object* obj,
        void* externalBody,
        size_t bodySize,
        DDSRefEnumCallback callback,
        ScanContext* sc,
        void* context);
};
```

### GC Integration

```cpp
// In gc.cpp, during object scanning
void ScanObject(Object* obj, promote_func* fn, ScanContext* sc)
{
    if (obj->IsDDSNonDefault())
    {
        OpsRoot* ops = DDS_GetOpsRoot(obj);
        ScanMode mode = ops->objectModelOps->GetScanMode(&g_NullContext, obj);

        switch (mode) {
            case SCAN_DEFAULT:
                // Use standard CGCDesc path
                go_through_object_cl(obj, fn, sc);
                break;

            case SCAN_CUSTOM:
                // Driver handles all scanning
                ops->objectModelOps->ScanRefs(&g_NullContext, obj, WrapPromote, sc, fn);
                break;

            case SCAN_HYBRID:
                // Standard + external
                go_through_object_cl(obj, fn, sc);
                ops->objectModelOps->ScanExternalRefs(&g_NullContext, obj,
                    GetExternalBody(obj), GetExternalBodySize(obj),
                    WrapPromote, sc, fn);
                break;
        }
    }
    else
    {
        // Default path (unchanged)
        go_through_object_cl(obj, fn, sc);
    }
}
```

---

## Correctness Requirements

### GC Invariants That Must Be Preserved

1. **All live references enumerated** - Missing a reference causes premature collection
2. **All reported references are valid** - Invalid pointer causes GC corruption
3. **References updated on relocation** - If object moves, references must be updated
4. **Write barriers maintained** - New references must trigger barrier

### Driver Contract

```cpp
// Driver MUST report all references reachable from the object
// Driver MUST handle reference updates when GC relocates targets
// Driver MUST call WriteBarrier when references change
```

---

## Implementation Tasks

1. [ ] Define `ScanMode` enum and `GetScanMode` interface method
2. [ ] Add `ScanExternalRefs` to IObjectModelOps
3. [ ] Modify GC scanning path to check DDS bit
4. [ ] Implement hybrid scanning logic
5. [ ] Add reference update callback for relocation
6. [ ] Create test driver with external body
7. [ ] Stress test with compacting GC

---

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Missed reference | Critical | Extensive testing; conservative default |
| Invalid pointer reported | Critical | Validation in debug builds |
| Relocation not handled | Critical | Force pinning initially; add relocation support |
| Performance regression | Medium | Only invoke custom path for routed objects |

---

## References

- Phase 1 Doc: Part VI §6.1 (GC scanning = Default only)
- CLR Integration Reference: `Phase1/CLR-Integration-Reference.md` §4
- GC Source: `src/runtime/src/coreclr/gc/gc.cpp`
- CGCDesc: `src/runtime/src/coreclr/vm/gcdesc.h`
