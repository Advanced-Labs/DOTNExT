# DOTNExT Vision - Glossary and Design Variants

> **Document Type:** Reference / Decision Log
> **Version:** 1.0
> **Date:** 2025-12-05
> **Parent:** Vision-DOTNExT-Memory-Architecture.md

---

## Part 1: Glossary

### Core Concepts

| Term | Definition |
|------|------------|
| **Engram** | A self-contained memory package representing one or more objects with their identity, data, relationships, and metadata. The universal unit of memory in DOTNExT. Named after the hypothetical means by which memory traces are stored in the brain. |
| **Thing** | An entity within an Engram. Every managed object with Engram support is a "Thing" with UUID, type, data, and relations. |
| **UUID** | Universally Unique Identifier. Every Thing has one, assigned at creation. Uses UUIDv7 (time-ordered) format. |
| **Engram Level** | The complexity level of an Engram (0-5), from simple native tracking to full living memory participation. |

### Systems

| Term | Definition |
|------|------------|
| **CMS** | Central Memory System. The unified authority over all memory in the process. Tracks allocations, coordinates subsystems, manages drivers. |
| **MOM** | Managed Object Manager. Evolution of the GC with identity tracking, lifecycle events, and ORION integration. |
| **ORION** | Object Relationship and Intelligence Network. The graph/semantic engine maintaining the web of object relationships. |
| **Memantics** | Semantic Memory. The ultimate memory system providing living, semantic, code-aware memory. Where ORION ends, Memantics begins. |
| **Memory Driver** | A plugin that bridges live memory to external storage. Interface between Engrams and persistence targets. |

### Object States and Relationships

| Term | Definition |
|------|------------|
| **Internal Reference** | A reference to a Thing within the same Engram. Uses local index. |
| **External Reference** | A reference to a Thing outside the Engram. Uses full UUID. |
| **Lazy Reference** | An unresolved reference that triggers resolution on first access. |
| **Clone** | A full copy of a Thing with synchronization to the original. Mirror copy. |
| **Fork** | A copy of a Thing retaining historical lineage but no sync. Independent after copy. |
| **Proxy** | A lightweight reference to a remote Thing. Operations forwarded to original. |

### Semantic Terms

| Term | Definition |
|------|------------|
| **Embedding** | A vector representation of meaning. Objects, fields, and relationships can have embeddings. |
| **Semantic Search** | Finding things by meaning similarity rather than exact value match. |
| **Association** | A weighted relationship that strengthens with use. Part of living memory. |
| **Temporal Awareness** | The system's ability to understand and track time-based aspects (versions, history, when things were known). |

### Distribution Terms

| Term | Definition |
|------|------------|
| **Node** | A DOTNExT VM instance. Has a Node UUID. |
| **Domain** | A cluster/federation of nodes. Optional grouping for coordination. |
| **Origin** | The node where a Thing was created. Tracked in Engram metadata. |
| **Custody Chain** | The record of nodes that have held/modified a Thing. For provenance tracking. |
| **Resolution** | The process of obtaining a Thing referenced but not locally present. |

### Reflection+

| Term | Definition |
|------|------------|
| **Reflection+** | Enhanced reflection integrated with ORION. Provides semantic type queries, relationship-aware introspection. |
| **Type Semantics** | The meaning of a type beyond its structure. What it represents, not just what fields it has. |

---

## Part 2: Design Variants and Decision Points

Throughout the vision, several design decisions have alternatives. This section documents the variants discussed and the reasoning.

### Variant 1: GC Extension vs. New System

**The Question:** Should we extend the existing GC or create a separate MOM?

**Option A: Extend GC in Place**
- Modify existing GC code directly
- Add UUID tracking, lifecycle events, ORION hooks
- Pros: Single component, no new boundaries
- Cons: Risks GC complexity explosion, harder to maintain

**Option B: Layer MOM on Top of GC**
- Keep GC focused on memory management
- MOM wraps GC and adds identity/lifecycle/integration
- Pros: Separation of concerns, GC stays maintainable
- Cons: Two components to coordinate

