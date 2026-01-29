# T01: VContext Enhancement

> **Work Package:** WP2.0 (Infrastructure)
> **Dependencies:** Phase 1 complete
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Enhance the VContext struct (defined in Phase 1) to carry transaction handles and other Phase 2 runtime state through driver operations.

---

## Background

Phase 1 defined VContext as an empty placeholder:
```cpp
struct VContext {
    uint32_t version;
    uint32_t flags;
    void* reserved[6];
};
```

Phase 2 needs VContext to carry:
- Transaction handles (Voron write/read transaction)
- Future: security context, activation context

---

## Implementation

### 1. Update VContext Structure

**File:** `src/runtime/src/coreclr/vm/tds/tdsinterfaces.h`

```cpp
// Phase 2: VContext with transaction support
struct VContext {
    uint32_t version;        // = VCONTEXT_VERSION_2
    uint32_t flags;          // VCTX_FLAG_* values

    // Transaction state (Phase 2)
    void* transaction;       // Voron transaction handle (managed pointer)
    void* transactionScope;  // Optional: transaction scope marker

    // Future (reserved)
    void* securityCtx;       // Phase 3+: capability/principal
    void* activationCtx;     // Phase 4+: distributed activation

    void* reserved[2];       // Future expansion
};

// Version constants
#define VCONTEXT_VERSION_1  1  // Phase 1 (empty)
#define VCONTEXT_VERSION_2  2  // Phase 2 (transaction)

// Flags
#define VCTX_FLAG_READ_TX   0x0001  // Read-only transaction
#define VCTX_FLAG_WRITE_TX  0x0002  // Write transaction
#define VCTX_FLAG_DIRTY     0x0004  // Context has dirty objects
```

### 2. VContext Management Functions

**File:** `src/runtime/src/coreclr/vm/tds/tdscontext.h` (new)

```cpp
// VContext lifecycle management
VContext* TDS_CreateContext();
void TDS_DestroyContext(VContext* ctx);

// Transaction binding (called from managed code)
void TDS_BindTransaction(VContext* ctx, void* txHandle, bool isWrite);
void TDS_UnbindTransaction(VContext* ctx);

// Accessors
bool TDS_IsWriteTransaction(VContext* ctx);
void* TDS_GetTransaction(VContext* ctx);
```

### 3. Per-Thread Context

Consider a per-thread implicit VContext for convenience:

```cpp
// Thread-local current context
extern __thread VContext* t_CurrentVContext;

VContext* TDS_GetCurrentContext();
void TDS_SetCurrentContext(VContext* ctx);
```

---

## Managed API Updates

**File:** `System.Private.CoreLib/src/System/OS/VContext.cs` (new or update)

```csharp
namespace System.OS
{
    /// <summary>
    /// Execution context for virtual object operations.
    /// </summary>
    public sealed class VContext : IDisposable
    {
        internal IntPtr NativeHandle { get; }

        internal VContext(IntPtr handle) => NativeHandle = handle;

        public bool IsWriteTransaction => GetFlag(VContextFlags.WriteTx);
        public bool HasTransaction => NativeHandle != IntPtr.Zero &&
                                       (GetFlag(VContextFlags.ReadTx) || GetFlag(VContextFlags.WriteTx));

        public void Dispose() { /* Release native context */ }

        [Flags]
        internal enum VContextFlags
        {
            ReadTx = 0x0001,
            WriteTx = 0x0002,
            Dirty = 0x0004
        }
    }
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `vm/tds/tdsinterfaces.h` | Modify | Update VContext struct |
| `vm/tds/tdscontext.h` | Create | Context management declarations |
| `vm/tds/tdscontext.cpp` | Create | Context management implementation |
| `System/OS/VContext.cs` | Create | Managed wrapper |

---

## Acceptance Criteria

- [ ] VContext struct updated with transaction and flags fields
- [ ] Version bumped to VCONTEXT_VERSION_2
- [ ] TDS_CreateContext/DestroyContext implemented
- [ ] Transaction binding functions work
- [ ] Per-thread context accessor works
- [ ] Managed VContext class created
- [ ] Existing Phase 1 code still works (backward compatible)

---

## Testing

```cpp
// Native test
void Test_VContextTransactionBinding()
{
    VContext* ctx = TDS_CreateContext();
    assert(ctx->version == VCONTEXT_VERSION_2);
    assert(!TDS_IsWriteTransaction(ctx));

    void* fakeTx = (void*)0x12345678;
    TDS_BindTransaction(ctx, fakeTx, true);
    assert(TDS_IsWriteTransaction(ctx));
    assert(TDS_GetTransaction(ctx) == fakeTx);

    TDS_DestroyContext(ctx);
}
```

---

## References

- Phase 2 Main Doc: Section 7 (Transactions)
- Phase 1: tdsinterfaces.h (original VContext)
