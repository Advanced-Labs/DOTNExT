# DOTNExT Unified Safe Points

> **Document Type:** Technical Design
> **Version:** 1.0
> **Date:** 2025-12-08
> **Status:** DESIGN - Convergence of GC, Preemption, and Checkpointing
> **Session:** Research session identifying synergy between runtime concerns

---

## 1. Executive Summary

This document describes the convergence of three runtime concerns at **safe points**:

1. **Garbage Collection** - Needs consistent reference state
2. **Preemptive Scheduling** - Needs clean suspension points (BEAM-like)
3. **Process Checkpointing** - Needs serializable state

**Key Insight:** All three require the same fundamental property: a point where execution state is consistent, complete, and capturable. The JIT already computes this for GC. We're proposing to **reuse this infrastructure** for preemption and checkpointing.

---

## 2. The Convergence

### 2.1 What Each Concern Needs

| Concern | Requirement | At Safe Points? |
|---------|-------------|-----------------|
| **GC** | Know where all managed refs are | ✓ GC info provides this |
| **GC** | No partial/torn state | ✓ Between operations |
| **Preemption** | Clean suspension point | ✓ Same as GC |
| **Preemption** | Resumable context | ✓ Frame is complete |
| **Checkpoint** | Serializable state | ✓ GC info + frame layout |
| **Checkpoint** | All references known | ✓ GC info provides this |

### 2.2 The Beautiful Convergence

```
                    GC Safe Points
                         │
                         ▼
    ┌────────────────────────────────────────────────┐
    │  • Method call sites                            │
    │  • Loop back-edges                              │
    │  • Return points                                │
    │  • Yield points (iterators/async)              │
    └────────────────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
    ┌──────────┐  ┌──────────────┐  ┌──────────────┐
    │   GC     │  │  Reduction   │  │  Checkpoint  │
    │  Pause   │  │  (BEAM-like) │  │  Capture     │
    └──────────┘  └──────────────┘  └──────────────┘

    Same points serve all three purposes!
```

---

## 3. Current JIT Safe Point Implementation

### 3.1 Where JIT Inserts Safe Points

| Location | Why | Always? |
|----------|-----|---------|
| **Method call sites** | Callee-saved regs known, stack frame complete | Yes |
| **Loop back-edges** | Prevents infinite loops blocking GC | Yes |
| **Method returns** | Frame teardown is clean | Yes |
| **Long straight-line code** | If no calls for a while | Configurable |

### 3.2 What JIT Records at Each Safe Point

```cpp
// GC Info at each safe point
struct GCInfoAtSafePoint
{
    // Which registers hold managed references
    RegisterMask liveGCRegs;       // e.g., RBX, RDI have refs

    // Stack slots with managed references
    StackSlotMap gcStackSlots;     // e.g., [rbp-8], [rbp-16]

    // Interior pointers (pointing into arrays/strings)
    InteriorPointerInfo interiorPtrs;

    // Pinned references
    PinnedRefInfo pinnedRefs;
};
```

### 3.3 JIT Output Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  JIT Output for a Method                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Native Code                                                    │
│  ├── Instruction stream                                         │
│  └── Safe points embedded (call sites, loop back-edges)        │
│                                                                 │
│  GC Info Blob                                                   │
│  ├── Safe point table                                           │
│  │   ├── Offset 0x10: { regs: RBX, RDI; stack: [rbp-8] }       │
│  │   ├── Offset 0x25: { regs: RBX; stack: [rbp-8, rbp-16] }    │
│  │   └── ...                                                    │
│  └── Fully interruptible ranges (if enabled)                   │
│                                                                 │
│  Unwind Info                                                    │
│  ├── Frame layout                                               │
│  ├── Register save locations                                    │
│  └── Stack adjustments                                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Extending for DOTNExT

### 4.1 Extended Safe Point Structure

```cpp
// DOTNExT Extended Safe Point
struct ExtendedSafePoint
{
    // === Existing GC Info ===
    GCInfoAtSafePoint gcInfo;

    // === NEW: Reduction Counting (BEAM-like) ===
    uint32_t reductionCost;       // Reductions consumed to reach here
    bool isPreemptionCandidate;   // Can yield here?

    // === NEW: Checkpointing ===
    bool isCheckpointCandidate;   // Can capture state here?
    uint16_t frameSerializeSize;  // Bytes needed to capture frame

    // === Shared (already have) ===
    uint32_t nativeOffset;        // Position in native code
    UnwindInfo* unwindInfo;       // Frame layout for restoration
};
```

