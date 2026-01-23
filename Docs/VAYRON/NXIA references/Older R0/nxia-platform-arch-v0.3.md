# NXIA — Platform Overview & Architecture (v3.0)

> **Date:** 2025-12-28  
> **Status:** Consolidated design document  
> **Scope:** Vision, architecture, and compositional memory model for the NXIA runtime-kernel substrate  
> **Major Change from v2.0:** Memory Classes are now presets over a compositional algebra  
> **Companion Documents:**  
> - NXIA Memory Architecture Specification v0.2 (pending update)  
> - NXIA Implementation Roadmap v0.2 (pending update)  
> - NXIA Strategic Position v0.1  
> - NXIA Design Evolution v0.2 (pending update)  
> - NXIA Compositional Memory v0.1  

---

## What Changed in v3.0

| Aspect | v2.0 | v3.0 |
|--------|------|------|
| Memory Classes | Four discrete categories (Native, Managed, Capability, Memantic) | Presets over a compositional algebra |
| Primitives | Memory Classes are primitives | Orthogonal axes are primitives; classes are derived |
| Flexibility | Choose one of four bundles | Select any valid combination across ~12 axes |
| Extension | New class = new category | New axis option = expanded space |

**This is not a rejection but a generalization.** The four Memory Classes remain as convenient presets—sensible defaults for common use cases. The compositional substrate enables everything in between and beyond.

---

## Part I: Vision & Thesis

### 1. The Software Operating System

NXIA is a **Software Operating System**—a runtime-kernel substrate that virtualizes software infrastructure the way traditional operating systems virtualize hardware.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    The Analogy That Defines NXIA                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  HARDWARE OS (what it accomplished):                                    │
│  ───────────────────────────────────────────────────────────────────    │
│  Before: Programs managed physical memory, talked to hardware directly  │
│  After:  OS virtualizes CPU, RAM, disk, network                         │
│  Result: Programs focus on their purpose, not hardware details          │
│                                                                         │
│  SOFTWARE OS (what NXIA accomplishes):                                  │
│  ───────────────────────────────────────────────────────────────────    │
│  Before: Apps rebuild identity, serialization, caching, security        │
│  After:  NXIA virtualizes objects, state, relations, execution          │
│  Result: Components focus on their purpose, not infrastructure          │
│                                                                         │
│  The key insight: Make primitives ONCE, at the substrate level          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

**The structural mapping:**

| Hardware OS | Software OS (NXIA) |
|-------------|-------------------|
| Virtual memory (address spaces) | Universal objects (OID everywhere) |
| Processes/threads | Pathways (capturable, resumable) |
| Files (named, permissioned) | Engram layers (content-addressed, versioned) |
| Page faults | Fault-in (layers, objects, code, grants) |
| Filesystem | VNS (Virtual Naming System) |
| File permissions | VSS capabilities (object/member/relation level) |
| Device drivers | Memory Device Drivers (MDD) |

### 2. The Problem NXIA Solves

Modern software suffers from **semantic debt**: the accumulated cost of expressing the same concepts (identity, relations, security, persistence) differently in every layer.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    The 95% Tax                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Typical enterprise application:                                        │
│                                                                         │
│    Business Logic (what it actually does)              ~5%              │
│    ════════════════════════════════════════════════════════════         │
│                                                                         │
│    Infrastructure (rebuilding primitives)              ~95%             │
│    ════════════════════════════════════════════════════════════         │
│    │ Identity mapping (DB IDs ↔ cache keys ↔ API paths ↔ objects)      │
│    │ Serialization (ORM, JSON, Protobuf, cache format)                 │
│    │ Persistence (queries, transactions, migrations)                   │
│    │ Caching (population, invalidation, consistency)                   │
│    │ Security (scattered checks throughout code)                       │
│    │ Communication (APIs, queues, events)                              │
│    └────────────────────────────────────────────────────────────────    │
│                                                                         │
│  Each concern must handle every other concern → O(n²) complexity        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3. The NXIA Bargain

**Traditional:** O(n × m × k) integration complexity  
**NXIA:** O(n + m + k) substrate cost

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    The Bargain                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  TRADITIONAL (multiplicative tax):                                      │
│  ───────────────────────────────────────────────────────────────────    │
│  App → serialize → validate → transport → deserialize →                 │
│      → revalidate → cache → persist → index → replicate                 │
│                                                                         │
│  At EACH arrow: different identity, format, security, versioning        │
│                                                                         │
│  NXIA (additive cost):                                                  │
│  ───────────────────────────────────────────────────────────────────    │
│  Object in MMS → access (zero-copy) → mutate (COW, epoch-bounded) →     │
│               → persist (choose sector) → replicate (transfer Engram)   │
│                                                                         │
│  At EACH step: SAME identity, format, security, versioning              │
│  Cost is additive (each primitive paid once) not multiplicative         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4. The Core Insight: Compositional Primitives

**v3.0 introduces a fundamental refinement:** NXIA's power comes not from fixed categories but from **orthogonal, composable primitives**.

Previous versions described four Memory Classes (Native, Managed, Capability, Memantic) as the foundation. v3.0 reveals these as **presets**—convenient bundles over a richer compositional algebra.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    The Compositional Insight                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  PREVIOUS UNDERSTANDING (v2.0):                                         │
│  ───────────────────────────────────────────────────────────────────    │
│  Four Memory Classes as primitives:                                     │
│    Native → Managed → Capability → Memantic                             │
│                                                                         │
│  Problem: These bundle orthogonal concerns together                     │
│    - Want GC without OID? Can't.                                        │
│    - Want relations without semantic layer? Can't.                      │
│    - Want OID with manual lifetime? Can't.                              │
│                                                                         │
│  REFINED UNDERSTANDING (v3.0):                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  Orthogonal axes are primitives, classes are presets:                   │
│    Lifecycle × Identity × Mutability × Enforcement × Relations × ...    │
│                                                                         │
│  Memory Classes = validated points in this configuration space          │
│  Custom compositions = full access to the space                         │
│                                                                         │
│  NXIA's own principle fulfilled:                                        │
│  "Primitives should be orthogonal. Primitives should be minimal."       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part II: The Compositional Memory Model

### 5. The Buffet Metaphor

Imagine a buffet where each station offers options for a different aspect of memory behavior. You select one option from each station. Your selections form a **composition**—a complete specification of how memory should behave.

Some selections are free. Some are cheap. Some are expensive. Some require other selections (dependencies). Some preclude other selections (conflicts).

The four Memory Classes are like "combo meals"—pre-selected bundles that represent sensible, tested combinations. But you can also build your own plate.

### 6. The Twelve Axes

