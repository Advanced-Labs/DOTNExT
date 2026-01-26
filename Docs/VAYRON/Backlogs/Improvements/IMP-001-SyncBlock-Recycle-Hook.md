# IMP-001: SyncBlock Recycle Hook (Clean Solution)

> **Status:** Backlog
> **Origin:** Phase 1 Gap Closure - Generation Tag Safety Net Decision
> **Priority:** Medium (Phase 1 works without this; improves robustness)

---

## Summary

Phase 1 uses a **generation tag safety net** to prevent stale `ops_root` mappings when SyncBlock indexes are reused. This improvement implements the **clean solution**: a direct hook into SyncBlock recycling to remove mappings proactively.

---

## Current State (Phase 1)

```cpp
// Phase 1 approach: generation tag validation
struct OpsRootEntry {
    OpsRoot* ops;
    uint32_t generationTag;
};

OpsRoot* Get(DWORD syncBlockIndex) {
    auto entry = m_table.Get(syncBlockIndex);
    if (entry && entry.generationTag == GetCurrentGeneration(syncBlockIndex)) {
        return entry.ops;
    }
    return &g_DefaultOpsRoot;  // Stale or missing
}
```

**Limitations:**
- Generation validation on every lookup (minor overhead)
- Stale entries remain in table until overwritten
- No proactive cleanup

---

## Proposed Improvement

### Option A: Hook into SyncBlock::OnRecycle()

If `SyncBlock::OnRecycle()` exists or can be added:

```cpp
void SyncBlock::OnRecycle()
{
    // ... existing cleanup ...

    DWORD index = GetSyncBlockIndex();
    g_OpsRootTable.OnSyncBlockRecycled(index);
}
```

### Option B: Hook into SyncBlockCache::GetNextFreeSyncBlock()

```cpp
SyncBlock* SyncBlockCache::GetNextFreeSyncBlock()
{
    SyncBlock* block = GetFreeEntry();
    if (block) {
        DWORD index = block->GetIndex();
        g_OpsRootTable.OnSyncBlockRecycled(index);  // Clean before reuse
    }
    return block;
}
```

### Option C: Hook into GCToEEInterface::SyncBlockCacheWeakPtrScan()

During GC's weak pointer scanning, identify SyncBlocks being reclaimed.

---

## Benefits

1. **Proactive cleanup** - Mappings removed before index reuse
2. **Remove generation overhead** - No validation needed on lookup
3. **Memory efficiency** - Table doesn't accumulate stale entries
4. **Correctness guarantee** - Eliminates race condition window

---

## Implementation Tasks

1. [ ] Locate exact SyncBlock recycling code path in `syncblk.cpp`
2. [ ] Identify best hook point (OnRecycle, GetNextFree, or GC scan)
3. [ ] Implement `OpsRootTable::OnSyncBlockRecycled()`
4. [ ] Add stress tests for SyncBlock reuse scenarios
5. [ ] Remove generation tag validation (optional, or keep as defense-in-depth)

---

## Acceptance Criteria

- [ ] `ops_root` entries are removed before SyncBlock index reuse
- [ ] No stale mapping bugs under stress testing
- [ ] No performance regression on hot path

---

## References

- Phase 1 Doc: Part VI §6.2 (Explicitly Deferred)
- CLR Source: `src/runtime/src/coreclr/vm/syncblk.cpp`
- CLR Integration Reference: `Phase1/CLR-Integration-Reference.md` §2.3