### 4.2 Extended JIT Output

```
Existing JIT Output:
┌─────────────────────────────────────┐
│  Native code                        │
│  GC info (safe points, ref locs)    │
│  Unwind info                        │
└─────────────────────────────────────┘

DOTNExT Extended Output:
┌─────────────────────────────────────┐
│  Native code                        │
│  + Reduction checks at back-edges   │  ◄── Small code addition
│  GC info (safe points, ref locs)    │
│  + Checkpoint capability flags      │  ◄── Metadata addition
│  + Frame serialization hints        │  ◄── Metadata addition
│  Unwind info                        │
└─────────────────────────────────────┘
```

---

## 5. BEAM-Like Reduction Counting

### 5.1 How BEAM Does It

```
BEAM Process:
  - Has reduction counter (starts at ~2000-4000)
  - Each function call decrements counter
  - When counter hits 0, scheduler preempts
  - Next process runs
  - No OS thread context switch
```

### 5.2 DOTNExT Implementation

```cpp
// Per-thread reduction state
struct ThreadReductionState
{
    int32_t reductionCounter;      // Current budget
    int32_t reductionBudget;       // Reset value (configurable)
};

// JIT emits at loop back-edges and calls:
void EmitReductionCheck()
{
    // dec qword ptr [r14 + offsetof(reductionCounter)]
    // jz  ReductionExhaustedHelper
}
```

### 5.3 Generated Code Example

```asm
; Original loop:
;   while (condition) { DoWork(); }

; With reduction checking:
LoopTop:
    ; === Reduction check ===
    dec     qword ptr [r14 + REDUCTION_COUNTER]  ; r14 = thread state
    jz      ReductionYield                        ; Yield if exhausted

    ; === Existing GC poll ===
    cmp     dword ptr [g_GCSuspendFlag], 0
    jnz     GCPollHelper

SafePoint_0x10:                                   ; GC info emitted here
    ; Actual loop body
    call    DoWork

    test    eax, eax
    jnz     LoopTop
```

---

## 6. Checkpoint Integration

### 6.1 How It Works

At any safe point, we can:
1. **Pause execution** (same mechanism as GC)
2. **Enumerate live references** (GC info tells us)
3. **Capture frame data** (unwind info tells us layout)
4. **Serialize to Tasklet or ProcessImage**

### 6.2 Trigger Mechanisms

| Trigger | How | Use Case |
|---------|-----|----------|
| **Explicit** | `ProcessImage.Checkpoint()` call | Application-controlled |
| **Policy** | Time-based, operation-count | Automatic |
| **External** | Flag set by management thread | Orchestrated migration |
| **Preemption** | Combined with reduction exhaustion | Fair + checkpointable |

### 6.3 Checkpoint at Safe Point

```cpp
void HandleCheckpointRequest(Thread* thread, SafePointInfo* safePoint)
{
    // 1. We're at a safe point - state is consistent

    // 2. Use GC info to find all references
    EnumerateGCRoots(thread, safePoint->gcInfo, [](ObjectRef ref) {
        AddToObjectGraph(ref);
    });

    // 3. Use unwind info to capture frame
    CaptureFrame(thread, safePoint->unwindInfo);

    // 4. If Runtime-Async, use Tasklet mechanism
    if (IsRuntimeAsyncMethod(safePoint->method))
    {
        auto tasklet = CreateTasklet(thread, safePoint);
        SerializeTasklet(tasklet);
    }

    // 5. Continue or suspend based on policy
}
```

---

## 7. Unified Safe Point Handler

### 7.1 Option A: Separate Checks (Current-ish)

```cpp
void SafePointPoll()
{
    // Three separate checks
    if (g_GCSuspendPending)
        GCYield();

    if (--tls_reductions <= 0)
        PreemptiveYield();

    if (g_CheckpointRequested)
        CaptureCheckpoint();
}
```

**Pros:** Simple, clear separation
**Cons:** Multiple memory reads, multiple branches

### 7.2 Option B: Unified Flag Word