A **composition** is a validated selection across twelve orthogonal axes:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Categories vs. Composition                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  CATEGORICAL (v2.0):                                                    │
│  ───────────────────────────────────────────────────────────────────    │
│  Four boxes. Pick one. Accept everything in that box.                   │
│                                                                         │
│    ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│    │ Native   │  │ Managed  │  │Capability│  │ Memantic │              │
│    │ (minimal)│  │ (+GC,OID)│  │(+Security)│  │ (+Full)  │              │
│    └──────────┘  └──────────┘  └──────────┘  └──────────┘              │
│                                                                         │
│  COMPOSITIONAL (v3.0):                                                  │
│  ───────────────────────────────────────────────────────────────────    │
│  Twelve axes. Select options. Pay for what you use.                     │
│                                                                         │
│    Lifecycle:     [Manual|RefCounted|Tracing|Arena|Persistent|...]      │
│    Identity:      [None|Address|OID|ContentHash]                        │
│    Mutability:    [Immutable|COW|InPlace|AppendOnly]                    │
│    Versioning:    [None|Stamp|ContentAddressed]                         │
│    Enforcement:   [Raw|RuntimeChecked|CapabilityGated|PolicyEvaluated]  │
│    Observability: [None|AccessLogging|Provenance|FullAudit]             │
│    Relations:     [None|ForwardOnly|Bidirectional|FullIndexed]          │
│    Semantic:      [None|Embeddable|Queryable|AutoUpdating]              │
│    Concurrency:   [Unsynchronized|ReadSafe|Atomic|Transactional]        │
│    Layout:        [Default|Packed|Aligned|Custom]                       │
│    Durability:    [Ephemeral|Checkpointable|Durable|Replicated]         │
│    Distribution:  [Local|Migratable|LocationTransparent]                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7. Axis Reference

Each axis is documented with options, costs, and dependencies:

#### Axis 1: Lifecycle (How memory is reclaimed)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `Manual` | Explicit deallocation by caller | None | Discipline |
| `RefCounted` | Freed when reference count hits zero | Inc/dec per ref change | — |
| `RefCountedCyclic` | RefCount + cycle detection | Periodic cycle scan | — |
| `Tracing` | Garbage collected via reachability | Write barriers, pauses | Root registration |
| `TracingGenerational` | Generational GC | Write barriers, minor/major GC | Root registration |
| `Arena` | Freed in bulk with region | None until arena drop | Arena association |
| `Stack` | Freed at scope exit | None (compile-time) | Lexical scope |
| `Static` | Never freed (process lifetime) | None | — |
| `Persistent` | Survives process (sector-managed) | Sector sync | Durable sector |

#### Axis 2: Identity (How the object is referenced)

| Option | Description | Cost | Enables |
|--------|-------------|------|---------|
| `None` | Pure value, no stable reference | None | — |
| `Address` | Memory address is identity | None | Raw pointer access |
| `OID` | Stable 64-bit identifier | OID allocation, mapping | Relations, distribution, envelope |
| `ContentHash` | Hash of content is identity | Hash computation | Content-addressed versioning, dedup |

**Dependencies:** `OID` or `ContentHash` required for Relations and Distribution.

#### Axis 3: Mutability (How content changes)

| Option | Description | Cost | Implications |
|--------|-------------|------|--------------|
| `Immutable` | Never changes after creation | None | Can share freely, content-addressable |
| `COW` | Copy-on-write semantics | Copy on mutation | Preserves history, enables snapshots |
| `InPlace` | Direct mutation | None | Traditional mutable semantics |
| `AppendOnly` | Can extend, not modify existing | Depends on structure | Log-like structures |

**Dependencies:** `ContentHash` identity requires `Immutable` or `COW`.

#### Axis 4: Versioning (How history is tracked)

| Option | Description | Cost |
|--------|-------------|------|
| `None` | No version tracking | None |
| `Stamp` | Monotonic epoch + sequence | 8 bytes per object |
| `ContentAddressed` | Hash-based version identity | Hash computation |
| `FullHistory` | Complete change log | Unbounded storage |

#### Axis 5: Enforcement (How access is controlled)

| Option | Description | Cost |
|--------|-------------|------|
| `Raw` | No checks (trusted code only) | None |
| `RuntimeChecked` | Type/bounds checking | Per-operation validation |
| `CapabilityGated` | Token-based access | Capability lookup |
| `PolicyEvaluated` | Full policy evaluation | Policy engine invocation |

#### Axis 6: Observability (What operations are recorded)

| Option | Description | Cost |
|--------|-------------|------|
| `None` | No recording | None |
| `AccessLogging` | Log read/write operations | Log append per operation |
| `Provenance` | Track origin and derivation | Provenance chain maintenance |
| `FullAudit` | Complete audit trail | Full audit infrastructure |

#### Axis 7: Relations (How edges are maintained)

| Option | Description | Cost |
|--------|-------------|------|
| `None` | No relation tracking | None |
| `ForwardOnly` | Outgoing edges indexed | B+tree for outgoing |
| `Bidirectional` | Both directions indexed | B+trees for both |
| `FullIndexed` | All relation types queryable | Full RS integration |

**Dependencies:** Requires `OID` or `ContentHash` identity.

#### Axis 8: Semantic (AI/embedding capabilities)

| Option | Description | Cost |
|--------|-------------|------|
| `None` | No semantic layer | None |
| `Embeddable` | Can generate embeddings on demand | Embedding computation |
| `Queryable` | Indexed for semantic search | Vector index maintenance |
| `AutoUpdating` | Embeddings update on mutation | Continuous embedding updates |

#### Axis 9: Concurrency (Thread-safety guarantees)

| Option | Description | Cost |
|--------|-------------|------|
| `Unsynchronized` | Single-threaded access only | None |
| `ReadSafe` | Multiple readers, exclusive writer | RwLock overhead |
| `Atomic` | Lock-free atomic operations | Atomic instruction overhead |
| `Transactional` | Full transactional semantics | Transaction management |

#### Axis 10: Layout (Memory arrangement)

| Option | Description | Use Case |
|--------|-------------|----------|
| `Default` | Runtime-determined layout | General purpose |
| `Packed` | Minimize padding | Storage efficiency |
| `Aligned` | Cache-line alignment | Performance-critical |
| `Custom` | Application-specified | Interop, hardware |

#### Axis 11: Durability (Persistence characteristics)

| Option | Description | Cost |
|--------|-------------|------|
| `Ephemeral` | Process lifetime only | None |
| `Checkpointable` | Can be snapshotted | Snapshot overhead |
| `Durable` | Survives process restart | Persistent sector sync |
| `Replicated` | Multiple authoritative copies | Replication protocol |

#### Axis 12: Distribution (Location transparency)

| Option | Description | Cost |
|--------|-------------|------|
| `Local` | Single-node only | None |
| `Migratable` | Can move between nodes | Migration protocol |
| `LocationTransparent` | Anywhere in federation | Full distribution |

### 8. The Composition Algebra

Compositions aren't arbitrary—they must satisfy constraints:

**Validation Rules (formal notation):**
```
ContentHash        → requires (Immutable OR COW)
Relations.*        → requires (OID OR ContentHash)
Distribution.LocationTransparent → requires (OID OR ContentHash)
PolicyEvaluated    → requires OID
Semantic.Queryable → requires Relations.FullIndexed
Durable            → requires (Checkpointable OR stronger)
```

**Conflicts:**
```
Manual    ⊗ Tracing         (exactly one lifecycle)
None      ⊗ Relations.*     (can't index without identity)
Raw       ⊗ PolicyEvaluated (exactly one enforcement)
```

**Cost Model:**
```
Total Cost = Σ(axis_cost) + interaction_costs

Where interaction_costs accounts for:
  - GC write barriers × mutation frequency
  - Hash computation × modification rate
  - Index maintenance × relation density
```

**Validation:** Before a composition is used, the runtime validates that all constraints are satisfied. Invalid compositions are rejected at compile time or object creation time.

### 9. Memory Class Presets

The four Memory Classes are now validated presets—specific compositions for common use cases:

```rust
// Native: Maximum performance, minimal overhead, trusted code only
const NATIVE: Composition = Composition {
    lifecycle:     Manual,
    identity:      Address,
    mutability:    InPlace,
    versioning:    None,
    enforcement:   Raw,
    observability: None,
    relations:     None,
    semantic:      None,
    concurrency:   Unsynchronized,
    layout:        Default,
    durability:    Ephemeral,
    distribution:  Local,
};
// Cost: ~0 bytes overhead per object

// Managed: GC-managed, OID-identified, runtime-checked
const MANAGED: Composition = Composition {
    lifecycle:     TracingGenerational,
    identity:      OID,
    mutability:    InPlace,
    versioning:    Stamp,
    enforcement:   RuntimeChecked,
    observability: None,
    relations:     None,
    semantic:      None,
    concurrency:   Unsynchronized,
    layout:        Default,
    durability:    Ephemeral,
    distribution:  Local,
};
// Cost: ~64 bytes envelope per object

// Capability: Security-aware, COW, capability-gated
const CAPABILITY: Composition = Composition {
    lifecycle:     TracingGenerational,
    identity:      OID,
    mutability:    COW,
    versioning:    Stamp,
    enforcement:   CapabilityGated,
    observability: None,
    relations:     None,
    semantic:      None,
    concurrency:   ReadSafe,
    layout:        Default,
    durability:    Checkpointable,
    distribution:  Local,
};
// Cost: ~64 bytes envelope + capability checks

// Memantic: Full-featured, policy-evaluated, all capabilities
const MEMANTIC: Composition = Composition {
    lifecycle:     TracingGenerational,
    identity:      OID,
    mutability:    COW,
    versioning:    ContentAddressed,
    enforcement:   PolicyEvaluated,
    observability: Provenance,
    relations:     FullIndexed,
    semantic:      Queryable,
    concurrency:   ReadSafe,
    layout:        Default,
    durability:    Durable,
    distribution:  LocationTransparent,
};
// Cost: ~64+ bytes envelope + relation indexes + semantic embeddings
```

### 10. Novel Compositions

The compositional model enables configurations impossible with fixed classes:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Novel Compositions                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  HIGH-PERFORMANCE GRAPH NODE                                            │
│  ───────────────────────────────────────────────────────────────────    │
│  Lifecycle: Arena              Identity: OID                            │
│  Mutability: InPlace           Enforcement: Raw                         │
│  Relations: ForwardOnly        (everything else minimal)                │
│                                                                         │
│  Use case: Graph algorithms where all nodes freed together              │
│  Benefit: OID for edges + arena for bulk deallocation                   │
│  Not possible in v2.0: OID required Managed which required GC           │
│                                                                         │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                         │
│  PURE IMMUTABLE VALUE                                                   │
│  ───────────────────────────────────────────────────────────────────    │
│  Lifecycle: TracingGenerational  Identity: None                         │
│  Mutability: Immutable           Enforcement: RuntimeChecked            │
│                                                                         │
│  Use case: Functional programming values, no stable reference needed    │
│  Benefit: GC without OID allocation overhead                            │
│  Not possible in v2.0: Managed bundled GC with OID                      │
│                                                                         │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                         │
│  SECURE EPHEMERAL SECRET                                                │
│  ───────────────────────────────────────────────────────────────────    │
│  Lifecycle: TracingGenerational  Identity: OID                          │
│  Enforcement: PolicyEvaluated    Observability: AccessLogging           │
│  Durability: Ephemeral           (never persisted by design)            │
│                                                                         │
│  Use case: Cryptographic keys, session tokens                           │
│  Benefit: Full security without persistence risk                        │
│  Not possible in v2.0: PolicyEvaluated implied Memantic implied Durable │
│                                                                         │
│  ─────────────────────────────────────────────────────────────────────  │
│                                                                         │
│  CONTENT-ADDRESSED IMMUTABLE DATA                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  Lifecycle: TracingGenerational  Identity: ContentHash                  │
│  Mutability: Immutable           Versioning: ContentAddressed           │
│  Distribution: LocationTransparent                                      │
│                                                                         │
│  Use case: Unison-style code, IPFS-style content                        │
│  Benefit: Identity = hash enables global deduplication                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 11. The Native Butt Principle

A crucial invariant: **regardless of composition, everything is bytes in pages**.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    The Native Butt Principle                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Every object, regardless of its composition, sits on a substrate of    │
│  raw bytes in physical memory pages. The composition adds:              │
│                                                                         │
│    • Metadata (envelope, indexes, embeddings)                           │
│    • Enforcement (access checks, capability validation)                 │
│    • Behavior (GC tracing, relation indexing)                           │
│                                                                         │
│  But the raw data is always there:                                      │
│                                                                         │
│    ┌─────────────────────────────────────────────┐                      │
│    │  Semantic Layer (embeddings, meaning)       │  ← Optional          │
│    ├─────────────────────────────────────────────┤                      │
│    │  Relations Layer (edges, graph)             │  ← Optional          │
│    ├─────────────────────────────────────────────┤                      │
│    │  Security Layer (capabilities, policy)      │  ← Optional          │
│    ├─────────────────────────────────────────────┤                      │
│    │  Identity Layer (OID, envelope)             │  ← Optional          │
│    ├─────────────────────────────────────────────┤                      │
│    │  Lifecycle Layer (GC tracking)              │  ← Optional          │
│    ╞═════════════════════════════════════════════╡                      │
│    │  RAW BYTES IN PAGES                         │  ← Always present    │
│    └─────────────────────────────────────────────┘                      │
│                                                                         │
│  This enables:                                                          │
│                                                                         │
│  1. KERNEL RAW ACCESS                                                   │
│     The kernel can always access raw bytes, bypassing composition       │
│     enforcement. Essential for GC, persistence, debugging.              │
│                                                                         │
│  2. VIEW PROJECTIONS                                                    │
│     Same memory accessed through different composition "views."         │
│     A Memantic object's data readable through Native view by kernel.    │
│                                                                         │
│  3. COMPOSITION NARROWING                                               │
│     With authority, a richer composition can be temporarily narrowed.   │
│     Access Capability object through Managed view (kernel-only).        │
│                                                                         │
│  4. ZERO-COPY INTEROP                                                   │
│     Data doesn't move between compositions. Same bytes, different       │
│     enforcement layered on top.                                         │
│                                                                         │
│  Metaphor: A security checkpoint doesn't change what's in your bag.     │
│  It changes what access is permitted. The bag contents are invariant.   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 12. View Projections

