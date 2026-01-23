# NXIA Compositional Memory: From Classes to Algebra

> **Version:** 0.1 (Draft)  
> **Date:** 2025-12-28  
> **Status:** Design Exploration / Proposed Major Change  
> **Companion to:** NXIA Platform Overview v2.0, NXIA Memory Architecture Specification v0.1  
> **Purpose:** Document the reasoning toward a generalized compositional memory model that subsumes and extends the current Memory Classes paradigm.

---

## Executive Summary

This document explores a fundamental generalization of NXIA's Memory Classes (Native, Managed, Capability, Memantic) into a **compositional algebra** where these classes become named points in a vastly larger configuration space.

**The core insight:** Memory Classes are currently presented as discrete categories, but they're actually bundles of orthogonal choices across multiple axes (lifecycle, identity, enforcement, relations, etc.). By making this compositional structure explicit, NXIA gains:

1. **Fine-grained selection** — Choose exactly the capabilities needed, pay only for what you use
2. **Gradual adoption** — Start minimal, add capabilities incrementally as requirements emerge
3. **Runtime adaptation** — Dynamically modify composition as circumstances change
4. **Universal raw access** — Regardless of composition, the underlying bytes remain accessible to authorized code
5. **View projections** — Access the same memory through different capability lenses
6. **Foundation for Systemics** — The compositional pattern extends beyond memory to execution, communication, and ultimately to affinitic dynamics

**Relationship to current NXIA:** This is not a rejection of the Memory Classes design but a generalization. The current four classes become convenient presets — sensible defaults for common use cases — while the compositional substrate enables everything in between and beyond.

---

## Part I: The Problem

### 1. Why This Question Arose

During the design of NXIA's Memory Classes, a pattern emerged: each successive class (Native → Managed → Capability → Memantic) added capabilities on top of the previous. This suggested a latent structure:

```
Native    = base
Managed   = Native + GC + OID + type safety
Capability = Managed + capability tokens + security metadata
Memantic  = Capability + relations + semantic + provenance + ...
```

But this linear progression obscures the actual structure. The capabilities being added are **not** a single dimension — they're multiple orthogonal concerns being bundled together:

| Concern | Native | Managed | Capability | Memantic |
|---------|--------|---------|------------|----------|
| Lifecycle | Manual | Tracing GC | Tracing GC | Tracing GC |
| Identity | Address | OID | OID | OID |
| Enforcement | None | Runtime | Capability | Policy |
| Relations | None | None | None | Indexed |
| Semantic | None | None | None | Queryable |
| Versioning | None | Stamp | Stamp | Content-addressed |

**The question:** Why bundle these particular combinations? What if you want:
- GC lifetime but no OID (pure values)?
- OID identity but manual lifetime (for predictable destruction)?
- Relations without semantic layer?
- Capability enforcement without GC?

The current design forces you into one of four bundles. The compositional design lets you pick à la carte.

### 2. The Deeper Motivation

Beyond flexibility, there's a architectural principle at stake:

**Current:** Memory Classes are primitives. The system is built around four categories.

**Compositional:** Memory characteristics are primitives. Classes are derived conveniences.

This inversion matters because:

1. **Primitives should be orthogonal.** GC and identity are independent concerns. Bundling them couples things that need not be coupled.

2. **Primitives should be minimal.** Each primitive should do one thing. Bundles do many things, obscuring costs and interactions.

3. **Primitives enable reasoning.** With orthogonal primitives, you can reason about each axis independently. With bundles, you must reason about combinations.

4. **Primitives enable extension.** Adding a new axis (e.g., a new concurrency mode) in a compositional system just adds options. In a bundle system, it potentially requires new bundles or bundle variants.

### 3. The Constraints We Must Preserve

Any generalization must preserve NXIA's core invariants:

- **Universal envelope for identified objects** — Objects with OID identity have envelopes
- **Sector authority model** — Storage planes remain well-defined
- **Fault-in as universal access pattern** — Missing data triggers structured acquisition
- **Content-addressed layers** — Layer hashing for versioning and deduplication
- **Relation indexing** — Graph structure queryable via B+trees

The compositional model must express these as emergent properties of certain compositions, not as exceptions or special cases.

---

## Part II: The Compositional Vision

### 4. The Buffet Metaphor

Imagine a buffet where each station offers options for a different aspect of memory behavior. You walk through, selecting one option from each station (or "none" where applicable). Your selections form a **composition** — a complete specification of how memory for this type/object should behave.

Some selections are free (no runtime cost). Some are cheap. Some are expensive. Some require other selections (dependencies). Some preclude other selections (conflicts).

The four Memory Classes are like "combo meals" — pre-selected bundles that represent sensible, tested combinations. But you can also build your own plate.

### 5. The Algebra

Formally, a composition is a tuple of selections across axes:

```
Composition = (
    Lifecycle,
    Identity,
    Mutability,
    Versioning,
    Enforcement,
    Observability,
    Relations,
    Semantic,
    Concurrency,
    Layout,
    Durability,
    Distribution
)
```

Each axis is a type with a set of options. Some axes are enumerations (mutually exclusive options). Some are flag sets (combinable options). Some are parameterized (options with arguments).

The composition algebra includes:

- **Validation rules** — Which combinations are legal
- **Dependency rules** — Which selections require others
- **Cost model** — What overhead each selection implies
- **Compatibility rules** — Which compositions can interoperate

---

## Part III: The Buffet Stations

### 6. Complete Enumeration of Axes

