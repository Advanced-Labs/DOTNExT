# T03: Dirty Tracking

> **Work Package:** WP2.0 (Infrastructure)
> **Dependencies:** T01 (VContext Enhancement)
> **Estimated Complexity:** Medium
> **Status:** Code Complete - Awaiting TAI Build Verification

---

## Objective

Implement a mechanism to track which virtual objects have been modified (are "dirty") and need to be persisted on flush.

---

## Background

The FlushPersist mode (Phase 2 recommendation) requires:
- Field writes mark object as dirty
- Dirty objects are persisted on explicit flush or end-of-turn
- Dirty state is cleared after successful persist

This enables batched writes instead of per-field persistence.

---

## Implementation

### 1. Dirty Bit in Object Header

Extend the TDS bit usage or add a dirty flag to OpsRootEntry:

**Option A: Use header flags** (simpler)
```cpp
// In syncblk.h - could use additional bits if available
// Or track in OpsRootEntry (cleaner separation)
```

**Option B: Dirty flag in OpsRootEntry** (recommended)

**File:** `src/runtime/src/coreclr/vm/tds/opsroottable.h` (modify)

```cpp
struct OpsRootEntry
{
    DWORD syncBlockIndex;
    OpsRoot* ops;
    UINT32 generationTag;
    VUID vuid;              // From T02
    UINT32 flags;           // NEW: per-object flags

    // Flag constants
    static const UINT32 FLAG_DIRTY = 0x0001;
    static const UINT32 FLAG_PERSISTED = 0x0002;  // Has been persisted at least once

    bool IsDirty() const { return (flags & FLAG_DIRTY) != 0; }
    void SetDirty() { flags |= FLAG_DIRTY; }
    void ClearDirty() { flags &= ~FLAG_DIRTY; }
};
```

### 2. Dirty Set (for efficient enumeration)

Track dirty objects in a separate collection for efficient flush:

**File:** `src/runtime/src/coreclr/vm/tds/dirtyset.h` (new)

```cpp
#ifndef _DIRTYSET_H_
#define _DIRTYSET_H_

#include "common.h"
#include "shash.h"
#include "vuid.h"

// DirtySet - Tracks objects that need to be persisted
// Uses SyncBlockIndex as key (stable during object lifetime)

struct DirtyEntry
{
    DWORD syncBlockIndex;
    INT64 dirtyTimestamp;  // When first marked dirty (for ordering)
};

class DirtySetTraits : public DefaultSHashTraits<DirtyEntry>
{
public:
    typedef DWORD key_t;
    static key_t GetKey(const DirtyEntry& e) { return e.syncBlockIndex; }
    static BOOL Equals(key_t k1, key_t k2) { return k1 == k2; }
    static count_t Hash(key_t k) { return (count_t)k; }

    static DirtyEntry Null() { return DirtyEntry{0, 0}; }
    static DirtyEntry Deleted() { return DirtyEntry{(DWORD)-1, 0}; }
    static bool IsNull(const DirtyEntry& e) { return e.syncBlockIndex == 0 && e.dirtyTimestamp == 0; }
    static bool IsDeleted(const DirtyEntry& e) { return e.syncBlockIndex == (DWORD)-1; }
};

class DirtySet
{
private:
    SHash<DirtySetTraits> m_set;
    CrstExplicitInit m_lock;

public:
    void Initialize();
    void Destroy();

    // Mark object as dirty
    void MarkDirty(DWORD syncBlockIndex);

    // Clear dirty state (after persist)
    void ClearDirty(DWORD syncBlockIndex);

    // Check if dirty
    bool IsDirty(DWORD syncBlockIndex);

    // Get all dirty entries (for flush)
    // Returns count, fills buffer up to maxCount
    size_t GetDirtyEntries(DirtyEntry* buffer, size_t maxCount);

    // Clear all (after flush all)
    void ClearAll();

    // Count
    size_t GetCount();
};

extern DirtySet g_DirtySet;

#endif // _DIRTYSET_H_
```

### 3. DirtySet Implementation

**File:** `src/runtime/src/coreclr/vm/tds/dirtyset.cpp` (new)

```cpp
#include "common.h"
#include "dirtyset.h"

DirtySet g_DirtySet;

void DirtySet::Initialize()
{
    CONTRACTL { THROWS; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;
    m_lock.Init(CrstDirtySet, CrstFlags(CRST_DEFAULT));
}

void DirtySet::Destroy()
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;
    m_lock.Destroy();
}

void DirtySet::MarkDirty(DWORD syncBlockIndex)
{
    CONTRACTL { THROWS; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);

    // Check if already dirty
    if (m_set.LookupPtr(syncBlockIndex) != nullptr)
        return;

    DirtyEntry entry;
    entry.syncBlockIndex = syncBlockIndex;
    entry.dirtyTimestamp = GetTickCount64();

    m_set.Add(entry);
}

void DirtySet::ClearDirty(DWORD syncBlockIndex)
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);

    DirtyEntry* entry = const_cast<DirtyEntry*>(m_set.LookupPtr(syncBlockIndex));
    if (entry != nullptr && !DirtySetTraits::IsDeleted(*entry))
    {
        m_set.RemovePtr(entry);
    }
}

bool DirtySet::IsDirty(DWORD syncBlockIndex)
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);
    const DirtyEntry* entry = m_set.LookupPtr(syncBlockIndex);
    return entry != nullptr && !DirtySetTraits::IsDeleted(*entry);
}

size_t DirtySet::GetDirtyEntries(DirtyEntry* buffer, size_t maxCount)
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);

    size_t count = 0;
    for (auto iter = m_set.Begin(); iter != m_set.End() && count < maxCount; ++iter)
    {
        const DirtyEntry& entry = *iter;
        if (!DirtySetTraits::IsNull(entry) && !DirtySetTraits::IsDeleted(entry))
        {
            buffer[count++] = entry;
        }
    }
    return count;
}

void DirtySet::ClearAll()
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);
    m_set.RemoveAll();
}

size_t DirtySet::GetCount()
{
    CONTRACTL { NOTHROW; GC_NOTRIGGER; MODE_ANY; } CONTRACTL_END;

    CrstHolder lock(&m_lock);
    return m_set.GetCount();
}
```