The same memory can be accessed through different compositional lenses:

```rust
impl<T> Ref<T> {
    /// Project this reference through a different composition
    /// Requires authority to narrow or widen
    fn as_view(&self, target_comp: Composition) -> Result<View<T>> {
        let current = self.composition();
        
        // Validate projection is legal
        validate_projection(current, target_comp)?;
        
        // Check authority for narrowing (removing checks)
        if target_comp.is_narrower_than(current) {
            require_kernel_authority()?;
        }
        
        // Create view with different enforcement
        Ok(View::new(self.slot, target_comp))
    }
    
    /// Get raw pointer (kernel authority required)
    fn as_native_ptr(&self) -> Result<*mut T> {
        require_kernel_authority()?;
        Ok(self.slot.data_ptr())
    }
}

// Usage:
let obj = allocate::<MyType>(MEMANTIC);

// Different views of the same memory:
let raw_view = obj.as_view(NATIVE)?;      // Kernel authority required
let managed_view = obj.as_view(MANAGED)?; // Drops security, relations
let native_ptr = obj.as_native_ptr()?;    // Raw pointer, kernel only
```

---

## Part III: The Layer Model

### 13. Engrams and Layers

An **Engram** is the portable representation of an object. It consists of **layers**—decomposed slices of object state that can be independently stored, versioned, faulted, and transferred.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Engram Structure                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                         ENGRAM                                   │   │
│  │  OID: 0x7F3A_B2C1_D4E5_F678                                     │   │
│  ├─────────────────────────────────────────────────────────────────┤   │
│  │                                                                  │   │
│  │  Layer 0: ENVELOPE (always present for OID objects)             │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ OID, TypeRef, Composition, SecurityLabel, VersionStamp     │ │   │
│  │  │ Hash: 0xA1B2...                                            │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  Layer 1: STRUCTURAL (type-defined fields)                      │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ Field values, inline data, embedded references             │ │   │
│  │  │ Hash: 0xC3D4...                                            │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  Layer 2: RELATIONAL (edges to other objects)                   │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ Outgoing edges: [(type, target_oid), ...]                  │ │   │
│  │  │ Hash: 0xE5F6...                                            │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  Layer 3: SEMANTIC (AI-queryable meaning)                       │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ Embedding vector, concept tags, similarity index           │ │   │
│  │  │ Hash: 0x1728...                                            │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  Layer 4: PROVENANCE (history and lineage)                      │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ Creation context, modification history, causal chain       │ │   │
│  │  │ Hash: 0x3940...                                            │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  Layer 5+: EXTENSION (custom layers)                            │   │
│  │  ┌────────────────────────────────────────────────────────────┐ │   │
│  │  │ Application-defined data, plugin layers                    │ │   │
│  │  │ Hash: 0x...                                                │ │   │
│  │  └────────────────────────────────────────────────────────────┘ │   │
│  │                                                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 14. Layer Presence and Composition

An object's **composition** determines which layers are present:

| Composition Selection | Layer Implications |
|----------------------|-------------------|
| `Identity: OID` | Layer 0 (envelope) present |
| `Identity: None` | No envelope, no layers |
| `Relations: Bidirectional+` | Layer 2 (relational) present |
| `Semantic: Queryable+` | Layer 3 (semantic) present |
| `Observability: Provenance+` | Layer 4 (provenance) present |

**Preset layer profiles:**
- **Native:** No layers (no envelope, raw bytes only)
- **Managed:** Layer 0 + Layer 1
- **Capability:** Layer 0 + Layer 1
- **Memantic:** Layer 0 + Layer 1 + Layer 2 + Layer 3 + Layer 4

### 15. Content-Addressing

Every layer is identified by its **content hash**. This provides fundamental benefits:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Content-Addressing Benefits                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  DEDUPLICATION                                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  Object A Layer 1: Hash 0xABCD → stored once                            │
│  Object B Layer 1: Hash 0xABCD → same hash, reuses storage              │
│                                                                         │
│  VERSIONING                                                             │
│  ───────────────────────────────────────────────────────────────────    │
│  Object at T1: Layer 1 = 0xABCD                                         │
│  Object at T2: Layer 1 = 0xEF01 (modified)                              │
│  Diff: Just compare hashes. Identical layers = identical content.       │
│                                                                         │
│  DISTRIBUTION                                                           │
│  ───────────────────────────────────────────────────────────────────    │
│  Node A: "I need layer 0xABCD"                                          │
│  Node B: "I have 0xABCD" → transfers content                            │
│  Node A: Verifies hash matches, knows content is correct                │
│                                                                         │
│  CACHING                                                                │
│  ───────────────────────────────────────────────────────────────────    │
│  Cache key = layer hash                                                 │
│  Cache hit = content known to be correct (hash is identity)             │
│  No invalidation problem: hash changes when content changes             │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part IV: The Sector Authority Model

### 16. What Sectors Are

A **Sector** is a region of authority—a storage plane with specific characteristics and ownership.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Sector Types                                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  RAM SECTOR (volatile, fast)                                            │
│  ───────────────────────────────────────────────────────────────────    │
│  • Authoritative for ephemeral objects                                  │
│  • Fastest access, volatile                                             │
│  • Working set for active computation                                   │
│                                                                         │
│  PERSISTENT SECTOR (durable, crash-safe)                                │
│  ───────────────────────────────────────────────────────────────────    │
│  • Authoritative for durable objects                                    │
│  • Memory-mapped file, survives restart                                 │
│  • COW snapshots for crash consistency                                  │
│                                                                         │
│  GPU SECTOR (parallel, compute-optimized)                               │
│  ───────────────────────────────────────────────────────────────────    │
│  • Projection of graph structure for parallel queries                   │
│  • CSR format for efficient traversal                                   │
│  • Embeddings for semantic operations                                   │
│                                                                         │
│  REMOTE SECTOR (distributed, federated)                                 │
│  ───────────────────────────────────────────────────────────────────    │
│  • Objects on other nodes                                               │
│  • Authoritative at their home node                                     │
│  • Accessed via fault-in protocol                                       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 17. Authority vs. Cache

A critical distinction:

- **Authoritative sector:** The source of truth for an object. Mutations go here.
- **Cache:** A copy in another sector for performance. Read-only, may be stale.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Authority Model                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Object O with OID 0x1234:                                              │
│                                                                         │
│  Persistent Sector: AUTHORITATIVE                                       │
│  ├── Layer 0 (envelope): owned, latest                                 │
│  ├── Layer 1 (structural): owned, latest                               │
│  └── Layer 2 (relational): owned, latest                               │
│                                                                         │
│  RAM Sector: CACHE                                                      │
│  ├── Layer 0: cached, may be stale                                     │
│  └── Layer 1: cached, may be stale                                     │
│      (Layer 2 not cached—access goes to persistent)                    │
│                                                                         │
│  GPU Sector: DERIVED                                                    │
│  └── CSR projection of Layer 2                                         │
│      (not authoritative, rebuilt on change)                            │
│                                                                         │
│  On mutation:                                                           │
│  1. Write to authoritative sector (Persistent)                          │
│  2. Invalidate/update caches (RAM cache marked stale)                   │
│  3. Optionally propagate to derived (GPU rebuilt if needed)             │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part V: Fault-In as Universal Pattern

### 18. The Fault-In Abstraction

**Fault-in** is NXIA's universal mechanism for acquiring missing resources. When code accesses something not present, the runtime doesn't fail—it faults in what's needed.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Fault-In Taxonomy                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  LAYER FAULT                                                            │
│  ───────────────────────────────────────────────────────────────────    │
│  Trigger: Access object, required layer not in local sector             │
│  Resolution: Fetch layer from authoritative sector                      │
│  Example: Read semantic layer, fault from persistent to RAM             │
│                                                                         │
│  OBJECT FAULT                                                           │
│  ───────────────────────────────────────────────────────────────────    │
│  Trigger: Follow reference to OID, object not in local sector           │
│  Resolution: Fetch object (or at least envelope) from authority         │
│  Example: Traverse relation, target object at remote node               │
│                                                                         │
│  CODE FAULT                                                             │
│  ───────────────────────────────────────────────────────────────────    │
│  Trigger: Call method, implementation not present                       │
│  Resolution: Fetch and JIT-compile code                                 │
│  Example: Invoke pathway, code stored in persistent sector              │
│                                                                         │
│  CAPABILITY FAULT                                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  Trigger: Access requires capability not held                           │
│  Resolution: Request grant from authority, cache if granted             │
│  Example: Read member, need read capability for that member             │
│                                                                         │
│  TYPE FAULT                                                             │
│  ───────────────────────────────────────────────────────────────────    │
│  Trigger: Encounter object, type metadata not present                   │
│  Resolution: Fetch type definition from VTS                             │
│  Example: Deserialize object, need field layout                         │
│                                                                         │
│  The pattern: Missing X → trap → acquire X → resume                     │
│  Unified mechanism, different resources                                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 19. Fault-In and Distribution

Fault-in makes distribution transparent. Code written for local access works unchanged when objects are remote:

```rust
// This code works regardless of where customer is stored
let customer = get_object(customer_oid);  // May fault from remote sector
let orders = customer.orders();           // May fault relation layer
for order in orders {
    let total = order.total();            // May fault each order object
    process(total);
}
```

If `customer` is remote, the first access faults it in. If orders are at a third node, traversal faults them. The code doesn't change—fault-in handles it.

---

## Part VI: The Relation System

### 20. Relations as First-Class Primitives

NXIA treats object relationships as first-class, indexed, secured primitives—not embedded pointers.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Relation Model                                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  TRADITIONAL (embedded pointers):                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  class Customer {                                                       │
│      orders: List<Order>     // Forward reference, embedded             │
│  }                                                                      │
│  Problem: How do you find all customers for an order? Full scan.        │
│                                                                         │
│  NXIA (indexed relations):                                              │
│  ───────────────────────────────────────────────────────────────────    │
│  Customer --[HAS_ORDER]--> Order                                        │
│                                                                         │
│  Primary index: (Customer OID, HAS_ORDER) → [Order OIDs]               │
│  Reverse index: (Order OID, HAS_ORDER) → [Customer OIDs]               │
│                                                                         │
│  Both directions: O(log n) lookup via B+tree                            │
│                                                                         │
│  ADDITIONAL BENEFITS:                                                   │
│  ───────────────────────────────────────────────────────────────────    │
│  • Relations have their own security: capability to traverse edge      │
│  • Relations can have properties: edge metadata                        │
│  • Relations queryable: "find all X related to Y by Z"                 │
│  • Relations versioned: graph structure has history                    │
│  • Relations distributed: edges can span nodes                         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 21. Authority Model for Relations

