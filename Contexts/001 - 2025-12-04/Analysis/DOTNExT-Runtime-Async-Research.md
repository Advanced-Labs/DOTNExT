# .NET Runtime-Async: Technical Research

> **Document Type:** Technical Research
> **Version:** 1.0
> **Date:** 2025-12-08
> **Status:** STRATEGIC - Foundation for DOTNExT async evolution
> **Session:** Research session with Louis exploring BEAM-like capabilities

---

## 1. Executive Summary

.NET 10 introduces **Runtime-Async** - a fundamental shift from compiler-generated async state machines to JIT-managed execution suspension. This research documents the feature, its implementation, and its strategic implications for DOTNExT/VAYRON.

**Key Finding:** Runtime-Async provides primitives that can enable BEAM-like preemption and process image persistence - capabilities previously thought to require fundamental CLR redesign.

---

## 2. What is Runtime-Async?

### 2.1 The Fundamental Shift

| Aspect | Compiler-Async (Current) | Runtime-Async (.NET 10) |
|--------|--------------------------|-------------------------|
| **Transformation** | Roslyn generates state machine | Roslyn emits linear IL + flag |
| **State storage** | Heap-allocated struct fields | Stack frames captured to Tasklets |
| **Resume mechanism** | `MoveNext()` switch statement | JIT-generated jump to exact IP |
| **What CLR sees** | Ordinary struct + methods | `MethodImplOptions.Async` flag |
| **State captured** | Compiler-selected locals | **Entire frame** - all locals, temps, registers |

### 2.2 The Key Insight

From the .NET team's design:

> "A normal code generated function at a function call point is effectively a state machine. The index for resumption is the IP that returning to the function will set, and the stackframe + saved registers are the current state."

**Translation:** The JIT already manages state machines via stack frames. Runtime-Async stops duplicating this work in the compiler.

---

## 3. Technical Implementation

### 3.1 How to Enable (.NET 10)

```xml
<!-- Project file -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <EnablePreviewFeatures>true</EnablePreviewFeatures>
  <Features>$(Features);runtime-async=on</Features>
</PropertyGroup>
```

```powershell
# Environment variable at runtime
$env:DOTNET_RuntimeAsync = "1"
```

### 3.2 New API Surface

```csharp
// New MethodImplOptions flag (value 0x2000)
[MethodImpl(MethodImplOptions.Async)]
async Task MyMethod() { }

// New helpers in System.Runtime.CompilerServices
public static class AsyncHelpers
{
    public static void AwaitAwaiterFromRuntimeAsync<TAwaiter>(TAwaiter awaiter)
        where TAwaiter : INotifyCompletion;

    public static void UnsafeAwaitAwaiterFromRuntimeAsync<TAwaiter>(TAwaiter awaiter)
        where TAwaiter : ICriticalNotifyCompletion;
}

// Experimental warning
// SYSLIB5007: "Runtime Async is experimental"
```

### 3.3 Tasklet Architecture

When an async method suspends at an `await`:

```
┌─────────────────────────────────────────────────────────────────┐
│  Stack during execution                                         │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐                                                │
│  │ Foo() frame │  ◄── await point here                         │
│  ├─────────────┤                                                │
│  │ Bar() frame │                                                │
│  ├─────────────┤                                                │
│  │ Thunk frame │  ◄── capture stops here                       │
│  └─────────────┘                                                │
│         │                                                       │
│         ▼  SUSPEND                                              │
│  ┌─────────────┐                                                │
│  │  Tasklet 1  │ ──► Foo's frame data, IP, registers           │
│  ├─────────────┤                                                │
│  │  Tasklet 2  │ ──► Bar's frame data, IP, registers           │
│  └─────────────┘                                                │
│     (on heap, GC-tracked)                                       │
│                                                                 │
│         ▼  RESUME                                               │
│  Stack reconstructed from Tasklets                              │
│  Execution continues at captured IP                             │
└─────────────────────────────────────────────────────────────────┘
```

### 3.4 Tasklet Contents

Each Tasklet captures:

| Data | Purpose |
|------|---------|
| **Frame data** | All locals, parameters, temporaries |
| **Instruction pointer** | Exact bytecode/native position to resume |
| **Callee-saved registers** | CPU state (platform-specific) |
| **Unwind information** | How to restore the frame |
| **GC info** | Where managed references are located |