**Option C: Replace GC Entirely**
- Build new memory manager from scratch with all features
- Pros: Clean design, no legacy constraints
- Cons: Massive effort, proven GC algorithms valuable

**Current Direction:** Option B - Layer MOM on GC. The GC's algorithms are proven. MOM adds new concerns without disturbing core GC logic.

---

### Variant 2: CMS Scope

**The Question:** How much should CMS track?

**Option A: Managed Memory Only**
- CMS only tracks what MOM reports
- Native memory invisible to CMS
- Pros: Simpler, less overhead
- Cons: Incomplete picture, can't track native allocations

**Option B: All Memory via Opt-In**
- Native allocations can optionally register with CMS
- "Important" native memory tracked
- Pros: Balanced, no overhead for unimportant allocations
- Cons: Inconsistent tracking

**Option C: All Memory Universally**
- Every allocation (native and managed) tracked
- Complete memory picture
- Pros: Full visibility, everything serializable
- Cons: Overhead on every native allocation

**Current Direction:** Option B - Opt-in for native. Not all native memory needs tracking. C++ runtime internals don't need Engram identity. But significant native objects (e.g., large buffers, interop objects) can opt-in.

---

### Variant 3: ORION Responsibility Boundary

**The Question:** Should ORION persist its graph, or is that CMS's job?

**Option A: ORION Has Own Persistence**
- ORION manages its own graph storage
- Separate from Engram persistence
- Pros: Optimized graph storage
- Cons: Two persistence systems, sync issues

**Option B: CMS Persists ORION State**
- ORION is live-only
- CMS extracts graph as Engrams for persistence
- ORION rebuilt on load
- Pros: Single persistence path
- Cons: Full graph extraction may be heavy

**Option C: ORION Uses Memory Driver**
- ORION persists via CMS Memory Driver interface
- Graph-aware drivers (Neo4j, Memantics) used
- Pros: Consistent interface, leverages graph DBs
- Cons: Driver dependency

**Current Direction:** Option B/C hybrid - ORION is live graph, CMS handles persistence, but specialized drivers can accept graph-native format. ORION doesn't manage storage directly but can export for graph-aware drivers.

---

### Variant 4: Where UUIDs Live

**The Question:** Where to store object UUIDs?

**Option A: Object Header Extension**
- Add UUID directly to every object header
- Pros: Fastest access
- Cons: 16 bytes added to EVERY object, massive memory increase

**Option B: SyncBlock Extension**
- Store UUID in SyncBlock (only allocated when needed)
- Use header bit to indicate presence
- Pros: No cost for non-Engram objects, standard extension point
- Cons: SyncBlock allocation has overhead

**Option C: Side Table**
- External table mapping address → UUID
- Header bit indicates presence
- Pros: Zero per-object overhead except 1 bit
- Cons: Lookup cost, must handle GC compaction

**Option D: Hybrid SyncBlock + Side Table**
- Side table for hot path (address → UUID)
- SyncBlock for full metadata
- Escalate as needed
- Pros: Balances performance and overhead
- Cons: Complexity

**Current Direction:** Option D - Hybrid. Header bit marks "has Engram data". Side table for fast UUID lookup. SyncBlock extension for full metadata. This balances performance with memory efficiency.

---

### Variant 5: Memory Driver Location

**The Question:** Native or managed drivers?

**Option A: Native Only**
- All drivers implemented in C/C++
- Maximum performance
- Pros: Fastest possible
- Cons: Harder to write, fewer contributors

**Option B: Managed Only**
- All drivers in C#
- Easy to develop
- Pros: Large developer pool, easy debugging
- Cons: Overhead for slow media is fine, but overhead itself exists

**Option C: Core Native, Extensions Managed**
- Built-in drivers (native Engram, file) are native
- Plugin drivers can be managed
- Pros: Best of both, performance where needed, extensibility where valued
- Cons: Two APIs to maintain

**Current Direction:** Option C - Core native, extensions managed. The overhead of managed code is acceptable when the bottleneck is I/O to external systems. Native drivers for critical path (native Engram format, file system).

---

