# DOTNExT Unwinder Async2 Analysis

> **Document Type:** Technical Research Analysis
> **Version:** 1.0
> **Date:** 2025-12-09
> **Status:** RESEARCH - Analysis of experimental feature and potential for DOTNExT
> **Context:** Emerged from research into .NET runtime-async experiment

---

## 1. Executive Summary

The .NET team developed two prototypes for runtime-async (moving async state machine generation from Roslyn to the runtime): a **JIT-based** approach and a **VM/Unwinder-based** approach. While the JIT approach was chosen for productization in .NET 10, the **Unwinder approach has unique capabilities** that may be valuable for DOTNExT's goals around Execution Pathways, Process Image Persistence, and BEAM-like execution models.

This document analyzes:
- What the Unwinder prototype is and what it achieved
- What's missing for production use
- What would be needed to complete those features
- Why this matters for DOTNExT beyond just serialization

---

## 2. What is the Unwinder Async2 Prototype?

### 2.1 Location and Status

| Item | Details |
|------|---------|
| **Repository** | [dotnet/runtimelab](https://github.com/dotnet/runtimelab) |
| **Branch** | `feature/async2-experiment` |
| **Design Doc** | `docs/design/features/runtime-handled-tasks.md` |
| **Status** | Experiment complete, not productized |
| **How to Enable** | `DOTNET_RuntimeAsyncViaJitGeneratedStateMachines=0` |
| **Base Version** | .NET 9 development cycle |

### 2.2 How It Works

The Unwinder approach captures execution state by **unwinding the actual stack** at suspension points:

```
┌─────────────────────────────────────────────────────────────────┐
│  At Await Point (Suspension)                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Detect await on incomplete task                             │
│  2. Unwind stack from current frame to thunk function           │
│  3. For each frame encountered:                                 │
│     - Create Tasklet structure                                  │
│     - Capture: frame data, IP, locals, registers                │
│     - Register with GC for reference tracking                   │
│  4. Link Tasklets into chain                                    │
│  5. Store in TLS continuation                                   │
│  6. Return to dispatcher                                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  On Resumption                                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Dispatcher retrieves Tasklet chain                          │
│  2. Pop top Tasklet from chain                                  │
│  3. Restore registers from Tasklet                              │
│  4. Reconstruct frame                                           │
│  5. Jump to saved IP                                            │
│  6. Repeat for each Tasklet in chain                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.3 What a Tasklet Contains

Each Tasklet captures a complete stack frame:

```
┌─────────────────────────────────────────────────────────────────┐
│  Tasklet Structure                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ├── Method Token (identifies the method)                       │
│  ├── IP Offset (exact instruction within method)                │
│  ├── Frame Size                                                 │
│  ├── Local Variables (complete values)                          │
│  │   ├── Including byrefs (pointers to stack locations)        │
│  │   └── Including temporaries                                  │
│  ├── Callee-Saved Registers                                     │
│  ├── GC Reference Map (which locals are references)             │
│  └── Next Tasklet (link to caller's frame)                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.4 Key Capability: Byref Support

The Unwinder prototype **can capture byrefs (managed pointers to stack locations)** across suspension:

```csharp
// This COULD work with Unwinder (not with JIT prototype or Roslyn-async):
async Task ProcessAsync(ref int counter)
{
    counter++;
    await SomeOperationAsync();  // Byref survives suspension!
    counter++;  // Still valid reference to original location
}
```

This is impossible with the JIT prototype or current Roslyn-async because they store state in heap-allocated structs, and byrefs cannot be struct fields.

---

## 3. Current Limitations

### 3.1 Feature Matrix

| Feature | JIT Prototype | Unwinder Prototype |
|---------|---------------|-------------------|
| Basic async/await | ✅ Yes | ✅ Yes |
| Generics | ✅ Yes | ❌ No |
| Exception handling | ✅ Yes | ❌ No |
| Byref across suspension | ❌ No | ✅ Yes |
| Return buffer handling | ✅ Yes | ❌ No |
| Ref structs in async | ❌ No | ✅ Yes (via byref) |
| Production ready | ❌ No (in progress for .NET 10) | ❌ No |

### 3.2 What "Lacks Generics" Means

Generic methods and types do not work:

```csharp
// DOES NOT WORK with Unwinder prototype:
async Task<T> ProcessAsync<T>(T item)
{
    await Task.Delay(100);
    return item;
}

// DOES NOT WORK:
async Task<List<string>> GetItemsAsync() { ... }
```

This is severe because generics are pervasive in modern C# code.

### 3.3 What "Lacks Exception Handling" Means

Try/catch across suspension points does not work correctly:

```csharp
// MAY NOT WORK CORRECTLY with Unwinder prototype:
async Task FooAsync()
{
    try
    {
        await SomethingThatMightThrowAsync();
    }
    catch (Exception ex)  // May not catch properly
    {
        // Handle error
    }
}
```

Additionally, there's a restriction on both prototypes:
> "No call to a method with an async2 modreq will be permitted in a finally, fault, filter, or catch clause."

---

## 4. What Would Be Needed to Complete These Features

### 4.1 Adding Generics Support

**The Core Challenge:**

The CLR uses "shared generics" where a single piece of JIT'd code serves all reference-type instantiations. To make this work, each call site needs access to a **generic dictionary** that provides actual types at runtime.

```
┌─────────────────────────────────────────────────────────────────┐
│  Generic Method Execution                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Method<T> where T is reference type                            │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Shared JITted Code (uses System.__Canon as T placeholder) │ │
│  │                                                            │ │
│  │ When needs actual T:                                       │ │
│  │   → Look up in Generic Dictionary                          │ │
│  │   → Dictionary pointer passed via hidden parameter         │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**What Needs to Be Implemented:**

| Component | Description | Complexity |
|-----------|-------------|------------|
| **Dictionary Capture** | Save generic dictionary pointers in Tasklet | Medium |
| **Context Propagation** | Ensure dictionary is restored on resume | Medium |
| **Type Handle Resolution** | Handle `System.__Canon` → actual type mapping | High |
| **Value Type Instantiations** | Each value type gets separate code, needs separate handling | High |
| **Nested Generics** | `Task<List<Dictionary<K,V>>>` chains | High |
| **Hidden Parameter Preservation** | Generic methods receive type info via hidden parameter | Medium |

**Why It's Hard:**

Generic methods receive type information via a hidden parameter. The Tasklet structure must:
1. Recognize this hidden parameter exists
2. Capture it alongside other locals
3. Restore it correctly on resume
4. Handle the `System.__Canon` canonical type used for shared code

### 4.2 Adding Exception Handling

**The Core Challenge:**

.NET uses two-phase exception handling:
- **Phase 1 (Search):** Walk stack looking for handler
- **Phase 2 (Unwind):** Actually unwind to handler, running finally blocks

With Tasklets, the "stack" is partially real stack frames and partially heap-allocated Tasklet objects. The EH walker must understand both.

```
Normal Stack:              With Tasklets:
┌─────────────┐           ┌─────────────┐
│  Frame 3    │           │  Frame 3    │  ← Real stack
├─────────────┤           ├─────────────┤
│  Frame 2    │           │ [Tasklet 2] │  ← Heap object!
├─────────────┤           ├─────────────┤
│  Frame 1    │           │ [Tasklet 1] │  ← Heap object!
├─────────────┤           ├─────────────┤
│  Frame 0    │           │  Frame 0    │  ← Real stack
└─────────────┘           └─────────────┘
```

**What Needs to Be Implemented:**

| Component | Description | Complexity |
|-----------|-------------|------------|
| **Hybrid Stack Walker** | EH walker that understands both real frames and Tasklet chains | High |
| **Tasklet EH Metadata** | Store handler information (try/catch/finally regions) in Tasklet | Medium |
| **Finally Execution** | Run finally blocks across Tasklet boundaries | High |
| **Filter Support** | Exception filters need access to both stack types | Very High |
| **Nested EH Regions** | try/catch inside try/finally across suspension points | Very High |
| **Cross-Tasklet Rethrow** | Exceptions thrown in one Tasklet, caught in another | High |

**Current Workaround:**

The design doc mentions: "EH is currently handled by rethrowing exceptions at the suspension point." This is functional but adds overhead and may not handle all cases correctly.

---

## 5. Why This Matters for DOTNExT

### 5.1 Beyond Serialization

The interest in Unwinder/Tasklets serves **multiple strategic goals** for DOTNExT:

| Goal | How Tasklets Help | Document Reference |
|------|-------------------|-------------------|
| **Engram Extraction** | Serialize execution state + reachable objects | `DOTNExT-Engrams-Revised.md` |
| **Process Image Persistence** | CRIU-like checkpoint/restore inside VM | `DOTNExT-Process-Image-Persistence.md` |
| **Execution Pathways** | Tasklet chains as trackable, migratable units | `DOTNExT-Execution-Pathways.md` |
| **Preemptive Scheduling** | Suspend at safe points, not just await | `DOTNExT-Unified-SafePoints.md` |
| **BEAM-like Processes** | Lightweight execution contexts | `Erlang-BEAM-Architecture-Reference.md` |
| **Async+ Enhancement** | Tasklet-based persistence vs Roslyn codegen | `DOTNExT-Runtime-RnD-Primer.md` |

### 5.2 The Unified Safe Points Vision

From DOTNExT research, three different runtime concerns share the same requirement:

| Concern | What It Needs |
|---------|---------------|
| **Garbage Collection** | Know reference locations, consistent state, pause/resume |
| **Preemptive Scheduling** | Clean suspension point, resumable context |
| **Checkpointing** | Serializable state, reference locations, resume capability |

**The Insight:** GC safe points = Preemption points = Checkpoint points

The Unwinder's stack frame capture mechanism could serve all three purposes.

### 5.3 BEAM-Like Execution Model

DOTNExT research envisions "Execution Pathways" - BEAM-like lightweight processes built on Tasklets:

```
┌─────────────────────────────────────────────────────────────────┐
│  Execution Pathway = Tasklet Chain + Identity + Scheduling      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  IDENTITY                                                       │
│  ├── Pathway UUID                                               │
│  ├── Origin (where started)                                     │
│  └── Current location (where executing)                         │
│                                                                 │
│  EXECUTION STATE                                                │
│  ├── Tasklet Chain (captured frames)                            │
│  ├── Status: Running | Suspended | Persisted                    │
│  └── Scheduling info (priority, reduction budget)               │
│                                                                 │
│  CAPABILITIES                                                   │
│  ├── Track: Know what pathways are executing                    │
│  ├── Monitor: Observe progress, resources                       │
│  ├── Suspend: Pause at safe points                              │
│  ├── Migrate: Move between nodes                                │
│  └── Persist: Save and resume later                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

For this vision, the Unwinder's byref support enables:
- **True preemption** at any safe point (not just await)
- **Complete state capture** including stack-pointing references
- **Transparent migration** without requiring cooperative code

---

## 6. Why the Unwinder is More Interesting for DOTNExT

### 6.1 The Core Difference

| Approach | What It Captures | When It Can Capture |
|----------|------------------|---------------------|
| **JIT Runtime-Async** | State machine (like Roslyn, but JIT-generated) | Only at `await` points |
| **Unwinder** | Actual stack frames with everything | At **any safe point** |

### 6.2 The JIT Approach is Designed for Async Replacement

The JIT runtime-async is optimized for replacing Roslyn's async codegen. It captures state only at await points because that's all async needs. It's a better async - but still just async.

### 6.3 The Unwinder Captures Real Execution State

The Unwinder can pause and capture a running method **mid-execution** - between any two instructions at a safe point:

```
JIT Approach:                    Unwinder Approach:

void Foo() {                     void Foo() {
    DoA();                           DoA();        ← Can capture here
    DoB();                           DoB();        ← Can capture here
    await X();  ← Capture here       await X();    ← Can capture here
    DoC();                           DoC();        ← Can capture here
}                                }
```

### 6.4 The BEAM Connection

BEAM's preemptive scheduling works by:
1. Counting "reductions" (operations)
2. At safe points, checking if budget exhausted
3. **Suspending mid-execution** if needed
4. Resuming later from exactly that point

**This requires capturing state at arbitrary safe points, not just await.**

The Unwinder does exactly this - it can capture a stack frame at any point the JIT marked as safe (same points GC uses). This is the foundation for BEAM-like preemptive scheduling in DOTNExT.

### 6.5 Byref Support - Why It's Critical for DOTNExT

For true preemption, you might suspend code like:

```csharp
void ProcessBuffer(ref Span<byte> buffer) {
    for (int i = 0; i < buffer.Length; i++) {
        // ← Preemption could happen HERE
        Process(buffer[i]);
    }
}
```

The JIT approach **cannot** handle this - `ref Span<byte>` contains a byref that can't be stored in a heap object.

The Unwinder **can** - it captures the actual stack frame where the byref lives.

### 6.6 Summary: Why Unwinder > JIT for DOTNExT Goals

| DOTNExT Goal | JIT Runtime-Async | Unwinder |
|--------------|-------------------|----------|
| Checkpoint at await | ✅ Yes | ✅ Yes |
| Checkpoint anywhere (safe point) | ❌ No | ✅ Yes |
| BEAM-like preemption | ❌ No | ✅ Yes |
| Process image persistence | ❌ Partial | ✅ Full |
| Handle byrefs/Span | ❌ No | ✅ Yes |
| Execution Pathways | ❌ Limited | ✅ Full |

### 6.7 Engineering Gaps vs Architectural Limitations

**The missing generics/EH in the Unwinder prototype are engineering gaps** - they could be implemented with sufficient effort. The approach fundamentally supports them; they just weren't completed.

**The JIT approach's limitations are architectural** - it fundamentally can't capture at arbitrary safe points or handle byrefs because it uses heap-allocated state machines. No amount of engineering can change this.

**For DOTNExT's goals, the Unwinder approach is the right foundation.**

---

## 7. What the Unwinder Proved is Possible

Regardless of production readiness, the Unwinder prototype demonstrates that:

| Capability | Status | Implication |
|------------|--------|-------------|
| **Stack frame capture to heap** | ✅ Works | Execution state CAN be serializable |
| **Byref preservation** | ✅ Works | References to stack CAN survive suspension |
| **Frame reconstruction** | ✅ Works | Captured state CAN be resumed |
| **GC integration** | ✅ Works | Tasklets CAN be GC-managed |
| **Chain linking** | ✅ Works | Full call stacks CAN be captured |

**This proves the approach is viable.** The missing pieces (generics, EH) are engineering challenges, not fundamental impossibilities.

---

## 8. Options for DOTNExT

### 8.1 Option A: Complete the Unwinder for Full Async Replacement

**Goal:** Make Unwinder production-ready with generics and EH support.

**Effort:** Significant - requires deep CLR expertise and substantial development.

**Benefit:** Full byref support in async, true BEAM-like suspension at any point.

### 8.2 Option B: Use Unwinder Mechanisms for Checkpoint Only

**Goal:** Extract the stack capture mechanism for Process Image/Engram use without replacing async/await.

**Effort:** Moderate - reuse existing capture code for different purpose.

**Benefit:** Gets checkpoint/persistence without async compatibility burden.

**Trade-off:** Capture only at explicitly cooperative points, not mid-execution.

### 8.3 Option C: Wait for .NET Evolution

**Goal:** Let Microsoft complete JIT runtime-async, build on that.

**Effort:** Minimal - track upstream development.

**Benefit:** Production-quality foundation (but without byref support).

**Trade-off:** May never get byref support if JIT approach is final.

### 8.4 Option D: Hybrid Approach

**Goal:** Use JIT runtime-async for async/await, use Unwinder-derived mechanisms for checkpointing separately.

**Effort:** Moderate - two parallel systems.

**Benefit:** Best of both worlds - production async + full checkpoint capability.

**Trade-off:** Complexity of maintaining two systems.

---

## 9. Key Technical Decisions Needed

| Decision | Options | Trade-offs |
|----------|---------|------------|
| **Complete Unwinder?** | Full implementation vs selective reuse | Effort vs capability |
| **Generics approach** | Implement fully vs restrict usage | Compatibility vs simplicity |
| **EH approach** | Full hybrid walker vs restriction on try/catch across suspension | Completeness vs complexity |
| **Integration point** | Replace async vs parallel checkpoint system | Simplicity vs flexibility |
| **Base version** | Fork from .NET 9 experiment vs wait for .NET 10 | Timing vs stability |

---

## 10. Related Documents

| Document | Relevance |
|----------|-----------|
| `DOTNExT-Runtime-Async-Research.md` | Detailed Tasklet mechanism analysis |
| `DOTNExT-Process-Image-Persistence.md` | How Tasklets enable CRIU-like checkpoint |
| `DOTNExT-Execution-Pathways.md` | BEAM-like execution model built on Tasklets |
| `DOTNExT-Unified-SafePoints.md` | GC/Preemption/Checkpoint unification |
| `DOTNExT-Engrams-Revised.md` | Engrams include Tasklet chains as execution layer |
| `Erlang-BEAM-Architecture-Reference.md` | BEAM patterns we're drawing from |
| `DOTNExT-Runtime-RnD-Primer.md` | Overall runtime R&D context |

---

## 11. Sources

- [dotnet/runtimelab - async2-experiment branch](https://github.com/dotnet/runtimelab/tree/feature/async2-experiment)
- [runtime-handled-tasks.md design doc](https://github.com/dotnet/runtimelab/blob/feature/async2-experiment/docs/design/features/runtime-handled-tasks.md)
- [.NET 9 Runtime Async Experiment · Issue #94620](https://github.com/dotnet/runtime/issues/94620)
- [.NET Runtime-Async Feature · Issue #109632](https://github.com/dotnet/runtime/issues/109632)
- [Shared Generics (BOTR)](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/shared-generics.md)
- [async2 experiment concludes (blog)](https://steven-giesel.com/blogPost/59752c38-9c99-4641-9853-9cfa97bb2d29)

---

*This document analyzes the .NET Unwinder async2 prototype, its capabilities, limitations, and potential value for DOTNExT's broader goals around Execution Pathways, Process Image Persistence, and BEAM-like execution models.*
