# WIP-04: Virtual Execution Engine (VEE)

> **Document Type:** Work In Progress - Architecture Design
> **Version:** 0.1
> **Date:** 2025-12-15
> **Status:** WIP - Initial conceptualization based on Unwinder/Async2 research
> **Context:** Emerged from Louis's clarification that Async+ (Roslyn) is NOT the path, but Unwinder capabilities ARE

---

## 1. Executive Summary

The **Virtual Execution Engine (VEE)** is a new execution model being developed within the **VCR (Virtual Core Runtime)**. It represents a fundamental rearchitecting of how execution is managed, controlled, and scheduled in the DOTNExT platform.

**Key insight from Louis:**

> "The Unwinder showed what was possible and - partially - how. This led us to deciding we'd be developing a new execution model inside the VCR which will do more virtualization, management/scheduling etc and to some extent remodel an execution engine/model which is closer to what bare-metal operating systems have, the goal being to trade some speed for maximum possible flexibility, dynamism/plasticity/mutability, control, etc."

**NOT Async+ (Roslyn):** The Async+ experiment with Roslyn state machine persistence was interesting but is NOT part of the vision.

**YES Unwinder/Async2:** The Unwinder approach demonstrated capabilities that ARE foundational:
- Capture execution state at any safe point (not just await)
- True stack frame serialization (including byrefs)
- BEAM-like preemption possibilities

---

## 2. What the Unwinder Proved Was Possible

### 2.1 From the Async2 Experiment

The .NET runtime team's Unwinder prototype demonstrated:

| Capability | Proven | Implication |
|------------|--------|-------------|
| **Stack frame capture to heap** | ✅ | Execution state CAN be serializable |
| **Byref preservation** | ✅ | References to stack CAN survive suspension |
| **Frame reconstruction** | ✅ | Captured state CAN be resumed |
| **GC integration** | ✅ | Tasklets CAN be GC-managed |
| **Chain linking** | ✅ | Full call stacks CAN be captured |

### 2.2 Why Unwinder > JIT Runtime-Async for VEE

| Feature | JIT Runtime-Async | Unwinder |
|---------|-------------------|----------|
| Capture at await | ✅ | ✅ |
| Capture at ANY safe point | ❌ | ✅ |
| BEAM-like preemption | ❌ | ✅ |
| Handle byrefs/Span | ❌ | ✅ |
| True execution state capture | ❌ | ✅ |

**The JIT approach is architecturally limited** - it uses heap-allocated state machines.

**The Unwinder approach is architecturally aligned** - it captures real stack frames.

---

## 3. VEE Design Goals

### 3.1 Trade-offs Embraced

The VEE explicitly trades:

| Give Up | Gain |
|---------|------|
| Raw speed | Maximum flexibility |
| Minimal overhead | Full dynamism |
| Static optimization | Runtime plasticity |
| Tight native coupling | Complete mutability |
| - | Unprecedented control |

### 3.2 Core Capabilities

| Capability | Description |
|------------|-------------|
| **Universal Capture** | Capture execution state at any safe point, not just cooperative yields |
| **True Preemption** | BEAM-like preemptive scheduling without OS thread overhead |
| **Full State Serialization** | Stack frames, locals, byrefs - everything captured |
| **Process Image Support** | CRIU-like checkpoint/restore inside the managed runtime |
| **Execution Migration** | Move executing computation between nodes |
| **Fine-grained Scheduling** | Reduction counting, fair scheduling, priority control |
| **Inspection/Modification** | Runtime observation and mutation of executing code |

### 3.3 Relation to Bare-Metal OS

**Louis's guidance:**

> "...remodel an execution engine/model which is closer to what bare-metal operating systems have"

What this means:

| Bare-Metal OS | VEE Equivalent |
|---------------|----------------|
| Hardware interrupts | Safe point polling |
| Process context blocks | Tasklet chains |
| Preemptive scheduler | Reduction-based scheduler |
| Memory protection | Logical isolation via VCOM |
| System calls | VOS service calls |
| Kernel/userspace | VCR/VOS services |

---

## 4. VEE Architecture

### 4.1 Position in Stack

