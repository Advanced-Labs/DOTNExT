# DOTNExT Execution Pathways - Universal Execution Model

> **Document Type:** Technical Research & Design Options
> **Version:** 2.0
> **Date:** 2025-12-10
> **Status:** RESEARCH - Vision clarified, concept exploration
> **Context:** Emerged from runtime R&D; evolved beyond async-specific origins
> **Key Update v2.0:** Reframed from "Tasklet-based" to universal execution model. Async was doorway, not destination. DOTNExT may create its own constructs.

---

## 1. Executive Summary

This document explores **Execution Pathways** - a new execution model for DOTNExT where **all execution** (not just async) runs as capturable, inspectable, manipulable state.

**Evolution of Understanding:**

1. **Started with Async+** - Persist/restore async state machines via Roslyn codegen
2. **Discovered limitations** - Roslyn approach limited to await points, compiler-selected state
3. **Found Unwinder** - .NET 9 experiment capturing real stack frames at any safe point
4. **Key realization** - We don't want "better async"; we want **universal execution capture**

**The Vision:** A DOTNExT execution mode where:
- **Everything** runs as capturable execution units (not just async methods)
- **Safe points** become control points where runtime/AI can intervene
- **Execution is first-class data** - inspect, serialize, fork, migrate, compare, kill
- **AI can manipulate execution flow** from managed space with full introspection

**Trade-off Philosophy:** DOTNExT trades speed/resources for capabilities. AI is the bottleneck by orders of magnitude - runtime overhead buys intelligent control. "Slow but Smart is the new Speed."

**Note:** DOTNExT may develop its own constructs for execution capture - not necessarily "Tasklets", potentially different in design. We study the Unwinder for **techniques**, not wholesale adoption.

---

## 1.4. The Semantic Inversion: `sync` is the New Exception

### Everything is Yieldable by Default

In DOTNExT's execution model, **all code can yield at any safe point**. This is "async-like" behavior by nature. The word "async" loses meaning because it's not distinguishing anything.

**The inversion:**

| Traditional .NET | DOTNExT |
|------------------|---------|
| `void Foo()` = synchronous, blocking | `void Foo()` = can yield at any safe point |
| `async Task Foo()` = may yield | Redundant - everything may yield |
| No keyword for sync | **`sync` keyword marks non-yielding** |

### The `sync` Keyword

**Declaration-site:** Method NEVER yields internally

```csharp
sync void AtomicOperation()
{
    // Guaranteed: no yields, no preemption, no checkpoints
    // Always runs to completion atomically
}
```

**Call-site:** Execute entire call tree without yields

```csharp
var result = sync SomeMethod();

// Creates "all sync-scoped call":
// - SomeMethod runs without yields
// - Everything SomeMethod calls runs without yields
// - Transitive through entire call tree
// - Until execution returns here
```

### Sync Scope Behavior

```
Normal call:                    sync call:
────────────────────────────────────────────────────────
ProcessData()                   sync ProcessData()
  ├── SubMethod1()  [can yield]   ├── SubMethod1()  [NO yield]
  │   └── Deep()    [can yield]   │   └── Deep()    [NO yield]
  └── SubMethod2()  [can yield]   └── SubMethod2()  [NO yield]
```

### Declaration vs Call-site Interaction

```csharp
sync void AlwaysSync() { ... }  // Declaration: always sync
void MayYield() { ... }          // Normal: can yield

AlwaysSync();       // Runs sync (method enforces)
sync MayYield();    // Runs sync (call-site enforces)
sync AlwaysSync();  // Redundant but harmless
MayYield();         // Can yield (normal behavior)
```

### Async/Await Compatibility

```csharp
// async/await kept for .NET compatibility
async Task LegacyStyle()
{
    await SomeOperation();  // Explicit yield hint
}

// In DOTNExT:
// - async is documentation ("yields expected here")
// - await is explicit yield point hint
// - But yields happen at any safe point anyway
// - These aren't required, just compatibility
```

---

## 1.5. AI-Enabled Execution Capabilities

**When execution is first-class data and AI can reason about it, entirely new capabilities emerge:**