```cpp
void SafePointPoll()
{
    // Single read, multiple bits
    uint32_t flags = tls_safepoint_flags;
    if (flags != 0)  // Anything pending?
    {
        if (flags & GC_SUSPEND)
            GCYield();
        if (flags & REDUCTION_EXHAUSTED)
            PreemptiveYield();
        if (flags & CHECKPOINT_REQUESTED)
            CaptureCheckpoint();
    }
}
```

**Pros:** Single memory read on hot path
**Cons:** Slightly more complex flag management

### 7.3 Option C: Counter-Based (Like BEAM)

```cpp
void SafePointPoll()
{
    // Single counter serves multiple purposes
    if (--tls_combined_counter <= 0)
    {
        // Counter exhausted - figure out why
        HandleSafePointTrigger();
    }
}

void HandleSafePointTrigger()
{
    // Check what caused the trigger
    if (g_GCSuspendPending) { GCYield(); return; }
    if (g_CheckpointRequested) { CaptureCheckpoint(); return; }

    // Default: reduction exhausted, yield to scheduler
    tls_combined_counter = REDUCTION_BUDGET;
    YieldToScheduler();
}
```

**Pros:** Minimal hot path (just decrement + branch)
**Cons:** Conflates different concerns

### 7.4 Recommended: Option B (Unified Flag Word)

Best balance of performance and clarity:

```cpp
// Thread-local safe point state
struct SafePointState
{
    uint32_t flags;           // Bit flags for pending actions
    int32_t reductionCounter; // BEAM-like budget
};

// Flag definitions
enum SafePointFlags : uint32_t
{
    NONE              = 0,
    GC_SUSPEND        = 1 << 0,
    REDUCTION_YIELD   = 1 << 1,
    CHECKPOINT        = 1 << 2,
    // Room for more...
};

// Hot path (JIT-emitted)
void SafePointPollFast()
{
    auto* state = GetThreadSafePointState();

    // Check reduction counter
    if (--state->reductionCounter <= 0)
        state->flags |= REDUCTION_YIELD;

    // Check all flags
    if (state->flags != 0)
        SafePointHandleSlow(state);
}

// Cold path (called rarely)
void SafePointHandleSlow(SafePointState* state)
{
    if (state->flags & GC_SUSPEND)
    {
        state->flags &= ~GC_SUSPEND;
        SuspendForGC();
    }

    if (state->flags & CHECKPOINT)
    {
        state->flags &= ~CHECKPOINT;
        CaptureCheckpoint();
    }

    if (state->flags & REDUCTION_YIELD)
    {
        state->flags &= ~REDUCTION_YIELD;
        state->reductionCounter = REDUCTION_BUDGET;
        YieldToScheduler();
    }
}
```

---

## 8. JIT Modifications Required

### 8.1 Changes to flowgraph.cpp

```cpp
// src/coreclr/jit/flowgraph.cpp

void Compiler::fgInsertSafePoints()
{
    for (BasicBlock* block : Blocks)
    {
        // === Existing: GC safe points ===
        if (NeedsGCSafePoint(block))
        {
            InsertGCSafePoint(block);
        }

        // === NEW: Reduction counting ===
        if (compOptions.EnableReductionCounting)
        {
            if (IsLoopBackEdge(block) || IsMethodCall(block))
            {
                InsertReductionCheck(block);
            }
        }

        // === NEW: Checkpoint candidacy ===
        if (compOptions.EnableCheckpointing)
        {
            MarkCheckpointCandidate(block);
        }
    }
}
```

### 8.2 Changes to codegencommon.cpp

```cpp
// src/coreclr/jit/codegencommon.cpp

void CodeGen::EmitSafePoint(SafePointInfo* info)
{
    // === NEW: Unified safe point poll ===
    if (compiler->opts.EnableUnifiedSafePoints)
    {
        // dec [tls + reductionCounter]
        getEmitter()->emitIns_AR_R(INS_dec, EA_4BYTE,
            REG_NA, REG_THREAD_STATE, offsetof(reductionCounter));

        // Load flags
        getEmitter()->emitIns_R_AR(INS_mov, EA_4BYTE,
            REG_TMP, REG_THREAD_STATE, offsetof(flags));

        // Test if any flag set
        getEmitter()->emitIns_R_R(INS_test, EA_4BYTE, REG_TMP, REG_TMP);

        // Jump to slow path if any flag
        getEmitter()->emitIns_J(INS_jnz, SafePointSlowPath);
    }
    else
    {
        // Existing GC poll code...
    }

    // Record safe point metadata
    RecordSafePointMetadata(currentOffset, info);
}
```

