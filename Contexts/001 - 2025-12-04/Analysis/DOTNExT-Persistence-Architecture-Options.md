# DOTNExT Persistence Architecture - Options & Relationships

> **Document Type:** Architecture Options Analysis
> **Version:** 1.0
> **Date:** 2025-12-08
> **Status:** RESEARCH - Mapping the design space
> **Context:** Synthesizing Runtime-Async, Engrams, Process Image, and Pathways

---

## 1. Purpose

This document maps out the **architectural options** for DOTNExT's persistence and distribution capabilities. Multiple concepts have emerged that need to be related:

- **Primitives:** GC heap walk, Tasklets, Safe Points, GC Info
- **Engrams:** Bounded graph extractions
- **Process Image:** Complete process checkpoint
- **Execution Pathways:** Trackable execution contexts
- **VCOM:** Distributed object identity

The goal is to understand how these **relate to each other** and what **architectural choices** we have.

---

## 2. The Primitives Layer

### 2.1 What We Have

These are the **building blocks** that everything else is built from:

```
┌─────────────────────────────────────────────────────────────────┐
│  PRIMITIVES (Infrastructure)                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  FROM GC:                                                       │
│  ├── GC Heap Walk - enumerate all objects                       │
│  ├── CGCDesc - reference field layout per type                  │
│  ├── GC Roots - static fields, stack, handles                   │
│  └── Object Graph - reachable set from any root                 │
│                                                                 │
│  FROM JIT:                                                      │
│  ├── GC Info - reference locations at safe points               │
│  ├── Unwind Info - frame layout for reconstruction              │
│  └── Safe Points - consistent state locations                   │
│                                                                 │
│  FROM RUNTIME-ASYNC:                                            │
│  ├── Tasklets - captured stack frames                           │
│  ├── Tasklet Chains - full call stacks                          │
│  └── Suspend/Resume - execution state management                │
│                                                                 │
│  FROM TYPE SYSTEM:                                              │
│  ├── MethodTable - type identity                                │
│  ├── Assembly metadata - code identity                          │
│  └── Reflection - runtime type inspection                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Key Insight

**All the primitives already exist.** We're not creating new runtime capabilities - we're **combining and packaging** existing ones.

---

## 3. Architectural Options

### 3.1 Option A: Flat Architecture

Everything built directly on primitives, no intermediate concepts:

```
┌─────────────────────────────────────────────────────────────────┐
│  OPTION A: Flat Architecture                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│              ┌─────────────┐                                    │
│              │  Primitives │                                    │
│              └──────┬──────┘                                    │
│       ┌─────────────┼─────────────┐                            │
│       ▼             ▼             ▼                             │
│  ┌─────────┐  ┌───────────┐  ┌─────────┐                       │
│  │ Object  │  │ Execution │  │ Process │                       │
│  │ Persist │  │ Persist   │  │ Image   │                       │
│  └─────────┘  └───────────┘  └─────────┘                       │
│                                                                 │
│  Pros: Simple, direct                                           │
│  Cons: No reuse, concepts not unified                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Option B: Engrams as Foundation

Engram is the fundamental unit; everything is composed of Engrams:

```
┌─────────────────────────────────────────────────────────────────┐
│  OPTION B: Engrams as Foundation                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│              ┌─────────────┐                                    │
│              │  Primitives │                                    │
│              └──────┬──────┘                                    │
│                     │                                           │
│                     ▼                                           │
│              ┌─────────────┐                                    │
│              │   ENGRAM    │  ◄── The fundamental unit          │
│              └──────┬──────┘                                    │
│       ┌─────────────┼─────────────┐                            │
│       ▼             ▼             ▼                             │
│  ┌─────────┐  ┌───────────┐  ┌─────────┐                       │
│  │ Object  │  │ Execution │  │ Process │                       │
│  │ Engram  │  │ Engram    │  │ Image   │                       │
│  └─────────┘  └───────────┘  │(Engrams)│                       │
│                              └─────────┘                       │
│                                                                 │
│  Pros: Unified model, composable                                │
│  Cons: Engram abstraction may not fit all cases                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.3 Option C: Layered Architecture

Clear layers with defined responsibilities:

```
┌─────────────────────────────────────────────────────────────────┐
│  OPTION C: Layered Architecture                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  APPLICATIONS                                            │   │
│  │  Process Image | Migration | Async+ | Distributed Exec  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  CONCEPTS                                                │   │
│  │  Engrams (bounded graphs) | Pathways (exec contexts)    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  PRIMITIVES                                              │   │
│  │  GC Walk | Tasklets | Safe Points | Type System          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Pros: Clear separation, each layer well-defined               │
│  Cons: More abstraction, potential overhead                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.4 Option D: VCOM-Centric

VCOM is central; Engrams serve VCOM:

```
┌─────────────────────────────────────────────────────────────────┐
│  OPTION D: VCOM-Centric                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                     ┌──────────────┐                           │
│                     │     VCOM     │  ◄── Central concept       │
│                     │ (distributed │                           │
│                     │   objects)   │                           │
│                     └──────┬───────┘                           │
│              ┌─────────────┼─────────────┐                     │
│              ▼             ▼             ▼                      │
│        ┌──────────┐  ┌──────────┐  ┌──────────┐               │
│        │ Engrams  │  │ Pathways │  │  Process │               │
│        │(VCOM     │  │(VCOM     │  │  Image   │               │
│        │ graphs)  │  │ execution│  │ (backup) │               │
│        └──────────┘  └──────────┘  └──────────┘               │
│              │             │             │                      │
│              └─────────────┴─────────────┘                     │
│                            │                                    │
│                     ┌──────┴───────┐                           │
│                     │  Primitives  │                           │
│                     └──────────────┘                           │
│                                                                 │
│  Pros: Unified around distributed identity                      │
│  Cons: Everything must be VCOM? (constraint)                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.5 Option E: Dual Track

VCOM objects and non-VCOM objects handled differently:

```
┌─────────────────────────────────────────────────────────────────┐
│  OPTION E: Dual Track                                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│        VCOM Track                    Non-VCOM Track            │
│        (distributed)                 (local/captured)          │
│                                                                 │
│     ┌──────────────┐              ┌──────────────┐             │
│     │ VCOM Objects │              │ Regular Objs │             │
│     │ (UUID, grain)│              │ (no UUID)    │             │
│     └──────┬───────┘              └──────┬───────┘             │
│            │                             │                      │
│            ▼                             ▼                      │
│     ┌──────────────┐              ┌──────────────┐             │
│     │ Distributed  │              │ Engram       │             │
│     │ Resolution   │              │ Capture      │             │
│     └──────────────┘              └──────────────┘             │
│            │                             │                      │
│            └──────────────┬──────────────┘                     │
│                           │                                     │
│                    ┌──────┴───────┐                            │
│                    │   Combined   │                            │
│                    │   Engram     │                            │
│                    └──────────────┘                            │
│                                                                 │
│  Pros: Best of both - VCOM for distributed, Engram for local   │
│  Cons: Two models to understand                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Relationship Matrix

### 4.1 How Concepts Relate

| Concept | Built From | Produces | Used By |
|---------|------------|----------|---------|
| **Primitives** | CLR internals | Raw capture capability | Everything |
| **Engram** | Primitives | Bounded graph package | Storage, Transfer, Process Image |
| **Pathway** | Tasklets + Engrams | Execution context | Scheduling, Migration |
| **Process Image** | Engrams or Primitives | Full checkpoint | Hibernation, Migration |
| **VCOM** | NewOrleans + Engrams? | Distributed objects | Application layer |

### 4.2 Dependency Options

**DECISION NEEDED:** What depends on what?