### Speculative Parallel Execution
```
Fork pathway at decision point
    ├── Branch A (parameters X)
    ├── Branch B (parameters Y)
    └── Branch C (parameters Z)

AI monitors all branches:
- Evaluate intermediate results at checkpoints
- Kill branches showing poor convergence
- Invest more resources in promising branches
- Eventually select winner(s) or combine results
```

### Execution Rewinding
```
Checkpoint → Execute → Problem detected
                         ↓
                    Rewind to checkpoint
                         ↓
                    Modify parameters
                         ↓
                    Re-execute with AI-guided adjustments
```

### Distributed Redundant Execution
```
Same pathway executed on multiple nodes:
- Racing: First to return wins (latency optimization)
- Consensus: Wait for 2+ to agree (trust verification)
- Comparison: Detect if any node "lied" (Byzantine tolerance)
```

### Forward-Only Execution Pathways
```
Certain pathway types never return:
- Fire-and-forget processing
- Event propagation chains
- One-way data transformations
- No call stack, pure continuation
```

### Intelligent Pathway Routing
```
AI examines execution state + context:
- Route to node with relevant data locality
- Route to node with specialized hardware
- Route to low-latency path for time-sensitive work
- Route to trusted nodes for sensitive computations
```

### Meta-Execution (AI Customizes Execution Algorithms)
```
From managed space, AI can:
- Define custom scheduling policies
- Implement domain-specific checkpointing strategies
- Create hybrid execution patterns
- Modify how pathways interact/communicate
```

**These capabilities are only possible because:**
1. Execution state is captured at any safe point (not just await)
2. Captured state is manipulable from managed space
3. AI has full introspection into execution
4. Runtime overhead is acceptable (AI is the real bottleneck)

---

## 1.6. Execution Hints System (Future Direction)

Beyond `sync`, DOTNExT may support **execution hints** - guidance to the runtime about execution preferences:

### Potential Hint Types

```csharp
// Checkpoint hints
[Checkpoint]           // "Good place to checkpoint here"
[NoCheckpoint]         // "Avoid checkpointing in this region"

// Preemption hints
[PreemptionBudget(1000)]  // "Allow ~1000 reductions before yield"
[NeverPreempt]            // Similar to sync, scoped

// Migration hints
[MigrationBoundary]    // "Pathway can migrate here"
[NoMigration]          // "Keep on same node through this"
[PreferNode("gpu-cluster")]  // "Route to specific node type"

// Speculation hints
[Speculative]          // "May run speculatively/in parallel"
[Deterministic]        // "Same inputs = same outputs, safe to cache"

// Debugging/monitoring hints
[Trace]                // "Record execution details here"
[Breakpoint]           // "AI/debugger attention point"
```

### Block-Scoped Hints

```csharp
void ProcessWithHints()
{
    [Checkpoint]
    var data = LoadData();

    sync  // Or [Atomic]
    {
        // Critical section - no yields
        UpdateSharedState(data);
    }

    [Speculative]
    {
        // AI might run multiple versions
        var result = ExpensiveComputation(data);
    }
}
```

### AI-Consumable vs Runtime-Enforced

| Hint Type | Enforcement |
|-----------|-------------|
| `sync` | **Runtime-enforced** - guaranteed behavior |
| `[Checkpoint]` | Advisory - AI/scheduler decides |
| `[Speculative]` | Advisory - AI may parallelize |
| `[PreferNode]` | Advisory - routing preference |

**Note:** This hints system is a future direction. Initial focus is on the `sync` keyword and universal execution capture.

---

## 2. What BEAM Has vs What We Have

### 2.1 BEAM Process Model

| Feature | BEAM Provides | How |
|---------|---------------|-----|
| **Isolation** | Complete memory separation | Per-process heap |
| **Identity** | PID identifies process | VM-assigned |
| **Preemption** | Forced yielding | Reduction counting |
| **Migration** | Process can move between nodes | Built-in distribution |
| **Supervision** | Fault containment | Process dies, others unaffected |
| **Message Passing** | Only communication | No shared memory |

### 2.2 What Unwinder Techniques Demonstrate (Study, Not Adopt)

