# DOTNExT Sync Semantics

> **Document Type:** Language/Runtime Design Specification
> **Version:** 1.0
> **Date:** 2025-12-10
> **Status:** DESIGN - Exploring sync keyword semantics for DOTNExT execution model
> **Context:** Emerged from semantic inversion discussion - everything yields by default, sync is the exception

---

## 1. Executive Summary

In DOTNExT's universal execution model, **all code can yield at any safe point by default**. This is "async-like" behavior as the baseline. The `sync` keyword marks the **exception** - code that must not yield.

This is a **semantic inversion** from traditional .NET where sync is default and async is marked.

---

## 2. The Semantic Inversion

### Traditional .NET Model

```
Default behavior:    Synchronous (blocking)
Exception marker:    async/await
Mental model:        "Code blocks unless marked async"
```

### DOTNExT Universal Execution Model

```
Default behavior:    Yieldable at any safe point
Exception marker:    sync
Mental model:        "Code can yield unless marked sync"
```

### Why This Makes Sense

1. **Everything is "async" by nature** - Safe point capture enables yields anywhere
2. **Async keyword becomes meaningless** - Not distinguishing anything if everything yields
3. **The rare case is now non-yielding** - Lock-holding, timing-critical, atomic operations
4. **AI-first execution needs yields** - For checkpointing, preemption, speculation

---

## 3. The `sync` Keyword

### 3.1 Declaration-Site Usage

Mark a method as inherently non-yielding:

```csharp
sync void CriticalOperation()
{
    // Guaranteed: no yields, no preemption, no checkpoints inside
    // Runs to completion atomically from external perspective
}
```

**Semantics:**
- Method body never yields at safe points
- JIT does not insert yield checks in this method
- Any method can call this; it always runs sync
- Nested calls from here execute in sync scope

### 3.2 Call-Site Usage

Request synchronous execution of a call tree:

```csharp
var result = sync SomeMethod();
```

**Semantics:**
- `SomeMethod` and everything it calls runs without yielding
- Creates a "sync scope" that propagates through call tree
- Returns only when call tree completes
- Transitive - entire call tree is sync

### 3.3 Block Scope Usage

Create a sync scope for a block:

```csharp
void ProcessData()
{
    // Can yield here

    sync
    {
        // Cannot yield inside this block
        DoA();
        DoB();
        DoC();
    }

    // Can yield here
}
```

---

## 4. Sync Scope Behavior

### 4.1 Propagation

Sync scope propagates down through all calls:

```
Normal:                         With sync call:
───────────────────────────────────────────────────────────────
Process()                       sync Process()
├── LoadData()      [yield OK]  ├── LoadData()      [NO yield]
│   └── ReadFile()  [yield OK]  │   └── ReadFile()  [NO yield]
├── Transform()     [yield OK]  ├── Transform()     [NO yield]
│   ├── Parse()     [yield OK]  │   ├── Parse()     [NO yield]
│   └── Validate()  [yield OK]  │   └── Validate()  [NO yield]
└── SaveData()      [yield OK]  └── SaveData()      [NO yield]
```

### 4.2 Declaration vs Call-Site Interaction

```csharp
sync void AlwaysSync() { ... }   // Declaration forces sync
void MayYield() { ... }          // Normal - can yield

// Combinations:
AlwaysSync();        // Runs sync (declaration enforces)
sync AlwaysSync();   // Runs sync (redundant but harmless)
MayYield();          // Can yield (normal behavior)
sync MayYield();     // Runs sync (call-site enforces)
```

**Rule:** If either declaration OR call-site specifies sync, execution is sync.

### 4.3 Nesting

```csharp
void Outer()
{
    // Can yield

    sync
    {
        // Sync scope 1

        sync
        {
            // Still sync (nested sync is redundant)
        }

        // Still in sync scope 1
    }

    // Can yield again
}
```

Nested sync scopes don't "stack" - you're either in a sync scope or not.

---

## 5. What `sync` Prevents

| Behavior | Normal | In Sync Scope |
|----------|--------|---------------|
| Yield at safe points | Yes | **No** |
| Scheduler preemption | Yes | **No** |
| Checkpoint capture | Yes | **No** |
| Pathway migration | Yes | **No** |
| Speculative forking | Yes | **No** |

### 5.1 What `sync` Does NOT Prevent

| Behavior | Prevented? | Explanation |
|----------|------------|-------------|
| Actual I/O blocking | No | Thread blocks for I/O, but no scheduler yield |
| Lock contention | No | Thread waits for lock (traditional blocking) |
| GC pauses | No | GC still runs (though shorter, no yield extension) |

**Key distinction:**
- `sync` prevents **DOTNExT execution model yields**
- `sync` does NOT prevent **OS-level blocking**

```csharp
sync void Example()
{
    // Sync prevents DOTNExT yields, but:
    File.ReadAllText("big.txt");  // Thread blocks on I/O (OS-level)
    lock (obj) { }                 // Thread waits if lock held
    // These are traditional sync behaviors, not DOTNExT yields
}
```

---

## 6. Sync and Checkpointing Interaction

### 6.1 Checkpoint Boundaries

```csharp
void Process()
{
    DoA();              // Checkpoint possible after this

    sync
    {
        DoB();          // NO checkpoint here
        DoC();          // NO checkpoint here
    }                   // Checkpoint possible after sync scope

    DoD();              // Checkpoint possible after this
}
```

### 6.2 Process Image Implications