| Dependency | Option A | Option B | Option C |
|------------|----------|----------|----------|
| Engram → Primitives | ✅ | ✅ | ✅ |
| Process Image → Engrams | ❌ | ✅ | Maybe |
| Pathways → Engrams | ❌ | ✅ | ✅ |
| VCOM → Engrams | ❌ | Maybe | Separate |
| Engram → VCOM | ❌ | ❌ | ❌ (independent) |

---

## 5. Process Image Composition Options

### 5.1 Option A: Monolithic

Process Image is a single large capture, not composed of Engrams:

```
Process Image = {
    Header,
    Type Table,
    Object Table (all objects),
    Execution Table (all Tasklets),
    Static Table,
    ...
}
```

**Pros:** Simpler, optimized for whole-process
**Cons:** No reuse of Engram machinery

### 5.2 Option B: Engram Collection

Process Image is a collection of Engrams:

```
Process Image = {
    Static Roots Engram,
    Thread 1 Execution Engram,
    Thread 2 Execution Engram,
    ...
    Shared Objects Engram,
    Assembly Engram,
}
```

**Pros:** Reuses Engram infrastructure, incremental possible
**Cons:** More complex, potential duplication

### 5.3 Option C: Single Root Engram

Process Image is one Engram with roots = everything:

```
Process Image = Engram(roots: all_static_roots + all_tasklets)
// Just a very large Engram
```

**Pros:** Unified model
**Cons:** Large single unit, no granularity

### 5.4 Option D: Hierarchical Engrams

Engrams can contain Engrams:

```
Process Image = Engram {
    children: [
        Engram { type: "statics", ... },
        Engram { type: "execution", thread: 1, ... },
        Engram { type: "execution", thread: 2, ... },
    ],
    shared_refs: ...
}
```

**Pros:** Composable, hierarchical
**Cons:** Complex structure

---

## 6. Async+ Implementation Options

### 6.1 Option A: Roslyn Codegen (Original)

Modify Roslyn's async state machine generation:

```
async Task Foo() { ... }
         │
         ▼ Roslyn transforms
State machine with:
  - UUID fields for VCOM refs
  - Serialization support
  - Resume logic
```

**Pros:** Works with current runtime
**Cons:** Complex Roslyn modifications, lossy capture

### 6.2 Option B: Runtime-Async + Engrams

Use Tasklets + Engram extraction:

```
async Task Foo() { ... }
         │
         ▼ Runtime-Async
Tasklet captures complete frame
         │
         ▼ Engram
Extract Tasklet + referenced objects
```

**Pros:** Complete capture, cleaner
**Cons:** Requires .NET 10+, Runtime-Async integration

### 6.3 Option C: Hybrid

Roslyn for some aspects, Runtime-Async for others:

```
Roslyn: Mark methods, add hooks
Runtime-Async: Do actual capture
Engram: Package for persistence
```

**Pros:** Gradual adoption
**Cons:** Two systems to maintain

---

## 7. Distribution Options

### 7.1 Where Do Engrams Live?

**DECISION NEEDED:** Storage and distribution model?

| Option | Storage | Distribution | Trade-off |
|--------|---------|--------------|-----------|
| **Local Only** | Local files/memory | None | Simple, not distributed |
| **Centralized** | Single database | All nodes query | Simple, bottleneck |
| **Replicated** | Each node has copy | Sync on change | Consistent, sync overhead |
| **Distributed** | Sharded across nodes | Route to owner | Scalable, complex |
| **Content-Addressed** | Hash-based storage | Deduplicated | Immutable-friendly |
| **VCOM-Backed** | NewOrleans/RavenDB | Grain resolution | Integrated with VCOM |

### 7.2 How Do Engrams Move?

| Mechanism | Description | Use Case |
|-----------|-------------|----------|
| **Push** | Source sends to destination | Known recipient |
| **Pull** | Destination fetches from source | On-demand |
| **Store-and-Forward** | Via intermediate storage | Async transfer |
| **Stream** | Incremental transfer | Large Engrams |
| **VNS Resolution** | Find via name system | Discovery |