### 3.5 Suspension Flow

```
1. Method reaches await point
2. AwaitAwaiterFromRuntimeAsync() called
3. If awaiter not complete:
   a. Walk stack from current point to thunk
   b. For each frame: create Tasklet with frame data + IP + registers
   c. Register Tasklets with GC (special reporting)
   d. Unwind actual stack
   e. Pass continuation delegate to awaiter
4. When awaiter completes:
   a. Dispatcher retrieves Tasklet chain
   b. Restore registers from Tasklet
   c. Reconstruct stack from Tasklet data
   d. Jump to captured IP
   e. Execution continues
```

### 3.6 JIT Restrictions

Methods using Runtime-Async cannot use:

| Restriction | Reason |
|-------------|--------|
| `localloc` | Stack allocation can't be captured to Tasklet |
| Pinning locals | Pinned pointers invalid after stack reconstruction |
| `tail.` prefix | Tail calls complicate frame capture |
| `ldloca`/`ldarga` returning unmanaged pointers | Would be invalid after resume |

---

## 4. What Runtime-Async Captures vs Compiler-Async

### 4.1 The Capture Difference

**Compiler-Async (Roslyn):**
```csharp
// State machine only captures what compiler decides to preserve
struct StateMachine
{
    int _state;           // Which await point
    int _localA;          // Explicitly hoisted
    // Temporary expressions? Maybe, maybe not
    // Compiler's discretion
}
```

**Runtime-Async:**
```
Tasklet captures EVERYTHING:
- All locals (even compiler temporaries)
- Exact instruction pointer
- Register values
- Complete frame layout
Nothing is lost - it's a perfect snapshot
```

### 4.2 Implications

| Compiler-Async | Runtime-Async |
|----------------|---------------|
| Resume at state N (switch case) | Resume at exact IP (any instruction) |
| Access declared locals | Access ANY local/temp |
| Compiler's interpretation | Reality of execution |
| Lossy abstraction | Perfect snapshot |

---

## 5. Roslyn's Role in Runtime-Async

**Roslyn still does codegen**, but different codegen:

### 5.1 Current Roslyn Output (Compiler-Async)

```csharp
// Input:
async Task<int> Foo()
{
    var x = await GetDataAsync();
    return x + 1;
}

// Roslyn generates:
struct Foo_StateMachine : IAsyncStateMachine
{
    public int _state;
    public AsyncTaskMethodBuilder<int> _builder;
    public int _x;

    public void MoveNext()
    {
        switch (_state)
        {
            case 0: /* first await */ break;
            case 1: /* after first await */ break;
        }
    }
}
```

### 5.2 Runtime-Async Roslyn Output

```csharp
// Roslyn generates (conceptual):
[MethodImpl(MethodImplOptions.Async)]
Task<int> Foo()
{
    // Linear IL, closer to source
    var x = AsyncHelpers.Await(GetDataAsync());
    return Task.FromResult(x + 1);
}

// JIT then:
// 1. Sees MethodImplOptions.Async
// 2. At Await() calls, generates suspend/resume code
// 3. Manages Tasklet creation internally
```

---

## 6. Tasklet: Managed or Native?

**Both.** Tasklet is a managed wrapper around native runtime structures:

```
┌─────────────────────────────────────────────────────────────────┐
│  Managed Layer (C#)                                             │
├─────────────────────────────────────────────────────────────────┤
│  Tasklet (managed class)                                        │
│  ├── Reference to frame data                                    │
│  ├── Continuation delegate                                      │
│  └── Links to other Tasklets (chain)                           │
├─────────────────────────────────────────────────────────────────┤
│  Native Layer (C++ in CoreCLR)                                  │
├─────────────────────────────────────────────────────────────────┤
│  Frame capture/restore primitives                               │
│  ├── Unwind info structures (same as exception handling)        │
│  ├── Register save areas                                        │
│  ├── GC reporting hooks (special, not standard frame reporting) │
│  └── Stack reconstruction code (optimized assembly)             │
└─────────────────────────────────────────────────────────────────┘
```

### 6.1 GC Integration

From the design doc:

> "Tasklet objects are registered with the GC so that they will be properly reported, but that reporting is not based on normal GC reporting"