**Note:** We study these techniques to inform DOTNExT's own design, not to adopt Tasklets directly.

| Technique | What It Proves | DOTNExT Implication |
|-----------|----------------|---------------------|
| **Frame → Heap** | Stack frames can become heap objects | Execution state can be reified |
| **Any Safe Point** | Capture at GC safe points, not just await | Universal capture possible |
| **Byref Preservation** | Real stack semantics survive capture | No artificial restrictions |
| **Chain Linking** | Full call stack capturable | Complete execution context |
| **GC Integration** | Captured state is GC-managed | Memory safety preserved |

**What Unwinder doesn't provide (gaps we'd fill differently):**

| Gap | Unwinder Status | DOTNExT Direction |
|-----|-----------------|-------------------|
| **Isolation** | ❌ None (shared heap) | VCOM layer or custom |
| **Preemption** | ❌ Not built-in | Safe point hooks |
| **Generics** | ❌ Not implemented | Required for us |
| **Exception Handling** | ❌ Not implemented | Required for us |
| **Identity** | ❌ None | Pathway identity system |

### 2.3 The Gap (What DOTNExT Must Build)

| Capability | BEAM | Unwinder Demo | DOTNExT Target |
|------------|------|---------------|----------------|
| Isolation | ✅ Per-process heap | ❌ Shared heap | Logical via VCOM |
| Identity | ✅ PID | ❌ None | Pathway UUID |
| Preemption | ✅ Reduction counting | ❌ None | Safe point hooks |
| Migration | ✅ Built-in | Partial | First-class feature |
| Supervision | ✅ OTP | ❌ None | Pathway supervision |
| AI Control | N/A | N/A | **Novel capability** |

---

## 3. The Execution Pathway Concept

### 3.1 Definition

**Execution Pathway = Captured execution state with identity, forming a first-class entity**

(The underlying representation may use Tasklet-like structures, but DOTNExT may design its own constructs.)

```
┌─────────────────────────────────────────────────────────────────┐
│  Execution Pathway                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  IDENTITY                                                       │
│  ├── Pathway UUID                                               │
│  ├── Origin (where started)                                     │
│  ├── Current location (where executing)                         │
│  └── Lineage (parent pathway, if spawned)                       │
│                                                                 │
│  EXECUTION STATE                                                │
│  ├── Tasklet Chain                                              │
│  │   ├── Tasklet 1: Method, IP, locals                         │
│  │   ├── Tasklet 2: Method, IP, locals                         │
│  │   └── ...                                                    │
│  ├── Status: Running | Suspended | Waiting | Completed          │
│  └── Scheduling info (priority, reduction budget)               │
│                                                                 │
│  REFERENCED STATE                                               │
│  ├── Objects reachable from Tasklet locals                      │
│  └── (Captured as Engram when persisting)                       │
│                                                                 │
│  CONTEXT                                                        │
│  ├── ExecutionContext                                           │
│  ├── SynchronizationContext                                     │
│  └── AsyncLocal values                                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 What This Enables

| Capability | Description |
|------------|-------------|
| **Tracking** | Know what pathways are executing |
| **Monitoring** | Observe pathway progress, resources |
| **Suspension** | Pause a pathway at safe points |
| **Migration** | Move pathway between nodes |
| **Persistence** | Save pathway, resume later |
| **Debugging** | Inspect pathway state |
| **Distributed Execution** | Pathway spans nodes |

---

## 4. Pathway Lifecycle

### 4.1 Creation

**DECISION NEEDED:** When is a Pathway created?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Explicit** | Developer calls `Pathway.Start(...)` | Manual control, opt-in |
| **Automatic Async** | Every async method starts a Pathway | Many pathways, overhead |
| **Task-Aligned** | Each Task root is a Pathway | Natural mapping |
| **Thread-Aligned** | Each thread is a Pathway | Coarse granularity |
| **Grain-Aligned** | Each grain activation is a Pathway | VCOM integration |

### 4.2 States

```
┌─────────────────────────────────────────────────────────────────┐
│  Pathway State Machine                                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│         ┌──────────┐                                            │
│         │ Created  │                                            │
│         └────┬─────┘                                            │
│              │ Schedule                                         │
│              ▼                                                  │
│         ┌──────────┐    Preempt/Await    ┌───────────┐         │
│         │ Running  │ ◄─────────────────► │ Suspended │         │
│         └────┬─────┘     Resume          └─────┬─────┘         │
│              │                                 │                │
│              │ Complete                        │ Persist        │
│              ▼                                 ▼                │
│         ┌──────────┐                    ┌───────────┐          │
│         │Completed │                    │ Persisted │          │
│         └──────────┘                    └───────────┘          │
│                                               │                │
│                                               │ Restore        │
│                                               ▼                │
│                                         ┌───────────┐          │
│                                         │ Suspended │          │
│                                         └───────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.3 Operations

