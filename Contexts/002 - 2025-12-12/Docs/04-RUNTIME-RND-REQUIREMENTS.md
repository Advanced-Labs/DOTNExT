# Runtime R&D Requirements - DOTNExT Platform

> **Document Type:** Comprehensive R&D Requirements Analysis
> **Created:** 2025-12-14
> **Purpose:** Map all runtime-level R&D needed for DOTNExT vision against .NET 9 baseline
> **Status:** DRAFT v1.0

---

## Executive Summary

This document systematically identifies all runtime-level R&D required for the DOTNExT platform by:

1. **Cataloging** every runtime capability needed for the vision
2. **Mapping** against what .NET 9 already provides
3. **Identifying** the delta (what must be changed, added, or extended)
4. **Grouping** items by shared dependencies for coherent design
5. **Prioritizing** based on dependency chains and strategic importance

**Key Finding:** The runtime R&D falls into **5 major capability clusters** that share foundational dependencies. Designing these coherently rather than per-feature yields significant architectural synergies.

---

## Table of Contents

1. [Capability Clusters Overview](#1-capability-clusters-overview)
2. [Cluster A: Universal Execution Capture](#2-cluster-a-universal-execution-capture)
3. [Cluster B: Engram Infrastructure](#3-cluster-b-engram-infrastructure)
4. [Cluster C: Process/Pathway Execution Model](#4-cluster-c-processpathway-execution-model)
5. [Cluster D: Security & Isolation](#5-cluster-d-security--isolation)
6. [Cluster E: Memory System Extensions](#6-cluster-e-memory-system-extensions)
7. [.NET 9 Baseline Assessment](#7-net-9-baseline-assessment)
8. [Cross-Cluster Dependencies](#8-cross-cluster-dependencies)
9. [Implementation Phases](#9-implementation-phases)
10. [Open Architectural Questions](#10-open-architectural-questions)

---

## 1. Capability Clusters Overview

### The Five Clusters

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      DOTNExT RUNTIME R&D CLUSTERS                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  CLUSTER A: Universal Execution Capture                                      │
│  ├── Unified Safe Points                                                     │
│  ├── Unwinder/Frame Capture Techniques                                       │
│  ├── Tasklet/Execution Frame Implementation                                  │
│  └── Generics & Exception Handling Support                                   │
│                                                                             │
│  CLUSTER B: Engram Infrastructure                                            │
│  ├── Engram Extraction (object graph + execution state)                     │
│  ├── Engram Persistence (serialize/deserialize)                             │
│  ├── Engram Absorption/Hydration                                            │
│  └── Boundary Definition (what's IN vs OUT)                                 │
│                                                                             │
│  CLUSTER C: Process/Pathway Execution Model                                  │
│  ├── Process Identity & Lifecycle                                           │
│  ├── Pathway Identity & Scheduling                                          │
│  ├── sync Keyword (semantic inversion)                                      │
│  ├── BEAM-like Reduction Counting                                           │
│  └── Process Image Persistence (checkpoint/restore)                         │
│                                                                             │
│  CLUSTER D: Security & Isolation                                             │
│  ├── Security Interception Points                                           │
│  ├── Capability Model Integration                                           │
│  └── Logical Isolation (VCOM-based)                                         │
│                                                                             │
│  CLUSTER E: Memory System Extensions                                         │
│  ├── UUID Assignment/Tracking                                               │
│  ├── Relationship Recording (reference writes)                              │
│  ├── Type System Extensions (Engram awareness)                              │
│  └── JIT Helper Integration                                                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Why Clusters Matter

These clusters share **foundational dependencies**:

| Shared Dependency | Used By |
|-------------------|---------|
| **Safe Points** | Clusters A, B, C |
| **GC Info Tables** | Clusters A, B, E |
| **Frame Capture** | Clusters A, B, C |
| **Type Metadata** | Clusters B, D, E |
| **JIT Modifications** | Clusters A, C, E |

Designing these coherently avoids:
- Duplicate safe point mechanisms
- Conflicting JIT modifications
- Redundant metadata structures
- Inconsistent interception points

---

## 2. Cluster A: Universal Execution Capture

### 2.1 Vision

Enable capturing complete execution state at any safe point, not just async await points. This is the foundation for Process Image Persistence, Engrams, BEAM-like preemption, and AI-controlled execution.

### 2.2 R&D Items

| ID | Item | .NET 9 Status | Delta Required |
|----|------|---------------|----------------|
| **A-001** | Unified Safe Points | Partial | Extend GC safe points for preemption + checkpoint |
| **A-002** | Unwinder Frame Capture | Experimental | Port/adapt from runtimelab `async2-experiment` |
| **A-003** | Tasklet/Frame Structure | Experimental | Adapt or design custom structure |
| **A-004** | Generics Support in Frame Capture | Missing | Implement (engineering gap) |
| **A-005** | Exception Handling Across Frames | Missing | Implement hybrid EH walker |
| **A-006** | Byref Preservation | In Unwinder | Verify/adapt for DOTNExT |
| **A-007** | Frame Restoration | In Unwinder | Verify/adapt for DOTNExT |

### 2.3 .NET 9 Baseline

**What .NET 9 HAS:**
- GC safe points (method call sites, loop back-edges, returns)
- GC Info tables (reference locations at safe points)
- Unwind Info (frame layout for stack walking)
- Profiler API hooks for observation

**What .NET 9 LACKS:**
- Unified safe points serving GC + preemption + checkpoint
- Production-ready frame capture to heap objects
- Generics/EH support in frame capture
- Preemption at safe points (only GC triggers)

### 2.4 Design Considerations

**A-001: Unified Safe Points**

```cpp
// Current .NET 9: GC-only polling
void SafePointPoll_Current()
{
    if (g_GCSuspendPending)
        GCYield();
}

// DOTNExT: Unified polling
void SafePointPoll_DOTNExT()
{
    uint32_t flags = tls_safepoint_flags;
    if (flags != 0)
    {
        if (flags & GC_SUSPEND)      GCYield();
        if (flags & CHECKPOINT)       CaptureCheckpoint();
        if (flags & REDUCTION_YIELD)  PreemptiveYield();
    }
}
```

**A-002: Unwinder Techniques**

Source: `dotnet/runtimelab` branch `feature/async2-experiment`

Key mechanisms to study/adapt:
- Stack unwinding into heap-allocated Tasklet objects
- Method token + IP offset + locals preservation
- GC integration for reference tracking
- Chain linking for full call stack capture

**A-004/A-005: Engineering Gaps**

The Unwinder experiment lacks:
- **Generics**: Must capture/restore generic dictionary pointers
- **Exception Handling**: Need hybrid EH walker for real frames + Tasklet chains

These are engineering challenges, not architectural impossibilities.

### 2.5 Shared Dependencies

```
A-001 (Unified Safe Points)
    ├── Required by: A-002, A-003, C-003, C-004
    └── Foundation for: All safe point-based features

A-002/A-003 (Frame Capture)
    ├── Required by: B-001, B-002, C-005
    └── Foundation for: Engram execution layer, Process Image
```

---

## 3. Cluster B: Engram Infrastructure

### 3.1 Vision

Engrams are bounded extractions from a larger computation graph - portable units containing code, state, and optionally execution context. The runtime must support efficient extraction, persistence, transmission, and hydration.

### 3.2 R&D Items

| ID | Item | .NET 9 Status | Delta Required |
|----|------|---------------|----------------|
| **B-001** | Engram Extraction (Objects) | Missing | GC-powered object graph traversal |
| **B-002** | Engram Extraction (Execution) | Missing | Frame capture integration |
| **B-003** | Engram Serialization Format | Missing | Design portable format |
| **B-004** | Engram Boundary Definition | Missing | API for specifying boundaries |
| **B-005** | Engram Hydration (Objects) | Missing | Graph reconstruction with address translation |
| **B-006** | Engram Hydration (Execution) | Missing | Frame restoration and resume |
| **B-007** | Cross-Node Engram Protocol | Missing | Transfer protocol design |
| **B-008** | Engram Versioning | Missing | Type version handling |

### 3.3 .NET 9 Baseline

**What .NET 9 HAS:**
- CGCDesc (object reference layout per type)
- GC heap walking capability
- Serialization infrastructure (but not GC-powered)
- Handle tables for reference types

**What .NET 9 LACKS:**
- GC-powered graph extraction with boundaries
- Execution state inclusion in serialization
- Cross-node transfer protocol
- Version-aware deserialization

### 3.4 The Five Engram Layers

```
┌────────────────────────────────────────────────────────────────┐
│  TOPOLOGY LAYER                                                 │
│  Where things live in distributed space                         │
│  - Node locations, placement decisions, remote references       │
├────────────────────────────────────────────────────────────────┤
│  OBJECTS LAYER                                                  │
│  Instance state and references                                  │
│  - Field values, UUIDs (for VCOM objects), reference graph      │
├────────────────────────────────────────────────────────────────┤
│  EXECUTION LAYER                                                │
│  Current execution state                                        │
│  - Stack frames (Tasklets), continuation points, locals         │
├────────────────────────────────────────────────────────────────┤
│  BINARIES LAYER (Cache)                                         │
│  Compiled code for execution                                    │
│  - JITted native code, cached per-platform                      │
├────────────────────────────────────────────────────────────────┤
│  CODE/TYPES LAYER (Primary)                                     │
│  Type definitions, source code                                  │
│  - C# source (primary artifact), type metadata, versions        │
└────────────────────────────────────────────────────────────────┘
```

### 3.5 Design Considerations

**B-001: GC-Powered Extraction**

```csharp
// Conceptual API
var engram = Engram.Extract(
    roots: new[] { myObject },
    boundary: EngramBoundary.ReachableWithDepth(3),
    includeExecution: false
);
```

**Key insight**: GC already knows the complete object graph. We leverage CGCDesc and GC heap walking, not custom tracking.

**B-004: Boundary Definition**

| Boundary Type | Description |
|---------------|-------------|
| **Depth-limited** | Include objects up to N hops from root |
| **Type-filtered** | Include/exclude by type |
| **UUID-bounded** | Stop at VCOM objects (external references) |
| **Custom predicate** | Application-defined logic |

### 3.6 Shared Dependencies

```
B-001/B-002 (Engram Extraction)
    ├── Depends on: A-002 (Frame Capture), E-001 (UUID)
    └── Required by: C-005 (Process Image)

B-005/B-006 (Engram Hydration)
    ├── Depends on: A-007 (Frame Restoration)
    └── Required by: C-006 (Process Restore)
```

---

## 4. Cluster C: Process/Pathway Execution Model

### 4.1 Vision

A new execution model where:
- **Processes** are isolation boundaries with identity
- **Pathways** are execution flows (the scheduling unit)
- **Everything yields by default** (`sync` is the exception)
- **BEAM-like preemption** via reduction counting
- **Process Image Persistence** enables checkpoint/restore/migrate

### 4.2 R&D Items

| ID | Item | .NET 9 Status | Delta Required |
|----|------|---------------|----------------|
| **C-001** | Process Identity System | Missing | UUID, name, capabilities |
| **C-002** | Pathway Identity System | Missing | UUID, state, scheduling info |
| **C-003** | sync Keyword (JIT) | Missing | JIT recognizes sync, no yield points |
| **C-004** | Reduction Counting | Missing | JIT emits decrement at safe points |
| **C-005** | Process Image Capture | Missing | Full state serialization |
| **C-006** | Process Image Restore | Missing | State reconstruction and resume |
| **C-007** | Pathway Scheduler | Missing | Custom scheduler with queues |
| **C-008** | Process State Machine | Missing | Created→Running→Suspended→etc. |
| **C-009** | Pathway Supervision | Missing | Fault handling, restart strategies |

### 4.3 .NET 9 Baseline

**What .NET 9 HAS:**
- Thread pool and TaskScheduler
- ExecutionContext flow
- async/await state machines (Roslyn-generated)
- No concept of "process" within the runtime

**What .NET 9 LACKS:**
- Process/Pathway abstraction layer
- sync keyword
- Reduction counting / preemptive yielding
- Checkpoint/restore capability
- Supervision strategies

### 4.4 Semantic Inversion: sync is the Exception

```
Traditional .NET:              DOTNExT:
───────────────────────────────────────────────────────────────
void Foo() = synchronous       void Foo() = can yield at any safe point
async Task Foo() = may yield   [redundant - everything may yield]
No keyword for sync            sync void Foo() = NEVER yields
```

**sync Keyword Semantics:**

```csharp
// Declaration-site: Method NEVER yields
sync void AtomicOperation()
{
    // Guaranteed: no yields, no preemption, no checkpoints
}

// Call-site: Execute entire call tree without yields
var result = sync SomeMethod();
// Creates "sync scope" - transitive through all calls
```

### 4.5 Process States

```
Created → Running → Suspending → Suspended → Checkpointed
                                                  ↓
                                    ┌─────────────┼─────────────┐
                                    ↓             ↓             ↓
                               Persisted     Migrating     Hibernated
                                    ↓             ↓
                               [storage]    Resumed (on target node)
                                              (Running)
```

### 4.6 Shared Dependencies

```
C-003/C-004 (sync + Reduction)
    ├── Depends on: A-001 (Unified Safe Points)
    └── Required by: C-007 (Scheduler)

C-005/C-006 (Process Image)
    ├── Depends on: A-002 (Frame Capture), B-001 (Engram Extraction)
    └── Foundation for: Migration, Persistence
```

---

## 5. Cluster D: Security & Isolation

### 5.1 Vision

Security is a pluggable driver system with interception points throughout the runtime. Even if Gen-1 security is minimal, the **hook points must exist** to avoid retrofitting later.

### 5.2 R&D Items

| ID | Item | .NET 9 Status | Delta Required |
|----|------|---------------|----------------|
| **D-001** | Security Interception Points | Partial (CAS removed) | Define hook architecture |
| **D-002** | Security Driver Interface | Missing | Pluggable driver contract |
| **D-003** | Compile-time Hooks | Missing | Roslyn emits security markers |
| **D-004** | JIT-time Hooks | Missing | Security preamble insertion |
| **D-005** | VTable Interception | Missing | Security wrapper for dispatch |
| **D-006** | Capability Model | Missing | Process/Pathway capabilities |
| **D-007** | Logical Isolation (VCOM) | Partial | Actor-model enforcement |

### 5.3 .NET 9 Baseline

**What .NET 9 HAS:**
- CAS was removed (considered failed model)
- Assembly-level security attributes (limited)
- OS-level process isolation

**What .NET 9 LACKS:**
- Pluggable security driver system
- Fine-grained interception points
- Capability-based model within runtime
- Intra-process isolation

### 5.4 Security Interception Points

| Point | When | Hook Opportunity |
|-------|------|------------------|
| **Compile-time** | Roslyn emits IL | Mark security requirements in metadata |
| **Load-time** | Assembly/Type loading | Vet before allowing load |
| **JIT-time** | Method compilation | Add security preamble/checks |
| **Call-time** | Method dispatch | VTable wrapper interception |
| **Field-time** | Field access | Property wrapper or JIT helper |
| **Reflection** | Dynamic operations | Intercept all dynamic access |

### 5.5 Gen-1 Principle: Hook Points Now, Enforcement Later

```
Gen-1: Hook points exist, drivers are no-ops (passthrough)
       ↓
Gen-2: CBS driver, RBAC driver, etc. implemented
       ↓
Gen-3: Security optimization (compile-time resolution where possible)
```

---

## 6. Cluster E: Memory System Extensions

### 6.1 Vision

Extend the memory system to support Engram concepts:
- UUID assignment for objects needing identity
- Relationship recording for reference writes
- Type awareness for Engram-enabled types
- JIT helpers for Engram operations

### 6.2 R&D Items

| ID | Item | .NET 9 Status | Delta Required |
|----|------|---------------|----------------|
| **E-001** | UUID Assignment (Lazy) | Missing | Side table, assigned on demand |
| **E-002** | UUID Lookup | Missing | Object → UUID resolution |
| **E-003** | Relationship Recording | Missing | Reference write interception |
| **E-004** | Type Engram Flags | Missing | MethodTable extension or attribute |
| **E-005** | JIT Helper: Field Assign | Missing | Relationship recording on write |
| **E-006** | JIT Helper: Object Create | Missing | UUID assignment hook |
| **E-007** | VM Intrinsics | Missing | System.Runtime.Engram namespace |

### 6.3 .NET 9 Baseline

**What .NET 9 HAS:**
- CGCDesc (reference field layout)
- Write barriers (for GC card marking)
- Handle tables (weak/strong/pinned)
- MethodTable (type metadata)
- JIT helper infrastructure

**What .NET 9 LACKS:**
- UUID tracking for objects
- Relationship graph recording
- Engram-aware type flags
- Engram-specific JIT helpers

### 6.4 Anti-Pattern Warning

> ❌ **Don't add UUID to every object header**
> Why: Affects billions of objects, unacceptable overhead

> ✅ **Use side table or lazy assignment**
> Zero cost for objects that never need UUID

### 6.5 Design Options

**E-001: UUID Assignment**

| Option | Approach | Trade-off |
|--------|----------|-----------|
| **A** | Every allocation gets UUID | Unacceptable overhead |
| **B** | Opt-in via `[Engram]` attribute | Requires type annotation |
| **C** | Lazy assignment on first need | Zero cost until needed (RECOMMENDED) |

**E-003: Relationship Recording**

| Option | Approach | Trade-off |
|--------|----------|-----------|
| **A** | Intercept write barrier | Every reference write pays cost |
| **B** | JIT helper for marked types | Only Engram types pay cost |
| **C** | Lazy recording on extraction | Zero cost until Engram extracted (RECOMMENDED for Gen-1) |

---

## 7. .NET 9 Baseline Assessment

### 7.1 Modularity Scores

| Component | Modularity | Interface | Can Replace? | Notes |
|-----------|------------|-----------|--------------|-------|
| **GC** | EXCELLENT | IGCHeap (v5.3), IGCToCLR (v2) | YES | Proven: Workstation/Server, Segments/Regions |
| **JIT** | GOOD | ICorJitCompiler | YES | Proven: RyuJIT, LLILC, multiple cross-compilers |
| **Type System** | POOR | None | NO - Fork Required | Deep integration, no clean interface |
| **VES/Threading** | POOR | None | NO - Fork Required | Deep integration |
| **Profiler** | EXCELLENT | ICorProfilerCallback | YES | Standard extension point |
| **Hosting** | GOOD | hostfxr API | YES | Standard |

### 7.2 Key Extension Points

| Extension Point | Location | Use Case |
|-----------------|----------|----------|
| **Profiler API** | `src/coreclr/inc/corprof.idl` | Hook object creation, observe GC, IL rewriting |
| **GC Interface** | `src/coreclr/gc/gcinterface.h` | Leverage reference tracking, add handle types |
| **Type System** | `src/coreclr/vm/class.cpp` | Extend MethodTable for UUID, add flags |
| **JIT Helpers** | `src/coreclr/inc/jithelpers.h` | Add ENGRAM_FIELD_ASSIGN, ENGRAM_NEW |
| **VM Intrinsics** | `src/coreclr/vm/ecalllist.h` | System.Runtime.CompilerServices.Engram |

### 7.3 What Can Be Reused vs Must Be Built

**REUSE (extend existing):**
- GC safe points → extend for unified safe points
- GC Info tables → use for execution capture
- CGCDesc → use for object extraction
- Unwind Info → use for frame restoration
- Write barriers → hook for relationship recording
- Handle tables → extend with Engram handle types

**BUILD (new systems):**
- Process/Pathway abstraction
- Engram extraction/hydration
- UUID management
- sync keyword support
- Pathway scheduler
- Security driver system

### 7.4 Unwinder Experiment Analysis

From `dotnet/runtimelab` branch `feature/async2-experiment`:

| Capability | Status | Implication |
|------------|--------|-------------|
| Frame → Heap capture | ✅ Works | Proves execution state can be reified |
| Byref preservation | ✅ Works | Real stack semantics survive |
| Chain linking | ✅ Works | Full call stacks capturable |
| GC integration | ✅ Works | Memory safety preserved |
| Generics | ❌ Missing | Engineering gap - must implement |
| Exception handling | ❌ Missing | Engineering gap - must implement |

**Key Decision**: DOTNExT stays on .NET 9 because the Unwinder experiment is .NET 9 based.

---

## 8. Cross-Cluster Dependencies

### 8.1 Dependency Graph

```
                          A-001 (Unified Safe Points)
                                     │
               ┌─────────────────────┼─────────────────────┐
               │                     │                     │
               ▼                     ▼                     ▼
        A-002 (Frame Capture)  C-003 (sync)         C-004 (Reduction)
               │                     │                     │
               │                     └─────────┬───────────┘
               │                               │
               ▼                               ▼
        B-001/B-002               C-007 (Pathway Scheduler)
        (Engram Extraction)
               │
               ▼
        C-005/C-006 (Process Image)
               │
               ▼
        Migration, Persistence, AI Control
```

### 8.2 Foundation Items (Must Complete First)

| Priority | Item | Why Foundation |
|----------|------|----------------|
| **1** | A-001: Unified Safe Points | Everything depends on safe point unification |
| **2** | A-002: Frame Capture | Engrams and Process Images need this |
| **3** | E-001: UUID Assignment | Object identity for Engrams |
| **4** | C-001/C-002: Identity Systems | Process/Pathway need identity before features |
| **5** | D-001: Security Hook Points | Must exist early even if no-ops |

### 8.3 Synergies from Coherent Design

| If Designed Together | Synergy Gained |
|----------------------|----------------|
| A-001 + C-003 + C-004 | Single JIT modification serves all three |
| B-001 + B-002 | Unified extraction API for objects + execution |
| E-001 + E-003 + E-004 | Consistent type metadata approach |
| D-001 through D-005 | Coherent interception architecture |

---

## 9. Implementation Phases

### Phase 1: Safe Points Foundation

| Item | Description |
|------|-------------|
| A-001 | Implement unified safe point polling |
| A-002 (partial) | Study and prototype frame capture |
| E-004 | Design Engram type flags approach |
| D-001 | Define security hook point architecture |

**Deliverable:** Runtime with unified safe points and frame capture prototype

### Phase 2: Execution Capture

| Item | Description |
|------|-------------|
| A-002 (complete) | Full frame capture implementation |
| A-003 | Tasklet/Frame structure finalized |
| A-006/A-007 | Byref preservation and restoration |
| C-001/C-002 | Process/Pathway identity systems |

**Deliverable:** Working execution state capture and identity systems

### Phase 3: Engram Infrastructure

| Item | Description |
|------|-------------|
| B-001/B-002 | Engram extraction |
| B-005/B-006 | Engram hydration |
| E-001/E-002 | UUID assignment and lookup |
| B-003/B-004 | Serialization format and boundaries |

**Deliverable:** Working Engram extraction/hydration

### Phase 4: Process Model

| Item | Description |
|------|-------------|
| C-003 | sync keyword in JIT |
| C-004 | Reduction counting |
| C-005/C-006 | Process Image capture/restore |
| C-007 | Pathway scheduler |
| C-008 | Process state machine |

**Deliverable:** BEAM-like execution model working

### Phase 5: Engineering Gaps & Polish

| Item | Description |
|------|-------------|
| A-004 | Generics support in frame capture |
| A-005 | Exception handling across frames |
| C-009 | Supervision strategies |
| D-002 through D-007 | Security driver implementation |
| E-003/E-005/E-006 | Relationship recording and JIT helpers |

**Deliverable:** Production-quality runtime features

*Note: No time estimates provided - work with AI assistance makes traditional time estimates unreliable.*

---

## 10. Open Architectural Questions

### 10.1 Execution Model Questions

| Question | Options | Decision Needed |
|----------|---------|-----------------|
| When is a Pathway created? | Explicit / Task-aligned / Grain-aligned | Phase 2 |
| What's the scheduling model? | OS threads / Thread pool / Custom N:M | Phase 4 |
| How is reduction budget set? | Fixed / Adaptive / Per-pathway config | Phase 4 |
| Does DOTNExT create own frame structure? | Use Tasklet / Design custom | Phase 2 |

### 10.2 Engram Questions

| Question | Options | Decision Needed |
|----------|---------|-----------------|
| What serialization format? | Custom binary / Protobuf / MessagePack | Phase 3 |
| How are boundaries specified? | Depth / Type / Predicate / Multiple | Phase 3 |
| How are external references handled? | Proxy / UUID marker / Fail | Phase 3 |
| How are type versions handled? | Reject / Migrate / Best-effort | Phase 5 |

### 10.3 Security Questions

| Question | Options | Decision Needed |
|----------|---------|-----------------|
| When are security checks performed? | JIT-time / Runtime / Both | Phase 5 |
| How granular are capabilities? | Method / Type / Assembly / Namespace | Phase 5 |
| How are capabilities propagated? | Inherit / Explicit / Policy | Phase 5 |

### 10.4 Memory Questions

| Question | Options | Decision Needed |
|----------|---------|-----------------|
| How are UUIDs stored? | Side table / MethodTable extension / Object flag | Phase 2 |
| When is relationship recorded? | Write-time / Extraction-time | Phase 3 |
| Per-process GC regions? | Yes / No / Future research | Later |

---

## Summary: The R&D Landscape

### By Cluster

| Cluster | Items | Status |
|---------|-------|--------|
| A: Universal Execution Capture | 7 | Foundation |
| B: Engram Infrastructure | 8 | Core Feature |
| C: Process/Pathway Model | 9 | Core Feature |
| D: Security & Isolation | 7 | Required for Gen-1 hooks |
| E: Memory Extensions | 7 | Supporting Infrastructure |

### By Priority

**Must Have (Gen-1):**
- Unified Safe Points (A-001)
- Frame Capture basics (A-002, A-003, A-006, A-007)
- Engram Extraction/Hydration (B-001, B-002, B-005, B-006)
- Process/Pathway Identity (C-001, C-002)
- Security Hook Points (D-001)
- UUID Assignment (E-001)

**Should Have (Gen-1):**
- sync keyword (C-003)
- Process Image (C-005, C-006)
- Reduction counting (C-004)
- Pathway scheduler (C-007)

**Can Defer:**
- Generics in frames (A-004)
- EH in frames (A-005)
- Supervision (C-009)
- Security drivers (D-002-D-007)
- Relationship recording (E-003)

---

*This document provides a comprehensive map of runtime R&D required for DOTNExT. It should be used alongside the R&D Item Inventory (03-RND-ITEM-INVENTORY.md) and Consolidated Vision (02-CONSOLIDATED-VISION.md) for planning and execution.*

*Version 1.0 - 2025-12-14*
