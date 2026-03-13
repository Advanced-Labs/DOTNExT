# F6 Current State Audit: Runtime, Lifecycle, and Component Model

Date: 2026-03-09
Author: Claude Opus 4.6
Purpose: Map existing Scynapse runtime/lifecycle features for spec-to-code bridge work

---

## Plain-Language Summary

**What this is:** A map of Scynapse's current runtime and lifecycle capabilities -- what Orleans provides as a starting base, what Scynapse has added, and what's marked for replacement or deep modification.

**Why it matters:** F6 design (component admission, activation, migration, recovery) should understand what exists before designing new things. But a critical framing note: **Scynapse is not a platform built on top of Orleans.** Orleans is the starting material that will be deeply modified -- its internals are fair game for change wherever the Component Model demands it. Design work should not constrain itself to "what can we build on Orleans's public API."

**What happens next:** Codex uses this to understand the starting codebase. The gap between "what exists" and "what the Component Model needs" defines the design work -- but that gap is just one slice of a much larger R&D program (see Section 8).

**R&D framing:** The Component Model, CNS, routing fabric, mediation layer, and everything in Vision Part 8 represent the bulk of Scynapse's ambition. The current conformance/protocol work, while valuable, covers a small fraction of the total design surface. All design decisions in this space remain provisional and experimental.

---

## 1. Source Documentation

### Feature Docs (Scynapse-specific extensions)
- `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/PluginGrainArchitecture.md` (434 lines) -- dynamic assembly loading architecture
- `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/DynamicGrainAccess.md` -- DLR-based dynamic grain references
- `Docs/Scynapse/Scynapse Features/StatePropertyAccess.md` (568 lines) -- StateTask property access (4 phases)
- `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/Scynapse v0 - Dynamic Grain Features.md` -- overview of all dynamic grain features

### Orleans Internals (reference)
- `Docs/Scynapse/Original Orleans Internals/05-runtime-activation.md` -- grain activation/deactivation
- `Docs/Scynapse/Original Orleans Internals/06-clustering-membership.md` -- cluster membership
- `Docs/Scynapse/Original Orleans Internals/07-messaging-networking.md` -- messaging system

### Vision Docs
- `Docs/Scynapse/Scynapse-Vision.md` Part 2 (fork history, what survives) and Part 3 (what v1 is)
- `Docs/Scynapse/Scynapse-V1.md` -- full v1 technical reference

---

## 2. Feature Inventory

### 2.1 Scynapse-Added Features (Our Extensions to Orleans)

| Feature | Location | Maturity | Tests | Carries Forward? |
|---|---|---|---|---|
| **Plugin Grain Loading** | `src/Scynapse/src/Scynapse.Runtime/DynamicGrains/` | Production | Yes | Concept yes, MDCP mechanism likely redesigned |
| **Grain Type Directory (GTD)** | `src/Scynapse/src/Scynapse.Core.Abstractions/DynamicGrains/` | Complete | Partial | **Discard** -- NewOrleans-era centralized singleton, wrong architecture for decentralized fabric |
| **Dynamic Grain Access** | DLR-based `DynamicGrainReference` | Complete (6 phases) | Yes | **Keep as reference/PoC, won't be upgraded into final form.** The DLR usage is deliberate and connects to deeper CNS plans (IDE/Language-Server/Shell extensions collaborating with DLR-based types for real-time intellisense over CNS, with compiler/analyzer support for static binding when preferred). Useful for prototyping and communicating ideas. The McMaster DotNetCorePlugins (MDCP) library used for assembly isolation may also have continued value. |
| **Grain Events (naturalized)** | `src/Scynapse/src/Scynapse.Core.Abstractions/Events/` + CodeGen | Complete | Integrated | Uncertain -- depends on whether Orleans streams survive into Component Model |
| **StateTask Properties** | `src/Scynapse/src/Scynapse.Core.Abstractions/State/` + CodeGen | Complete (4 phases) | Yes | Concept interesting for OOP surface, mechanism uncertain |
| **Dashboard** | `src/Scynapse/src/Dashboard/` | Production | Yes | **Reference only.** Scynapse will develop its own dashboard and utilities. Current implementation is useful as reference for what to monitor, not as a codebase to extend. |
| **Security Integration** | `Scynapse.Security.Orleans/` | Production (213 tests) | Yes | Pattern survives, Orleans-specific types change (see F5 audit) |

### 2.2 Orleans Infrastructure We Inherit (Not Scynapse-Added)