Through analysis, we identify approximately twelve major axes. Each is documented with its options, dependencies, and costs.

#### Axis 1: Lifecycle (How memory is reclaimed)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `Manual` | Explicit deallocation by caller | None | Discipline |
| `RefCounted` | Freed when reference count hits zero | Inc/dec per ref change | — |
| `RefCountedCyclic` | RefCount + cycle detection | Periodic cycle scan | — |
| `Tracing` | Garbage collected via reachability | Write barriers, pauses | Root registration |
| `TracingGenerational` | Generational GC | Write barriers, minor/major GC | Root registration |
| `TracingConcurrent` | Concurrent/incremental GC | More complex barriers | Root registration |
| `Arena` | Freed in bulk with region | None until arena drop | Arena association |
| `Stack` | Freed at scope exit | None (compile-time) | Lexical scope |
| `Static` | Never freed (process lifetime) | None | — |
| `Persistent` | Survives process (sector-managed) | Sector sync | Durable sector |

**Mutual exclusions:** These are mutually exclusive for a given allocation. An object has exactly one lifecycle policy.

**Note:** `Persistent` interacts with Durability axis — it's about *how* the lifecycle extends beyond process, while Durability is about *whether* it does.

#### Axis 2: Identity (How the object is referenced)

| Option | Description | Cost | Enables |
|--------|-------------|------|---------|
| `None` | Pure value, no stable reference | None | — |
| `Address` | Memory address is identity | None | Raw pointer access |
| `OID` | Stable 64-bit identifier | OID allocation, mapping | Relations, distribution, envelope |
| `ContentHash` | Hash of content is identity | Hash computation | Content-addressed versioning, dedup |

**Mutual exclusions:** An object has exactly one primary identity scheme.

**Dependencies:**
- `OID` or `ContentHash` required for Relations
- `OID` or `ContentHash` required for Distribution
- `ContentHash` requires `Immutable` or `COW` mutability

**Note:** `ContentHash` identity means the object's identity changes when its content changes. This is the Unison model for code. It's powerful but constraining.

#### Axis 3: Mutability (How content changes)

| Option | Description | Cost | Implications |
|--------|-------------|------|--------------|
| `Immutable` | Never changes after creation | None | Can share freely, content-addressable |
| `COW` | Copy-on-write semantics | Copy on mutation | Preserves history, enables snapshots |
| `InPlace` | Direct mutation | None | Traditional mutable semantics |
| `AppendOnly` | Can extend, not modify existing | Depends on structure | Log-like structures |
| `WriteOnce` | Fields freeze after first write | Per-field tracking | Initialization patterns |

**Mutual exclusions:** Mostly exclusive, though `WriteOnce` can combine with `InPlace` for unfrozen fields.

**Dependencies:**
- `ContentHash` identity requires `Immutable` or `COW`
- `Immutable` enables free sharing across threads

#### Axis 4: Versioning (How history is tracked)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `None` | No version tracking | None | — |
| `Stamp` | Monotonic version counter | 8 bytes, increment on mutation | — |
| `DeltaChain` | Reconstructible history via deltas | Delta storage | `ContentHash` or `Stamp` |
| `ContentAddressed` | Each version identified by hash | Hash per version | `Immutable` or `COW` |
| `Branching` | Multiple concurrent versions | Branch metadata | `ContentAddressed` |

**Stackable:** These can layer. `Stamp` ⊂ `DeltaChain` ⊂ `ContentAddressed` ⊂ `Branching`.

#### Axis 5: Enforcement (How access is controlled)

| Option | Description | Cost | Provides |
|--------|-------------|------|----------|
| `Raw` | Direct load/store, caller responsible | None | Maximum performance |
| `TypeChecked` | Runtime verifies type compatibility | Type check per access | Type safety |
| `RuntimeMediated` | Accessor methods, bounds checks | Method call overhead | Memory safety |
| `CapabilityGated` | Unforgeable token required | Token validation | Fine-grained authorization |
| `PolicyEvaluated` | Full VSS policy check per access | Policy engine invocation | Dynamic, contextual security |

**Stackable/Progressive:** These form a hierarchy. Higher levels include lower guarantees.

**Note:** Current NXIA's "Enforcement Ladder by Memory Class" maps directly here:
- Native → Raw
- Managed → RuntimeMediated
- Capability → CapabilityGated
- Memantic → PolicyEvaluated

#### Axis 6: Observability (What access is tracked)

| Option | Description | Cost | Provides |
|--------|-------------|------|----------|
| `None` | No tracking | None | — |
| `ReadTraps` | Notification on read | Trap overhead | Lazy loading, access counting |
| `WriteTraps` | Notification on write | Trap overhead | Change detection, reactive updates |
| `AccessLogging` | Record who/when/what | Log storage | Audit trail |
| `Provenance` | Full causal history | Significant storage | Lineage, debugging |
| `ExecutionCapture` | Replayable access sequence | Major storage | Time-travel debugging |

**Additive:** These are flags that can be combined. `+Provenance` means enable provenance tracking.

**Maps to Layer 3 (PROVENANCE) in current NXIA.**

#### Axis 7: Relations (How graph structure is managed)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `None` | Not in object graph | None | — |
| `EmbeddedPointers` | Traditional references | None | — |
| `ForwardIndexed` | Outgoing edges in index | Index maintenance | `OID` or `ContentHash` identity |
| `BidirectionalIndexed` | Forward + reverse indexes | 2x index maintenance | `OID` or `ContentHash` identity |
| `RichEdges` | Relation objects with metadata | Edge object overhead | `OID` or `ContentHash` identity |

