# T05: Storage_Voron Driver

> **Work Package:** WP2.2
> **Dependencies:** T01 (VContext), T02 (VUID), T04 (Voron Embedding)
> **Estimated Complexity:** High
> **Status:** Pending

---

## Objective

Implement the first real `IStorageOps` driver backed by Voron, enabling virtual objects to persist durably.

---

## Background

Phase 1 defined `IStorageOps` interface with placeholder methods:
- `Persist` / `Materialize` - store/load object bodies
- `IsDirty` / `MarkDirty` - dirty tracking hooks
- `BeginTransaction` / `CommitTransaction` / `RollbackTransaction` - transaction management

Phase 2 makes these real with Voron as the backing store.

---

## Implementation

### 1. VoronStorageOps Class (Managed)

**File:** `System.Private.CoreLib/src/System/OS/Storage/VoronStorageOps.cs` (new)

```csharp
namespace System.OS.Storage
{
    using Voron;
    using Voron.Impl;

    /// <summary>
    /// IStorageOps implementation backed by Voron.
    /// </summary>
    internal sealed class VoronStorageOps
    {
        private readonly VoronStorage _storage;

        public VoronStorageOps()
        {
            _storage = VoronStorage.Instance;
        }

        /// <summary>
        /// Persist an object's body to Voron storage.
        /// </summary>
        public bool Persist(VContext ctx, object obj, out VUID vuid)
        {
            ArgumentNullException.ThrowIfNull(obj);

            // Get or generate VUID
            vuid = GetOrCreateVUID(obj);

            // Get write transaction from context or create one
            var tx = GetWriteTransaction(ctx);
            bool ownsTx = tx == null;

            if (ownsTx)
            {
                tx = _storage.WriteTransaction();
            }

            try
            {
                var tree = tx.CreateTree("vobjects");

                // Serialize body
                var bodyBytes = BodyEncoder.Serialize(obj);

                // Store with VUID as key
                var vuidBytes = new byte[16];
                vuid.WriteBytes(vuidBytes);
                tree.Add(Slice.From(tx.Allocator, vuidBytes), bodyBytes);

                if (ownsTx)
                {
                    tx.Commit();
                }

                return true;
            }
            catch
            {
                if (ownsTx)
                {
                    tx?.Dispose();  // Rollback on exception
                }
                throw;
            }
            finally
            {
                if (ownsTx)
                {
                    tx?.Dispose();
                }
            }
        }

        /// <summary>
        /// Materialize an object from Voron storage by VUID.
        /// </summary>
        public object? Materialize(VContext ctx, VUID vuid, Type expectedType)
        {
            if (vuid.IsEmpty) return null;

            var tx = GetReadTransaction(ctx);
            bool ownsTx = tx == null;

            if (ownsTx)
            {
                tx = _storage.ReadTransaction();
            }

            try
            {
                var tree = tx.ReadTree("vobjects");
                if (tree == null) return null;

                var vuidBytes = new byte[16];
                vuid.WriteBytes(vuidBytes);

                var result = tree.Read(Slice.From(tx.Allocator, vuidBytes));
                if (result == null) return null;

                // Deserialize body into new object
                var bodyBytes = result.Reader.AsSpan().ToArray();
                return BodyEncoder.Deserialize(bodyBytes, expectedType);
            }
            finally
            {
                if (ownsTx)
                {
                    tx?.Dispose();
                }
            }
        }

        /// <summary>
        /// Delete an object from storage.
        /// </summary>
        public bool Delete(VContext ctx, VUID vuid)
        {
            if (vuid.IsEmpty) return false;

            using var tx = _storage.WriteTransaction();
            var tree = tx.CreateTree("vobjects");

            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);

            tree.Delete(Slice.From(tx.Allocator, vuidBytes));
            tx.Commit();

            return true;
        }

        /// <summary>
        /// Begin a new write transaction.
        /// </summary>
        public VoronTransaction BeginTransaction()
        {
            return new VoronTransaction(_storage.WriteTransaction(), isWrite: true);
        }

        /// <summary>
        /// Commit a transaction.
        /// </summary>
        public bool Commit(VoronTransaction tx)
        {
            if (tx == null || !tx.IsWrite) return false;

            tx.Inner.Commit();
            return true;
        }

        /// <summary>
        /// Rollback a transaction.
        /// </summary>
        public void Rollback(VoronTransaction tx)
        {
            tx?.Inner.Dispose();
        }

        private Transaction? GetWriteTransaction(VContext ctx)
        {
            // Extract transaction from VContext if present
            return ctx?.Transaction as Transaction;
        }

        private Transaction? GetReadTransaction(VContext ctx)
        {
            return ctx?.Transaction as Transaction;
        }

        private VUID GetOrCreateVUID(object obj)
        {
            // Check if object already has a VUID (from OpsRootEntry)
            var existing = TypeDriverHelper.GetVUID(obj);
            if (!existing.IsEmpty) return existing;

            // Generate new VUID
            var vuid = VUID.New();
            TypeDriverHelper.SetVUID(obj, vuid);
            return vuid;
        }
    }

    /// <summary>
    /// Transaction wrapper for VContext integration.
    /// </summary>
    internal sealed class VoronTransaction : IDisposable
    {
        public Transaction Inner { get; }
        public bool IsWrite { get; }

        public VoronTransaction(Transaction tx, bool isWrite)
        {
            Inner = tx;
            IsWrite = isWrite;
        }

        public void Dispose() => Inner?.Dispose();
    }
}
```

### 2. Native IStorageOps Shim

For native code that needs to call storage operations:

