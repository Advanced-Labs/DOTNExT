# T02: OpsRoot Side Table

> **Work Package:** WP2
> **Dependencies:** T01 (Header Bit Infrastructure)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement GC-safe mapping from objects to `OpsRoot*` using SyncBlockIndex as the stable key, with generation tag safety net.

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/runtime/src/coreclr/vm/dds/opsroottable.h` | Side table declaration |
| `src/runtime/src/coreclr/vm/dds/opsroottable.cpp` | Side table implementation |

## Files to Modify

| File | Changes |
|------|---------|
| `src/runtime/src/coreclr/vm/CMakeLists.txt` | Add new DDS source files |
| `src/runtime/src/coreclr/vm/ceemain.cpp` | Initialize OpsRootTable |

---

## Implementation Steps

### Step 1: Create Directory Structure

```bash
mkdir -p src/runtime/src/coreclr/vm/dds
```

### Step 2: Implement opsroottable.h

```cpp
#ifndef _OPSROOTTABLE_H_
#define _OPSROOTTABLE_H_

#include "common.h"
#include "shash.h"

// Forward declarations
struct OpsRoot;
class Object;

//-----------------------------------------------------------------------------
// OpsRoot entry with generation tag for safety
//-----------------------------------------------------------------------------
struct OpsRootEntry {
    OpsRoot* ops;
    uint32_t generationTag;  // Validates entry is not stale
};

//-----------------------------------------------------------------------------
// Hash traits for SyncBlockIndex keys
//-----------------------------------------------------------------------------
class OpsRootTableTraits : public DefaultSHashTraits<OpsRootEntry>
{
public:
    typedef DWORD key_t;

    static key_t GetKey(const OpsRootEntry& e);
    static BOOL Equals(key_t k1, key_t k2) { return k1 == k2; }
    static count_t Hash(key_t k) { return (count_t)k; }

    static const OpsRootEntry Null();
    static const OpsRootEntry Deleted();
    static bool IsNull(const OpsRootEntry& e);
    static bool IsDeleted(const OpsRootEntry& e);
};

//-----------------------------------------------------------------------------
// Thread-safe table: SyncBlockIndex → OpsRoot*
//-----------------------------------------------------------------------------
class OpsRootTable
{
private:
    typedef SHash<OpsRootTableTraits> TableType;

    TableType m_table;
    CrstExplicitInit m_lock;
    uint32_t m_currentGeneration;  // Incremented on recycle events

public:
    void Initialize();
    void Destroy();

    // Get OpsRoot for object (validates generation)
    OpsRoot* Get(Object* obj);

    // Get by SyncBlockIndex directly
    OpsRoot* GetByIndex(DWORD syncBlockIndex);

    // Set OpsRoot for object (ensures SyncBlock exists)
    void Set(Object* obj, OpsRoot* ops);

    // Remove entry
    void Remove(Object* obj);

    // Called when SyncBlock is recycled
    void OnSyncBlockRecycled(DWORD syncBlockIndex);

    // Get current generation for an index
    uint32_t GetCurrentGeneration(DWORD syncBlockIndex);

    // Debug/diagnostics
    void EnumerateEntries(void (*callback)(DWORD, OpsRoot*, void*), void* context);
    size_t GetCount();
};

// Global instance
extern OpsRootTable g_OpsRootTable;

#endif // _OPSROOTTABLE_H_
```

### Step 3: Implement opsroottable.cpp

```cpp
#include "common.h"
#include "dds/opsroottable.h"
#include "dds/opsroot.h"
#include "syncblk.h"
#include "object.h"

// Global instance
OpsRootTable g_OpsRootTable;

void OpsRootTable::Initialize()
{
    m_lock.Init(CrstOpsRootTable);  // May need to add CrstType
    m_currentGeneration = 1;
}

void OpsRootTable::Destroy()
{
    m_lock.Destroy();
}

OpsRoot* OpsRootTable::Get(Object* obj)
{
    if (!obj->IsDDSNonDefault()) {
        return &g_DefaultOpsRoot;
    }

    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
    if (syncBlockIndex == 0) {
        return &g_DefaultOpsRoot;
    }

    return GetByIndex(syncBlockIndex);
}

OpsRoot* OpsRootTable::GetByIndex(DWORD syncBlockIndex)
{
    CrstHolder lock(&m_lock);

    OpsRootEntry* entry = m_table.Lookup(syncBlockIndex);
    if (entry == nullptr) {
        return &g_DefaultOpsRoot;
    }

    // Validate generation (safety net for reuse)
    uint32_t currentGen = GetCurrentGeneration(syncBlockIndex);
    if (entry->generationTag != currentGen) {
        // Stale entry - remove it
        m_table.Remove(syncBlockIndex);
        return &g_DefaultOpsRoot;
    }

    return entry->ops;
}

