# T07: FieldAccess_Persist Driver

> **Work Package:** WP2.4
> **Dependencies:** T03 (Dirty Tracking), T05 (Storage_Voron Driver)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement a non-default FieldAccessDevice driver that marks objects as dirty on write and handles flush to persistent storage.

---

## Background

The FlushPersist mode works as follows:
1. Field writes mark object as dirty via `IStorageOps.MarkDirty`
2. Dirty objects are persisted on explicit `Flush()` or end-of-turn
3. After successful persist, dirty flag is cleared

This driver intercepts the `OnAfterAccess` hook for writes.

---

## Implementation

### 1. PersistentFieldAccessOps

**File:** `System.Private.CoreLib/src/System/OS/Storage/PersistentFieldAccessOps.cs` (new)

```csharp
namespace System.OS.Storage
{
    /// <summary>
    /// FieldAccess driver that tracks dirty state for persistence.
    /// </summary>
    internal sealed class PersistentFieldAccessOps
    {
        /// <summary>
        /// Called after a field write operation.
        /// Marks the object as dirty for later persistence.
        /// </summary>
        public void OnAfterWrite(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            // Only mark dirty if object is routed (virtual)
            if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                return;

            TypeDriverHelper.MarkDirty(obj);
        }

        /// <summary>
        /// Flush a single object to storage.
        /// </summary>
        public bool Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            if (!TypeDriverHelper.IsDirty(obj))
                return true;  // Nothing to do

            // Persist to storage
            if (VirtualOpsRoot.Persist(obj, out _))
            {
                // Clear dirty flag on success
                TypeDriverHelper.ClearDirty(obj);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Flush all dirty objects to storage.
        /// </summary>
        public int FlushAll()
        {
            var storage = VoronStorage.Instance;
            using var tx = storage.WriteTransaction();

            int flushedCount = 0;

            // Get all dirty objects
            // Note: This needs native support to enumerate dirty objects
            foreach (var obj in GetDirtyObjects())
            {
                if (FlushInTransaction(obj, tx))
                {
                    flushedCount++;
                }
            }

            tx.Commit();
            return flushedCount;
        }

        private bool FlushInTransaction(object obj, Voron.Impl.Transaction tx)
        {
            // Create VContext with this transaction
            var ctx = new VContext { Transaction = tx };

            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            var tree = tx.CreateTree("vobjects");
            var bodyBytes = BodyEncoder.Serialize(obj);

            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);
            tree.Add(Voron.Slice.From(tx.Allocator, vuidBytes), bodyBytes);

            TypeDriverHelper.ClearDirty(obj);
            return true;
        }

        private IEnumerable<object> GetDirtyObjects()
        {
            // This needs native support via QCall
            // Returns objects from the dirty set
            return TypeDriverHelper.EnumerateDirtyObjects();
        }
    }
}
```

### 2. Native Integration

Hook the FieldAccess intrinsics to call this driver:

**File:** `src/runtime/src/coreclr/vm/tds/tdsintrinsics.cpp` (modify)

```cpp
// Modify TDS_WriteField to call OnAfterWrite
void TDS_WriteField(Object* obj, FieldDesc* field, void* value)
{
    // ... existing write logic ...

    // Call OnAfterWrite for dirty tracking
    OpsRoot* ops = g_OpsRootTable.Get(obj);
    if (ops != &g_DefaultOpsRoot && ops->fieldAccessOps != nullptr)
    {
        // Mark dirty via native dirty set
        DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
        if (syncBlockIndex != 0)
        {
            g_DirtySet.MarkDirty(syncBlockIndex);
        }
    }
}
```

### 3. TypeDriverHelper Extensions

**File:** `System.Private.CoreLib/src/System/OS/TypeDriverHelper.cs` (modify)

```csharp
namespace System.OS
{
    public static partial class TypeDriverHelper
    {
        /// <summary>
        /// Clear the dirty flag for an object.
        /// </summary>
        public static void ClearDirty(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ClearDirtyInternal(ObjectHandleOnStack.Create(ref obj));
        }

        /// <summary>
        /// Enumerate all dirty objects.
        /// </summary>
        internal static IEnumerable<object> EnumerateDirtyObjects()
        {
            // This is complex - needs to iterate dirty set and resolve objects
            // Simplified for Phase 2: use callback-based enumeration
            var objects = new List<object>();
            EnumerateDirtyObjectsInternal((obj) => objects.Add(obj));
            return objects;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ClearDirty")]
        private static partial void ClearDirtyInternal(ObjectHandleOnStack obj);

        // Native callback for enumeration
        private delegate void DirtyObjectCallback(object obj);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_EnumerateDirtyObjects")]
        private static partial void EnumerateDirtyObjectsInternal(DirtyObjectCallback callback);
    }
}
```

