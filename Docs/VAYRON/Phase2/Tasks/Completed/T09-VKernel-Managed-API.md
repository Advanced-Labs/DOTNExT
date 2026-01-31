# T09: VKernel Managed API

> **Work Package:** WP2.5
> **Dependencies:** T05 (Storage_Voron Driver), T07 (FieldAccess_Persist Driver), T08 (Driver Registry)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement the primary managed API surface for virtual objects: `VKernel.Get<T>`, `VKernel.New<T>`, and transaction/flush operations.

---

## Background

VKernel is the main entry point for application code working with virtual objects:
- `VKernel.Get<T>(vuid)` - Load existing object by VUID
- `VKernel.New<T>()` - Create new virtual object
- `VKernel.Flush()` / `VKernel.FlushAll()` - Persist dirty objects
- Transaction scopes for batched operations

---

## Implementation

### 1. VKernel Static Class

**File:** `System.Private.CoreLib/src/System/OS/VKernel.cs` (new/replace)

```csharp
namespace System.OS
{
    /// <summary>
    /// Kernel API for virtual object operations.
    /// </summary>
    public static class VKernel
    {
        private static readonly object s_initLock = new object();
        private static bool s_initialized;

        /// <summary>
        /// Initialize the virtual kernel. Called automatically on first use.
        /// </summary>
        public static void Initialize()
        {
            if (s_initialized) return;

            lock (s_initLock)
            {
                if (s_initialized) return;

                // Initialize Voron storage
                VoronStorage.Initialize();

                // Initialize driver registry
                InitializeNative();

                s_initialized = true;
            }
        }

        /// <summary>
        /// Shutdown the virtual kernel.
        /// </summary>
        public static void Shutdown()
        {
            lock (s_initLock)
            {
                if (!s_initialized) return;

                // Flush any pending changes
                FlushAll();

                // Shutdown storage
                VoronStorage.Shutdown();

                ShutdownNative();

                s_initialized = false;
            }
        }

        #region Object Creation

        /// <summary>
        /// Create a new virtual object with a new VUID.
        /// </summary>
        public static T New<T>() where T : class, new()
        {
            EnsureInitialized();

            // Create the managed object
            var obj = new T();

            // Generate VUID
            var vuid = VUID.New();

            // Set up for virtual operation
            TypeDriverHelper.SetVUID(obj, vuid);
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Register type if not already registered
            EnsureTypeRegistered<T>();

            return obj;
        }

        /// <summary>
        /// Create a new virtual object with a specific VUID.
        /// </summary>
        public static T New<T>(VUID vuid) where T : class, new()
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                throw new ArgumentException("VUID cannot be empty", nameof(vuid));

            // Create the managed object
            var obj = new T();

            // Set VUID
            TypeDriverHelper.SetVUID(obj, vuid);
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            EnsureTypeRegistered<T>();

            return obj;
        }

        #endregion

        #region Object Loading

        /// <summary>
        /// Get an existing virtual object by VUID.
        /// </summary>
        public static T? Get<T>(VUID vuid) where T : class
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return null;

            // Try to materialize from storage
            return VirtualOpsRoot.Materialize<T>(vuid);
        }

        /// <summary>
        /// Get an existing virtual object by VUID, or create new if not found.
        /// </summary>
        public static T GetOrNew<T>(VUID vuid) where T : class, new()
        {
            var obj = Get<T>(vuid);
            if (obj != null)
                return obj;

            return New<T>(vuid);
        }

        /// <summary>
        /// Check if an object exists in storage.
        /// </summary>
        public static bool Exists(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            return VirtualOpsRoot.Exists(vuid);
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Persist a single object to storage.
        /// </summary>
        public static void Persist(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            if (!TypeDriverHelper.IsNonDefaultRouted(obj))
            {
                throw new InvalidOperationException("Object is not a virtual object");
            }

            VirtualOpsRoot.Persist(obj, out _);
            TypeDriverHelper.ClearDirty(obj);
        }

        /// <summary>
        /// Flush a single dirty object to storage.
        /// </summary>
        public static bool Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            if (!TypeDriverHelper.IsDirty(obj))
                return true;  // Nothing to do

            if (VirtualOpsRoot.Persist(obj, out _))
            {
                TypeDriverHelper.ClearDirty(obj);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Flush all dirty objects to storage.
        /// </summary>
        public static int FlushAll()
        {
            EnsureInitialized();

            int flushedCount = 0;

            using var tx = VoronStorage.Instance.WriteTransaction();

            foreach (var obj in TypeDriverHelper.EnumerateDirtyObjects())
            {
                if (FlushInTransaction(obj, tx))
                {
                    flushedCount++;
                }
            }

            tx.Commit();
            return flushedCount;
        }

        private static bool FlushInTransaction(object obj, object tx)
        {
            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            if (VirtualOpsRoot.PersistInTransaction(obj, tx))
            {
                TypeDriverHelper.ClearDirty(obj);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get count of objects pending flush.
        /// </summary>
        public static int GetPendingFlushCount()
        {
            return TypeDriverHelper.GetDirtyCount();
        }

        #endregion

        #region Deletion

        /// <summary>
        /// Delete a virtual object from storage.
        /// </summary>
        public static bool Delete(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
                return false;

            return VirtualOpsRoot.Delete(vuid);
        }

        /// <summary>
        /// Delete a virtual object by VUID.
        /// </summary>
        public static bool Delete(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            return VirtualOpsRoot.Delete(vuid);
        }

        #endregion

        #region Transactions

        /// <summary>
        /// Begin a transaction scope.
        /// </summary>
        public static VTransaction BeginTransaction()
        {
            EnsureInitialized();
            return new VTransaction();
        }

        /// <summary>
        /// Execute an action within a transaction.
        /// </summary>
        public static void WithTransaction(Action action)
        {
            using var tx = BeginTransaction();
            try
            {
                action();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Execute a function within a transaction.
        /// </summary>
        public static T WithTransaction<T>(Func<T> func)
        {
            using var tx = BeginTransaction();
            try
            {
                var result = func();
                tx.Commit();
                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        #endregion

        #region Helpers

        private static void EnsureInitialized()
        {
            if (!s_initialized)
                Initialize();
        }

        private static void EnsureTypeRegistered<T>()
        {
            if (!TypeDriverRegistry.IsRegisteredForPersist<T>())
            {
                // Auto-register with default flags
                TypeDriverRegistry.Register<T>();
            }
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_VKernelInitialize")]
        private static partial void InitializeNative();

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_VKernelShutdown")]
        private static partial void ShutdownNative();

        #endregion
    }
}
```