### Variant 6: Semantic Encoding Responsibility

**The Question:** Who generates embeddings?

**Option A: Application Provides All**
- Application code explicitly sets embeddings
- Runtime just stores them
- Pros: Application control
- Cons: Burden on developers

**Option B: Runtime Auto-Generates**
- Runtime computes embeddings from object content
- Model integrated into runtime
- Pros: Automatic, consistent
- Cons: Which model? Heavy runtime dependency?

**Option C: Pluggable Embedding Providers**
- Interface for embedding generation
- Multiple providers (local model, API, custom)
- Pros: Flexible, can improve over time
- Cons: Complexity

**Option D: Layered (Default + Override)**
- Runtime provides default embeddings (simple heuristics or local model)
- Application can override with explicit embeddings
- Memantics can compute richer embeddings
- Pros: Works out of box, upgradeable
- Cons: Multiple levels to understand

**Current Direction:** Option D - Layered. Default embeddings computed at type level (type name, field names). Object-level embeddings optional. Application can provide richer embeddings. Memantics has full model integration for living memory.

---

### Variant 7: External Reference Resolution

**The Question:** What happens when loading an Engram with references to Things not present?

**Option A: Fail Fast**
- If reference can't be resolved, throw exception
- Pros: Explicit, no surprises
- Cons: Rigid, blocks many use cases

**Option B: Null Fill**
- Replace unresolved references with null
- Pros: Simple, permissive
- Cons: Silent data loss, NullReferenceExceptions later

**Option C: Lazy Proxy**
- Create lazy wrapper that resolves on access
- Pros: Deferred cost, only pay for what you use
- Cons: Complexity, surprising latency on access

**Option D: Configurable Strategy**
- Per-load or per-type configuration
- Choose: Fail, Null, Lazy, Fetch, Proxy
- Pros: Flexibility for different use cases
- Cons: Configuration complexity

**Current Direction:** Option D - Configurable with Lazy as default. Different scenarios need different strategies. Default to lazy (most permissive), allow override per type or per load operation.

---

### Variant 8: Memantics Relationship to CMS

**The Question:** Is Memantics just another Memory Driver, or something more?

**Option A: Just a Driver**
- Memantics implements IMemoryDriver
- No special treatment
- Pros: Consistency, standard interface
- Cons: Can't leverage full platform integration

**Option B: Privileged Driver**
- Memantics is a driver but has access to more internals
- Special APIs for living memory features
- Pros: Full capability, still uses driver interface
- Cons: Two classes of drivers

**Option C: Parallel to CMS**
- Memantics is a peer of CMS, not a driver
- Direct integration at same level
- Pros: No interface limitations
- Cons: Architectural complexity, bypass of CMS coordination

**Current Direction:** Option B - Privileged driver. Memantics uses driver interface for compatibility but has additional APIs for living memory, type storage, and semantic features. CMS recognizes Memantics specially for features like type persistence.

---

## Part 3: Design Principles Summary

From the variants and decisions above, these principles emerge:

1. **Layer, Don't Replace Core Algorithms**
   - GC algorithms are proven; MOM layers on top
   - Separation of concerns preserves maintainability

2. **Opt-In Over Universal**
   - Not everything needs full Engram treatment
   - Levels allow gradual feature adoption

3. **Hybrid Where Trade-offs Exist**
   - Native for performance-critical paths
   - Managed for extensibility
   - Side table + SyncBlock for storage

4. **Configurable, Not Rigid**
   - Different scenarios need different strategies
   - Provide sensible defaults, allow override

5. **Single Persistence Path**
   - CMS coordinates all persistence
   - Drivers provide destination variety
   - No competing persistence systems

6. **Semantic is Layered**
   - Basic: Type-level heuristics
   - Enhanced: Application-provided
   - Full: Memantics integration

7. **Clear Component Boundaries**
   - MOM: Identity and lifecycle
   - ORION: Graph and queries
   - CMS: Coordination and persistence
   - Drivers: External destinations

---

*This document captures terminology and design decisions. Update as decisions evolve.*

*Version 1.0 - 2025-12-05*