### 4. VKernel Flush Methods

**File:** `System.Private.CoreLib/src/System/OS/VKernel.cs` (modify)

```csharp
namespace System.OS
{
    public static class VKernel
    {
        private static readonly PersistentFieldAccessOps _fieldAccessOps = new();

        /// <summary>
        /// Flush a single object to storage.
        /// </summary>
        public static void Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            _fieldAccessOps.Flush(obj);
        }

        /// <summary>
        /// Flush all dirty virtual objects to storage.
        /// </summary>
        public static int FlushAll()
        {
            return _fieldAccessOps.FlushAll();
        }

        /// <summary>
        /// Get count of objects pending flush.
        /// </summary>
        public static int GetPendingFlushCount()
        {
            return TypeDriverHelper.GetDirtyCount();
        }
    }
}
```

---

## QCalls for Native Support

**File:** `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` (add)

```cpp
extern "C" void QCALLTYPE TDSNative_ClearDirty(QCall::ObjectHandleOnStack obj)
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
            g_DirtySet.ClearDirty(syncBlockIndex);
        }
    }
    END_QCALL;
}

typedef void (STDMETHODCALLTYPE *DirtyObjectCallback)(OBJECTREF obj);

extern "C" void QCALLTYPE TDSNative_EnumerateDirtyObjects(DirtyObjectCallback callback)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;
    GCX_COOP();

    // Get dirty entries
    DirtyEntry buffer[256];
    size_t count = g_DirtySet.GetDirtyEntries(buffer, 256);

    for (size_t i = 0; i < count; i++)
    {
        // Resolve SyncBlockIndex to Object
        SyncTableEntry* entry = &SyncTableEntry::GetSyncTableEntry()[buffer[i].syncBlockIndex];
        if (entry != nullptr && entry->m_Object != nullptr)
        {
            callback(ObjectToOBJECTREF(entry->m_Object));
        }
    }

    END_QCALL;
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `System/OS/Storage/PersistentFieldAccessOps.cs` | Create | Persist-aware field driver |
| `System/OS/VKernel.cs` | Modify | Add Flush methods |
| `System/OS/TypeDriverHelper.cs` | Modify | Add ClearDirty, enumeration |
| `vm/tds/tdsintrinsics.cpp` | Modify | Hook OnAfterWrite |
| `vm/tds/tdsqcalls.cpp` | Modify | Add dirty enumeration QCall |

---

## Acceptance Criteria

- [ ] Field writes mark objects as dirty
- [ ] VKernel.Flush(obj) persists single object
- [ ] VKernel.FlushAll() persists all dirty objects
- [ ] Dirty flag cleared after successful persist
- [ ] FlushAll uses single transaction for efficiency
- [ ] Field access still works normally after flush

---

## Testing

```csharp
[Fact]
public void FieldAccess_WriteMarks Dirty()
{
    var obj = new TestObject();
    TypeDriverHelper.EnableNonDefaultRouting(obj);

    Assert.False(TypeDriverHelper.IsDirty(obj));

    obj.IntField = 42;  // Write

    Assert.True(TypeDriverHelper.IsDirty(obj));
}

[Fact]
public void Flush_ClearsDirtyFlag()
{
    var obj = new TestObject { IntField = 42 };
    TypeDriverHelper.EnableNonDefaultRouting(obj);
    TypeDriverHelper.MarkDirty(obj);

    VKernel.Flush(obj);

    Assert.False(TypeDriverHelper.IsDirty(obj));
}

[Fact]
public void FlushAll_PersistsAllDirty()
{
    var obj1 = new TestObject { IntField = 1 };
    var obj2 = new TestObject { IntField = 2 };

    TypeDriverHelper.EnableNonDefaultRouting(obj1);
    TypeDriverHelper.EnableNonDefaultRouting(obj2);
    TypeDriverHelper.MarkDirty(obj1);
    TypeDriverHelper.MarkDirty(obj2);

    int flushed = VKernel.FlushAll();

    Assert.Equal(2, flushed);
    Assert.Equal(0, VKernel.GetPendingFlushCount());
}
```

---

## References

- Phase 2 Main Doc: Section 11.4 (WP2.4 FieldAccess_PersistOnFlush)
- Phase 2 Main Doc: Section 6 (FlushPersist mode)