**Progressive:** `Forward` ⊂ `Bidirectional`. `RichEdges` can combine with either.

**Maps to Layer 2 (RELATIONS) in current NXIA.**

#### Axis 8: Semantic (How meaning is attached)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `None` | No semantic metadata | None | — |
| `Embedded` | Embedding vector stored | Vector storage (~KB) | — |
| `Queryable` | Indexed for similarity search | Index maintenance | `Embedded` |
| `AutoUpdating` | Recompute embedding on mutation | Compute cost | `Embedded` |

**Additive with dependencies.**

**Maps to Layer 4 (SEMANTIC) in current NXIA.**

#### Axis 9: Concurrency (How parallel access is managed)

| Option | Description | Cost | Guarantees |
|--------|-------------|------|------------|
| `Unsynchronized` | Caller manages | None | None |
| `ReadSafe` | Concurrent reads, exclusive writes | RW lock | Read consistency |
| `MutexProtected` | Full mutual exclusion | Lock overhead | Exclusive access |
| `LockFree` | Atomic operations | CAS overhead | Progress guarantee |
| `ActorIsolated` | Owned by single pathway | Ownership tracking | No shared access |
| `STM` | Software transactional memory | Transaction overhead | Composable atomicity |

**Mutual exclusions within synchronization strategy.**

#### Axis 10: Layout (How data is physically arranged)

| Option | Description | Cost | Benefit |
|--------|-------------|------|---------|
| `AoS` | Array of structures | None (default) | Locality per object |
| `SoA` | Structure of arrays | Transform cost | SIMD-friendly |
| `Packed` | No padding | Potential misalignment | Space efficiency |
| `Aligned(N)` | N-byte alignment | Padding | Cache/SIMD alignment |
| `Sparse` | Only non-default values | Indirection | Space for sparse data |
| `Compressed` | Compressed representation | Compress/decompress | Space efficiency |
| `Encrypted` | Encrypted at rest | Encrypt/decrypt | Confidentiality |

**Mostly independent, some combinations nonsensical.**

#### Axis 11: Durability (How persistence is managed)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `Ephemeral` | RAM only, lost on crash | None | — |
| `Checkpointable` | Can be persisted on demand | Snapshot cost | — |
| `Durable` | Automatically survives crash | Sync overhead | Persistent sector |
| `Derivable` | Can be recomputed from spec | Derivation spec | — |
| `Replicated` | Copied to multiple sites | Replication overhead | Distribution |

**Combinable:** `Durable` + `Replicated` is common.

**Maps to Sector bindings in current NXIA.**

#### Axis 12: Distribution (How location is managed)

| Option | Description | Cost | Requires |
|--------|-------------|------|----------|
| `Local` | Single address space only | None | — |
| `Migratable` | Can move between nodes | Migration protocol | `OID` or `ContentHash` |
| `Replicatable` | Can have copies | Consistency protocol | `OID` or `ContentHash` |
| `LocationTransparent` | Access doesn't know where | Resolution overhead | `OID` or `ContentHash`, VNS |
| `Federated` | Cross-domain identity | Federation protocol | `OID`, VNS |

**Progressive with dependencies.**

---

## Part IV: The Constraint System

### 7. Dependency Graph

Some selections require others. This forms a directed graph:

```
ContentHash identity ──requires──► Immutable OR COW mutability
DeltaChain versioning ──requires──► ContentHash identity
BidirectionalIndexed relations ──requires──► OID OR ContentHash identity
Federated distribution ──requires──► OID identity
PolicyEvaluated enforcement ──requires──► OID identity (for subject/object)
Queryable semantic ──requires──► Embedded semantic
Durable durability ──requires──► Persistent sector binding
Replicated durability ──requires──► Distribution ≠ Local
```

**Validation:** A composition is valid only if all dependencies are satisfied.

### 8. Conflict Rules

Some selections preclude others:

```
Manual lifecycle ──conflicts──► Tracing lifecycle
  (Can't be both manually freed and GC'd)

Immutable mutability ──conflicts──► WriteTraps observability
  (No writes to trap)

None identity ──conflicts──► Relations ≠ None
  (Can't have relations to something without identity)

Encrypted layout ──conflicts──► Raw enforcement
  (Can't do raw access to encrypted data without decryption)

Address identity ──conflicts──► Migratable distribution
  (Address changes on migration)
```

**Validation:** A composition is invalid if any conflict exists.

### 9. Cost Model

Each selection implies runtime cost:

| Selection | Space Overhead | Time Overhead | Notes |
|-----------|----------------|---------------|-------|
| `OID` identity | 8 bytes + map entry | Lookup on access | Amortized via caching |
| `Tracing` lifecycle | Write barrier | GC pauses | Generational reduces pause |
| `RefCounted` lifecycle | Count field | Inc/dec per ref | Cycle detection adds more |
| `ContentHash` identity | 20-32 bytes hash | Hash on mutation | Can batch/defer |
| `Provenance` observability | ~100+ bytes/object | Log on mutation | Grows with history |
| `BidirectionalIndexed` relations | 2 index entries/edge | B+tree ops | O(log N) |
| `Embedded` semantic | ~3KB (768 × f32) | Compute on create | GPU can accelerate |
| `PolicyEvaluated` enforcement | Policy binding | Policy eval/access | Can cache decisions |

**The composition's total cost is roughly additive across selections**, though some combinations have synergies or extra costs.