Tasklets use **special GC reporting** because:
- They contain byref pointers into captured frames
- Register locations within Tasklets are GC roots
- Standard frame reporting doesn't apply to heap-stored frames

---

## 7. Performance Characteristics

### 7.1 Experiment Results

The .NET team's conclusion:

> "Runtime-async is at least as good as compiler-async in all configurations measured"

| Scenario | Runtime-Async Performance |
|----------|--------------------------|
| Non-suspended (sync completion) | **Comparable to synchronous code** |
| Deep call stacks | **Faster** (no heap state machine per frame) |
| Frequent suspension | Comparable or better |
| Memory | **Can be higher** (captures entire frames) |

### 7.2 Why It's Faster

1. **No state machine allocation** for sync-completing awaits
2. **Single capture** of entire call chain vs. nested state machines
3. **JIT optimization** of suspend/resume paths
4. **Native assembly** for frame capture/restore

---

## 8. Behavioral Differences

### 8.1 ExecutionContext Propagation

**Critical difference:**

| Compiler-Async | Runtime-Async |
|----------------|---------------|
| Changes to ExecutionContext/AsyncLocal **not visible** to callers | Changes **can be visible** (unless crossing thunk boundary) |
| State machine isolation | Frame-based context |

This is a semantic change that code might depend on.

---

## 9. Strategic Implications for DOTNExT

### 9.1 Async+ Over Runtime-Async

Runtime-Async provides better foundation for Async+ persistence:

| Challenge | Compiler-Async Approach | Runtime-Async Approach |
|-----------|------------------------|------------------------|
| State extraction | Parse state machine struct | Tasklet already has frame |
| Resume point | State integer → switch case | Exact IP in Tasklet |
| Reference capture | Compiler-hoisted fields | All locals/temps captured |
| Complexity | High (modify Roslyn) | Lower (hook Tasklet lifecycle) |

### 9.2 Process Image Persistence

Tasklets capture what we need for process checkpointing:
- Complete execution state
- GC-tracked (runtime knows all references)
- Serializable structure

**If we can serialize Tasklets + GC heap, we have process image persistence.**

### 9.3 BEAM-Like Preemption

Runtime-Async's suspension mechanism enables:
- Preemptive yielding at safe points
- Fair scheduling without OS threads
- BEAM-style reduction counting

**The infrastructure overlaps with what we need.**

---

## 10. Open Questions for DOTNExT

### 10.1 Tasklet Serialization

- Is Tasklet structure documented/stable?
- Can we serialize/deserialize Tasklets across process boundaries?
- What about cross-machine (different memory layout)?

### 10.2 Public API

Current API is minimal (`AsyncHelpers`). For Async+/VAYRON we may need:
- Tasklet creation hooks
- Tasklet serialization API
- Resume-from-serialized API

### 10.3 Fork Strategy

Options:
1. **Use stock Runtime-Async** + build persistence on top
2. **Fork Runtime-Async** in DOTNExT for deeper integration
3. **New parallel feature** inspired by Runtime-Async but designed for persistence

---

## 11. Sources

- [.NET Runtime-Async Feature - Issue #109632](https://github.com/dotnet/runtime/issues/109632)
- [.NET 9 Runtime Async Experiment - Issue #94620](https://github.com/dotnet/runtime/issues/94620)
- [API Proposal: Public API for Runtime Async - Issue #114310](https://github.com/dotnet/runtime/issues/114310)
- [async2 Experiment Concludes - Steven Giesel](https://steven-giesel.com/blogPost/59752c38-9c99-4641-9853-9cfa97bb2d29)
- [Runtime Handled Tasks Design Doc](https://github.com/dotnet/runtimelab/blob/feature/async2-experiment/docs/design/features/runtime-handled-tasks.md)

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Process-Image-Persistence.md | Uses Runtime-Async as foundation |
| DOTNExT-Unified-SafePoints.md | Safe point convergence with Runtime-Async |
| Vision-Async+-Solution.md | Async+ can build on Runtime-Async |
| Erlang-BEAM-Architecture-Reference.md | BEAM patterns that Runtime-Async enables |

---

*This document captures research into .NET Runtime-Async conducted 2025-12-08. This feature fundamentally changes what's possible for DOTNExT's async and persistence capabilities.*

*Version 1.0 - 2025-12-08*
