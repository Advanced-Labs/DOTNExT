# DOTNExT: The Grand Vision

**Author**: Claude Opus
**Date**: 2025-11-28
**Status**: Living document - update with each session

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Conceptual Layers](#conceptual-layers)
3. [Async/Await Evolution](#asyncawait-evolution)
4. [Persistence Model](#persistence-model)
5. [Memory System Redesign](#memory-system-redesign)
6. [DOTNExT as Meta-OS](#dotnext-as-meta-os)
7. [Semantic Augmentation Concepts](#semantic-augmentation-concepts)
8. [Evolution Paths](#evolution-paths)
9. [Key Design Principles](#key-design-principles)
10. [Open Questions](#open-questions)

---

## Executive Summary

DOTNExT is envisioned as a **next-generation runtime platform** that evolves from:
- A fork of Orleans (NewOrleans) with dynamic grain loading
- Through increasingly deep modifications to Roslyn and the .NET runtime
- Toward a **Meta-Operating System** comparable to Android in architectural scope

The core insight: **25+ years of computing advancement** since .NET's design means we can afford continuous "bookkeeping" overhead that enables:
- Seamless state snapshotting and migration
- Distributed execution without explicit serialization boundaries
- Time-travel debugging and execution branching
- AI-first programming models with semantic memory

---

## Conceptual Layers

The vision spans multiple implementation depths:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ Layer 5: Applications & Workflows                                        │
│ - User code written in C* (C# superset)                                  │
│ - Transparent distribution and persistence                               │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
┌─────────────────────────────────────────────────────────────────────────┐
│ Layer 4: NewOrleans Framework                                            │
│ - Dynamic grain loading (GTD, GTC, Package System)                       │
│ - Distributed async orchestration                                        │
│ - Soft/Hard persistence abstractions                                     │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
┌─────────────────────────────────────────────────────────────────────────┐
│ Layer 3: Roslyn / C* Compiler                                            │
│ - Augmented async/await codegen                                          │
│ - Persistence-aware code transformation                                  │
│ - C* as transpilation target for other languages                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
┌─────────────────────────────────────────────────────────────────────────┐
│ Layer 4: DOTNExT VM/CLR                                                  │
│ - Redesigned memory management (continuous bookkeeping)                  │
│ - Modular VM extensions (kernel-like architecture)                       │
│ - Extended CIL/IR for new paradigms                                      │
│ - VM-to-VM collaboration primitives                                      │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
┌─────────────────────────────────────────────────────────────────────────┐
│ Layer 1: Native/OS Interface                                             │
│ - Drivers (virtual + native parts)                                       │
│ - OS-like services                                                       │
│ - Hardware abstraction                                                   │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Async/Await Evolution

### Current State (Standard .NET)

Async/await is purely a **local concurrency mechanism**:
- Roslyn transforms async methods into state machines
- State machines are ephemeral (lost on process death)
- No built-in distribution or persistence semantics

### Evolution Path

#### Stage 1: Framework-Level (NewOrleans Today)
- Custom `[AsyncMethodBuilder]` intercepts suspensions
- Explicit checkpointing at await boundaries
- Framework-managed persistence and migration

#### Stage 2: Roslyn Augmentation
- Modified codegen captures more state
- Awareness of soft/hard persistence distinctions
- Attribute-driven behavior:
  ```csharp
  [NonDistributable]  // Keep local
  [SoftPersist]       // Checkpoint for recovery
  [HardPersist]       // Canonical persistence
  public async Task<T> MyWorkflowAsync() { ... }
  ```

#### Stage 3: Runtime-Integrated
- VM natively understands distributed async
- Memory system enables seamless state capture
- No explicit serialization boundaries needed

### Version Management Strategy

**Critical insight from user**: Don't hot-swap state machines mid-execution.

```
Version Transition Model:

Time ──────────────────────────────────────────────────────►

v1.0 Running:  [SM1]──await──[SM1]──await──[SM1]──complete
               [SM2]──await──[SM2]──await──────────abort
               [SM3]──await──────────────────────────drain

v2.0 Deployed: ────────────────────[NEW]──await──[NEW]──...

Strategies:
1. DRAIN: Let v1.0 instances complete naturally
2. ABORT: Cancel v1.0 instances at next await
3. MIGRATE: Special migration code transforms v1.0 state → v2.0
4. TIMEOUT: Force abort after deadline
```

New calls route to new version. Old continuations:
- Continue with old version code (drain)
- OR are aborted
- OR are migrated via explicit transformation logic

Only unload old types after complete drainage/abortion.

---

## Persistence Model

### Three-Tier Persistence

```
┌─────────────────────────────────────────────────────────────────────┐
│                        HARD PERSISTENCE                              │
│  - Canonical application state                                       │
│  - Traditional database/file persistence                             │
│  - Survives everything                                               │
│  - Committed at transaction boundaries                               │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ Transaction commit
                              │
┌─────────────────────────────────────────────────────────────────────┐
│                        SOFT PERSISTENCE                              │
│  - Recovery/transfer state                                           │
│  - Async state machine snapshots                                     │
│  - Execution context for migration                                   │
│  - Overwritable/deletable after hard commit                          │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ Checkpoint at await
                              │
┌─────────────────────────────────────────────────────────────────────┐
│                        EPHEMERAL STATE                               │
│  - In-flight computation                                             │
│  - Local variables between awaits                                    │
│  - Lost on failure (acceptable for non-critical)                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Transaction Model

```csharp
[DistributedWorkflow]
public async Task ProcessOrderAsync(Order order)
{
    // Soft-persisted execution state
    var validation = await ValidateAsync(order);      // checkpoint
    var payment = await ChargeAsync(order);           // checkpoint
    var shipment = await ShipAsync(order);            // checkpoint

    // Transaction boundary - hard persist
    await CommitTransactionAsync();
    // Soft persistence for this flow can now be purged
}
```

At `CommitTransactionAsync()`:
1. Hard persistence of canonical state
2. Mark soft persistence as overwritable
3. Previous soft checkpoints no longer needed for recovery

### Attribute-Based Control

```csharp
// Method-level
[NonDistributable]           // Must execute locally
[SoftPersistDisabled]        // No checkpointing (fast but no recovery)
[HardPersistBoundary]        // Method completion = transaction commit

// Await-level (possible with Roslyn modification)
var result = await [Checkpoint] LongOperationAsync();
var local = await [NoCheckpoint] QuickLocalAsync();
```

---

## Memory System Redesign

### The Fundamental Insight

> "No matter the computer or its operating system, its volatile memory system operating against the RAM is itself serial."

Current .NET memory model:
- Objects allocated in managed heap
- References are raw pointers (process-local)
- Serialization is an afterthought (explicit, expensive)

### Proposed DOTNExT Memory Model

**Continuous Bookkeeping**: Invest ongoing overhead to maintain snapshot-readiness.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    DOTNExT Memory Manager                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐           │
│  │  Object A   │────▶│  Object B   │────▶│  Object C   │           │
│  │  [OID: 001] │     │  [OID: 002] │     │  [OID: 003] │           │
│  └─────────────┘     └─────────────┘     └─────────────┘           │
│        │                   │                   │                    │
│        ▼                   ▼                   ▼                    │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  Reference Abstraction Layer                 │   │
│  │  - All refs are OIDs (Object IDs), not raw pointers         │   │
│  │  - OID → local pointer resolution table                      │   │
│  │  - Enables portable references across VMs                    │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                      │
│                              ▼                                      │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    Snapshot Registry                         │   │
│  │  - Tracks all objects and their dependencies                │   │
│  │  - Graph structure always known                              │   │
│  │  - Subgraph extraction in O(n) of subgraph size             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Snapshot Capabilities

**Full Process Snapshot**:
- Entire managed heap
- VM state
- Optionally native state

**Selective Snapshot**:
- Specific object subgraph
- Async state machine + dependencies
- Grain state + closure

**With Code**:
- Include loaded assemblies
- Type definitions for deserialization
- Version information

### Cross-VM Transfer

```
VM-A                                    VM-B
┌──────────────┐                       ┌──────────────┐
│ Object Graph │  ──snapshot──►        │              │
│ (OID refs)   │                       │   Receive    │
│              │  ──transfer──►        │   ────────   │
│              │                       │   Remap OIDs │
│              │                       │   Merge/Link │
└──────────────┘                       └──────────────┘
```

**Merge strategies**:
- Fresh allocation (new OIDs)
- Consolidation (match existing equivalent objects)
- Linking (references to shared immutables)

### Benefits of Continuous Bookkeeping

1. **Predictable performance** - No snapshot-time spike
2. **Fast snapshot** - Structure already known
3. **Orchestratable** - Can coordinate multi-VM snapshots
4. **History capability** - Retain past states
5. **Time-travel** - Rewind for debugging, exploration
6. **Better persistence** - Aligned with OOP graph nature
7. **Semantic augmentation ready** - Structure available for AI analysis

---

## DOTNExT as Meta-OS

### Architectural Vision

```
┌─────────────────────────────────────────────────────────────────────┐
│                         DOTNExT Meta-OS                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      Application Layer                        │  │
│  │  - User applications in C*/C#/F#/etc                         │  │
│  │  - Workflows, Grains, Services                                │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      VM Services Layer                        │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐             │  │
│  │  │ NewOrleans  │ │  Persistence │ │  Networking │             │  │
│  │  │   Service   │ │   Service    │ │   Service   │             │  │
│  │  └─────────────┘ └─────────────┘ └─────────────┘             │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐             │  │
│  │  │  Security   │ │   Semantic   │ │  Scheduling │             │  │
│  │  │   Service   │ │   Service    │ │   Service   │             │  │
│  │  └─────────────┘ └─────────────┘ └─────────────┘             │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      VM Kernel Layer                          │  │
│  │  - Memory Manager (with bookkeeping)                          │  │
│  │  - Thread/Task Scheduler                                      │  │
│  │  - Module Loader                                              │  │
│  │  - JIT/AOT Compiler                                           │  │
│  │  - GC (modified for snapshot support)                         │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      Extension Layer                          │  │
│  │  ┌─────────────────────┐  ┌─────────────────────┐            │  │
│  │  │   CIL/IR Extensions │  │   Driver Framework   │            │  │
│  │  │   (new paradigms)   │  │   (virtual+native)   │            │  │
│  │  └─────────────────────┘  └─────────────────────┘            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      Native/OS Interface                      │  │
│  │  - Platform abstraction                                       │  │
│  │  - Native interop                                             │  │
│  │  - Hardware access                                            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Modular VM Extensions

Like OS kernel modules:
- **Loading a module has systemic effects** (new capabilities available)
- **Modules can extend CIL/IR** (new opcodes, new semantics)
- **Libraries can target modules** (optimized implementations)

### C* as Universal IL

```
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│     C#      │ │     F#      │ │   Python    │ │    Rust     │
│   Source    │ │   Source    │ │   Source    │ │   Source    │
└──────┬──────┘ └──────┬──────┘ └──────┬──────┘ └──────┬──────┘
       │               │               │               │
       ▼               ▼               ▼               ▼
┌─────────────────────────────────────────────────────────────────┐
│                    C* (High-Level IL)                            │
│  - C# superset with DOTNExT extensions                          │
│  - Transpilation target for other languages                      │
│  - Benefits from Roslyn optimizations                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Extended CIL/IR                               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    DOTNExT VM Execution                          │
└─────────────────────────────────────────────────────────────────┘
```

### Driver Model

```csharp
// Virtual driver component (managed)
public class GpuComputeDriver : IVMDriver
{
    public void Initialize(IDriverContext context) { }
    public void OnModuleLoad() { /* systemic effects */ }

    // Exposed to user code
    public GpuBuffer AllocateBuffer(int size) { }
    public void Dispatch(GpuKernel kernel) { }
}

// Native component (unmanaged, platform-specific)
// Linked via driver manifest
```

---

## Semantic Augmentation Concepts

### Memantics (Semantic Memory)

The memory system becomes semantically aware:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Memantics Layer                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  AFFINITICS                                                         │
│  - Objects have semantic encodings (embeddings)                     │
│  - Affinities computed between objects                              │
│  - "These objects relate conceptually"                              │
│                                                                     │
│  SYNAPTICS                                                          │
│  - Spaces where affinities drive interactions                       │
│  - "Reactors" animate/articulate object interactions                │
│  - Auto-wiring based on semantic proximity                          │
│  - Emergent behaviors from affinity networks                        │
│                                                                     │
│  PATHWAYS                                                           │
│  - Detected execution flow patterns                                 │
│  - Optimizable based on observed behavior                           │
│  - AI-informed pathway prediction                                   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### AI-First Runtime Capabilities

1. **Semantic observability** - AI can understand what code/data means
2. **Intention intelligence** - Infer purpose from patterns
3. **Security intelligence** - Detect anomalous semantic patterns
4. **Auto-wiring** - Connect components by semantic affinity
5. **Emergent modulation** - AI-influenced execution flow

### Time-Travel and Branching

With history-capable memory:

```
                    ┌──[Branch A]──►
                    │
──[t0]──[t1]──[t2]──┼──[t3]──[t4]──► (main timeline)
                    │
                    └──[Branch B]──►

Use cases:
- Debugging: rewind to pre-failure state
- Exploration: "what if" execution branches
- AI training: explore execution variations
- Recovery: rollback to known-good state
```

---

## Evolution Paths

### The Portability Principle

> "Some code could be made reusable/portable/refactorable **by design** between lib/framework implementations and VM-level implementations."

### Staged Evolution Model

```
Stage 1: Library/Framework        Stage 2: Codegen-Heavy           Stage 3: VM-Native
──────────────────────────────    ──────────────────────────────    ──────────────────

┌─────────────────────────┐       ┌─────────────────────────┐       ┌─────────────────┐
│ Pure C# implementation  │       │ Roslyn generates        │       │ VM primitives   │
│ using existing runtime  │  ──►  │ optimized code for      │  ──►  │ directly        │
│ Extension points only   │       │ framework concepts      │       │ implement       │
└─────────────────────────┘       └─────────────────────────┘       └─────────────────┘

Example: Soft Persistence

Stage 1: [AsyncMethodBuilder]     Stage 2: Roslyn emits        Stage 3: VM checkpoint
         intercepts awaits,                checkpoint calls              instruction,
         reflection to                     with optimized                memory manager
         serialize state                   state capture                 handles it
```

### Interface Preservation

Design interfaces that survive implementation migration:

```csharp
// This interface works at all stages
public interface ISoftPersistable
{
    Task CheckpointAsync();
    Task<bool> RestoreAsync(byte[] snapshot);
}

// Stage 1: Framework implements via reflection
// Stage 2: Codegen implements efficiently
// Stage 3: VM provides native implementation

// User code stays the same across stages
```

---

## Key Design Principles

1. **Continuous investment over spike cost** - Prefer ongoing overhead for snapshot-readiness over expensive on-demand serialization

2. **Abstracted references** - OIDs over raw pointers enable cross-VM portability

3. **Semantic by default** - Build for AI augmentation from the start

4. **Staged evolution** - Design for migration from lib → codegen → VM

5. **Interface stability** - APIs that survive implementation changes

6. **Version-aware execution** - Old code handles old state; explicit migration paths

7. **Modular extension** - Kernel-like architecture for VM capabilities

---

## Open Questions

1. **GC integration** - How does continuous bookkeeping interact with garbage collection? Does GC need to preserve OID mappings?

2. **Native interop** - How do we handle pointers from native code? Quarantine zones?

3. **Performance bounds** - What's the actual overhead of continuous bookkeeping? Benchmarks needed.

4. **Concurrency** - How do snapshots work with concurrent mutations? Copy-on-write? MVCC?

5. **Determinism** - For time-travel to work, do we need deterministic execution? How do we handle I/O?

6. **Semantic encoding** - What embedding model for objects? How to encode types, values, relationships?

7. **C* scope** - What extensions to C# does C* need? Syntax for persistence control? New keywords?

---

## References

- `/docs/NewOrleans/` - NewOrleans framework documentation
- `/docs/NewOrleans/References/PluginGrainArchitecture.md` - MDCP-based grain loading
- `/docs/NewOrleans/References/DynamicGrainAccess.md` - Dynamic grain access design
- `/AI-Contexts/Claude-Opus/AsyncDistributedComputing-Assessment.md` - Initial async/await analysis
- dotnet/runtime issue #109632 - Runtime-async tracking
- dotnet/runtime `docs/design/specs/runtime-async.md` - Runtime-async specification

---

*This document is a living record of the DOTNExT vision. Update with each session as understanding deepens.*