### 10. Tech Tree Visualization

The dependencies form a "tech tree" where higher capabilities require lower ones:

```
                            ┌─────────────┐
                            │    NONE     │ (no identity)
                            │  identity   │
                            └──────┬──────┘
                                   │
           ┌───────────────────────┼───────────────────────┐
           ▼                       ▼                       ▼
    ┌─────────────┐         ┌─────────────┐         ┌─────────────┐
    │   ADDRESS   │         │     OID     │         │ CONTENTHASH │
    │  identity   │         │  identity   │         │  identity   │
    └─────────────┘         └──────┬──────┘         └──────┬──────┘
                                   │                       │
                    ┌──────────────┼──────────────┐        │
                    ▼              ▼              ▼        ▼
             ┌──────────┐   ┌──────────┐   ┌──────────────────┐
             │RELATIONS │   │FEDERATION│   │ DELTA VERSIONING │
             │ indexed  │   │          │   │                  │
             └────┬─────┘   └──────────┘   └──────────────────┘
                  │
          ┌───────┴───────┐
          ▼               ▼
   ┌────────────┐   ┌────────────┐
   │  REVERSE   │   │    RICH    │
   │  INDEXING  │   │   EDGES    │
   └────────────┘   └────────────┘
```

```
    ┌─────────────┐
    │  EMBEDDED   │ (semantic vector)
    │  semantic   │
    └──────┬──────┘
           │
           ▼
    ┌─────────────┐
    │  QUERYABLE  │ (similarity search)
    │  semantic   │
    └──────┬──────┘
           │
           ▼
    ┌─────────────┐
    │ AUTOUPDATE  │ (recompute on change)
    │  semantic   │
    └─────────────┘
```

---

## Part V: Mapping to NXIA Internals

### 11. Where Composition Lives

In the current NXIA architecture, composition would map to:

| Composition Aspect | NXIA Internal Location |
|--------------------|------------------------|
| Lifecycle policy | Segment type (segments grouped by GC strategy) |
| Identity scheme | Envelope presence/format |
| Mutability mode | COW flags, page permissions |
| Versioning mode | Layer structure, delta chain presence |
| Enforcement level | Enforcement modality flag (new), handler registration |
| Observability | Layer 3 presence, trap registrations |
| Relations | Layer 2 presence, index registrations |
| Semantic | Layer 4 presence, GPU sector binding |
| Concurrency | Segment/page properties, pathway affinity |
| Layout | Segment properties, slot structure |
| Durability | Sector bindings |
| Distribution | Sector bindings, VNS registration |

### 12. The Composition Descriptor

A compact encoding of composition could fit in the envelope:

```rust
#[repr(C)]
struct CompositionDescriptor {
    // Axis selections (each axis uses minimal bits)
    lifecycle: u4,        // 16 options
    identity: u2,         // 4 options  
    mutability: u3,       // 8 options
    versioning: u3,       // 8 options
    enforcement: u3,      // 8 options
    concurrency: u3,      // 8 options
    layout: u4,           // 16 options (flags)
    durability: u4,       // 16 options (flags)
    distribution: u4,     // 16 options (flags)
    
    // Layer presence (current design)
    layers: LayerMask,    // 32 bits
    
    // Total: ~48 bits of composition + 32 bits layers = 80 bits = 10 bytes
    // Fits in current envelope with room to spare
}
```

Alternatively, composition could be **type-level** rather than instance-level:

```rust
// Composition is part of the type, not the instance
struct Customer<C: Composition> {
    // ...
}

// Type aliases for convenience
type ManagedCustomer = Customer<ManagedComposition>;
type MemanticCustomer = Customer<MemanticComposition>;
```

This trades flexibility (per-instance composition) for efficiency (no per-instance descriptor).

**Recommendation:** Support both. Types have default composition; instances can override.

### 13. Segments as Composition-Homogeneous Regions

Current NXIA already groups objects by Memory Class into segments. Compositional NXIA generalizes this:

**Segments are grouped by compatible compositions.**

"Compatible" means: same lifecycle (GC strategy), same enforcement level (determines access path), compatible layouts.

Objects with different compositions can coexist if their differences are in layers (relations, semantic, provenance) rather than fundamental memory management.

```
Segment Types:
  ManualDirect     - Manual lifecycle, Raw enforcement
  TracingMediated  - Tracing GC, RuntimeMediated enforcement  
  TracingCapability - Tracing GC, CapabilityGated enforcement
  TracingPolicy    - Tracing GC, PolicyEvaluated enforcement
  RefCountedDirect - RefCounted lifecycle, Raw enforcement
  ArenaBulk        - Arena lifecycle, Raw enforcement
  ...
```

Within a segment, objects may differ in relations, semantic, observability, etc. These are handled by layer presence and handler registration, not segment type.

### 14. Axis Handlers (Generalized Memory Drivers)

Each axis has associated **handlers** — code that implements the axis behavior:

```rust
trait LifecycleHandler {
    fn on_allocate(&self, obj: &mut ObjectSlot, segment: &Segment);
    fn on_reference(&self, from: OID, to: OID);
    fn on_unreference(&self, from: OID, to: OID);
    fn on_deallocate(&self, obj: &mut ObjectSlot);
    fn collect(&self, roots: &[OID]) -> Vec<OID>; // For tracing
}

trait EnforcementHandler {
    fn check_access(&self, subject: &Pathway, object: OID, rights: Rights) -> Result<(), Denied>;
    fn wrap_view(&self, raw: RawView) -> MediatedView;
}

trait RelationsHandler {
    fn on_edge_add(&self, source: OID, rel: RelationType, target: OID);
    fn on_edge_remove(&self, source: OID, rel: RelationType, target: OID);
    fn query_forward(&self, source: OID, rel: RelationType) -> Vec<OID>;
    fn query_reverse(&self, target: OID, rel: RelationType) -> Vec<OID>;
}

// ... handlers for other axes
```