```
┌─────────────────────────────────────────────────────────────────┐
│  VOS Services (Userspace)                                        │
│  VNS, Persistence, Security, Distribution                        │
├─────────────────────────────────────────────────────────────────┤
│  NewOrleans (VOS Infrastructure)                                 │
│  Grains, clustering, messaging                                   │
├─────────────────────────────────────────────────────────────────┤
│  VCR (Virtual Core Runtime) ────── DOTNExT Kernel               │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  VEE (Virtual Execution Engine) ◄── NEW                   │  │
│  │  - Execution control                                      │  │
│  │  - Scheduling (reduction-based)                           │  │
│  │  - State capture (Unwinder-derived)                       │  │
│  │  - Preemption management                                  │  │
│  │  - Checkpoint coordination                                │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │  MMS (Memantic Memory System) - ORION                     │  │
│  │  - Object identity and relations                          │  │
│  │  - Semantic metadata                                      │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │  CLR Core (GC, JIT, Type System)                          │  │
│  │  - Memory management                                      │  │
│  │  - Compilation                                            │  │
│  │  - Type resolution                                        │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 VEE Components

| Component | Responsibility |
|-----------|----------------|
| **Pathway Manager** | Manages execution pathways (lightweight processes) |
| **Scheduler** | Reduction-based fair scheduling |
| **State Capturer** | Unwinder-derived stack frame capture |
| **Checkpoint Coordinator** | Coordinates checkpoint across pathways |
| **Preemption Controller** | Manages preemption at safe points |
| **Migration Handler** | Coordinates execution migration between nodes |

---

## 5. Unified Safe Points

### 5.1 The Convergence

Three runtime concerns share the same requirement:

| Concern | What It Needs | At Safe Points? |
|---------|---------------|-----------------|
| **GC** | Know reference locations, consistent state | ✅ GC info provides |
| **Preemption** | Clean suspension point, resumable | ✅ Same |
| **Checkpoint** | Serializable state, all refs known | ✅ Same |

**Key Insight:** GC safe points = Preemption points = Checkpoint points

### 5.2 Extended Safe Point Structure

```cpp
// VEE Extended Safe Point
struct VEESafePoint
{
    // === Existing GC Info ===
    GCInfoAtSafePoint gcInfo;      // Reference locations

    // === VEE: Reduction Counting (BEAM-like) ===
    uint32_t reductionCost;        // Reductions consumed
    bool isPreemptionCandidate;    // Can yield here?

    // === VEE: Checkpointing ===
    bool isCheckpointCandidate;    // Can capture here?
    uint16_t frameSerializeSize;   // Bytes to capture frame

    // === Shared ===
    uint32_t nativeOffset;
    UnwindInfo* unwindInfo;
};
```

### 5.3 Unified Flag Word

```cpp
enum SafePointFlags : uint32_t
{
    NONE              = 0,
    GC_SUSPEND        = 1 << 0,
    REDUCTION_YIELD   = 1 << 1,
    CHECKPOINT        = 1 << 2,
    // Room for more...
};
```

---

## 6. BEAM-Like Scheduling

### 6.1 Reduction Counting

From BEAM (Erlang VM):
- Each process has reduction counter (starts ~2000-4000)
- Function calls decrement counter
- When counter hits 0, scheduler preempts
- No OS thread context switch overhead

### 6.2 VEE Implementation

```cpp
// Per-pathway reduction state
struct PathwayReductionState
{
    int32_t reductionCounter;      // Current budget
    int32_t reductionBudget;       // Reset value (configurable)
};

// JIT emits at loop back-edges and calls:
// dec [pathway_state + reductionCounter]
// jz  ReductionExhaustedHelper
```

### 6.3 Generated Code Pattern

```asm
LoopTop:
    ; === VEE: Reduction check ===
    dec     qword ptr [r14 + REDUCTION_COUNTER]
    jz      ReductionYield

    ; === Existing GC poll ===
    cmp     dword ptr [g_GCSuspendFlag], 0
    jnz     GCPollHelper

SafePoint_0x10:
    ; Actual loop body
    call    DoWork
    jnz     LoopTop