| Feature | Location | Relevance to Component Model |
|---|---|---|
| **Grain Activation/Deactivation** | `src/Scynapse/src/Scynapse.Runtime/Catalog/` | Core lifecycle -- maps to Varion activation. Semantics mostly carry forward. |
| **Placement Strategies** (9 built-in) | `src/Scynapse/src/Scynapse.Runtime/Placement/` | Maps to "which Node hosts which Varion." Resource-optimized and role-based placement are closest to Component Model needs. |
| **Placement Filters** | Same + `PlacementFilterAttribute` | Extensible -- could implement Varia-aware placement constraints. |
| **Activation Rebalancing** | `src/Scynapse/src/Scynapse.Runtime/Placement/Rebalancing/` | Advanced (entropy-based, state-preserving migration). Directly relevant to Cell/Hive distribution. |
| **Activation Collection** (GC) | `src/Scynapse/src/Scynapse.Runtime/Catalog/ActivationCollector.cs` | Idle grain deactivation. Maps to Varion lifecycle management. |
| **Activation Migration** | `ActivationMigrationManager.cs` | Handles activation movement between silos. Maps to Varion migration between Nodes. |
| **Manifest System** | `src/Scynapse/src/Scynapse.Core/Manifest/` | Cluster-wide type registry. Maps to Component type discovery -- but centralized, needs decentralized replacement. |
| **Silo/Client Lifecycle** | `src/Scynapse/src/Scynapse.Runtime/Lifecycle/` | Full lifecycle observation pattern with staged startup/shutdown. Maps to Node lifecycle. |
| **Cluster Membership** | `src/Scynapse/src/Scynapse.Runtime/MembershipService/` | Node discovery and failure detection. Foundation for fabric participation. |
| **Messaging/Networking** | `src/Scynapse/src/Scynapse.Runtime/Messaging/` | Message routing between silos. Foundation for inter-Node communication. |
| **Serialization** | `src/Scynapse/src/Scynapse.Serialization/` | Code-generated serialization pipeline. Carries forward -- Component types will still need serialization. |

---

## 3. Mapping to M0-A Contract Concepts

| M0-A Concept | Current Analog | Gap |
|---|---|---|
| **Varia** (component unit) | Grain type (loose .NET class) | No Component packaging, membrane, or self-description. Grain types are flat, not grouped into Components. |
| **Varion** (virtual object instance) | Grain activation | Close analog. Virtual actor semantics (activate on demand, deactivate on idle, identity persists) carry forward. |
| **Cell** (per-Node runtime partition for one Varia) | Silo (hosts all grain types) | No per-Varia isolation on a Node. All grains share one silo process. Cell concept is new architecture. |
| **Hive** (distributed envelope of one Varia) | Orleans cluster (hosts all grains) | No per-Varia clustering. The cluster is one thing hosting everything. Hive per-Component distribution is new. |
| **Node** (unified participant) | Silo + Client (separate roles) | Silo/Client split still exists. Node unification (I1) is not yet implemented. |
| **Mediated-first lifecycle** (I4) | Direct grain calls | No mediation layer. Grain calls go directly silo-to-silo. Mediation is new architecture. |
| **Policy-governed routing** (I6) | Placement strategies | Placement decides WHERE, not WHETHER. Policy-based routing denial doesn't exist. |
| **Dynamic type discovery** | Manifest system + GTD + Plugin loading | Partial. Types are discoverable within a cluster. Not decentralized, not cross-organization. |
| **Component packaging** | No analog | Fully new. No concept of a self-describing, distributable Component unit. |
| **CNS resolution** | GrainResourceInference (compile-time URIs) | No dynamic name resolution. URIs are derived from .NET types, not resolved at runtime. |

---

## 4. Key Gaps for Component Model

1. **No Component abstraction.** Grains are loose types. There's no grouping of related types into a Component with a membrane, metadata, and lifecycle. This is the foundational gap.

2. **No Node unification.** Silos and clients remain architecturally distinct. A client cannot host grains. This contradicts I1 (Node Unification). Dissolving this distinction is a significant architectural change.

3. **No mediation layer.** Inter-grain calls are direct (silo-to-silo routing). There's no mediated handshake, no policy evaluation before calls connect, no relay modes. The entire M0-B protocol (HandshakeInit -> Challenge -> Proof -> Accept) is a new layer that doesn't exist yet.

4. **No per-Component isolation.** All grains share one silo, one memory space, one failure domain. Cell isolation (per-Varia compute/memory/network envelope on a Node) is a significant infrastructure change. Note: MDCP provides lite isolation at the type/assembly level, but actual sandboxing is a separate concern -- designs are being evaluated (e.g., WSL/Linux namespaces, and/or a customization/fork of Hyperlight + Litebox from Microsoft).

5. **Centralized discovery.** The manifest system and GTD are cluster-wide singletons. Decentralized, cross-organization discovery (CNS) is entirely new.

---

## 5. What's Directly Reusable

Despite the gaps, substantial infrastructure carries forward:

- **Virtual actor semantics** -- grain activation/deactivation patterns are the right model for Varion lifecycle
- **Placement framework** -- extensible enough to implement Varia-aware placement and Cell-scoped placement directors
- **Activation rebalancing** -- entropy-based load distribution and state-preserving migration are directly relevant to Hive self-organization
- **Dynamic type loading** -- the Plugin Grain Loader's concept (runtime assembly isolation, manifest propagation) informs Component admission design, though the mechanism will change
- **Serialization pipeline** -- code-generated serialization carries forward regardless of architecture changes
- **Security integration pattern** -- call filters as enforcement points (see F5)
- **Lifecycle observation** -- staged lifecycle with observer pattern maps directly to Node and Component lifecycle management

---

## 6. Decision Maturity Classification

| Area | Tier | Rationale |
|---|---|---|
| Virtual actor semantics (activate/deactivate on demand) | Locked Commitment | Proven at scale by Orleans, foundational to Scynapse |
| Serialization pipeline | Locked Commitment | Code-generated, proven, carries forward |
| Placement framework (extensible strategies + filters) | Locked Commitment | Production, well-tested, extensible |
| Activation rebalancing (entropy-based, state-preserving) | Locked Commitment | Advanced, production-tested |
| Plugin grain loading (dynamic assembly isolation) | Design Baseline | Works, concept survives, mechanism may change for Components |
| Manifest system (cluster-wide type registry) | Design Baseline | Works for clusters, needs decentralization for fabric |
| Dashboard/monitoring | Reference Only | Scynapse will build its own dashboard and utilities |
| Grain events / StateTask properties | Design Baseline | Working, but dependent on Orleans grain model that's evolving |
| GTD (singleton catalog) | Discard | NewOrleans-era, wrong architecture |
| Node unification (dissolving silo/client) | Explored Direction | Vision goal, no implementation yet |
| Component Model (Varia/Varion/Cell/Hive) | Explored Direction | M0-A contracts + conformance harness, no production code |
| Mediation layer (multi-step handshake protocol) | Explored Direction | Conformance-validated, no production analog |
| Per-Component isolation (Cell semantics) | Explored Direction | Vision goal, significant infrastructure change needed |

---

## 7. Relevant Code Locations

```
src/Scynapse/src/Scynapse.Runtime/
  DynamicGrains/                    # Plugin loading, assembly isolation
  Catalog/                          # Activation lifecycle, collection, GC
  Placement/                        # 9 strategies, filters, rebalancing
  Lifecycle/                        # Staged lifecycle, observer pattern
  Messaging/                        # Inter-silo message routing
  MembershipService/                # Cluster membership, failure detection

src/Scynapse/src/Scynapse.Core.Abstractions/
  DynamicGrains/                    # GTD, grain packages, manifests
  Events/                           # Grain event subscriptions
  State/                            # StateTask, StateAttribute
  Lifecycle/                        # Lifecycle interfaces
  Manifest/                         # Type manifests, cluster manifests
  Placement/                        # Strategy and filter abstractions

src/Scynapse/src/Scynapse.CodeGenerator/
  EventCodeGenerator.cs             # Grain event code generation
  StatePropertyCodeGenerator.cs     # StateTask property code generation

src/Scynapse/src/Dashboard/        # Monitoring infrastructure
```

---

## 8. Critical Framing: Scynapse's Relationship to Orleans

**Scynapse is not built "on top of" Orleans.** Orleans is a starting base -- "a damn good base" -- that Scynapse will modify as deeply as needed. Orleans's internals (activation, messaging, placement, membership, serialization) are all modifiable. When the Component Model needs changes to how messages route, how activations are scoped, how membership works, or how types are discovered, we change Orleans itself.

This means:
- Protocol designs are **not constrained** to what Orleans's public API supports
- Runtime changes (to messaging, placement, activation, etc.) are **expected**, not exceptional
- The distinction between "Orleans infrastructure" and "Scynapse additions" will blur over time as Orleans internals get modified

**The scope of what's new is much larger than this audit covers.** The Component Model, CNS, routing fabric, mediation layer, democratic anonymity, federated trust, Component packaging/survival, infrastructure-as-Components -- these represent the vast majority of Scynapse's ambition (see Vision Part 8: eleven open problem areas). The current conformance/protocol work covers a valuable but small slice. All design decisions in this space are R&D-grade: provisional, experimental, and subject to redesign as we learn.

**NewOrleans-era features (GTD, Package System, Dynamic Grain Access DLR mechanism) should be treated as discards** -- they were designed for a different architecture with different aims. Their feature docs are useful as reference for the problems they were solving, but the solutions don't carry forward.

---

*This audit maps existing runtime/lifecycle to Component Model concepts. The key insight: Orleans gives us solid low-level runtime primitives (virtual actors, placement, rebalancing, serialization, lifecycle management) that Scynapse will build on AND modify. The new architecture (Components, mediation, CNS, policy routing, Cell/Hive, Node unification) requires both new layers and deep changes to existing Orleans internals.*