A composition determines which handlers are active for an object. The handlers are invoked at appropriate points (allocation, access, mutation, deallocation).

**This is the generalization of Memory Class Drivers (MCD).** Instead of drivers per class, there are handlers per axis. A composition assembles the relevant handlers.

---

## Part VI: The Native Substratum

### 15. Everything Has Raw Bytes Underneath

This is perhaps the most important architectural principle:

> **The Native Butt Principle:** Regardless of composition, every object ultimately exists as bytes in a page. The composition adds metadata, enforcement, and behavior — but the raw data is always present and, with sufficient authority, accessible.

Visually:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Object with Rich Composition                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Composition overlays:                                                   │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ + PolicyEvaluated enforcement (VSS checks)                       │   │
│  │ + Provenance tracking (Layer 3)                                  │   │
│  │ + Bidirectional relations (Layer 2 + indexes)                    │   │
│  │ + Semantic embedding (Layer 4)                                   │   │
│  │ + Tracing GC (root registration, barriers)                       │   │
│  │ + OID identity (envelope, OID→address map)                       │   │
│  │ + COW mutability (page-level versioning)                         │   │
│  │ + Durable persistence (sector sync)                              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                               │                                          │
│                               │ All built on top of:                     │
│                               ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                     RAW BYTES IN PAGE                            │   │
│  │  ┌──────────────────────────────────────────────────────────┐   │   │
│  │  │ [envelope: 64 bytes] [layer1: N bytes] [layer2: M bytes] │   │   │
│  │  │                                                           │   │   │
│  │  │ These are just bytes. They can be read/written directly   │   │   │
│  │  │ if you have kernel-level or raw-access authority.         │   │   │
│  │  └──────────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why this matters:**

1. **Kernel always has access.** MMS must manage memory; it needs raw access. This is non-negotiable.

2. **Drivers may need access.** A persistence driver needs to read bytes to write to disk. A network driver needs to read bytes to serialize for transmission. A GPU driver needs to read bytes to upload to VRAM.

3. **Hot paths may need access.** If enforcement overhead is unacceptable, authorized code can use raw access. This is the Native Memory Class use case.

4. **Introspection requires access.** Debuggers, profilers, migration tools — all need to see raw state.

5. **The abstraction is authority, not capability.** It's not that raw access is impossible; it's that raw access requires authorization that most code won't have.

### 16. Access Authority Levels

We define a hierarchy of access authority:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ACCESS AUTHORITY LEVELS                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Level 0: KERNEL                                                         │
│  ─────────────────────────────────────────────────────────────────────  │
│  Always has raw byte access to all memory.                              │
│  Examples: MMS core, GC, segment manager                                │
│                                                                          │
│  Level 1: SYSTEM DRIVERS                                                 │
│  ─────────────────────────────────────────────────────────────────────  │
│  Raw access to memory in their domain, via registered capability.       │
│  Examples: Persistence driver, network driver, GPU driver               │
│                                                                          │
│  Level 2: TRUSTED CODE                                                   │
│  ─────────────────────────────────────────────────────────────────────  │
│  Can request raw access with explicit capability token.                 │
│  Examples: Performance-critical inner loops, FFI boundaries             │
│                                                                          │
│  Level 3: MANAGED CODE                                                   │
│  ─────────────────────────────────────────────────────────────────────  │
│  Access through runtime mediation (accessors, bounds checks).           │
│  Examples: Normal application code in safe language subset              │
│                                                                          │
│  Level 4: SANDBOXED CODE                                                 │
│  ─────────────────────────────────────────────────────────────────────  │
│  Access through full policy evaluation.                                 │
│  Examples: Plugins, user-submitted code, cross-trust-boundary           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

The object's composition determines the **minimum** authority level for access. But higher authority can always "see through" to raw access.

---

## Part VII: Views and Projections

### 17. Same Memory, Different Lenses

A **view** is an access mode to an object. The object has one composition (its true nature), but can be accessed through different views that provide different subsets of its capabilities.

```
Object X has composition:
  Tracing + OID + COW + PolicyEvaluated + Relations + Semantic + Provenance

Possible views of X:

  MemanticView<X>     - Full access, all checks, all features
  CapabilityView<X>   - Capability checks, no semantic queries
  ManagedView<X>      - Runtime mediation, no security checks
  NativeView<X>       - Raw byte access, no checks
```

**Views are not copies.** They're access modes to the same underlying memory. The view determines:
- Which handlers fire on access
- Which checks are performed
- Which features are available
- What overhead is incurred

### 18. View Acquisition Rules

**Narrowing** (removing capabilities/checks):
- Requires authority (capability token or trust level)
- Does not change the object's composition
- Provides less safety, more performance
- The object still behaves according to its composition for other accessors

**Widening** (adding capabilities):
- May require composition modification (see next section)
- Or may just enable features that were dormant
- Cannot add capabilities the object's composition doesn't support