```

---

## 7. State Capture (Unwinder-Derived)

### 7.1 Tasklet Structure

Each Tasklet captures a complete stack frame:

```
┌─────────────────────────────────────────────────────────────────┐
│  Tasklet Structure                                              │
├─────────────────────────────────────────────────────────────────┤
│  ├── Method Token (identifies the method)                       │
│  ├── IP Offset (exact instruction within method)                │
│  ├── Frame Size                                                 │
│  ├── Local Variables (complete values)                          │
│  │   ├── Including byrefs (pointers to stack locations)        │
│  │   └── Including temporaries                                  │
│  ├── Callee-Saved Registers                                     │
│  ├── GC Reference Map (which locals are references)             │
│  └── Next Tasklet (link to caller's frame)                      │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 Capture Flow

```
At Suspension:
1. Detect need to suspend (preemption, checkpoint, etc.)
2. Unwind stack from current frame
3. For each frame:
   - Create Tasklet structure
   - Capture: frame data, IP, locals, registers
   - Register with GC for reference tracking
4. Link Tasklets into chain
5. Store in pathway state
6. Return to scheduler

On Resume:
1. Retrieve Tasklet chain
2. Pop top Tasklet
3. Restore registers
4. Reconstruct frame
5. Jump to saved IP
```

---

## 8. Relation to Other VCR Components

### 8.1 VEE + MMS (Memantic Memory System)

| VEE Role | MMS Role |
|----------|----------|
| Execute code against objects | Track object identity and relations |
| Capture execution state | Manage object graph |
| Handle checkpoints | Persist object state |
| Coordinate migration | Handle distributed objects |

### 8.2 VEE + VSS (Virtual Security System)

| VEE Role | VSS Role |
|----------|----------|
| Execute at interception points | Evaluate security policy |
| Provide execution context | Check permissions |
| Enforce decisions (block, allow) | Make access decisions |

### 8.3 VEE + VTS (Virtual Type System)

| VEE Role | VTS Role |
|----------|----------|
| Execute typed operations | Provide type metadata |
| Handle type mutations at runtime | Track type versions |
| Support dynamic dispatch | Resolve across type systems |

---

## 9. Implementation Phases

### Phase 1: Reduction Counting Foundation
- [ ] Add reduction counter to thread/pathway state
- [ ] JIT emits decrement at back-edges
- [ ] Simple yield mechanism to scheduler
- [ ] No checkpoint yet

### Phase 2: Unified Safe Point Polling
- [ ] Unified flag word
- [ ] Combined hot path
- [ ] GC integration

### Phase 3: State Capture
- [ ] Tasklet structure design
- [ ] Frame capture mechanism
- [ ] GC registration for Tasklets

### Phase 4: Checkpoint Support
- [ ] Checkpoint trigger mechanism
- [ ] Frame serialization
- [ ] Pathway state serialization

### Phase 5: Migration Support
- [ ] Cross-node Tasklet transfer
- [ ] Reference remapping
- [ ] Resume on different node

---

## 10. Open Questions

### Design Questions
1. How does VEE interact with existing thread pool?
2. Pathway lifetime and GC interaction?
3. Exception handling across Tasklet boundaries?
4. Generic method support in Tasklets?

### Implementation Questions
5. What JIT modifications are needed?
6. How to handle native interop?
7. Performance impact of reduction checking?
8. Debug/profiler integration?

---

## 11. Key Differences from Async+

| Aspect | Async+ (Roslyn - NOT the path) | VEE (The path) |
|--------|--------------------------------|----------------|
| Level | Compiler transformation | Runtime/kernel level |
| Capture | At await points only | At any safe point |
| Mechanism | State machine in heap | Real stack frame capture |
| Byref | ❌ Cannot handle | ✅ Fully supported |
| Preemption | ❌ Cooperative only | ✅ True preemption |
| Migration | Limited | Full support |

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Unwinder-Async2-Analysis.md | Source of VEE capabilities |
| DOTNExT-Unified-SafePoints.md | Safe point convergence |
| DOTNExT-Scheduler-Design.md | Scheduler integration |
| Erlang-BEAM-Architecture-Reference.md | Reduction counting inspiration |
| DOTNExT-Process-Image-Persistence.md | Checkpoint/restore use case |

---

*This document captures the VEE (Virtual Execution Engine) concept based on Louis's clarification that Unwinder capabilities are the path forward, not Async+ (Roslyn). VEE represents a fundamental rearchitecting of execution control within the VCR.*

*Version 0.1 - 2025-12-15 - Initial conceptualization*