| Operation | Description | Implementation |
|-----------|-------------|----------------|
| **Create** | Start new pathway | Allocate ID, register |
| **Suspend** | Pause at safe point | Capture Tasklets |
| **Resume** | Continue execution | Restore Tasklets, schedule |
| **Migrate** | Move to another node | Serialize, transfer, restore |
| **Persist** | Save to storage | Extract Execution Engram |
| **Restore** | Load from storage | Load Engram, create Pathway |
| **Terminate** | End pathway | Cleanup, notify |
| **Fork** | Create child pathway | New ID, linked lineage |

---

## 5. Pathways Without Isolation

### 5.1 The Shared Heap Problem

Unlike BEAM, pathways share the managed heap:
- Pathway A can reference objects also referenced by Pathway B
- Mutation in A visible to B
- Crash in A can corrupt state B depends on

### 5.2 Mitigation Strategies

**DECISION NEEDED:** How to handle shared state?

| Strategy | Description | Trade-off |
|----------|-------------|-----------|
| **Ignore** | Accept shared state as feature | Simple, but unsafe |
| **Ownership Tracking** | Track which pathway "owns" each object | Complex, overhead |
| **Copy-on-Write** | Snapshot on pathway creation | Memory cost, isolation |
| **Immutability Convention** | Encourage immutable objects | Works with discipline |
| **Message Passing Convention** | Pathways communicate via messages only | BEAM-like, requires discipline |
| **Compiler Enforcement** | Language/analyzer prevents sharing | Requires tooling |
| **VCOM Objects** | Only VCOM objects cross pathways | VCOM provides isolation semantics |

### 5.3 The VCOM Solution

If pathways only share VCOM objects:
- VCOM objects are grain-backed (actor semantics)
- Access is through proxies (method calls, not field access)
- Isolation at VCOM level, even though heap is shared

```
Pathway A                           Pathway B
    │                                   │
    │  local objects (private)          │  local objects (private)
    │                                   │
    └────────► VCOM Object ◄────────────┘
               (shared via proxy)
               (actor semantics)
```

---

## 6. Preemption via Safe Points

### 6.1 Adding BEAM-Like Reduction Counting

From `DOTNExT-Unified-SafePoints.md`, we can add reduction counting:

```cpp
// At safe points (loop back-edges, calls)
if (--pathway.reductionCounter <= 0)
{
    pathway.reductionCounter = REDUCTION_BUDGET;
    YieldToScheduler(pathway);
}
```

### 6.2 Pathway Scheduler

**DECISION NEEDED:** How are pathways scheduled?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **OS Threads** | Each pathway = OS thread | Heavy, but simple |
| **Thread Pool** | Pathways scheduled on pool | Current .NET model |
| **Custom Scheduler** | BEAM-like N:M scheduling | Lightweight, complex |
| **Per-Core Queues** | BEAM-style run queues | Scalable, work stealing |
| **Priority-Based** | Pathways have priority levels | QoS support |

### 6.3 Fair Scheduling

With reduction counting + safe points:
- No pathway can monopolize execution
- Long-running code yields at safe points
- Scheduler can prioritize, balance, migrate

---

## 7. Distributed Execution

### 7.1 Pathway Migration

A pathway can potentially move between nodes:

```
┌─────────────────────────────────────────────────────────────────┐
│  Pathway Migration                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  NODE A                              NODE B                     │
│  ┌─────────────────┐                ┌─────────────────┐        │
│  │ Pathway P       │                │                 │        │
│  │ (executing)     │                │                 │        │
│  └────────┬────────┘                └─────────────────┘        │
│           │                                                     │
│           │ 1. Suspend at safe point                           │
│           │ 2. Extract Execution Engram                        │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐    3. Transfer    ┌─────────────────┐     │
│  │ Engram P        │ ────────────────► │ Engram P        │     │
│  └─────────────────┘                   └────────┬────────┘     │
│                                                 │               │
│                                                 │ 4. Restore   │
│                                                 ▼               │
│                                        ┌─────────────────┐     │
│                                        │ Pathway P       │     │
│                                        │ (resuming)      │     │
│                                        └─────────────────┘     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 Migration Triggers

**DECISION NEEDED:** When does migration happen?

| Trigger | Description | Use Case |
|---------|-------------|----------|
| **Load Balancing** | Node overloaded | Scale |
| **Data Locality** | Pathway needs data on another node | Performance |
| **Fault Tolerance** | Node failing | Reliability |
| **Explicit** | Application requests | Control |
| **Policy** | Rules about where pathways run | Governance |

### 7.3 Migration Challenges

| Challenge | Description | Potential Solution |
|-----------|-------------|-------------------|
| **Local References** | Objects not in Engram | Capture or proxy |
| **External Resources** | Files, connections | Reconnect or proxy |
| **Thread Affinity** | Some code requires specific thread | Constraint tracking |
| **Latency** | Migration takes time | Background transfer |

---

## 8. Pathway Supervision

### 8.1 Fault Handling

Without isolation, a pathway crash can affect others. Supervision strategies:

| Strategy | Description | Trade-off |
|----------|-------------|-----------|
| **Let It Crash** | Crash kills process | Simple, but harsh |
| **Exception Isolation** | Catch, log, continue | May corrupt state |
| **Compensating Actions** | Undo partial work | Complex, application-specific |
| **Checkpoint/Retry** | Restore to last checkpoint, retry | Overhead, but robust |
| **Supervision Tree** | Parent pathway monitors children | BEAM-like, structured |

### 8.2 Supervision Trees for Pathways

```
┌─────────────────────────────────────────────────────────────────┐
│  Pathway Supervision Tree                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                 ┌─────────────┐                                 │
│                 │ Supervisor  │                                 │
│                 │ Pathway     │                                 │
│                 └──────┬──────┘                                 │
│            ┌───────────┼───────────┐                           │
│            │           │           │                            │
│       ┌────┴────┐ ┌────┴────┐ ┌────┴────┐                      │
│       │ Worker  │ │ Worker  │ │ Sub-Sup │                      │
│       │ P1      │ │ P2      │ │         │                      │
│       └─────────┘ └─────────┘ └────┬────┘                      │
│                                    │                            │
│                              ┌─────┴─────┐                     │
│                              │           │                      │
│                         ┌────┴────┐ ┌────┴────┐                │
│                         │ Worker  │ │ Worker  │                │
│                         │ P3      │ │ P4      │                │
│                         └─────────┘ └─────────┘                │
│                                                                 │
│  Restart Strategies:                                            │
│  - one_for_one: Restart only failed pathway                    │
│  - one_for_all: Restart all children                           │
│  - rest_for_one: Restart failed + later siblings               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 9. Pathways and Engrams

### 9.1 Execution Engram = Pathway Snapshot

An Execution Engram is the serialized form of a Pathway:

```
Pathway (live)          ──Extract──►    Execution Engram (data)
                                              │
Pathway (restored)      ◄──Restore──          │
```

### 9.2 Engram Contents for Pathways

| Component | Source | In Engram |
|-----------|--------|-----------|
| Pathway ID | Pathway metadata | UUID |
| Tasklet Chain | Runtime-Async | Serialized frames |
| Referenced Objects | GC walk from locals | Object Engram |
| Context | ExecutionContext | Serialized |
| Scheduling State | Scheduler | Priority, budget, etc. |

---

## 10. API Sketch