void OpsRootTable::Set(Object* obj, OpsRoot* ops)
{
    // Ensure object has a SyncBlock
    SyncBlock* syncBlock = obj->GetSyncBlock();
    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();

    CrstHolder lock(&m_lock);

    OpsRootEntry entry;
    entry.ops = ops;
    entry.generationTag = GetCurrentGeneration(syncBlockIndex);

    m_table.AddOrReplace(syncBlockIndex, entry);

    // Set the routing bit
    obj->GetHeader()->SetDDSNonDefault();
}

void OpsRootTable::Remove(Object* obj)
{
    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
    if (syncBlockIndex == 0) return;

    CrstHolder lock(&m_lock);
    m_table.Remove(syncBlockIndex);

    obj->GetHeader()->ClearDDSNonDefault();
}

void OpsRootTable::OnSyncBlockRecycled(DWORD syncBlockIndex)
{
    CrstHolder lock(&m_lock);

    // Remove any stale entry
    m_table.Remove(syncBlockIndex);

    // Increment generation to invalidate any cached lookups
    m_currentGeneration++;
}

uint32_t OpsRootTable::GetCurrentGeneration(DWORD syncBlockIndex)
{
    // For Phase 1, use global generation
    // Future: per-index generation tracking
    return m_currentGeneration;
}

size_t OpsRootTable::GetCount()
{
    CrstHolder lock(&m_lock);
    return m_table.GetCount();
}
```

### Step 4: Add to Build

**File:** `CMakeLists.txt`

```cmake
set(VM_SOURCES_DDS
    dds/opsroottable.cpp
    # Add more as created
)

list(APPEND VM_SOURCES ${VM_SOURCES_DDS})
```

### Step 5: Initialize at Startup

**File:** `ceemain.cpp`, in EE initialization

```cpp
void EEStartup()
{
    // ... existing initialization ...

    // Initialize DDS subsystem
    g_OpsRootTable.Initialize();
}
```

---

## Generation Tag Safety Net

The generation tag prevents stale mapping bugs:

1. Each entry stores the generation tag at time of creation
2. On lookup, current generation is compared
3. If mismatch, entry is stale and removed
4. On SyncBlock recycle, generation increments

This is a **safety net** until the clean recycle hook (IMP-001) is implemented.

---

## Acceptance Criteria

- [ ] `OpsRootTable` class compiles
- [ ] `g_OpsRootTable` global instance created
- [ ] Initialization called during EE startup
- [ ] `Get()` returns `g_DefaultOpsRoot` for unmarked objects
- [ ] `Set()` associates OpsRoot with object
- [ ] `Set()` ensures object has SyncBlock
- [ ] `Set()` sets DDS routing bit
- [ ] `Remove()` clears association and bit
- [ ] Generation tag prevents stale lookups
- [ ] Thread-safe (lock protects all operations)
- [ ] Existing tests pass (no regressions)

---

## Testing

### Unit Test

```cpp
void TestOpsRootTable()
{
    Object* obj = AllocateTestObject();

    // Initially default
    assert(g_OpsRootTable.Get(obj) == &g_DefaultOpsRoot);

    // Set custom OpsRoot
    OpsRoot* custom = CreateCustomOpsRoot();
    g_OpsRootTable.Set(obj, custom);

    assert(obj->IsDDSNonDefault());
    assert(g_OpsRootTable.Get(obj) == custom);

    // Remove
    g_OpsRootTable.Remove(obj);
    assert(!obj->IsDDSNonDefault());
    assert(g_OpsRootTable.Get(obj) == &g_DefaultOpsRoot);
}
```

### GC Survival Test

```cpp
void TestOpsRootSurvivesGC()
{
    Object* obj = AllocateTestObject();
    g_OpsRootTable.Set(obj, CreateCustomOpsRoot());

    GC_Collect();

    // Object still has routing (SyncBlockIndex is stable)
    assert(obj->IsDDSNonDefault());
    assert(g_OpsRootTable.Get(obj) != &g_DefaultOpsRoot);
}
```

---

## Notes

- SyncBlockIndex is used as key because it's stable across GC compaction
- Object addresses change during compaction, but SyncBlockIndex doesn't
- This avoids needing GC relocation callbacks
- Lock granularity can be improved later (reader-writer lock)

---

## References

- Main Doc: Part II §2.4 (Routing Logic)
- Main Doc: Part III §3.2 WP2
- CLR Integration Reference: §2 (SyncBlock Integration)
- Backlog: IMP-001 (Clean recycle hook)