### 2. VTransaction Class

**File:** `System.Private.CoreLib/src/System/OS/VTransaction.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// Transaction scope for batched virtual object operations.
    /// </summary>
    public sealed class VTransaction : IDisposable
    {
        private object? _nativeTransaction;
        private bool _committed;
        private bool _disposed;

        internal VTransaction()
        {
            _nativeTransaction = VoronStorage.Instance.WriteTransaction();
            VContextManager.PushTransaction(_nativeTransaction);
        }

        /// <summary>
        /// Commit all changes in this transaction.
        /// </summary>
        public void Commit()
        {
            ThrowIfDisposed();

            if (_committed)
                throw new InvalidOperationException("Transaction already committed");

            // Flush all dirty objects within this transaction
            foreach (var obj in TypeDriverHelper.EnumerateDirtyObjects())
            {
                if (VirtualOpsRoot.PersistInTransaction(obj, _nativeTransaction!))
                {
                    TypeDriverHelper.ClearDirty(obj);
                }
            }

            // Commit Voron transaction
            CommitTransaction(_nativeTransaction!);
            _committed = true;
        }

        /// <summary>
        /// Rollback all changes in this transaction.
        /// </summary>
        public void Rollback()
        {
            ThrowIfDisposed();

            if (_committed)
                throw new InvalidOperationException("Cannot rollback committed transaction");

            // Clear dirty flags without persisting
            foreach (var obj in TypeDriverHelper.EnumerateDirtyObjects())
            {
                TypeDriverHelper.ClearDirty(obj);
            }

            // Dispose Voron transaction (auto-rollback)
            DisposeTransaction(_nativeTransaction!);
            _nativeTransaction = null;
        }

        public void Dispose()
        {
            if (_disposed) return;

            VContextManager.PopTransaction();

            if (!_committed && _nativeTransaction != null)
            {
                // Auto-rollback on dispose without commit
                DisposeTransaction(_nativeTransaction);
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private static void CommitTransaction(object tx)
        {
            // Call Voron commit
            var commitMethod = tx.GetType().GetMethod("Commit");
            commitMethod?.Invoke(tx, null);
        }

        private static void DisposeTransaction(object tx)
        {
            if (tx is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
```

### 3. VContextManager

**File:** `System.Private.CoreLib/src/System/OS/VContextManager.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// Manages per-thread VContext for transaction scoping.
    /// </summary>
    internal static class VContextManager
    {
        [ThreadStatic]
        private static Stack<object>? t_transactionStack;

        internal static void PushTransaction(object tx)
        {
            t_transactionStack ??= new Stack<object>();
            t_transactionStack.Push(tx);
        }

        internal static void PopTransaction()
        {
            t_transactionStack?.Pop();
        }

        internal static object? CurrentTransaction
        {
            get
            {
                if (t_transactionStack == null || t_transactionStack.Count == 0)
                    return null;

                return t_transactionStack.Peek();
            }
        }

        internal static bool HasActiveTransaction => CurrentTransaction != null;
    }
}
```

### 4. VirtualOpsRoot Helper

