# IMP-002: JIT Helper Interception

> **Status:** Backlog
> **Origin:** Phase 1 Gap Closure - No JIT Surgery Decision
> **Priority:** High (Performance improvement for Phase 2+)
> **Target Phase:** Phase 2.5 (after persistence works)

---

## Summary

Phase 1 uses **syscall/intrinsics only** for driver dispatch, avoiding JIT modifications. This improvement adds **JIT helper interception** to achieve near-native field access performance for routed objects.

---

## Current State (Phase 1)

```csharp
// Phase 1: Explicit intrinsic calls
var value = VFieldRead<int>(obj, fieldOffset);
VFieldWrite(obj, fieldOffset, newValue);

// Compiler emits calls, no transparent interception
```

**Limitations:**
- Requires explicit API usage (not transparent)
- Higher overhead than inline field access
- Cannot optimize property accessors

---

## Proposed Improvement

### Modified JIT_GetFieldAddr

```cpp
HCIMPL2(void*, JIT_GetFieldAddr, Object* obj, FieldDesc* pFD)
{
    FCALL_CONTRACT;

    // Fast path: check DDS bit (~1ns)
    if (UNLIKELY(obj->IsDDSNonDefault()))
    {
        return DDS_GetFieldAddrHelper(obj, pFD);  // NOINLINE
    }

    // Original fast path (unchanged for default objects)
    return pFD->GetAddressGuaranteedInHeap(obj);
}
HCIMPLEND

NOINLINE void* DDS_GetFieldAddrHelper(Object* obj, FieldDesc* pFD)
{
    OpsRoot* ops = DDS_GetOpsRoot(obj);
    VContext* ctx = GetCurrentVContext();  // Phase 2+: may have transaction

    // Materialize if needed
    ops->objectModelOps->EnsureMaterialized(ctx, obj);

    // Get effective address
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, pFD);
    if (addr) return addr;

    return ops->fieldAccessOps->GetEffectiveAddress(ctx, obj, pFD);
}
```

### Write Barrier Modification

```cpp
void JIT_WriteBarrier_DDS(Object* obj, Object** dst, Object* ref)
{
    if (obj->IsDDSNonDefault())
    {
        OpsRoot* ops = DDS_GetOpsRoot(obj);
        FieldDesc* pFD = FindFieldDescFromAddress(obj, dst);
        if (pFD) {
            ops->fieldAccessOps->WriteBarrier(ctx, obj, pFD, ref);
            return;
        }
    }
    // Fall through to standard barrier
    SetObjectReference(dst, ref);
}
```

---

## Performance Targets

| Operation | Phase 1 (intrinsics) | With JIT Interception |
|-----------|---------------------|----------------------|
| Field read (default) | ~1ns | ~1ns (unchanged) |
| Field read (routed, hot) | ~100ns | ~15ns |
| Field read (routed, cold) | ~500ns | ~500ns |
| DDS bit check | - | ~1ns |

---

## Platform Considerations

### x64
- Modify `JIT_GetFieldAddr` in `jithelpers.cpp`
- Write barrier in ASM (`amd64/jithelpers_fast.asm`)

### ARM64
- Same approach, different files
- Verify helper locations in ARM64-specific code

---

## Prerequisites

Before implementing:
1. [ ] Phase 1 complete and tested
2. [ ] Phase 2 persistence working
3. [ ] VContext populated with transaction handles
4. [ ] Benchmark baseline established

---

## Implementation Tasks

1. [ ] Add DDS bit check to `JIT_GetFieldAddr`
2. [ ] Implement `DDS_GetFieldAddrHelper` (NOINLINE)
3. [ ] Modify write barrier for reference fields
4. [ ] Add platform-specific implementations (x64, ARM64)
5. [ ] Benchmark overhead on default objects (must be <2ns)
6. [ ] Benchmark improvement on routed objects
7. [ ] Stress test concurrent access

---

## Risks

| Risk | Mitigation |
|------|------------|
| Performance regression on default path | NOINLINE slow path; benchmark continuously |
| Correctness bugs in field access | Extensive testing; fuzzing |
| Platform-specific issues | CI matrix with all platforms |
| Write barrier bypass | Always call real barrier in driver |

---

## References

- Phase 1 Doc: Part VI §6.1 (JIT = None)
- CLR Integration Reference: `Phase1/CLR-Integration-Reference.md` §3
- JIT Helpers: `src/runtime/src/coreclr/vm/jithelpers.cpp`