### 10.1 Pathway Creation

```csharp
// Option A: Explicit creation
var pathway = Pathway.Start(async () =>
{
    // Pathway body
    await DoWork();
});

// Option B: Implicit from Task
var task = SomeAsyncMethod();  // Automatically creates Pathway
var pathwayId = task.PathwayId;

// Option C: VCOM integration
var grain = vcom.Get<IMyGrain>(id);  // Grain methods run in Pathway
```

### 10.2 Pathway Operations

```csharp
// Suspend
await pathway.SuspendAsync();

// Resume
await pathway.ResumeAsync();

// Migrate
await pathway.MigrateTo(targetNode);

// Persist
var engram = await pathway.ToEngramAsync();
await storage.SaveAsync(engram);

// Restore
var engram = await storage.LoadAsync<ExecutionEngram>(id);
var pathway = await Pathway.FromEngramAsync(engram);

// Monitor
var state = pathway.State;  // Running, Suspended, etc.
var location = pathway.CurrentNode;
```

### 10.3 Supervision

```csharp
// Supervisor pathway
var supervisor = Pathway.StartSupervisor(config =>
{
    config.Strategy = SupervisionStrategy.OneForOne;
    config.MaxRestarts = 3;
    config.WithinTimeSpan = TimeSpan.FromMinutes(1);
});

// Spawn children
var worker1 = supervisor.SpawnChild(async () => { /* work */ });
var worker2 = supervisor.SpawnChild(async () => { /* work */ });

// Failures handled by supervisor
```

---

## 11. Implementation Phases

### Phase 1: Basic Pathways
- [ ] Pathway identity assignment
- [ ] Pathway registry (track active pathways)
- [ ] Basic suspend/resume using Tasklets

### Phase 2: Scheduling
- [ ] Reduction counting at safe points
- [ ] Custom scheduler with pathway queues
- [ ] Fair scheduling across pathways

### Phase 3: Persistence
- [ ] Execution Engram extraction
- [ ] Pathway restore from Engram
- [ ] Storage integration

### Phase 4: Distribution
- [ ] Cross-node migration
- [ ] Distributed pathway registry
- [ ] Location tracking

### Phase 5: Supervision
- [ ] Supervision tree structure
- [ ] Restart strategies
- [ ] Fault propagation

---

## 12. Open Questions

### Identity
1. When are Pathways created? (explicit, automatic, task-aligned?)
2. How are Pathway IDs assigned? (UUID, hierarchical?)
3. How do Pathways relate to Tasks?

### Isolation
4. How to handle shared state? (ignore, track, copy, enforce?)
5. Do we require VCOM for cross-pathway state?
6. How strict is isolation?

### Scheduling
7. What scheduling model? (OS threads, pool, custom?)
8. How is reduction budget determined?
9. How are priorities assigned?

### Distribution
10. What triggers migration?
11. How are external resources handled on migration?
12. How does VNS track pathway locations?

### Supervision
13. What is the fault model?
14. Are supervision trees explicit or implicit?
15. How do restarts interact with state?

### Integration
16. How do Pathways integrate with existing async/await?
17. How do Pathways integrate with VCOM?
18. Can existing code run in Pathways without modification?

---

## 13. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-Async-Research.md | Tasklet mechanism underlying Pathways |
| DOTNExT-Unified-SafePoints.md | Safe points for preemption |
| DOTNExT-Engrams-Revised.md | Engrams for Pathway persistence |
| Erlang-BEAM-Architecture-Reference.md | BEAM process model inspiration |
| Vision-Engrams-Cyberspace-Verbatim.md | Distributed execution vision |

---

*This document explores Execution Pathways as a universal execution model for DOTNExT. Many design decisions remain open. The goal is to capture the concept space before committing to specific implementations.*

*Version 2.1 - 2025-12-10 - Added semantic inversion (`sync` is the new exception); sync keyword semantics; execution hints system*

*Version 2.0 - 2025-12-10 - Major reframe: async was doorway not destination; universal execution model; AI-enabled capabilities; DOTNExT may create own constructs*

*Version 1.0 - 2025-12-08 - Initial exploration*
