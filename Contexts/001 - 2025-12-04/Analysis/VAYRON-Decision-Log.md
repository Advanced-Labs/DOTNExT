# VAYRON Decision Log

> **Document Type:** Decision Record
> **Version:** 1.0
> **Date:** 2025-12-07
> **Purpose:** Track all architectural and strategic decisions

---

## Decision Format

Each decision follows this format:
- **ID:** Unique identifier (VDEC-XXX)
- **Date:** When decided
- **Status:** Proposed | Approved | Implemented | Superseded
- **Decision:** What was decided
- **Context:** Why this decision was needed
- **Options Considered:** What alternatives existed
- **Rationale:** Why this option was chosen
- **Consequences:** What this means going forward
- **Decider:** Who made the decision

---

## VDEC-001: Build Real Infrastructure First (No PoCs)

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

Build the VAYRON SDK, project templates, and VS2022 integration as production-quality infrastructure from the start. Do not build "proof of concept" implementations that will be thrown away.

### Context

When starting a complex platform, there's a choice:
1. Build quick PoCs to validate ideas, then rebuild properly
2. Build properly from the start, accepting slower initial progress

Traditional wisdom favors PoCs. But our context is different:
- AI-assisted development velocity is high
- Louis has VS extension experience
- DOTNExT already integrates with VS2022
- Good tooling investment compounds across all future work

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| PoC-first | Faster initial progress | Technical debt, throwaway code |
| Production-first | Compounds investment | Slower to first demo |

### Rationale

1. **Velocity has changed.** With AI assistance, "build it right" is faster than before.
2. **Experience exists.** Louis has built VS extensions before - not unknown territory.
3. **Existing foundation.** DOTNExT/VS2022 workflow already works.
4. **Compounding.** Every tooling improvement benefits all future work.
5. **Dogfooding.** Building VAYRON with VAYRON tooling reveals problems immediately.

### Consequences

- Initial development focuses on SDK/tooling before application-level features
- All work is done with expectation of production use
- Higher quality bar from day one
- Slower time to first "wow demo" but faster time to usable platform

---

## VDEC-002: Defer Async+ Continuation Completion

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

Do not complete Async+ continuation (awaiter resume) until VCOM infrastructure exists. Keep the analysis and partial implementation, but defer the final codegen work.

### Context

Async+ currently:
- ✅ Persists state machine states
- ✅ Reloads state machine states
- ❌ Does not properly resume at correct continuation point
- ❌ Does not rehydrate object references

The reference rehydration problem requires resolving UUIDs to live objects - which is exactly what VCOM will provide.

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| Complete Async+ now | Feature complete sooner | Build throwaway UUID resolution |
| Defer until VCOM | Uses real infrastructure | Async+ incomplete longer |

### Rationale

1. **Dependency ordering.** Async+ continuation depends on VCOM.Resolve(). Building a temporary version is waste.
2. **Historical analogy.** You don't build DCOM before COM. The higher abstraction needs the lower one.
3. **Value already captured.** State persistence works. Continuation can wait.
4. **Design informs.** Knowing *how* we'll solve it shapes VCOM design, even without implementing yet.

### Consequences

- Async+ remains partially complete
- VCOM design considers Async+ needs
- Reference rehydration implemented once, properly
- Some workflows blocked until VCOM exists

---

## VDEC-003: NewOrleans is Hidden Infrastructure

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

NewOrleans (our Orleans fork) is completely hidden from VAYRON developers. They never see "silos," "grains," or "cluster" terminology. These concepts are exposed as VAYRON concepts: "nodes," "VCOM objects," etc.

### Context

VAYRON is built on NewOrleans, but developers using VAYRON shouldn't need to understand Orleans to be productive. The distribution/persistence machinery should be invisible.

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| Expose Orleans directly | Less abstraction, familiar to some | Complexity leak, two mental models |
| Hide completely | Clean developer experience | Another abstraction layer |
| Partial exposure | Flexibility | Inconsistent, confusing |

### Rationale

1. **Goal alignment.** VAYRON frees AI from boilerplate. Orleans concepts are boilerplate.
2. **Precedent.** Developers don't need to understand TPL to use async/await.
3. **Cleaner mental model.** One set of concepts, not two overlapping.
4. **Future flexibility.** If we replace Orleans internals later, no API changes.

### Consequences

- VAYRON SDK never references Orleans namespaces publicly
- "VAYRON Node" = Orleans Silo
- "VCOM Object" = Orleans Grain (conceptually)
- Configuration uses VAYRON terminology
- Power users can still access internals if needed (escape hatch)

---

## VDEC-004: Three-Layer Resolution Model (MAC/IP/DNS Analogy)

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis + Claude

### Decision

VAYRON has three resolution layers, analogous to networking:

1. **Grain-level (MAC-like):** Direct grain key resolution. Internal only.
2. **VCOM-level (IP-like):** UUID-based object identity. Used by infrastructure.
3. **VNS-level (DNS-like):** Human-friendly addressing. Used by developers.

### Context

Different operations need different resolution mechanisms:
- Async+ continuation needs UUID → Object (VCOM level)
- Developer queries need semantic/named → Object (VNS level)
- Internal grain operations need key → grain (grain level)

Conflating these causes confusion and poor design decisions.

### Rationale

