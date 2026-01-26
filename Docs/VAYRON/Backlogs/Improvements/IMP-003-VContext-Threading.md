# IMP-003: VContext Thread Propagation

> **Status:** Backlog
> **Origin:** Phase 1 Gap Closure - Null Context Placeholder Decision
> **Priority:** High (Required for Phase 2 transactions)
> **Target Phase:** Phase 2

---

## Summary

Phase 1 uses **`g_NullContext`** as a placeholder passed through all driver operations. This improvement implements **VContext thread propagation** to carry transaction handles, security context, and other per-call state.

---

## Current State (Phase 1)

```cpp
// Phase 1: Global null context
VContext g_NullContext = { 1, 0, { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr } };

// All driver calls receive null context
ops->fieldAccessOps->Read(&g_NullContext, obj, field, buffer, size);
```

**Limitations:**
- No transaction context
- No security principal
- Cannot track ambient state

---

## Proposed Improvement

### VContext Structure (Phase 2)

```cpp
struct VContext {
    uint32_t version;           // Structure version
    uint32_t flags;             // Context flags

    // Phase 2: Transaction support
    void* transaction;          // Voron transaction handle
    uint32_t txFlags;           // Read/write, nested level

    // Phase 3+: Security
    void* securityPrincipal;    // Capability context

    // Phase 4+: Call dispatch
    void* callContext;          // Remote call metadata

    void* reserved[4];          // Future expansion
};
```

### Option A: Thread-Local Storage (TLS)

```cpp
// Store in TLS
thread_local VContext* t_CurrentContext = nullptr;

VContext* GetCurrentVContext() {
    return t_CurrentContext ? t_CurrentContext : &g_NullContext;
}

class VContextScope {
    VContext* _previous;
public:
    VContextScope(VContext* ctx) {
        _previous = t_CurrentContext;
        t_CurrentContext = ctx;
    }
    ~VContextScope() {
        t_CurrentContext = _previous;
    }
};
```

**Pros:**
- Fast access (~5ns)
- Works for native code
- Supports nesting

**Cons:**
- Doesn't flow across async/await

### Option B: AsyncLocal (Managed)

```csharp
// Managed-side for async flow
public static class VContextFlow
{
    private static readonly AsyncLocal<VContext> _current = new();

    public static VContext Current => _current.Value ?? VContext.Null;

    public static IDisposable Push(VContext ctx) {
        var previous = _current.Value;
        _current.Value = ctx;
        return new Scope(() => _current.Value = previous);
    }
}
```

**Pros:**
- Flows across async/await
- Standard .NET pattern

**Cons:**
- Managed overhead
- Native code needs P/Invoke to access

### Option C: Hybrid (Recommended)

```
Native code: TLS for fast access
Managed code: AsyncLocal with native sync

On async boundary crossing:
- Managed captures VContext
- On continuation, updates native TLS
```

---

## Usage Pattern (Phase 2)

```csharp
// High-level API
using (var tx = VKernel.BeginTransaction())
{
    // VContext automatically populated with tx handle
    account.Balance += 100;  // Driver receives VContext with tx
    tx.Commit();
}

// Low-level flow
var ctx = new VContext { transaction = voronTx };
using (VContextFlow.Push(ctx))
{
    // All driver calls in this scope receive ctx
}
```

---

## Implementation Tasks

1. [ ] Extend VContext struct with transaction fields
2. [ ] Implement TLS storage in native runtime
3. [ ] Implement AsyncLocal wrapper in managed code
4. [ ] Add hybrid sync mechanism for async boundaries
5. [ ] Update all driver interfaces to use current context
6. [ ] Add tests for context propagation

---

## Integration with Phase 2 StorageDevice

```cpp
// StorageDevice uses VContext for transaction
bool VoronStorage_Persist(VContext* ctx, Object* obj, uint64_t* outVuid)
{
    Transaction* tx = (Transaction*)ctx->transaction;
    if (!tx) {
        // Auto-begin transaction or error
    }
    // Use tx for Voron operations
}
```

---

## References

- Phase 1 Doc: Part VI §6.1 (VContext = Null placeholder)
- Phase 2 Doc: §7 (Transactions)
- .NET AsyncLocal: `System.Threading.AsyncLocal<T>`
