# DOTNExT Engrams - Revised Conceptualization

> **Document Type:** Technical Research & Design Options
> **Version:** 2.0
> **Date:** 2025-12-08
> **Status:** RESEARCH - Options documented, decisions pending
> **Supersedes:** archived/Engram-Design-v0.1.md (conceptually, not replaced)
> **Context:** Emerged from Runtime-Async research session

---

## 1. Executive Summary

This document revises and expands the Engram concept in light of discoveries about .NET Runtime-Async, GC infrastructure, and unified safe points. The original Engram design (v0.1) required type annotations and runtime hooks. The revised understanding leverages existing infrastructure to enable Engrams **without requiring all objects to be specially marked**.

**Key Revision:** Engrams are now understood as "bounded extractions from a larger graph" - a definition independent of implementation mechanism. The GC and Runtime-Async infrastructure provides the primitives; Engrams are semantic packaging of those primitives.

---

## 2. Engram Definition (Crystallized)

### 2.1 What Is An Engram?

**Engram = A bounded extraction from a larger graph**

An Engram is:
- A **subgraph** with explicit boundaries
- **Multi-layered** (code, execution, objects, topology)
- **Self-describing** enough to be loaded elsewhere
- **Boundary-aware** (knows what's inside vs. external)

The boundary can be:
- Tight: A single object and its immediate references
- Wide: An entire object graph reachable from roots
- Complete: An entire process state

### 2.2 What An Engram Is NOT

- NOT just serialization (carries semantic structure)
- NOT just a snapshot (has identity, relations, layers)
- NOT tied to specific types (any managed object can be engram'd)
- NOT requiring source modification (uses GC infrastructure)

---

## 3. The Layered Dimensions Model

An Engram consists of overlaid layers mapping the same "territory":

```
┌─────────────────────────────────────────────────────────────────┐
│  ENGRAM LAYERS                                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  LAYER: CODE/TYPES                                       │   │
│  │  ├── Type definitions (metadata, schema)                 │   │
│  │  ├── Source code (if code-as-first-class)               │   │
│  │  ├── Type relations (inheritance, implements, uses)      │   │
│  │  └── Semantic annotations (embeddings, tags)            │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  LAYER: BINARIES                                         │   │
│  │  ├── Compiled IL                                         │   │
│  │  ├── JIT'd native code (cached, optional)               │   │
│  │  ├── Assembly references                                 │   │
│  │  └── Version information                                 │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  LAYER: EXECUTION                                        │   │
│  │  ├── Tasklet chains (captured pathways)                  │   │
│  │  ├── Frame states (locals, parameters)                   │   │
│  │  ├── Instruction pointers                                │   │
│  │  ├── Register values                                     │   │
│  │  └── Pathway identity (if assigned)                      │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  LAYER: OBJECTS                                          │   │
│  │  ├── Instance identity (UUID or local ID)                │   │
│  │  ├── Field values (value types inline)                   │   │
│  │  ├── References (typed edges to other objects)           │   │
│  │  ├── Object metadata                                     │   │
│  │  └── Semantic embeddings (optional)                      │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  LAYER: TOPOLOGY (for distributed Engrams)               │   │
│  │  ├── Origin node                                         │   │
│  │  ├── Active locations                                    │   │
│  │  ├── Redundancy information                              │   │
│  │  ├── Domain/federation membership                        │   │
│  │  └── VNS position                                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  All layers overlay the SAME conceptual territory              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.1 Layer Inclusion Options

**DECISION NEEDED:** Which layers must an Engram always include?

| Option | Layers Included | Trade-off |
|--------|-----------------|-----------|
| **Minimal** | Objects only | Small, but needs external type resolution |
| **Self-Contained** | Objects + Types + Binaries | Larger, but loadable anywhere |
| **Executable** | Objects + Types + Binaries + Execution | Can resume computation |
| **Full** | All layers | Complete representation |
| **Configurable** | Per-extraction choice | Flexible but complex |

---

## 4. Infrastructure Foundation

### 4.1 What Primitives Enable Engrams?

The Runtime-Async research revealed infrastructure we can leverage:

| Primitive | What It Provides | Source |
|-----------|------------------|--------|
| **GC Heap Walk** | Enumerate all objects | GC already does this |
| **CGCDesc** | Reference field locations per type | GC metadata |
| **GC Info** | Reference locations at safe points | JIT-emitted |
| **Tasklets** | Captured execution frames | Runtime-Async |
| **Unwind Info** | Frame layout for reconstruction | JIT-emitted |
| **Safe Points** | Consistent state for capture | GC/JIT |

### 4.2 How Engrams Use Primitives

```
┌─────────────────────────────────────────────────────────────────┐
│  Engram Extraction Flow                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. IDENTIFY ROOTS                                              │
│     - Explicit objects provided by caller                       │
│     - OR: Tasklet references (for execution engrams)            │
│     - OR: All roots (for process engrams)                       │
│                                                                 │
│  2. WALK GRAPH (using GC primitives)                            │
│     - Use CGCDesc to find reference fields                      │
│     - Follow references, marking visited                        │
│     - Classify: internal (in extraction) vs external            │
│                                                                 │
│  3. CAPTURE STATE                                               │
│     - For each object: serialize fields                         │
│     - For Tasklets: capture frame data, IP, registers           │
│     - Assign IDs (local ordinal or UUID)                        │
│                                                                 │
│  4. RECORD RELATIONS                                            │
│     - Reference edges become typed relations                    │
│     - External refs recorded with type hints                    │
│                                                                 │
│  5. GATHER TYPES (if self-contained)                            │
│     - Collect type metadata for all objects                     │
│     - Include assembly references or embed                      │
│                                                                 │
│  6. PACKAGE                                                     │
│     - Header (Engram identity, origin, timestamp)               │
│     - Type table                                                │
│     - Object table                                              │
│     - Relation table                                            │
│     - Execution table (if applicable)                           │
│     - External reference table                                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Engram vs VCOM: Relationship Options

### 5.1 The Original Assumption (Challenged)

The `Vision-Async+-Solution.md` assumed:
> All references must be VCOM objects to enable UUID-based resolution

**Problem:** This requires EVERYTHING to be grain-backed. System types, third-party libs, simple DTOs - all would need VCOM wrapping.

### 5.2 Revised Understanding

With GC-powered Engrams:
- ANY object can be captured (GC knows about all of them)
- VCOM objects have persistent identity (UUID, globally resolvable)
- Non-VCOM objects get local identity within Engram context
- Mixed graphs work: VCOM by UUID, non-VCOM captured inline

### 5.3 Relationship Options

**DECISION NEEDED:** How do Engrams and VCOM relate?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A: Engrams Independent** | Engrams work without VCOM; VCOM is just another object type | Maximum flexibility, but no global identity for non-VCOM |
| **B: VCOM-Enhanced Engrams** | Engrams detect VCOM objects, store UUID; others stored inline | Best of both worlds, some complexity |
| **C: Engrams ARE VCOM** | Every Engram is itself a VCOM object with UUID | Engrams become first-class distributed entities |
| **D: Layered** | Engram is the capture format; VCOM provides distribution | Clean separation of concerns |

### 5.4 External Reference Handling

When an Engram has references to objects NOT included:

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Null** | Set to null on load | Acceptable data loss |
| **Lazy Proxy** | Create proxy, resolve on access | Deferred resolution |
| **Fetch** | Immediately fetch from source | Complete restoration |
| **VCOM Resolve** | Use VCOM.Resolve(uuid) if VCOM object | Distributed resolution |
| **Error** | Fail if external refs exist | Strict self-containment |

---

## 6. Engram Identity Options

### 6.1 Does An Engram Have Identity?

**DECISION NEEDED:** Should Engrams themselves have UUID identity?

| Option | Description | Implications |
|--------|-------------|--------------|
| **No Identity** | Engrams are just data packages | Simpler, but no way to reference an Engram |
| **Content Hash** | Identity derived from content | Immutable, content-addressable |
| **Assigned UUID** | UUID assigned at creation | Mutable Engrams possible, can reference |
| **Composite** | Origin + Timestamp + Hash | Traceable lineage |

### 6.2 Object Identity Within Engrams

**DECISION NEEDED:** How are objects identified within an Engram?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Local Ordinal** | Object 0, 1, 2... | Compact, no global meaning |
| **UUID Always** | Every object gets UUID | Globally referenceable, overhead |
| **VCOM UUID + Local** | VCOM objects keep UUID, others get ordinal | Hybrid |
| **Content Hash** | Identity from content | Deduplication possible |

---

## 7. Execution Engrams

### 7.1 Concept

An "Execution Engram" captures not just objects but **computation in progress**:

```
┌─────────────────────────────────────────────────────────────────┐
│  Execution Engram                                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  TASKLET CHAIN                                                  │
│  ├── Tasklet 1: Method A, IP=0x42, locals=[x, y, z]            │
│  ├── Tasklet 2: Method B, IP=0x18, locals=[a, b]               │
│  └── Tasklet 3: Method C, IP=0x99, locals=[p]                  │
│                                                                 │
│  REFERENCED OBJECTS                                             │
│  ├── Object graph reachable from Tasklet locals                 │
│  └── Captured as standard Engram                                │
│                                                                 │
│  EXECUTION METADATA                                             │
│  ├── Pathway identity (if tracking execution flows)             │
│  ├── Scheduling state                                           │
│  └── Context (ExecutionContext, SynchronizationContext)         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 Execution Engram Options

**DECISION NEEDED:** How do Execution Engrams relate to Object Engrams?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Separate Types** | ExecutionEngram vs ObjectEngram | Clear distinction, different handling |
| **Unified** | All Engrams can optionally contain execution | Simpler model, mixed content |
| **Compositional** | ExecutionEngram contains ObjectEngrams | Layered, reusable |

---

## 8. Boundary Determination

### 8.1 What Defines An Engram's Boundary?

**DECISION NEEDED:** How is the extraction boundary determined?

| Option | Description | Use Case |
|--------|-------------|----------|
| **Explicit Roots** | Caller specifies root objects | Manual control |
| **Depth Limit** | Follow references up to N levels | Size control |
| **Type Filter** | Include/exclude by type | Domain boundaries |
| **Predicate** | Custom function decides | Maximum flexibility |
| **Closure** | Everything reachable from roots | Complete graphs |
| **Domain Boundary** | Stop at VCOM/federation boundaries | Distributed-aware |

### 8.2 Boundary Classification

Objects at the boundary can be:

| Classification | Meaning | In Engram |
|----------------|---------|-----------|
| **Internal** | Object is inside the Engram | Fully captured |
| **External** | Object is outside, referenced | UUID/hint stored |
| **Boundary** | Object is the edge | Could go either way |
| **Root** | Extraction starting point | Marked specially |

---

## 9. Process Image as Engram

### 9.1 Is A Process Image Just A Big Engram?

**DECISION NEEDED:** Relationship between Process Image and Engram?

| Option | Description | Implications |
|--------|-------------|--------------|
| **Process Image = Giant Engram** | Same format, roots = everything | Unified model |
| **Process Image = Collection of Engrams** | Multiple Engrams composed | Modular, incremental |
| **Different Concepts** | Process Image has different structure | Optimized for different use |
| **Process Image CONTAINS Engrams** | Engrams are building blocks | Compositional |

### 9.2 If Compositional, What Are The Pieces?

```
Process Image (Option: Collection of Engrams)
├── Static Roots Engram (all static fields)
├── Thread 1 Execution Engram
├── Thread 2 Execution Engram
├── Shared Object Engram (objects referenced by multiple)
└── Assembly Engram (code/types)
```

---

## 10. Distributed Engrams

### 10.1 Engrams Across Nodes

When Engrams span distributed systems:

```
┌─────────────────────────────────────────────────────────────────┐
│  Distributed Engram Scenarios                                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  SCENARIO A: Local Extraction, Remote Load                      │
│  - Extract on Node A                                            │
│  - Transfer (wire, store, etc.)                                 │
│  - Load on Node B                                               │
│  - External refs: resolved via VCOM or fetched                  │
│                                                                 │
│  SCENARIO B: Cross-Node Composition                             │
│  - Objects from Node A                                          │
│  - Execution from Node B                                        │
│  - Types from Node C                                            │
│  - Composed into single Engram locally                          │
│                                                                 │
│  SCENARIO C: Distributed Storage                                │
│  - Engram stored in distributed store (Neo4j cluster)           │
│  - Nodes load portions as needed                                │
│  - Sparse at distance, dense locally                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 10.2 Topology Layer Questions

**DECISIONS NEEDED:**

1. Does every Engram track its origin node?
2. How are cross-node references resolved?
3. What's the relationship between Engram storage and VNS?
4. How does an Engram know where its external refs live?

---

## 11. Semantic Integration

### 11.1 Engrams in the Semantic Memory System

If Engrams are the storage/transfer unit for the semantic graph:

```
┌─────────────────────────────────────────────────────────────────┐
│  Engram in Semantic Memory                                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  STORAGE (Neo4j + Vector)                                       │
│  ├── Each object → Node with properties                         │
│  ├── Each relation → Edge with type + weight                    │
│  ├── Each embedding → Vector property on node/edge              │
│  └── Engram boundary → Subgraph query                           │
│                                                                 │
│  QUERIES                                                        │
│  ├── Classical: "Find Order with ID X"                          │
│  │   → Returns Engram containing Order + relations              │
│  ├── Semantic: "Find orders similar to this pattern"            │
│  │   → Returns Engram with matching subgraph                    │
│  ├── Graph: "Find all objects 2 hops from Customer Y"           │
│  │   → Returns Engram with traversal results                    │
│                                                                 │
│  UPDATES                                                        │
│  ├── Load Engram → Merge into graph (by UUID)                   │
│  ├── Extract Engram → Query subgraph, package                   │
│  └── Sync → Diff Engrams, reconcile                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 11.2 Semantic Layer Options

**DECISION NEEDED:** Where do semantic embeddings live?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **In Engram** | Embeddings stored with objects | Self-contained, larger |
| **External Index** | Embeddings in separate vector store | Lighter Engrams, requires index |
| **Computed On Load** | Generate embeddings when loading | Smaller storage, compute cost |
| **Layered** | Embeddings are a separate layer | Can include or not |

---

## 12. Address Translation

### 12.1 The Problem

Objects have runtime addresses. These are meaningless after extraction.

```
At extraction:    Object A @ 0x1000, A.ref → 0x2000 (Object B)
After loading:    Object A @ 0x5000, A.ref must → 0x6000 (Object B)
```

### 12.2 Translation Strategies

| Strategy | How It Works | Trade-off |
|----------|--------------|-----------|
| **ID Table** | Address → ID at extract; ID → Address at load | Simple, requires fixup pass |
| **Relative Offsets** | Store as offset from Engram base | Fast, but fragile |
| **UUID Everywhere** | All refs stored as UUID | No translation, lookup overhead |
| **Hybrid** | Internal by ordinal, external by UUID | Balance |

### 12.3 Reference Fixup Flow

```
EXTRACT:
  Object A @ 0x1000 → Engram Object ID 1
  Object B @ 0x2000 → Engram Object ID 2
  A.field → 0x2000 becomes A.field → @2 (internal ref to ID 2)

LOAD:
  Allocate A @ 0x5000, record: ID 1 → 0x5000
  Allocate B @ 0x6000, record: ID 2 → 0x6000
  Fixup: A.field @2 → lookup ID 2 → 0x6000
```

---

## 13. Engram Format Options

### 13.1 Binary Format

**DECISION NEEDED:** What binary format for Engrams?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Custom Binary** | Purpose-built format | Optimized, but custom tooling |
| **MessagePack** | Efficient binary JSON-like | Good tooling, reasonable size |
| **Protocol Buffers** | Schema-based binary | Strong typing, versioning |
| **BSON** | Binary JSON (MongoDB) | Document-friendly |
| **Flat Structure** | Arrays + indices | Very fast, less flexible |

### 13.2 Schema Evolution

**DECISION NEEDED:** How to handle type changes between extract and load?

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **Strict** | Types must match exactly | Simple, but brittle |
| **Field IDs** | Fields identified by ID not name | Rename-safe |
| **Schema Registry** | Central schema versioning | Managed evolution |
| **Duck Typing** | Load what matches, ignore rest | Flexible, lossy |

---

## 14. Implementation Phases

### Phase 1: GC-Powered Object Engrams
- [ ] Object graph extraction using GC primitives
- [ ] Simple binary format
- [ ] Local ID assignment
- [ ] Reference fixup on load
- [ ] No execution state yet

### Phase 2: Execution Engrams
- [ ] Tasklet serialization
- [ ] Execution + Object combined
- [ ] Resume capability

### Phase 3: VCOM Integration
- [ ] VCOM object detection
- [ ] UUID preservation
- [ ] External ref resolution via VCOM

### Phase 4: Distributed Engrams
- [ ] Cross-node extraction
- [ ] Network transfer
- [ ] Topology tracking

### Phase 5: Semantic Layer
- [ ] Embedding storage
- [ ] Semantic queries
- [ ] Graph database integration

---

## 15. Open Questions Summary

### Identity
1. Do Engrams have UUID identity?
2. How are objects identified within Engrams?
3. How do VCOM UUIDs interact with Engram IDs?

### Structure
4. Which layers are mandatory vs optional?
5. Are Execution Engrams a separate type or unified?
6. Is Process Image made of Engrams or a different structure?

### Boundaries
7. How is extraction boundary determined?
8. How are external references handled?
9. What about circular references crossing boundaries?

### Distribution
10. How do Engrams move between nodes?
11. How is origin/location tracked?
12. How does VNS relate to Engram storage?

### Semantics
13. Where do embeddings live?
14. How are Engrams queried semantically?
15. How does the graph database store Engrams?

### Format
16. What binary format?
17. How is schema evolution handled?
18. How are addresses translated?

---

## 16. Related Documents

| Document | Relationship |
|----------|--------------|
| Vision-Engrams-Cyberspace-Verbatim.md | Louis's vision for distributed cyberspace |
| DOTNExT-Runtime-Async-Research.md | Tasklet mechanism enabling Execution Engrams |
| DOTNExT-Process-Image-Persistence.md | Process Image as whole-process Engram case |
| DOTNExT-Unified-SafePoints.md | Safe point infrastructure for consistent capture |
| archived/Engram-Design-v0.1.md | Original design (type-annotation based) |

---

*This document captures the revised Engram concept with all design options and open questions. Decisions should be made deliberately, not by default.*

*Version 2.0 - 2025-12-08*