1. **Separation of concerns.** Each layer has clear responsibility.
2. **Familiar model.** Networking analogy is widely understood.
3. **Clarifies dependencies.** Async+ needs VCOM, not VNS.
4. **Enables optimization.** Each layer can be optimized independently.

### Consequences

- VCOM.Resolve() is the UUID → Object operation
- VNS.Find() is the semantic → Object operation
- Developers typically use VNS
- Infrastructure (Async+, relationships) use VCOM
- Internal code uses grain layer

---

## VDEC-005: Code-as-First-Class, Binaries-as-Cache

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

VCOM types "own" their source code. Code is persisted as the primary artifact. Compiled binaries are cached for performance but are derived, not primary.

### Context

VAYRON enables self-evolving code. Objects can modify their type's code. For this to work, code must be:
- Accessible to objects
- Mutable at runtime
- Versioned and tracked
- The source of truth

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| Binary-first (traditional) | Simpler, familiar | Can't modify code at runtime |
| Code-first | Enables self-evolution | Requires runtime compilation |
| Both equally primary | Flexibility | Complexity, sync issues |

### Rationale

1. **Vision alignment.** Self-evolving code requires code-as-first-class.
2. **Debuggability.** Always have source for any object.
3. **AI integration.** AI works with code, not binaries.
4. **Caching is orthogonal.** We still get binary performance where needed.

### Consequences

- VTypeGrain stores source code
- Compilation happens at runtime (on demand)
- Binaries cached in file system / RavenDB
- Code mutations create new versions
- Objects can introspect their code

---

## VDEC-006: VARIA Uses Roslyn Fork for Transformation

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis + Claude

### Decision

VARIA (the developer surface layer) uses our Roslyn fork to transform developer code into VCOM-aware code. Not source generators, not IL weaving - full compiler control.

### Context

VARIA needs to transform:
- `new MyType()` → VCOM creation
- Property access → VCOM state access
- Method calls → grain invocations
- Reference types → UUID-based relationships

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| Source Generator | Standard tooling | Limited transformation |
| IL Weaving (Fody) | Full transformation | Post-compile, complex |
| Roslyn Fork | Maximum control | Non-standard, distribution |

### Rationale

1. **We have it.** DOTNExT includes Roslyn fork. Already done.
2. **Maximum capability.** Can transform anything.
3. **C= preparation.** If we add language features, compiler control is essential.
4. **Async+ precedent.** Already modifying compiler for Async+.

### Consequences

- VAYRON SDK includes our Roslyn compiler
- Non-standard compilation (acceptable for VAYRON ecosystem)
- Full transformation capability
- Can add language features later (C=)

---

## VDEC-007: Persistence Stores Selection

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

Initial VAYRON persistence uses:
- **RavenDB** (server): Document storage for object state, type definitions, code
- **Neo4j** (local): Graph storage for relationships, type hierarchy, semantic index
- **AuraDB** (cloud): Neo4j cloud equivalent for distributed deployments
- **File system**: Binary cache, bootstrap configuration

### Context

VAYRON needs:
- Document storage (object state)
- Graph storage (relationships, VNS)
- Semantic search (vector embeddings)
- Local and cloud options

### Rationale

1. **RavenDB + Neo4j both support vectors.** Semantic search covered.
2. **Graph native.** Neo4j is purpose-built for relationship queries.
3. **Proven at scale.** Both are production databases.
4. **AuraDB = Neo4j cloud.** Consistent graph model local and cloud.

### Consequences

- Two database dependencies (document + graph)
- Orleans storage providers needed for both
- Local development can use embedded or local instances
- Cloud deployment uses managed services

---

## VDEC-008: Single Node Default for Development

**Date:** 2025-12-07
**Status:** APPROVED
**Decider:** Louis

### Decision

Default VAYRON configuration runs a single node (single Orleans silo). Multi-node/cluster configuration is opt-in for when needed.

### Context

Most development and many production scenarios don't need multiple nodes. Starting simple reduces complexity and makes getting started easier.

### Rationale

1. **Simplicity first.** One node is easier to understand and debug.
2. **Still distributed.** Single node still uses Orleans patterns. Scaling is configuration, not code change.
3. **Development speed.** Local dev doesn't need cluster setup.
4. **Progressive complexity.** Add nodes when needed.

### Consequences

- Default `vayron.config.json` creates single local node
- No mandatory cluster setup for getting started
- Multi-node documented separately
- Same code works single or multi-node

---

## Future Decisions (Pending)

### VDEC-009: VNS Initial Scope

**Status:** PROPOSED
**Question:** Start with local-process VNS or distributed from day one?
**Recommendation:** Local process first. Distributed is grain placement which Orleans handles.

### VDEC-010: Async Object Construction

**Status:** PROPOSED
**Question:** How to handle `new()` when VCOM creation is async?
**Options:** Factory methods, lazy wrappers, or Roslyn transformation
**Recommendation:** Roslyn transformation - we control the compiler.

### VDEC-011: VS2022 Extension Scope

**Status:** PROPOSED
**Question:** What's in V1 of VS extension?
**Recommendation:** Project recognition + basic IntelliSense + debugging. Defer VNS browser.

---

*This log tracks architectural decisions. All significant decisions should be recorded here for future reference.*

*Version 1.0 - 2025-12-07*