```rust
// Hypothetical API

impl<T, C: Composition> Object<T, C> {
    // Narrowing - get a less-capable view (requires authority)
    fn as_raw(&self, auth: RawAccessToken) -> Result<NativeView<T>, Denied>;
    fn as_managed(&self, auth: TrustToken) -> Result<ManagedView<T>, Denied>;
    
    // Widening - enable dormant features (if composition supports)
    fn as_semantic(&self) -> Option<SemanticView<T>>
    where C: HasSemantic;
    
    fn as_relational(&self) -> Option<RelationalView<T>>
    where C: HasRelations;
}

// Usage in hot path
unsafe {
    let raw: NativeView<Customer> = customer.as_raw(kernel_auth)?;
    raw.balance += 100;  // Direct memory access, no checks
}

// Usage in application code
customer.balance += 100;  // Full composition enforcement
```

### 19. Language-Level Integration

In a language designed for NXIA, view selection could be syntactic:

```
// Normal access (full composition enforcement)
let balance = customer.balance;

// Explicit raw access (requires unsafe or capability)
unsafe native {
    let balance = customer.balance;  // Direct load
}

// Explicit mediated access (skip security, keep safety)
managed {
    let balance = customer.balance;  // Bounds-checked, no policy
}

// Query composition
if customer.has_semantic {
    let similar = customer.find_similar(threshold: 0.8);
}
```

---

## Part VIII: Dynamic Composition Modification

### 20. What Can Change at Runtime?

Some composition aspects are immutable after creation. Others can be modified.