**File:** `src/runtime/src/coreclr/vm/tds/storageops.h` (new)

```cpp
#ifndef _STORAGEOPS_H_
#define _STORAGEOPS_H_

#include "common.h"
#include "vuid.h"

// Native shim for calling managed VoronStorageOps
// Uses reverse P/Invoke or managed delegates

class StorageOpsNative
{
public:
    // Initialize the managed storage ops (call during TDS init)
    static void Initialize();

    // Persist object body to storage
    static bool Persist(Object* obj, VUID* outVuid);

    // Materialize object from storage
    static Object* Materialize(VUID vuid, MethodTable* expectedType);

    // Transaction management
    static void* BeginWriteTransaction();
    static bool CommitTransaction(void* txHandle);
    static void RollbackTransaction(void* txHandle);
};

#endif // _STORAGEOPS_H_
```

### 3. Integration with OpsRoot

Create a VirtualDefaultOpsRoot that uses VoronStorageOps:

**File:** `System.Private.CoreLib/src/System/OS/Storage/VirtualOpsRoot.cs` (new)

```csharp
namespace System.OS.Storage
{
    /// <summary>
    /// Default OpsRoot for virtual (persistent) types.
    /// </summary>
    internal static class VirtualOpsRoot
    {
        private static readonly VoronStorageOps _storage = new();

        public static VoronStorageOps Storage => _storage;

        /// <summary>
        /// Persist an object using the virtual default storage.
        /// </summary>
        public static bool Persist(object obj, out VUID vuid)
        {
            // Get current context or use implicit one
            var ctx = VContext.Current ?? new VContext();
            return _storage.Persist(ctx, obj, out vuid);
        }

        /// <summary>
        /// Materialize an object by VUID.
        /// </summary>
        public static T? Materialize<T>(VUID vuid) where T : class
        {
            var ctx = VContext.Current ?? new VContext();
            return _storage.Materialize(ctx, vuid, typeof(T)) as T;
        }
    }
}
```

---

## QCalls for Native Access

**File:** `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` (add)

```cpp
extern "C" BOOL QCALLTYPE TDSNative_PersistObject(
    QCall::ObjectHandleOnStack obj,
    VUID* outVuid)
{
    QCALL_CONTRACT;
    BOOL result = FALSE;
    BEGIN_QCALL;

    // Call managed VoronStorageOps.Persist via reverse P/Invoke
    // Implementation depends on managed interop strategy

    END_QCALL;
    return result;
}

extern "C" void QCALLTYPE TDSNative_MaterializeObject(
    VUID vuid,
    QCall::TypeHandle typeHandle,
    QCall::ObjectHandleOnStack result)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;

    // Call managed VoronStorageOps.Materialize via reverse P/Invoke

    END_QCALL;
}
```

---

## TypeDriverHelper Extensions

**File:** `System.Private.CoreLib/src/System/OS/TypeDriverHelper.cs` (modify)

```csharp
namespace System.OS
{
    public static partial class TypeDriverHelper
    {
        /// <summary>
        /// Get the VUID for an object (if persisted).
        /// </summary>
        public static VUID GetVUID(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetVUIDInternal(ObjectHandleOnStack.Create(ref obj));
        }

        /// <summary>
        /// Set the VUID for an object.
        /// </summary>
        internal static void SetVUID(object obj, VUID vuid)
        {
            ArgumentNullException.ThrowIfNull(obj);
            SetVUIDInternal(ObjectHandleOnStack.Create(ref obj), vuid);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetVUID")]
        private static partial VUID GetVUIDInternal(ObjectHandleOnStack obj);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_SetVUID")]
        private static partial void SetVUIDInternal(ObjectHandleOnStack obj, VUID vuid);
    }
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `System/OS/Storage/VoronStorageOps.cs` | Create | Main storage driver |
| `System/OS/Storage/VirtualOpsRoot.cs` | Create | Virtual type OpsRoot |
| `vm/tds/storageops.h` | Create | Native shim header |
| `vm/tds/storageops.cpp` | Create | Native shim implementation |
| `vm/tds/tdsqcalls.cpp` | Modify | Add storage QCalls |
| `vm/tds/opsroottable.h` | Modify | Store VUID in entry |
| `System/OS/TypeDriverHelper.cs` | Modify | Add VUID accessors |

---

## Acceptance Criteria

- [ ] VoronStorageOps.Persist stores object body to Voron
- [ ] VoronStorageOps.Materialize loads object from Voron
- [ ] VUID is assigned on first persist
- [ ] Transaction management works (begin/commit/rollback)
- [ ] VContext carries transaction through operations
- [ ] TypeDriverHelper.GetVUID/SetVUID work
- [ ] Round-trip test: persist → shutdown → materialize works

---

## Testing

```csharp
[Fact]
public void StorageOps_PersistAndMaterialize_RoundTrips()
{
    var original = new TestObject { IntField = 42, StringField = "test" };

    // Persist
    Assert.True(VirtualOpsRoot.Persist(original, out var vuid));
    Assert.False(vuid.IsEmpty);

    // Materialize
    var restored = VirtualOpsRoot.Materialize<TestObject>(vuid);
    Assert.NotNull(restored);
    Assert.Equal(42, restored.IntField);
    Assert.Equal("test", restored.StringField);
}

[Fact]
public void StorageOps_SurvivesRestart()
{
    // This test requires process restart simulation
    // See T10 Test Suite for full implementation
}
```

---

## References

- Phase 2 Main Doc: Section 11.2 (WP2.2 Storage_Voron Driver)
- Voron-Integration-Guide.md: Tree operations, transactions
- Phase 1: IStorageOps interface definition