### 8.3 Changes to gcinfo.cpp

```cpp
// src/coreclr/jit/gcinfo.cpp

void GCInfo::EmitExtendedSafePointInfo(SafePointInfo* info)
{
    // Existing GC info...
    EmitGCRefLocations(info);

    // NEW: Checkpoint capability
    if (compiler->opts.EnableCheckpointing)
    {
        EmitCheckpointInfo(info);
    }
}

void GCInfo::EmitCheckpointInfo(SafePointInfo* info)
{
    // Frame size for serialization
    writer.WriteUInt16(info->frameSerializeSize);

    // Flags
    writer.WriteByte(info->isCheckpointCandidate ? 1 : 0);

    // Local variable map (for serialization)
    for (auto& local : info->locals)
    {
        writer.WriteLocalInfo(local);
    }
}
```

---

## 9. Runtime Components

### 9.1 Scheduler Integration

```cpp
// src/coreclr/vm/scheduler.cpp (NEW)

class DOTNExTScheduler
{
public:
    // Called when reduction exhausted
    void YieldFromReduction(Thread* thread)
    {
        // Save minimal state (we're at safe point)
        SaveThreadContext(thread);

        // Pick next thread/task
        Thread* next = SelectNext();

        // Switch (cooperative - no OS context switch needed)
        SwitchTo(next);
    }

    // Called for checkpoint
    void CheckpointThread(Thread* thread)
    {
        // Capture using safe point info
        auto checkpoint = CaptureThreadState(thread);

        // Store via configured mechanism
        PersistCheckpoint(checkpoint);
    }
};
```

### 9.2 Configuration

```cpp
// Runtime configuration options

struct DOTNExTOptions
{
    // Reduction counting
    bool EnableReductionCounting = false;
    int32_t ReductionBudget = 4000;

    // Checkpointing
    bool EnableCheckpointing = false;
    CheckpointPolicy CheckpointPolicy = Manual;

    // Unified safe points
    bool EnableUnifiedSafePoints = false;
};
```

---

## 10. Performance Considerations

### 10.1 Hot Path Cost

| Component | Cost | Notes |
|-----------|------|-------|
| Reduction decrement | ~1 cycle | Single `dec` instruction |
| Flag check | ~1-3 cycles | Memory read + test |
| Branch (not taken) | ~0 cycles | Well-predicted |
| **Total hot path** | **~2-5 cycles** | Per safe point |

### 10.2 Cold Path Cost

| Action | Cost | Frequency |
|--------|------|-----------|
| GC yield | ~1000s cycles | Rare (GC trigger) |
| Preemptive yield | ~100s cycles | Every N reductions |
| Checkpoint | ~1000s-millions | Policy-dependent |

### 10.3 Memory Overhead

| Data | Size | Per |
|------|------|-----|
| SafePointState | 8 bytes | Thread |
| Extended GC info | ~10-50 bytes | Method (if checkpointable) |
| Checkpoint metadata | ~2-4 bytes | Safe point |

---

## 11. Implementation Phases

### Phase 1: Reduction Counting Only
- [ ] Add reduction counter to thread state
- [ ] JIT emits decrement at back-edges
- [ ] Simple yield mechanism
- [ ] No checkpoint yet

### Phase 2: Unified Polling
- [ ] Unified flag word
- [ ] Combined hot path
- [ ] GC integration

### Phase 3: Checkpoint Support
- [ ] Extended safe point metadata
- [ ] Frame serialization info
- [ ] Checkpoint trigger mechanism

### Phase 4: Full Integration
- [ ] Tasklet/ProcessImage integration
- [ ] Policy framework
- [ ] Performance tuning

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-Async-Research.md | Tasklet mechanism we build on |
| DOTNExT-Process-Image-Persistence.md | Uses checkpoint capability |
| Erlang-BEAM-Architecture-Reference.md | Inspiration for reduction counting |

---

*This document describes the unification of GC safe points, BEAM-like preemption, and process checkpointing into a single runtime mechanism. The insight is that all three need the same thing: consistent, capturable execution state.*

*Version 1.0 - 2025-12-08*