When checkpointing an entire process:
- Sync scopes are atomic units
- Cannot checkpoint a pathway mid-sync-scope
- Checkpoint waits for sync scopes to complete (per-pathway)
- Other pathways can checkpoint independently

### 6.3 Design Decision

**Per-pathway sync isolation:**
- Pathway A in sync scope: cannot checkpoint Pathway A
- Pathway B not in sync scope: can checkpoint Pathway B
- Process checkpoint waits for each pathway's sync scopes

---

## 7. Async/Await Compatibility

### 7.1 Existing Async Code

```csharp
async Task LegacyMethod()
{
    await SomeOperation();
}
```

**In DOTNExT:**
- `async` keyword is kept for compatibility
- Interpreted as documentation: "yields expected here"
- `await` is explicit yield point hint
- But yields happen at ANY safe point anyway
- The keywords are not required for DOTNExT behavior

### 7.2 Mixing Sync and Async

```csharp
async Task Mixed()
{
    await Step1();      // Yield hint (plus normal safe point yields)

    sync
    {
        await Step2();  // What happens here?
    }
}
```

**Design decision needed:** Should `await` inside `sync` scope be:
- Compile error (recommended)
- Treated as sync call (await ignored)
- Runtime exception

**Recommendation:** Compile error. `sync` means no yields; `await` requests yield. These are incompatible.

---

## 8. Implementation Considerations

### 8.1 JIT Integration

```cpp
// Normal method: JIT inserts yield checks at safe points
if (PathwayYieldRequested) { YieldPoint(); }

// sync method: JIT skips yield check insertion
// (GC checks still present, but not DOTNExT yields)
```

### 8.2 Runtime Tracking

```
Pathway State:
├── SyncDepth: int          // 0 = can yield, >0 = in sync scope
├── ...

On sync scope entry:  SyncDepth++
On sync scope exit:   SyncDepth--
On yield check:       if (SyncDepth > 0) skip yield
```

### 8.3 Call-Site Sync

```csharp
var result = sync SomeMethod();

// Compiles to approximately:
PathwayState.EnterSyncScope();
try
{
    var result = SomeMethod();
}
finally
{
    PathwayState.ExitSyncScope();
}
```

---

## 9. Edge Cases and Design Decisions

### 9.1 Sync Across Pathway Spawn

```csharp
sync
{
    var pathway = Pathway.Spawn(() => DoWork());
    // Does the spawned pathway inherit sync scope?
}
```

**Options:**
- A: Spawned pathway inherits sync (propagates to new pathway)
- B: Spawned pathway is independent (default yieldable)
- C: Spawn in sync scope is error

**Recommendation:** Option B - spawned pathways are independent. Sync is for the current call tree, not spawned concurrent work.

### 9.2 Timeout for Sync Call-Site

Some users might want bounded sync:

```csharp
var result = sync(timeout: 100ms) SomeMethod();
// "Try sync, but don't wait forever"
```

**Analysis:** This is complex. What happens on timeout?
- Exception? (Disrupts computation)
- Fall back to async? (Confusing - did it complete?)

**Recommendation:** Don't support sync with timeout. If you need bounded execution, use different mechanisms (cancellation tokens, etc.). `sync` should mean guaranteed sync.

### 9.3 Recursive Sync Methods

```csharp
sync void Recursive(int n)
{
    if (n > 0) Recursive(n - 1);
}
```

**Behavior:** Works fine. Entire recursion is sync. No special handling needed.

---

## 10. Use Cases for `sync`

### 10.1 Lock-Holding Code

```csharp
void SafeUpdate()
{
    lock (stateLock)
    {
        sync
        {
            // While holding lock, don't yield
            // Prevents deadlock from checkpoint during lock
            UpdateSharedState();
        }
    }
}
```

### 10.2 Hardware Timing

```csharp
sync void BitBang()
{
    // Precise timing required
    // No yields allowed
    SetPin(high);
    Delay(100);
    SetPin(low);
}
```

### 10.3 Interop with Sync-Expecting Code

```csharp
sync void NativeCallback()
{
    // Called from native code expecting sync completion
    // Must not yield back to scheduler
    ProcessAndReturn();
}
```

### 10.4 Performance-Critical Sections

```csharp
sync void TightLoop()
{
    // Hot path - skip yield check overhead
    for (int i = 0; i < 1000000; i++)
    {
        FastOperation();
    }
}
```

### 10.5 Atomic Operations

```csharp
var result = sync Transfer(from, to, amount);
// Entire transfer is atomic from caller's perspective
// No checkpoint mid-transfer
```

---

## 11. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-RnD-Primer.md | Overall context, semantic inversion overview |
| DOTNExT-Execution-Pathways.md | Pathway model that sync affects |
| DOTNExT-Process-Image-Persistence.md | Checkpoint interaction with sync |

---

## 12. Summary

| Aspect | Specification |
|--------|---------------|
| **Default** | All code can yield at any safe point |
| **`sync` declaration** | Method never yields internally |
| **`sync` call-site** | Execute call tree without yields |
| **`sync` block** | Code block without yields |
| **Propagation** | Transitive through call tree |
| **Checkpoint** | Cannot checkpoint mid-sync-scope |
| **async/await** | Compatibility; becomes hints not mechanisms |
| **`await` in sync** | Should be compile error |

**The key insight:** In a world where everything yields by default, `sync` is the escape hatch for code that must run atomically.

---

*This document specifies sync semantics for DOTNExT's universal execution model. The sync keyword marks the exception case where code must not yield.*

*Version 1.0 - 2025-12-10 - Initial specification*