### 4. Integration with TDS Initialization

**File:** `src/runtime/src/coreclr/vm/tds/defaultdrivers.cpp` (modify)

```cpp
void TDS_Initialize()
{
    // ... existing code ...
    g_OpsRootTable.Initialize();
    g_DirtySet.Initialize();  // NEW
}

void TDS_Shutdown()
{
    // ... existing code ...
    g_DirtySet.Destroy();  // NEW
    g_OpsRootTable.Destroy();
}
```

### 5. Add CrstDirtySet

**File:** `src/runtime/src/coreclr/inc/CrstTypes.def` (modify)

Add `CrstDirtySet` in alphabetical order.

---

## QCalls for Managed Access

**File:** `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` (add)

```cpp
extern "C" void QCALLTYPE TDSNative_MarkDirty(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;
    GCX_COOP();

    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        DWORD syncBlockIndex = OBJECTREFToObject(objRef)->GetHeader()->GetSyncBlockIndex();
        if (syncBlockIndex != 0)
        {
            g_DirtySet.MarkDirty(syncBlockIndex);
        }
    }
    END_QCALL;
}

extern "C" BOOL QCALLTYPE TDSNative_IsDirty(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;
    BOOL result = FALSE;
    BEGIN_QCALL;
    GCX_COOP();

    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        DWORD syncBlockIndex = OBJECTREFToObject(objRef)->GetHeader()->GetSyncBlockIndex();
        if (syncBlockIndex != 0)
        {
            result = g_DirtySet.IsDirty(syncBlockIndex) ? TRUE : FALSE;
        }
    }
    END_QCALL;
    return result;
}

extern "C" INT32 QCALLTYPE TDSNative_GetDirtyCount()
{
    QCALL_CONTRACT;
    INT32 count = 0;
    BEGIN_QCALL;
    count = (INT32)g_DirtySet.GetCount();
    END_QCALL;
    return count;
}
```

---

## Managed API

**File:** `System.Private.CoreLib/src/System/OS/TypeDriverHelper.cs` (modify)

```csharp
namespace System.OS
{
    public static partial class TypeDriverHelper
    {
        // Existing methods...

        /// <summary>
        /// Mark an object as dirty (needs to be persisted).
        /// </summary>
        public static void MarkDirty(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            MarkDirtyInternal(ObjectHandleOnStack.Create(ref obj));
        }

        /// <summary>
        /// Check if an object is dirty.
        /// </summary>
        public static bool IsDirty(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return IsDirtyInternal(ObjectHandleOnStack.Create(ref obj));
        }

        /// <summary>
        /// Get count of dirty objects.
        /// </summary>
        public static int GetDirtyCount() => GetDirtyCountInternal();

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_MarkDirty")]
        private static partial void MarkDirtyInternal(ObjectHandleOnStack obj);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_IsDirty")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsDirtyInternal(ObjectHandleOnStack obj);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetDirtyCount")]
        private static partial int GetDirtyCountInternal();
    }
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `vm/tds/dirtyset.h` | Create | DirtySet declaration |
| `vm/tds/dirtyset.cpp` | Create | DirtySet implementation |
| `vm/tds/opsroottable.h` | Modify | Add flags field |
| `vm/tds/defaultdrivers.cpp` | Modify | Initialize DirtySet |
| `inc/CrstTypes.def` | Modify | Add CrstDirtySet |
| `vm/tds/tdsqcalls.cpp` | Modify | Add dirty QCalls |
| `vm/qcallentrypoints.cpp` | Modify | Register dirty QCalls |
| `vm/CMakeLists.txt` | Modify | Add dirtyset.cpp |
| `System/OS/TypeDriverHelper.cs` | Modify | Add dirty methods |

---

## Acceptance Criteria

- [ ] DirtySet tracks dirty objects by SyncBlockIndex
- [ ] MarkDirty/ClearDirty work correctly
- [ ] GetDirtyEntries returns all dirty objects
- [ ] Thread-safe via CrstDirtySet
- [ ] Managed API for MarkDirty/IsDirty/GetDirtyCount
- [ ] Integration with TDS initialization

---

## Testing

```csharp
[Fact]
public void DirtyTracking_MarkAndCheck()
{
    var obj = new TestObject();
    TypeDriverHelper.EnableNonDefaultRouting(obj);

    Assert.False(TypeDriverHelper.IsDirty(obj));

    TypeDriverHelper.MarkDirty(obj);
    Assert.True(TypeDriverHelper.IsDirty(obj));
    Assert.Equal(1, TypeDriverHelper.GetDirtyCount());
}
```

---

## References

- Phase 2 Main Doc: Section 6 (Persistence Semantics - FlushPersist)