**File:** `System.Private.CoreLib/src/System/OS/VirtualOpsRoot.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// Internal helper for virtual object operations.
    /// </summary>
    internal static class VirtualOpsRoot
    {
        public static T? Materialize<T>(VUID vuid) where T : class
        {
            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            object? obj = null;
            MaterializeInternal(bytes, typeof(T).TypeHandle.Value, ObjectHandleOnStack.Create(ref obj));

            return obj as T;
        }

        public static bool Persist(object obj, out VUID vuid)
        {
            vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            return PersistInternal(ObjectHandleOnStack.Create(ref obj), bytes);
        }

        public static bool PersistInTransaction(object obj, object tx)
        {
            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            // Use transaction-aware persist
            return PersistInTransactionInternal(ObjectHandleOnStack.Create(ref obj), bytes, tx);
        }

        public static bool Exists(VUID vuid)
        {
            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            return ExistsInternal(bytes);
        }

        public static bool Delete(VUID vuid)
        {
            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            return DeleteInternal(bytes);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_Materialize")]
        private static partial void MaterializeInternal(
            byte[] vuidBytes,
            IntPtr typeHandle,
            ObjectHandleOnStack result);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_Persist")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PersistInternal(
            ObjectHandleOnStack obj,
            byte[] vuidBytes);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_PersistInTransaction")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PersistInTransactionInternal(
            ObjectHandleOnStack obj,
            byte[] vuidBytes,
            [MarshalAs(UnmanagedType.IUnknown)] object tx);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_Exists")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ExistsInternal(byte[] vuidBytes);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_Delete")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DeleteInternal(byte[] vuidBytes);
    }
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `System/OS/VKernel.cs` | Create | Main API entry point |
| `System/OS/VTransaction.cs` | Create | Transaction scope |
| `System/OS/VContextManager.cs` | Create | Per-thread context |
| `System/OS/VirtualOpsRoot.cs` | Create | Internal operations |
| `vm/tds/tdsqcalls.cpp` | Modify | Add VKernel QCalls |

---

## Acceptance Criteria

- [ ] VKernel.New<T>() creates virtual object with VUID
- [ ] VKernel.Get<T>(vuid) loads from storage
- [ ] VKernel.Persist(obj) saves to storage
- [ ] VKernel.Flush/FlushAll persists dirty objects
- [ ] VKernel.Delete removes from storage
- [ ] VTransaction provides ACID semantics
- [ ] Thread-safe operations
- [ ] Auto-initialization on first use

---

## Testing

```csharp
[Fact]
public void VKernel_NewCreatesVirtualObject()
{
    var obj = VKernel.New<TestObject>();

    Assert.NotNull(obj);
    Assert.False(TypeDriverHelper.GetVUID(obj).IsEmpty);
    Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
}

[Fact]
public void VKernel_PersistAndGet()
{
    var obj = VKernel.New<TestObject>();
    obj.Value = 42;

    VKernel.Persist(obj);

    var vuid = TypeDriverHelper.GetVUID(obj);
    var loaded = VKernel.Get<TestObject>(vuid);

    Assert.NotNull(loaded);
    Assert.Equal(42, loaded.Value);
}

[Fact]
public void VKernel_Transaction()
{
    var obj1 = VKernel.New<TestObject>();
    var obj2 = VKernel.New<TestObject>();

    VKernel.WithTransaction(() =>
    {
        obj1.Value = 1;
        obj2.Value = 2;
    });

    // Both should be persisted
    Assert.False(TypeDriverHelper.IsDirty(obj1));
    Assert.False(TypeDriverHelper.IsDirty(obj2));
}

[Fact]
public void VKernel_Delete()
{
    var obj = VKernel.New<TestObject>();
    VKernel.Persist(obj);

    var vuid = TypeDriverHelper.GetVUID(obj);
    Assert.True(VKernel.Exists(vuid));

    VKernel.Delete(vuid);
    Assert.False(VKernel.Exists(vuid));
}
```

---

## Usage Examples

```csharp
// Simple usage
var customer = VKernel.New<Customer>();
customer.Name = "John";
customer.Email = "john@example.com";
VKernel.Persist(customer);

// Later - load by VUID
var vuid = TypeDriverHelper.GetVUID(customer);
var loaded = VKernel.Get<Customer>(vuid);

// Transaction usage
VKernel.WithTransaction(() =>
{
    var order = VKernel.New<Order>();
    order.CustomerId = customer.Id;
    order.Items.Add(new OrderItem { ProductId = 1, Quantity = 2 });

    customer.OrderCount++;

    // All changes committed together
});

// Batch flush
for (int i = 0; i < 1000; i++)
{
    var item = VKernel.New<Item>();
    item.Index = i;
    // Dirty tracking - not persisted yet
}

int flushed = VKernel.FlushAll();  // Single transaction
Console.WriteLine($"Flushed {flushed} items");
```

---

## References

- Phase 2 Main Doc: Section 12 (WP2.5 Loader API)
- T05: Storage_Voron Driver
- T07: FieldAccess_Persist Driver
- T08: Driver Registry