**Generally immutable:**
- Identity scheme (changing from Address to OID requires assigning OID and updating all references)
- Lifecycle policy (changing from Manual to Tracing requires GC registration, finding all roots)
- Fundamental mutability (Immutable objects can't become Mutable)

**Generally mutable:**
- Layer presence (add/remove relations, semantic, provenance)
- Observability level (start/stop logging, traps)
- Some enforcement upgrades (add capability requirements)
- Durability changes (make durable, replicate)

### 21. Layer Addition/Removal

The most common dynamic composition changes are layer-related:

```rust
// Add relations capability
object.enable_relations()?;
// Creates Layer 2, registers with relation indexes

// Add semantic capability
object.enable_semantic(embedding)?;
// Creates Layer 4, optionally registers with semantic index

// Add provenance tracking
object.enable_provenance()?;
// Creates Layer 3, starts recording modifications

// Remove (if permitted)
object.disable_provenance()?;
// Drops Layer 3, stops recording
// May require capability to "hide tracks"
```

### 22. Composition Migration

For deeper changes, objects may need to **migrate** to a new composition:

```rust
// Create new object with different composition, copy state
let new_obj = object.migrate_to(NewComposition)?;

// This may involve:
// - Allocating in different segment (different GC)
// - Assigning OID (if gaining identity)
// - Computing hashes (if becoming content-addressed)
// - Registering with new systems
// - Updating references from old to new
```

Migration is expensive and may not be possible for all composition changes (e.g., cannot migrate to Address identity if references exist).

### 23. Security of Composition Changes

Composition modification is itself a privileged operation:

```
MUST require authorization:
- Downgrading enforcement (removing security checks)
- Disabling observability (hiding audit trail)
- Changing durability (could enable data loss)

MAY be freely allowed:
- Upgrading enforcement (adding security)
- Enabling observability (adding audit)
- Adding capabilities (more features)

Context-dependent:
- Relation changes (may affect graph invariants)
- Semantic changes (may affect search results)
```

The VSS security model extends to composition modification, not just object access.

---

## Part IX: Implications for NXIA Architecture

### 24. What Changes in NXIA

If NXIA adopts compositional memory:

**Envelope changes:**
- `MemoryClass` field becomes `CompositionDescriptor` (or composition is type-level)
- May need more bits for axis selections
- Or: composition is indirect (envelope points to composition definition)

**Segment organization changes:**
- Segments grouped by composition compatibility, not just Memory Class
- More segment types, but more precise grouping
- Allocation must find compatible segment or create new one

**Handler architecture:**
- MCD (Memory Class Driver) generalizes to per-axis handlers
- Handler composition based on object composition
- Handler dispatch on access/mutation/allocation/deallocation

**Memory Classes become presets:**
```rust
const NATIVE: Composition = Composition {
    lifecycle: Manual,
    identity: Address,
    enforcement: Raw,
    // ... all other axes at minimal settings
};

const MANAGED: Composition = Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    enforcement: RuntimeMediated,
    mutability: InPlace,
    // ...
};

const CAPABILITY: Composition = Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    enforcement: CapabilityGated,
    mutability: COW,
    layers: BASE_STATE | SECURITY,
    // ...
};

const MEMANTIC: Composition = Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    enforcement: PolicyEvaluated,
    mutability: COW,
    versioning: ContentAddressed,
    layers: BASE_STATE | RELATIONS | PROVENANCE | SEMANTIC | CODE | SECURITY,
    observability: Provenance,
    relations: BidirectionalIndexed,
    semantic: Queryable,
    // ...
};
```

These are convenience constructors, not special cases.

### 25. What Stays the Same

The compositional model preserves NXIA's core:

- **Universal envelope structure** — Still present for identified objects
- **Layer architecture** — Still content-addressed, independently storable
- **Sector model** — Still RAM/Persistent/GPU/Remote
- **Fault-in pattern** — Still the universal access mechanism
- **Relation indexing** — Still B+trees over edges
- **COW snapshots** — Still page-level copy-on-write
- **Epoch boundaries** — Still coherence mechanism

The change is in how these are *configured*, not in what they *are*.

### 26. New Capabilities Enabled

Compositional memory enables:

**1. Hybrid objects:**
Objects with unusual combinations not expressible in current classes.
- GC'd values without identity (pure, copyable)
- Manually-managed objects with relations (high-performance graph nodes)
- Capability-secured objects without semantic layer

**2. Gradual capability adoption:**
Start with minimal composition, add features as needed.
- Prototype with Native, add GC when stable
- Add relations when graph queries needed
- Add semantic when AI needs to reason about it

**3. Per-instance customization:**
Different instances of same type can have different compositions.
- Hot objects: minimal composition, maximum performance
- Important objects: full composition, maximum features
- Temporary objects: arena lifecycle, no persistence

**4. Runtime observability toggling:**
Debug production issues by enabling observability on suspect objects.
- Enable provenance on specific objects
- Enable access logging for security investigation
- Disable when done, minimal ongoing cost

**5. Cross-composition interop:**
Objects with different compositions can coexist and interact.
- Memantic objects can reference Native objects
- Native code can access Memantic objects via views
- Drivers can access any object via raw view

---

## Part X: Open Questions

### 27. Unresolved Design Decisions

**Composition descriptor location:**
- In envelope? (Per-instance, flexible, space cost)
- In type metadata? (Per-type, efficient, less flexible)
- Hybrid? (Type provides default, instance can override)

**Composition validation timing:**
- Compile-time only? (Catches errors early, limits dynamism)
- Runtime validation? (Flexible, overhead on composition change)
- Both? (Compile-time for type-level, runtime for modifications)

**Handler dispatch mechanism:**
- Virtual dispatch per access? (Flexible, overhead)
- JIT-specialized per composition? (Fast, complexity)
- Enum-based dispatch? (Predictable, limited)

**Cross-composition references:**
- Always allowed? (Flexible, complex semantics)
- Restricted by compatibility rules? (Safer, less flexible)
- Explicit coercion required? (Clear, verbose)

**Composition in Engrams:**
- Include full composition in Engram? (Portable, larger)
- Engram references composition by ID? (Compact, requires resolution)
- Composition is implicit from layers present? (Inferred, potentially ambiguous)

### 28. Performance Questions

**Composition dispatch overhead:**
- How expensive is selecting handlers based on composition?
- Can JIT eliminate this for monomorphic call sites?
- What's the working set impact of many compositions?

**Segment proliferation:**
- How many segment types in practice?
- Memory fragmentation with many small compatible groups?
- Allocation latency when finding compatible segment?

**View projection cost:**
- Is view narrowing zero-cost? (Should be)
- Is view widening zero-cost for dormant features? (Should be)
- What's the cost of view capability checking?

---

## Part XI: Connection to Systemics

### 29. Beyond Memory

The compositional pattern in memory is an instance of a more general pattern:

> **Compositional capability selection over a substrate that maintains invariants.**

Memory is one substrate. The same pattern applies to:

| Substrate | Composition Axes |
|-----------|------------------|
| Memory | Lifecycle, identity, enforcement, relations, semantic... |
| Execution | Isolation, preemption, priority, capture, distribution... |
| Communication | Ordering, reliability, buffering, multiplexing... |
| Types | Nominal/structural, variance, constraints, versioning... |
| Security | Discretionary/mandatory, role/attribute, temporal... |

**Systemics** is the generalization: every subsystem can be compositional, with objects selecting their capabilities across multiple subsystems.

### 30. Semantic as Foundation for Affinity

NXIA already has semantic embeddings (Layer 4). In the compositional model, semantic is one axis among many.

But semantic has a special property: **it encodes meaning in a form amenable to mathematical operations.**

- Proximity in embedding space ≈ semantic similarity
- Vector operations (addition, projection) ≈ semantic operations
- Clustering in embedding space ≈ categorization

If objects have semantic embeddings, and we can compute over those embeddings, we have the foundation for **affinitic dynamics**:

- Objects have affinities (encoded in embeddings)
- Affinities can attract, repel, modulate
- Systems can route/connect based on affinity
- Emergent structure arises from affinity interactions

This is the direction hinted at in the original question. Compositional memory is the substrate that makes it possible. The semantic axis is the bridge from static composition to dynamic affinity.

### 31. The Roadmap

```
Current NXIA:
  Fixed Memory Classes (Native, Managed, Capability, Memantic)
  
Compositional NXIA:
  Orthogonal axes, arbitrary compositions
  Memory Classes become presets
  
Systemic NXIA:
  Compositional pattern extended to all subsystems
  Objects have compositions across memory, execution, communication, security
  
Affinitic NXIA:
  Semantic embeddings drive dynamic composition
  Affinity-based routing, connection, reaction
  Emergent structure from compositional dynamics
```

Each step builds on the previous. Compositional memory is the first and most concrete step.

---

## Appendix A: Composition Examples

### A.1 Current Memory Classes as Compositions

```rust
// Native: Minimal overhead, caller responsibility
Composition {
    lifecycle: Manual,
    identity: Address,
    mutability: InPlace,
    versioning: None,
    enforcement: Raw,
    observability: None,
    relations: None,
    semantic: None,
    concurrency: Unsynchronized,
    layout: Default,
    durability: Ephemeral,
    distribution: Local,
}

// Managed: GC'd, type-safe, runtime-checked
Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    mutability: InPlace,
    versioning: Stamp,
    enforcement: RuntimeMediated,
    observability: None,
    relations: None,
    semantic: None,
    concurrency: Unsynchronized,
    layout: Default,
    durability: Ephemeral,
    distribution: Local,
}

// Capability: Security-aware, COW, capability-gated
Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    mutability: COW,
    versioning: Stamp,
    enforcement: CapabilityGated,
    observability: None,
    relations: None,
    semantic: None,
    concurrency: ReadSafe,
    layout: Default,
    durability: Checkpointable,
    distribution: Local,
}

// Memantic: Full-featured, policy-evaluated, all layers
Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    mutability: COW,
    versioning: ContentAddressed,
    enforcement: PolicyEvaluated,
    observability: Provenance,
    relations: BidirectionalIndexed,
    semantic: Queryable,
    concurrency: ReadSafe,
    layout: Default,
    durability: Durable,
    distribution: LocationTransparent,
}
```

### A.2 Novel Compositions Not Expressible in Current Classes

```rust
// High-performance graph node: Manual lifecycle with relations
Composition {
    lifecycle: Arena,           // Bulk free with graph
    identity: OID,              // Stable identity for edges
    mutability: InPlace,        // Direct mutation
    enforcement: Raw,           // No checks (trusted code)
    relations: ForwardIndexed,  // Outgoing edges indexed
    // ... minimal everything else
}

// Pure immutable value: GC'd but no identity
Composition {
    lifecycle: TracingGenerational,
    identity: None,             // Pure value, no stable reference
    mutability: Immutable,      // Never changes
    enforcement: RuntimeMediated,
    // ... no relations, semantic, etc.
}

// Secure ephemeral: Full security, no persistence
Composition {
    lifecycle: TracingGenerational,
    identity: OID,
    enforcement: PolicyEvaluated,
    observability: AccessLogging,
    durability: Ephemeral,      // Never persisted (security requirement)
    // ... 
}

// Semantic-only attachment: Add AI reasoning to existing object
Composition {
    // ... base composition from type
    semantic: Queryable | AutoUpdating,  // Add semantic capability
}
```

---

## Appendix B: Reasoning Trace

This appendix captures the reasoning process that led to this design, for AI readers who need to continue the work.

### B.1 The Initial Observation

The current Memory Classes (Native, Managed, Capability, Memantic) form a progression where each adds capabilities:

```
Native → Managed → Capability → Memantic
       +GC,OID   +Security    +Relations,Semantic,...
```

But this progression conflates orthogonal concerns. GC (lifecycle) is independent of OID (identity) is independent of security (enforcement) is independent of relations.

### B.2 The Key Question

> What if you want GC without OID? OID without GC? Relations without semantic? Security without relations?

The bundled design forces you to take combinations that may not match your needs. This suggests the bundles are not primitive — they're combinations of primitives.

### B.3 The Decomposition

We asked: what are the actual orthogonal axes of variation?

Through enumeration, we identified ~12 major axes (lifecycle, identity, mutability, versioning, enforcement, observability, relations, semantic, concurrency, layout, durability, distribution).

Each axis has options. A composition selects one option (or option set) per axis.

### B.4 The Constraints

Not all combinations are valid. We identified:
- **Dependencies:** ContentHash requires Immutable; Relations requires OID
- **Conflicts:** Manual conflicts with Tracing; None identity conflicts with Relations
- **Cost implications:** Each option has overhead

These constraints form an algebra. Valid compositions satisfy all constraints.

### B.5 The Native Butt Insight

Regardless of composition, everything is bytes in pages. The composition adds metadata and enforcement, but the raw data is always there.

This means:
- Kernel always has raw access
- Views can project same memory through different compositions
- Narrowing (removing checks) is possible with authority
- The abstraction is in enforcement, not representation

### B.6 The Dynamic Composition Question

Can composition change at runtime?

- **Layers:** Yes, easily (add/remove Layer 2, 3, 4...)
- **Observability:** Yes, easily (start/stop logging)
- **Fundamental axes:** Hard or impossible (lifecycle, identity)

This suggests a distinction between "soft" composition (changeable) and "hard" composition (fixed at allocation).

### B.7 The Connection to Systemics

If memory can be compositional, why not other subsystems?

- Execution (pathway composition)
- Communication (channel composition)
- Security (policy composition)

The pattern generalizes. And if semantic embeddings are a composition axis, they connect static composition to dynamic affinity-based behavior.

### B.8 The Design Decision

We're not proposing to replace Memory Classes, but to recognize them as presets over a compositional substrate.

The internal architecture becomes compositional. The external API can remain simple (use presets) while enabling power users to specify custom compositions.

This is the generalization that unlocks the next level of capability without abandoning the current design.

---

## Summary

NXIA's Memory Classes are bundles of orthogonal choices. By making the compositional structure explicit, we enable:

1. **Fine-grained selection** — Pick exactly what you need
2. **Gradual adoption** — Start minimal, add features
3. **Runtime adaptation** — Change composition dynamically
4. **Universal raw access** — Substrate always accessible
5. **View projections** — Same memory, different lenses
6. **Foundation for Systemics** — Pattern extends beyond memory

The current Memory Classes become convenience presets over the compositional algebra. This is not a rejection but a generalization.

The implementation path:
1. Formalize the axis structure and constraints
2. Design composition descriptor encoding
3. Implement axis handlers (generalized MCD)
4. Implement view projection mechanism
5. Implement dynamic composition modification
6. Expose compositional API while preserving preset convenience

This work is foundational for the larger vision of Systemics and, ultimately, Affinitics.

---

*End of NXIA Compositional Memory v0.1*