---

## 8. The VCOM Question

### 8.1 VCOM Objects in Engrams

**DECISION NEEDED:** How are VCOM objects handled?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A: Transparent** | VCOM objects captured like any other | Simple, but loses distribution semantics |
| **B: UUID Reference** | VCOM objects stored as UUID, resolved on load | Maintains identity, requires resolution |
| **C: Inline + UUID** | Capture state AND store UUID | Redundant but complete |
| **D: Choice Per Object** | Configurable | Flexible but complex |

### 8.2 Non-VCOM Objects Across Nodes

If objects aren't VCOM, how do they cross nodes?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Always Inline** | Non-VCOM always captured in Engram | Complete, but may duplicate |
| **Promote to VCOM** | Automatically make distributed objects VCOM | Unified, but overhead |
| **Ephemeral ID** | Temporary ID within transfer context | Lightweight, not persistent |
| **Error** | Don't allow non-VCOM across nodes | Strict, may be too limiting |

---

## 9. The Semantic Layer Question

### 9.1 Where Do Embeddings Live?

**DECISION NEEDED:** Semantic embeddings relationship to Engrams?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **In Engram** | Embeddings stored with objects | Self-contained, larger |
| **Separate Index** | Vector DB alongside Engram store | Lighter, two systems |
| **Computed** | Generate on load | Smaller storage, compute cost |
| **Layer** | Optional Engram layer | Flexible |

### 9.2 Graph Database Integration

How do Engrams map to graph storage (Neo4j)?

```
Engram                          Neo4j
───────                         ──────
Object                    →     Node
  - UUID                        - id property
  - fields                      - properties
  - type                        - labels

Reference                 →     Relationship
  - source                      - start node
  - target                      - end node
  - field name                  - type

Engram boundary           →     Subgraph query
```

---

## 10. Summary of Key Decisions Needed

### Architecture Level
1. **Flat vs Layered vs VCOM-Centric?**
2. **Is Engram the fundamental unit or one of several?**
3. **Does Process Image compose from Engrams?**

### Engram Design
4. **What layers must Engrams include?**
5. **How are objects identified (UUID, ordinal, hash)?**
6. **How are external references handled?**

### VCOM Integration
7. **How are VCOM objects handled in Engrams?**
8. **Can non-VCOM objects be distributed?**
9. **Does VCOM depend on Engrams or vice versa?**

### Distribution
10. **Where are Engrams stored?**
11. **How do Engrams move between nodes?**
12. **How does VNS relate to Engram storage?**

### Async+
13. **Roslyn codegen or Runtime-Async based?**
14. **How does Async+ use Engrams?**

### Semantics
15. **Where do embeddings live?**
16. **How do Engrams map to graph storage?**

---

## 11. Recommendation: Start Exploring

Given the number of open questions, I recommend:

1. **Don't commit to architecture yet** - More exploration needed
2. **Prototype Engram extraction** - Using GC primitives, validate feasibility
3. **Prototype Tasklet serialization** - Understand Runtime-Async limits
4. **Experiment with both** - VCOM and non-VCOM handling
5. **Let usage drive design** - Build Async+ or Process Image, see what Engram shape emerges

The architecture should **emerge from implementation experience**, not be designed in isolation.

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Engrams-Revised.md | Engram concept and options |
| DOTNExT-Execution-Pathways.md | Pathway concept using Engrams |
| DOTNExT-Runtime-Async-Research.md | Tasklet primitives |
| DOTNExT-Process-Image-Persistence.md | Process Image design |
| DOTNExT-Unified-SafePoints.md | Safe point infrastructure |
| Vision-Engrams-Cyberspace-Verbatim.md | Vision for distributed cyberspace |

---

*This document maps the architectural design space for DOTNExT persistence. The goal is clarity on options, not premature decisions.*

*Version 1.0 - 2025-12-08*