Relations are **authoritative in Layer 2** (the object's relation layer), with B+tree indexes as derived acceleration structures:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Relation Authority                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Layer 2 (per-object relation data) is SOURCE OF TRUTH                  │
│  B+tree indexes are DERIVED, REBUILDABLE views                          │
│                                                                         │
│  Index Structure:                                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  Primary Index:   (Source OID, RelationType) → [Target OIDs]            │
│  Reverse Index:   (Target OID, RelationType) → [Source OIDs]            │
│                                                                         │
│  Index Maintenance:                                                     │
│  ───────────────────────────────────────────────────────────────────    │
│  • On relation add/remove: update indexes synchronously                 │
│  • On crash: rebuild indexes from Layer 2 data                          │
│  • Index = optimization, Layer 2 = truth                                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 22. Relation Queries

The Relation System (RS) provides query capabilities:

```rust
// Find all orders for a customer
rs.query(customer_oid, RelationType::HAS_ORDER, Direction::Forward)

// Find customer for an order (reverse traversal)
rs.query(order_oid, RelationType::HAS_ORDER, Direction::Reverse)

// Multi-hop: Customer → Orders → Products
rs.traverse(customer_oid, &[HAS_ORDER, CONTAINS_PRODUCT])

// Pattern match: Find paths between two objects
rs.find_paths(customer_oid, product_oid, max_depth: 3)

// Graph analytics (GPU-accelerated when available)
rs.shortest_path(source_oid, target_oid)
rs.connected_components(subgraph)
rs.pagerank(subgraph, iterations: 20)
```

---

## Part VII: The Execution Model

### 23. Pathways

A **Pathway** is NXIA's unit of execution—a lightweight, capturable, resumable computation.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Pathway Model                                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  PATHWAY = Execution context that can be:                               │
│  • Paused: Execution suspended, state captured                          │
│  • Captured: Serialized to Engram, persisted or transferred             │
│  • Resumed: Restored on same or different node                          │
│  • Forked: Cloned for parallel exploration                              │
│                                                                         │
│  KEY INSIGHT: Fault-in is generalized yield                             │
│  ───────────────────────────────────────────────────────────────────    │
│  When a pathway faults (missing layer, object, code, capability),       │
│  it yields. The scheduler:                                              │
│  1. Captures pathway state                                              │
│  2. Initiates acquisition of missing resource                           │
│  3. Schedules other pathways                                            │
│  4. Resumes original pathway when resource available                    │
│                                                                         │
│  This unifies:                                                          │
│  • Async I/O (fault for data → resume when ready)                      │
│  • Remote calls (fault for remote object → resume when fetched)        │
│  • Lazy loading (fault for code → resume when compiled)                │
│  • Security elevation (fault for capability → resume if granted)       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 24. Epochs

An **Epoch** is a coherence boundary—a point where changes become visible to other pathways.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Epoch Model                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Within an epoch:                                                       │
│  • Mutations are private to the mutating pathway                        │
│  • Other pathways see pre-epoch state (COW isolation)                  │
│  • No coordination overhead                                             │
│                                                                         │
│  At epoch boundary:                                                     │
│  • Changes become visible atomically                                    │
│  • Conflicts detected and resolved                                      │
│  • Snapshots taken if configured                                        │
│  • Subscribers notified of changes                                      │
│                                                                         │
│  EPOCH AS TRANSACTION GENERALIZATION:                                   │
│  ───────────────────────────────────────────────────────────────────    │
│  Traditional: BEGIN → operations → COMMIT/ROLLBACK                     │
│  NXIA: Epoch start → operations → Epoch publish                        │
│                                                                         │
│  The difference: Epochs are implicit, automatic, and efficient.         │
│  You don't manually manage transactions—the runtime does.               │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part VIII: The Security Model

### 25. Virtual Security System (VSS)

Security in NXIA is not scattered checks—it's enforced at memory boundaries through the **Virtual Security System**.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Security Enforcement Tiers                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  TIER 1: RAW (Enforcement: Raw)                                         │
│  ───────────────────────────────────────────────────────────────────    │
│  No enforcement. Direct memory access. Kernel code only.                │
│                                                                         │
│  TIER 2: RUNTIME CHECKED (Enforcement: RuntimeChecked)                  │
│  ───────────────────────────────────────────────────────────────────    │
│  Type safety enforced. Bounds checking. No capability checks.           │
│  Trusted code within a security domain.                                 │
│                                                                         │
│  TIER 3: CAPABILITY GATED (Enforcement: CapabilityGated)                │
│  ───────────────────────────────────────────────────────────────────    │
│  Access requires capability tokens. Capabilities are:                   │
│  • Object-level: Can access this object                                │
│  • Member-level: Can read/write this field                             │
│  • Relation-level: Can traverse this edge type                         │
│  Checked at access time, cached for performance.                       │
│                                                                         │
│  TIER 4: POLICY EVALUATED (Enforcement: PolicyEvaluated)                │
│  ───────────────────────────────────────────────────────────────────    │
│  Full policy evaluation per access. Policies can consider:              │
│  • Accessor identity and roles                                         │
│  • Object classification and labels                                    │
│  • Context (time, location, purpose)                                   │
│  • History (prior accesses, patterns)                                  │
│  Most flexible, most overhead.                                          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 26. Capability Attenuation

Capabilities flow through the system, attenuating as they're delegated:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Capability Attenuation                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Root capability: FULL_ACCESS to Customer object                        │
│  │                                                                      │
│  ├─→ Attenuate to: READ_ONLY for Service A                             │
│  │   ├─→ Further attenuate: READ_NAME_ONLY for Component X             │
│  │   └─→ Further attenuate: READ_ORDERS for Component Y                │
│  │                                                                      │
│  └─→ Attenuate to: WRITE_ADDRESS for Service B                         │
│      └─→ Cannot further attenuate to WRITE_NAME (wasn't granted)       │
│                                                                         │
│  Principle: Capabilities can only be reduced, never amplified           │
│  Delegation is safe: You can't grant more than you have                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part IX: Storage Architecture

### 27. Storage Hierarchy

The physical storage follows a segment → page → slot hierarchy:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Storage Hierarchy                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  SEGMENT (4 MB)                                                         │
│  ───────────────────────────────────────────────────────────────────    │
│  • Unit of OS allocation (mmap)                                        │
│  • Thread-local ownership for allocation fast path                     │
│  • Unit of COW snapshot                                                │
│  • Composition-homogeneous (all objects same composition)              │
│                                                                         │
│  PAGE (64 KB)                                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  • Unit of mapping/protection                                          │
│  • Unit of fault-in                                                    │
│  • Unit of sector binding                                              │
│  • Contains page header + object slots                                 │
│                                                                         │
│  OBJECT SLOT (variable)                                                │
│  ───────────────────────────────────────────────────────────────────    │
│  • Envelope (64 bytes, if composition includes OID)                   │
│  • Layer presence bitmap                                               │
│  • Inline layers (if present and small)                                │
│  • Layer references (if external)                                      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 28. The Universal Envelope

Objects with `Identity: OID` have a 64-byte envelope:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Universal Envelope (64 bytes)                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌────────────────────────────────────────────────────────────────┐    │
│  │ OID               (8 bytes)  - Universal identity              │    │
│  │ TypeRef           (8 bytes)  - VTS type reference              │    │
│  │ CompositionRef    (4 bytes)  - Composition descriptor          │    │
│  │ SecurityLabel     (4 bytes)  - VSS classification              │    │
│  │ ProvenanceRef     (8 bytes)  - Layer 4 reference               │    │
│  │ VersionStamp      (8 bytes)  - Epoch + sequence                │    │
│  │ RelationCount     (4 bytes)  - Edge count hint                 │    │
│  │ SlotSize          (4 bytes)  - Total slot bytes                │    │
│  │ LayerMask         (2 bytes)  - Which layers present            │    │
│  │ Flags             (2 bytes)  - Status flags                    │    │
│  │ Reserved          (4 bytes)  - Future use                      │    │
│  │ Checksum          (8 bytes)  - Integrity verification          │    │
│  └────────────────────────────────────────────────────────────────┘    │
│                                                                         │
│  Note: Objects with `Identity: None` or `Identity: Address` have       │
│  no envelope—they're raw bytes only.                                   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part X: Subsystem Summary

### 29. The Six Subsystems

NXIA consists of six integrated subsystems:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    NXIA Subsystems                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  MMS - Memantic Memory System                                           │
│  ───────────────────────────────────────────────────────────────────    │
│  Foundation: Compositional memory, segments, pages, envelopes, layers,  │
│  sectors, fault-in, COW, content-addressing.                           │
│                                                                         │
│  RS - Relational System                                                 │
│  ───────────────────────────────────────────────────────────────────    │
│  Graph engine: B+tree indexes over edges, bidirectional traversal,     │
│  graph queries, GPU-accelerated operations.                             │
│                                                                         │
│  VEE - Virtual Execution Engine                                         │
│  ───────────────────────────────────────────────────────────────────    │
│  Execution: Pathways, epochs, fault-in scheduling, Cranelift JIT,      │
│  capture/resume, preemptive scheduling.                                │
│                                                                         │
│  VTS - Virtual Type System                                              │
│  ───────────────────────────────────────────────────────────────────    │
│  Types: Cross-runtime type graph, structural subtyping, type metadata, │
│  layout computation, generic instantiation.                            │
│                                                                         │
│  VNS - Virtual Naming System                                            │
│  ───────────────────────────────────────────────────────────────────    │
│  Discovery: Namespace hierarchy, service registration, resolution,     │
│  federation across nodes.                                               │
│                                                                         │
│  VSS - Virtual Security System                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  Security: Capabilities, policies, enforcement at memory boundary,      │
│  audit logging, classification labels.                                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 30. Subsystem Integration

The subsystems are not independent—they integrate through shared primitives:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Subsystem Integration                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  SHARED PRIMITIVES:                                                     │
│  ───────────────────────────────────────────────────────────────────    │
│  • OID: Used by MMS, RS, VEE (pathway identity), VNS, VSS              │
│  • Layers: MMS stores, RS queries, VSS secures                         │
│  • Fault-in: MMS mechanism, VEE scheduler, RS lazy loading             │
│  • Epochs: MMS versioning, VEE coherence, RS transaction boundary      │
│  • Composition: MMS enforces, VTS describes, VSS gates                 │
│                                                                         │
│  INTEGRATION FLOW (example: secure graph query):                        │
│  ───────────────────────────────────────────────────────────────────    │
│  1. VEE: Pathway executes query                                        │
│  2. RS: Traverses relation index                                       │
│  3. MMS: Each target OID may fault-in                                  │
│  4. VSS: Each access checked against pathway's capabilities            │
│  5. VTS: Type metadata faulted for deserialization                     │
│  6. MMS: Results materialized in pathway's epoch                       │
│  7. VEE: Pathway continues with results                                │
│                                                                         │
│  All subsystems participate. None operates in isolation.                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part XI: Implementation Architecture

### 31. Composition Infrastructure

The runtime maintains composition metadata and handlers:

```rust
// Composition registration and validation
struct CompositionRegistry {
    compositions: HashMap<CompositionId, ValidatedComposition>,
    presets: HashMap<PresetName, CompositionId>,
}

impl CompositionRegistry {
    fn validate_and_register(&mut self, comp: Composition) -> Result<CompositionId> {
        // Check all constraints
        self.validate_dependencies(&comp)?;
        self.validate_conflicts(&comp)?;
        
        // Compute cost model
        let cost = self.compute_cost(&comp);
        
        // Register and return ID
        let id = self.next_id();
        self.compositions.insert(id, ValidatedComposition { comp, cost });
        Ok(id)
    }
}

// Axis handlers (generalization of Memory Class Drivers)
trait AxisHandler<A: Axis> {
    fn on_allocate(&self, slot: &mut Slot, option: A::Option);
    fn on_access(&self, slot: &Slot, option: A::Option) -> AccessResult;
    fn on_mutate(&self, slot: &mut Slot, option: A::Option) -> MutateResult;
    fn on_reclaim(&self, slot: &mut Slot, option: A::Option);
}

// Composition dispatch
fn access(oid: OID, layer: LayerId) -> Result<LayerData> {
    let slot = resolve_oid(oid)?;
    let comp = slot.composition();
    
    // Enforcement check (per composition)
    comp.enforcement_handler.check_access(slot, layer)?;
    
    // Observability (per composition)
    if comp.observability != ObservabilityOption::None {
        comp.observability_handler.log_access(slot, layer);
    }
    
    // Actual access (may fault-in)
    slot.get_layer(layer)
}
```

### 32. Segment Allocation

Segments are composition-homogeneous for efficiency:

```rust
fn allocate<T>(composition: Composition) -> Result<Ref<T>> {
    let comp_id = registry.validate_and_register(composition)?;
    
    // Find or create segment for this composition
    let segment = segment_cache
        .get_for_composition(comp_id)
        .or_else(|| allocate_segment(comp_id))?;
    
    // Allocate slot
    let slot = segment.allocate_slot(size_of::<T>())?;
    
    // Initialize per composition
    if composition.identity.requires_envelope() {
        slot.init_envelope(comp_id);
    }
    
    Ok(Ref::new(slot))
}
```

### 33. Language Selection

**Kernel core:** Rust
- Memory safety without GC (essential for memory substrate)
- Zero-cost abstractions for performance-critical paths
- Strong type system catches integration errors
- Excellent ecosystem for systems programming

**Runtime services:** C#
- Productive for higher-level services
- Target for self-hosting (VTS, VNS on NXIA itself)
- Rich ecosystem for business logic

**JIT compilation:** Cranelift (not LLVM)
- Designed for JIT (fast compilation, reasonable code)
- Rust-native, better integration
- Simpler, more predictable
- Good enough optimization for most code

---

## Part XII: Implementation Strategy

### 34. Implementation Phases

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Implementation Phases                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Phase 1: FOUNDATION (Weeks 1-8)                                        │
│  ───────────────────────────────────────────────────────────────────    │
│  • Composition algebra (validation, cost model, compatibility)          │
│  • Axis handler infrastructure                                          │
│  • Segments, pages, RAM sector, basic fault-in                         │
│  • Implement MANAGED preset as proof of concept                         │
│                                                                         │
│  Phase 2: PERSISTENCE (Weeks 9-16)                                      │
│  ───────────────────────────────────────────────────────────────────    │
│  • Durability axis implementation                                       │
│  • Persistent sector, COW mechanism, snapshots, crash recovery          │
│  • Content-addressed layers, hash computation                           │
│  • Implement NATIVE and CAPABILITY presets                              │
│                                                                         │
│  Phase 3: RELATIONS (Weeks 17-24)                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  • Relations axis implementation                                        │
│  • B+tree indexes, primary/reverse indexes, Layer 2 authority           │
│  • RS query engine                                                      │
│                                                                         │
│  Phase 4: EXECUTION (Weeks 25-32)                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  • Cranelift JIT                                                        │
│  • Pathways, fault-in mechanism, epochs                                 │
│  • Pathway capture/resume, preemptive scheduling                        │
│  • Implement MEMANTIC preset                                            │
│                                                                         │
│  Phase 5: SECURITY (Weeks 33-40)                                        │
│  ───────────────────────────────────────────────────────────────────    │
│  • Enforcement axis implementation                                      │
│  • Capability model, policy evaluation, enforcement tiers, audit        │
│  • View projections with authority                                      │
│                                                                         │
│  Phase 6: INTEGRATION (Weeks 41-48)                                     │
│  ───────────────────────────────────────────────────────────────────    │
│  • CLR runtime host                                                     │
│  • Self-hosting (VTS, VNS as NXIA services)                             │
│  • Semantic layer (Layer 3), provenance layer (Layer 4)                 │
│  • Custom composition API                                               │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 35. API Layers

Three levels of API for different users:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    API Layers                                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Level 1: PRESETS (most users)                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  [Composition(Presets.Memantic)]                                        │
│  public class Customer { ... }                                          │
│                                                                         │
│  Just pick a preset. Don't think about axes.                            │
│                                                                         │
│  Level 2: CUSTOM COMPOSITIONS (advanced users)                          │
│  ───────────────────────────────────────────────────────────────────    │
│  [Composition(Lifecycle = Arena, Identity = OID, Relations = Forward)]  │
│  public class GraphNode { ... }                                         │
│                                                                         │
│  Override specific axes. System validates and fills defaults.           │
│                                                                         │
│  Level 3: FULL ALGEBRA (framework builders)                             │
│  ───────────────────────────────────────────────────────────────────    │
│  let comp = CompositionBuilder::new()                                   │
│      .lifecycle(Arena(arena_id))                                        │
│      .identity(OID)                                                     │
│      .custom_axis(MyAxis, MyOption)                                     │
│      .build()?;                                                         │
│                                                                         │
│  Full programmatic control. Extension axes. Runtime composition.        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part XIII: Migration & Compatibility

### 36. From v2.0 to v3.0

Existing v2.0 code continues to work unchanged:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Migration Path                                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  v2.0 Code:                                                             │
│  ───────────────────────────────────────────────────────────────────    │
│  [MemoryClass(Managed)]                                                 │
│  public class MyType { ... }                                            │
│                                                                         │
│  v3.0 Interpretation:                                                   │
│  ───────────────────────────────────────────────────────────────────    │
│  [Composition(Presets.Managed)]  // Exact same behavior                 │
│  public class MyType { ... }                                            │
│                                                                         │
│  The Memory Class attribute becomes syntactic sugar for preset          │
│  composition. No code changes required.                                 │
│                                                                         │
│  New v3.0 Capability:                                                   │
│  ───────────────────────────────────────────────────────────────────    │
│  [Composition(                                                          │
│      Lifecycle = TracingGenerational,                                   │
│      Identity = OID,                                                    │
│      Relations = ForwardOnly,  // Add just relations, nothing else      │
│  )]                                                                     │
│  public class MyType { ... }                                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 37. Summary of Changes from v2.0

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    v2.0 → v3.0 Changes                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  CONCEPTUAL SHIFT                                                       │
│  ───────────────────────────────────────────────────────────────────    │
│  v2.0: Memory Classes are primitives                                    │
│  v3.0: Orthogonal axes are primitives, classes are presets              │
│                                                                         │
│  NEW CONCEPTS                                                           │
│  ───────────────────────────────────────────────────────────────────    │
│  • Composition: Tuple of selections across twelve axes                  │
│  • Twelve Axes: Lifecycle, Identity, Mutability, Versioning,            │
│    Enforcement, Observability, Relations, Semantic, Concurrency,        │
│    Layout, Durability, Distribution                                     │
│  • Composition Algebra: Validation, dependencies, conflicts, costs      │
│  • Native Butt Principle: Raw bytes always accessible to kernel         │
│  • View Projections: Same memory, different composition lens            │
│                                                                         │
│  PRESERVED FROM v2.0                                                    │
│  ───────────────────────────────────────────────────────────────────    │
│  • Software Operating System thesis                                     │
│  • The Bargain (O(n) vs O(n²))                                         │
│  • Engram/Layer model                                                   │
│  • Sector authority model                                               │
│  • Fault-in as universal pattern                                        │
│  • Six subsystems (MMS, RS, VEE, VTS, VNS, VSS)                        │
│  • Implementation phases and language selection                         │
│                                                                         │
│  WHAT THIS ENABLES                                                      │
│  ───────────────────────────────────────────────────────────────────    │
│  • Fine-grained capability selection                                    │
│  • Configurations impossible with fixed classes                         │
│  • Gradual adoption path (start minimal, add features)                  │
│  • Runtime composition modification (for some axes)                     │
│  • Foundation for compositional patterns beyond memory                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part XIV: Glossary

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         GLOSSARY                                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  SUBSYSTEMS                                                             │
│  ───────────────────────────────────────────────────────────────────    │
│  MMS      Memantic Memory System — compositional memory model           │
│  RS       Relational System — graph engine over live objects            │
│  VEE      Virtual Execution Engine — pathways + epochs + fault-in       │
│  VTS      Virtual Type System — cross-runtime type graph                │
│  VNS      Virtual Naming System — discovery plane                       │
│  VSS      Virtual Security System — AuthN/AuthZ substrate               │
│                                                                         │
│  COMPOSITIONAL TERMS                                                    │
│  ───────────────────────────────────────────────────────────────────    │
│  Composition   Tuple of selections across twelve axes                   │
│  Axis          Independent dimension of memory behavior                 │
│  Preset        Named, validated composition (Native, Managed, etc.)     │
│  Constraint    Dependency or conflict between axis selections           │
│                                                                         │
│  MEMORY TERMS                                                           │
│  ───────────────────────────────────────────────────────────────────    │
│  OID       Object Identifier — 64-bit universal identity                │
│  Engram    Layered portable representation of object/subgraph           │
│  Layer     Content-addressed slice of object state                      │
│  Envelope  64-byte header for OID-identified objects                    │
│  Sector    Storage authority region (RAM/persistent/GPU/remote)         │
│                                                                         │
│  EXECUTION TERMS                                                        │
│  ───────────────────────────────────────────────────────────────────    │
│  Pathway   Scheduling entity — unit of execution                        │
│  Epoch     Publishable coherence boundary                               │
│  Fault-in  Universal mechanism for acquiring missing resources          │
│                                                                         │
│  STORAGE TERMS                                                          │
│  ───────────────────────────────────────────────────────────────────    │
│  Segment   4 MB allocation unit, composition-homogeneous                │
│  Page      64 KB mapping/protection unit                                │
│  COW       Copy-on-Write — efficient snapshots                          │
│  CSR       Compressed Sparse Row — GPU graph format                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Companion Documents

This overview is supported by detailed specifications:

1. **NXIA Memory Architecture Specification v0.2** (pending update)  
   Detailed memory architecture updated for compositional model.

2. **NXIA Implementation Roadmap v0.2** (pending update)  
   Practical guidance updated for composition-first implementation.

3. **NXIA Strategic Position v0.1**  
   Vision and positioning (unchanged—thesis still valid).

4. **NXIA Design Evolution v0.2** (pending update)  
   Reasoning record with new episode on compositional insight.

5. **NXIA Compositional Memory v0.1**  
   Detailed exploration of the compositional model.

---

## Closing

NXIA is a Software Operating System. It virtualizes software infrastructure the way traditional operating systems virtualize hardware.

**The thesis:** Most complexity is accidental, arising from wrong primitives. Make the right primitives once, at the substrate level.

**The refinement (v3.0):** The right primitives are orthogonal and composable. Classes are convenient presets over a richer configuration space.

**The bargain:** Pay O(n) substrate cost to eliminate O(n²) integration cost.

**The innovation:** Not any single primitive—the synthesis. Primitives unified into a coherent substrate where they reinforce rather than conflict.

**Success:** Developers wondering how they ever tolerated the old way.

---

*End of NXIA Platform Overview v3.0*
