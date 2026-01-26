# T06: GC Integration

> **Work Package:** WP6
> **Dependencies:** T02 (OpsRoot Side Table)
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Ensure GC correctly handles TDS (TypeDriver System) objects. For Phase 1, this means:
1. TDS objects use standard GC scanning (no custom scanning)
2. OpsRootTable entries are cleaned up when objects are collected

---

## Scope Clarification

### What Phase 1 Does
- Uses existing CGCDesc scanning for all objects
- Implements cleanup via generation tag (from T02)
- Optionally hooks SyncBlock recycling (if straightforward)

### What Phase 1 Does NOT Do
- Does NOT implement custom GC scanning
- Does NOT modify GC mark phase
- Custom scanning deferred to Phase 3+ (see IMP-004)

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/runtime/src/coreclr/vm/syncblk.cpp` | Optional: SyncBlock recycle hook |

---

## Implementation Steps

### Step 1: Verify Generation Tag Safety (from T02)

The generation tag implemented in T02 provides safety:

```cpp
// On lookup, stale entries are detected and removed
OpsRoot* OpsRootTable::GetByIndex(DWORD syncBlockIndex)
{
    // ...
    if (entry->generationTag != currentGen) {
        // Stale entry - remove it
        m_table.Remove(syncBlockIndex);
        return &g_DefaultOpsRoot;
    }
    // ...
}
```

This is sufficient for Phase 1 correctness.

### Step 2: Optional - SyncBlock Recycle Hook

If time permits, add clean recycle hook:

**Find the recycle point in syncblk.cpp:**

```cpp
// Look for:
// - SyncBlock::~SyncBlock()
// - SyncBlockCache::GetNextFreeSyncBlock()
// - CleanupSyncBlocks()
```

**Add hook:**

```cpp
// In appropriate location
void OnSyncBlockAboutToBeReused(DWORD index)
{
    g_OpsRootTable.OnSyncBlockRecycled(index);
}
```

**Note:** This is optional for Phase 1. The generation tag is the primary safety mechanism.

### Step 3: Verify GC Scanning Path

Confirm that TDS objects use standard scanning:

```cpp
// In gc scanning, the existing path handles TDS objects:
void ScanObject(Object* obj)
{
    // Standard CGCDesc-based scanning
    // TDS bit doesn't change this in Phase 1
    go_through_object_cl(obj, promote_func, scan_context);
}
```

TDS objects have standard CLR layout, so standard scanning works.

---

## Verification Tests

### Test 1: TDS Object Survives GC

```cpp
void TestTDSObjectSurvivesGC()
{
    Object* obj = AllocateTestObject();
    OpsRoot* custom = CreateCustomOpsRoot();
    TDS_SetOpsRoot(obj, custom);

    // Keep strong reference
    GCHandle handle = GCHandle::Alloc(obj, GCHandleType::Normal);

    // Force full GC
    GC_Collect(GC_GENERATION_MAX);
    GC_WaitForPendingFinalizers();

    // Object should still be routed
    Object* retrieved = (Object*)GCHandle::GetTarget(handle);
    assert(retrieved->IsTDSNonDefault());
    assert(g_OpsRootTable.Get(retrieved) == custom);

    GCHandle::Free(handle);
}
```

### Test 2: TDS Mapping Cleaned Up on Collection

```cpp
void TestTDSMappingCleanedUp()
{
    WeakGCHandle weakHandle;

    {
        Object* obj = AllocateTestObject();
        TDS_SetOpsRoot(obj, CreateCustomOpsRoot());
        weakHandle = GCHandle::Alloc(obj, GCHandleType::Weak);

        size_t countBefore = g_OpsRootTable.GetCount();
        assert(countBefore > 0);
    }
    // obj goes out of scope

    // Force GC
    GC_Collect(GC_GENERATION_MAX);
    GC_WaitForPendingFinalizers();
    GC_Collect(GC_GENERATION_MAX);

    // Object should be collected
    assert(GCHandle::GetTarget(weakHandle) == nullptr);

    // Mapping should be gone (or marked stale)
    // Note: May not be immediate due to generation tag approach
}
```

### Test 3: Reference Fields Scanned Correctly

```cpp
void TestTDSRefFieldsScanned()
{
    Object* parent = AllocateTestObject();
    Object* child = AllocateTestObject();

    // Make parent routed
    TDS_SetOpsRoot(parent, CreateCustomOpsRoot());

    // Set child as a field of parent
    FieldDesc* refField = GetRefField(parent);
    TDS_WriteRefField(parent, refField, child);

    // Only keep reference to parent
    WeakGCHandle childWeak = GCHandle::Alloc(child, GCHandleType::Weak);
    child = nullptr;  // Release strong reference

    // Force GC
    GC_Collect();

    // Child should survive (referenced by parent)
    assert(GCHandle::GetTarget(childWeak) != nullptr);
}
```

### Test 4: GC Compaction Survival

```cpp
void TestTDSObjectSurvivesCompaction()
{
    Object* obj = AllocateTestObject();
    OpsRoot* custom = CreateCustomOpsRoot();
    TDS_SetOpsRoot(obj, custom);

    void* originalAddr = (void*)obj;

    // Allocate many objects to trigger compaction
    for (int i = 0; i < 10000; i++) {
        AllocateTestObject();
    }

    // Force compacting GC
    GC_Collect(GC_GENERATION_MAX);

    // Object may have moved
    void* currentAddr = (void*)obj;
    // Note: obj reference is updated by GC

    // But routing should still work!
    // (SyncBlockIndex is stable, unlike object address)
    assert(obj->IsTDSNonDefault());
    assert(g_OpsRootTable.Get(obj) == custom);
}
```

---

## Acceptance Criteria

- [ ] TDS objects survive GC with routing intact
- [ ] Reference fields in TDS objects are scanned correctly
- [ ] Child objects referenced by TDS objects survive GC
- [ ] TDS objects survive compaction (SyncBlockIndex stable)
- [ ] Stale mappings are detected via generation tag
- [ ] (Optional) SyncBlock recycle hook cleans up mappings
- [ ] No GC corruption under stress
- [ ] Existing GC tests pass

---

## Notes

### Why This Is Low Complexity

Phase 1 makes minimal GC changes:
1. **No custom scanning** - Uses standard CGCDesc
2. **No mark phase changes** - Standard object marking
3. **Safety via generation tag** - Already in T02

The main work is verification and testing.

### Future Work (IMP-001, IMP-004)

- Clean SyncBlock recycle hook -> IMP-001
- Custom GC scanning for external bodies -> IMP-004

---

## References

- Main Doc: Part III SS3.2 WP6
- Main Doc: Part VI SS6.1 (GC scanning = Default only)
- CLR Integration Reference: SS4 (GC Integration Points)
- Backlog: IMP-001, IMP-004
