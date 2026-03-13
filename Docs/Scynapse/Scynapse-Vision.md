# Scynapse: The "Component is the Network" Vision

## Preamble

### What This Document Is

This is the foundational vision document for Scynapse — a distributed computing platform being developed as a fork/evolution of Microsoft Orleans. It explains what Scynapse is, what it builds on, what exists today, and where it is going.

**Audience:** AI agents joining the Scynapse project with no prior knowledge. You are assumed to be technically sophisticated and familiar with distributed systems, actor models, Microsoft Orleans, .NET, and technologies like NATS/Synadia. This document will use those as conceptual shortcuts where helpful.

**Companion document:** `Scynapse-V1.md` covers the current v1 implementation in full technical detail — every API, every test, every file path. Read that for implementation specifics. This document focuses on the *vision* and the *paradigm* — the conceptual framework that gives meaning to everything in v1 and everything that will follow.

### The Critical Disclaimer

**Scynapse is R&D.** There is no production deployment. No one outside the team sees the repository. V1 is the first milestone. V2 — the first version intended to be actually usable — is still ahead.

**Everything not yet implemented is open.** When this document describes future concepts — the Component Model, the CNS, routing fabrics, survival guarantees, infrastructure Components — these are *explored directions* in an actively evolving design space. Any of them could be completely redesigned, replaced, or abandoned. We are actively seeking better solutions in every relevant domain.

The vision itself — "Component is the Network" — is the stable gravitational center. The means to realize it are not. Do not read potential designs as commitments. Read them as the current best understanding of a problem space we are still mapping.

What IS committed: the code on the branch, the tests that pass, the security primitives that work. Everything else is hypothesis.

---

## Part 1: Orleans Foundations

Scynapse is built on top of Microsoft Orleans, so understanding Orleans is prerequisite to understanding Scynapse. This section covers what matters for the vision. If you already know Orleans well, skim for the critical observations at the end — they explain *why* Scynapse departs from Orleans.

### The Virtual Actor Model

Orleans is a distributed actor framework for .NET. Its core abstraction is the **grain** — a virtual actor. A grain is:

- **An object** with an interface (methods you can call), an identity (a key — string, integer, GUID), and optionally persistent state.
- **Virtual** — you don't create or destroy grains. You address them by type and key, and the runtime materializes them on demand. If no activation exists, calling a grain creates one. If it's idle too long, the runtime deactivates it. The grain's logical identity persists even when no activation exists.
- **Single-threaded** — the runtime guarantees that only one call executes on a grain activation at a time. No locks, no races. This is the key simplification Orleans provides.
- **Location-transparent** — you call a grain by its type and key. The runtime routes the call to whichever server hosts that grain's activation. You never specify "call the grain on server X."

### Silos and Clusters

A **silo** is a host process running the Orleans runtime. Multiple silos form a **cluster** via a membership protocol (backed by a database, Azure Table Storage, ZooKeeper, etc.). The cluster collectively hosts grain activations — the runtime distributes grains across silos and routes calls between them.

Silos are **servers**. Orleans also has a **client** concept — an external process that connects to the cluster to make grain calls but does not host grains itself. This server/client distinction is fundamental to Orleans and, as we'll see, is something Scynapse's vision dissolves.

### The Orleans Programming Model

A grain is defined as a .NET interface + implementation:

```csharp
// The contract — this is what callers see
public interface IOrderGrain : IGrainWithStringKey
{
    Task<OrderDetails> GetDetailsAsync();
    Task PlaceOrderAsync(OrderRequest request);
}

// The implementation — this runs inside a silo
public class OrderGrain : Grain, IOrderGrain
{
    public Task<OrderDetails> GetDetailsAsync() { /* ... */ }
    public Task PlaceOrderAsync(OrderRequest request) { /* ... */ }
}
```

Callers obtain a grain reference and call methods on it. The runtime handles serialization, routing, activation, deactivation, and failure recovery:

```csharp
var grain = grainFactory.GetGrain<IOrderGrain>("order-123");
var details = await grain.GetDetailsAsync(); // Could be on any silo
```

Orleans also provides:
- **Persistence** — grains can have durable state backed by storage providers
- **Streams** — pub/sub messaging (SMS, EventHub, etc.)
- **Reminders** — durable timers that survive grain deactivation
- **Grain directories** — how the runtime knows which silo hosts which grain
- **Serialization** — a code-generated serialization pipeline for grain call arguments and return values
- **RequestContext** — ambient key-value data that flows with grain calls through the call chain (critical for Scynapse's security system)

### What Orleans Does NOT Provide

These are the gaps that motivate Scynapse:

- **No security model.** Orleans trusts everything inside the cluster implicitly. There is no authentication, no authorization, no capability system. You can enable TLS for transport encryption, but there is no identity or access control at the grain call level.
- **No dynamic type loading.** Grain types must be known at compile time and present in all silos at startup. You cannot load new grain types at runtime into a running cluster.
- **Rigid server/client distinction.** Silos host grains and serve calls. Clients connect and make calls. A client cannot host grains. A silo cannot behave as a client of a separate cluster without additional plumbing.
- **No concept of "component" or "package."** Grain types are loose .NET classes. There is no grouping of related grains into a deployable, versionable, self-describing unit.
- **No decentralization story.** Orleans assumes a cooperative cluster under unified administrative control. There is no trust model for federating clusters, no mechanism for independent parties to participate in a shared fabric.
- **Grains are flat.** There is no hierarchy of containment — grains don't contain other grains, types don't group into larger units with their own boundaries.

### Critical Observation

Orleans solved a genuinely hard problem: making distributed programming feel like object-oriented programming. Grains behave like objects. Calls behave like method calls. The developer doesn't think about servers, serialization, or routing.

But Orleans solved it for a **specific topology**: a trusted cluster of servers, with external clients connecting in. Scynapse asks: *what if we took that same insight — distributed programming as object-oriented programming — and applied it to a fundamentally different topology?* One where every participant is both server and client. Where the unit of distribution is not a grain type but a *Component* — a living, self-organizing, self-describing organism in a shared cyberspace. Where the fabric itself is made of the same kind of Components it hosts.

That question is Scynapse.

---

## Part 2: From Orleans to Scynapse — The Fork History

### The NewOrleans Era (Pre-Vision)

Scynapse began as a fork of Microsoft Orleans, initially named **NewOrleans**. During this era, several extensions were added to Orleans that addressed some of its limitations. These are **legacy features** — they exist in the codebase but most will likely not survive in their current form as Scynapse evolves toward its vision. They are documented here for historical context and because some of the *problems* they address remain relevant even if the *solutions* will change.

#### Dynamic Grain Access (Likely Survives in Some Form)

**Problem:** Orleans requires compile-time references to grain interfaces. You cannot call a grain whose type you discover at runtime.

**Solution built:** `DynamicGrainReference` — a DLR-based (Dynamic Language Runtime) dynamic proxy that allows calling grain methods without static type references. Combined with `GrainFactoryExtensions` for creating dynamic references from type metadata.

**Why it matters for the vision:** In a world where Components are loaded and discovered at runtime, dynamic access to types you didn't know about at compile time is fundamental. The specific DLR-based implementation may not survive, but the capability — "call a type you learned about at runtime, with full OOP semantics" — is essential and will need to exist in Scynapse's final form.

**Current status:** Fully implemented (6 phases complete). See `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/DynamicGrainAccess.md` for the full design.

#### Plugin Grain Loading (Concept Likely Survives)

**Problem:** Orleans requires all grain types to be present at startup. You cannot add new grain types to a running cluster.

**Solution built:** `IPluginGrainLoader` — runtime assembly loading for grain types using .NET's `AssemblyLoadContext` for isolation. Includes manifest propagation so all silos learn about newly loaded types.

**Why it matters for the vision:** If Components are organisms that can appear in the fabric at runtime, the fabric must be able to load new types dynamically. The assembly-level isolation and manifest propagation are directionally correct even if the mechanism will be redesigned.

**Current status:** Implemented. Now access-controlled by Scynapse v1's security system (requires admin CCap to invoke).

#### Grain Type Directory — GTD (Unlikely to Be Reused)

**Problem:** No runtime-queryable catalog of what grain types exist in the cluster.

**Solution built:** `IGrainTypeDirectoryGrain` — a singleton grain that catalogs all grain types, their interfaces, methods, and metadata. Queryable at runtime.

**Why it matters for the vision:** Discovery is essential — Components need to find each other. But the GTD is a centralized singleton grain, which is architecturally wrong for a decentralized fabric. The *problem* (type discovery) is critical; the *solution* (a singleton catalog) will be replaced by something distributed.

**Current status:** Implemented, access-controlled. Left in codebase but expected to be superseded.

#### Other NewOrleans Era Features

- **Naturalized C# events on grains** — grain events bridged to Orleans SMS streams, allowing C#-style `event` syntax. Status: implemented, future uncertain.
- **StateTask\<T\> properties** — remote property access on grains via code-generated Get/Set methods, enabling C#-style property syntax across the network. Status: implemented, concept interesting for the OOP-surface vision.

### The Rename: NewOrleans to Scynapse

The rename from NewOrleans to Scynapse marks the conceptual break. NewOrleans was "Orleans with extensions." Scynapse is "a new platform with a vision, currently implemented on Orleans." The name change signals that Orleans is the foundation, not the destination.

**Naming note:** References to "NewOrleans" in the codebase are legacy. The canonical name is Scynapse.

---

## Part 3: Scynapse v1 — What Exists Today

V1 is fully documented in the companion document `Scynapse-V1.md`. This section provides a structural summary with emphasis on what matters for the vision.

### The Security System — The Focus of v1

V1's contribution is a **complete cryptographic security system** with no equivalent in stock Orleans. This is not a stopgap — the security primitives are designed to carry forward into the Component Model era. The security system is the first real Scynapse-native infrastructure, and its design reflects the vision even though it currently operates on Orleans's paradigm.

#### What Was Built

**Cryptographic Identity (Layer 0):**
Every entity in Scynapse has an Ed25519 keypair. The public key IS the identity — no registry, no central authority. Key types are distinguished by prefix (Organization, Node, User, ComponentType, Instance, etc.) using NATS NKeys-inspired Base32 encoding with CRC-16 checksums. This is implemented and tested.

**Signed Assertions (Layer 1):**
A single universal primitive for identity, capability, delegation, relation, revocation, and impersonation. All share the same structure, same verification algorithm, same CBOR serialization (CTAP2 canonical form), same Blake2b-256 content addressing. Identity is the degenerate case of capability — a self-signed assertion where issuer equals subject, meaning "I exist." This unification is deliberate: one library, one code path, one set of tests. Implemented and tested.

**Chain Verification (Layer 2):**
Recursive assertion chain walker with configurable max depth (default 32). Verifies: content hash integrity, Ed25519 signature, temporal bounds, replay prevention (nonce store), chain continuity (parent's subject == child's issuer), attenuation (each delegation can only narrow, never widen), and root termination (chain must end at a trusted root). Implemented and tested.

**Transport Security (Layer 3):**
Self-signed X.509 certificates with ECDSA P-256 for TLS handshake mechanics (because .NET's SslStream doesn't support Ed25519 for TLS yet) and the real Ed25519 public key embedded in a custom X.509 extension. This is explicitly a platform workaround — the ECDSA key has no security meaning. Implemented, with caveats: TLS-level assertion verification is currently bypassed in favor of call-filter-level enforcement.

**Orleans Integration (Layer 4):**
- `ScynapseIncomingCallFilter` — THE enforcement point. Every grain call passes through it.
- `ScynapseOutgoingCallFilter` — automatic CCap selection from wallet, bearer proof generation.
- `[SecurityPolicy]` and `[RequireCapability]` attributes for declarative grain security.
- `ICCapWallet` / `InMemoryCCapWallet` — client-side CCap storage with wildcard matching and expiry filtering.
- `GrainResourceInference` — automatic derivation of NATS-style dot-separated resource URIs from grain interfaces (`scynapse.app.IOrderGrain.PlaceOrder`).
- `ISecurityGatewayGrain` — breaks the bootstrap paradox (issue CCaps without requiring one).
- `DevelopmentMode` — zero-friction security setup for development.
- `UseScynapseSecurity()` one-liner for both silo and client configuration.
- Configuration loading from `appsettings.json`.

**Scy CLI:**
A topology-aware provisioning tool (inspired by NATS's `nsc`) that understands the Organization → Node → User trust hierarchy. Commands: `init org/node/user`, `grant` (issue CCaps), `bundle` (deployment packages), `inspect`, `verify`, `dev quickstart`. Generates correct delegation chains by construction.

**Test Coverage:**
213 tests across unit, Orleans integration, and cross-silo integration. Tests cover: key generation, assertion serialization, chain verification, attenuation, replay prevention, call filter enforcement, wallet CCap selection, cross-silo grain-to-grain CCap flow on real TestCluster with 2+ silos.

#### The Subject Namespace

Resource URIs in Scynapse use NATS-style dot-separated hierarchical names with wildcards:

```
scynapse.system.security.gateway    — system infrastructure
scynapse.app.IOrderGrain.PlaceOrder — specific grain method
scynapse.app.IOrderGrain.*          — all methods on a grain (one-segment wildcard)
scynapse.app.>                      — all application grains (multi-segment wildcard)
```

This naming scheme is not incidental. It is designed to become the vocabulary for the future CNS (Scynapse Name System), for security resource matching, and potentially for NATS interoperability. The wildcards (`*` matches one segment, `>` matches one or more trailing segments) are proven at massive scale in NATS.

#### The Hybrid Trust Model

V1 implements two simultaneous trust levels:

- **Node-Level Trust:** Silos with valid delegation chains from a trusted root trust each other's grain calls by default. This matches Orleans's inherent model.
- **Caller-Level Trust:** External clients must present a CCap (Crypto-Capability) matching the target grain's requirements. Verified: signature, chain, temporal bounds, attenuation, bearer proof.
- **Strict Mode:** Per-grain opt-in (`[SecurityPolicy(RequiresCallerCapability = true)]`) forces CCap verification even from trusted silos.
- **Original Caller Propagation:** The end-user's identity flows through the entire grain call chain via RequestContext.

#### Known Limitations of v1

These are documented, deliberate, and understood:

- TLS-level assertion verification is bypassed (identity enforced at call filter level only)
- Orleans streams and events bypass the grain call filter pipeline — not secured
- In-memory assertion and nonce stores — lose state on restart, no cross-silo revocation propagation
- `AllowAnonymous` default for unannotated grains (necessary for Orleans system grain compatibility)
- No online key rotation
- No automatic CCap delivery to caller wallet

See `Scynapse-V1.md` Part 7 (Known Limitations) for the complete list.

#### What v1 Proves

V1 is not a prototype or proof-of-concept in the dismissive sense. It proves several things that matter for the vision:

1. **A cryptographic identity and capability system can be integrated into Orleans's grain call pipeline.** The call filter pattern works. CCaps flow through RequestContext across silo boundaries.
2. **The unified assertion model is sound.** One primitive for identity, capability, delegation, relation, revocation. One verification algorithm. One serialization format. This is not theoretical — 213 tests confirm it works.
3. **The NATS-style subject namespace works for security resource matching.** Hierarchical dot-separated names with wildcards are expressive enough for grain-level access control.
4. **The separation of concerns is clean.** `Scynapse.Security` has zero Orleans dependency. `Scynapse.Security.Orleans` is pure integration. This means the security primitives can outlive the Orleans integration layer.

These are foundation stones, not scaffolding.

---

## Part 4: The Vision — Component is the Network

This is the heart of the document. Everything before this section is context. Everything after it is consequence.

### The Problem with Current Distributed Systems

Modern distributed systems are built from **services** — independent processes communicating over network protocols (HTTP, gRPC, message queues). Each service is a black box with a wire-protocol API. The developer must think in terms of serialization, network calls, retries, circuit breakers, service discovery, load balancing, and failure modes. The "objects" inside a service are invisible to the outside world. The distributed system is a graph of opaque endpoints.

Orleans improved on this by making distributed calls feel like method calls on objects. But Orleans kept the topology: trusted servers hosting grains, external clients connecting in. The "distributed system" is still a cluster — a bounded, administered, homogeneous island.

The real world is not a cluster. It's a heterogeneous fabric of devices, servers, edge nodes, phones, embedded systems — all with different capabilities, trust levels, lifetimes, and owners. Software needs to live across this fabric, not inside a single administered cluster.

### The Fabric: Homogeneous Nodes

Scynapse's starting point is the dissolution of the server/client distinction.

**In Scynapse, every participant is a Node.** A Node is a process running the Scynapse runtime. A "server" is a Node. A "client" is a Node. A phone running a Scynapse app is a Node. An edge device is a Node. They differ in *capability* (CPU, memory, connectivity, uptime) but not in *kind*. Every Node can host Components, serve calls, route traffic, participate in the fabric.

This is not an ideological commitment to symmetry. It's a structural necessity. If Components are organisms that live across the fabric, and the fabric is made of Nodes, then every Node must be able to host Component instances. A "client" that can only consume but never serve is a parasite — it takes from the fabric without contributing. In Scynapse, using a Component means becoming a server of that Component. This has a concrete consequence: **the more popular a Component is, the more Nodes serve it, the more resources it has, the more resilient it becomes.** Popularity is strength, not load.

The Nodes form a **fabric** — a substrate of interconnected processes running the Scynapse runtime. The fabric is the cyberspace in which Components live.

### What is a Component?

A Component is the fundamental unit of software in Scynapse. It is simultaneously:

**1. An Artifact.** A Component is a distributable unit — binaries, source code, both, or neither (a Component could be purely declarative — types, interfaces, dependency manifests, metadata). It is the "package" in the most complete sense: not just code to deploy, but a complete description of a piece of software including its types, its dependencies on other Components, its exposed surface, and its internal structure.

**2. A Virtual Entity.** A Component, like an Orleans grain, has a virtual existence that transcends any particular Node. "Component X" exists as a concept in the fabric — it has an identity, a version, dependencies, and a known interface — regardless of whether any instance of it is currently running on any Node. The Component's identity persists.

**3. Runtime Instances.** At any given time, a Component may have instances (activations) on zero, one, or many Nodes. These instances collectively form the Component's presence in the fabric. They are the Component *as a living entity*.

**The critical insight:** These three aspects are not separate things. They are one thing experienced at different levels of abstraction. The artifact, the virtual identity, and the runtime instances **continuously self-organize into a single coherent entity.** Because that entity is distributed across multiple Nodes, it can be described as a network.

**Component is the Network.**

A Component type running on Nodes A, B, and C forms a single virtual network — the Component IS the network it forms. Not "the Component uses a network" or "the Component is deployed to a network." The Component IS one. Its instances are its nodes. Its inter-instance communication is its links. Its type definition is its protocol. Its membrane is its boundary.

### The Organism Metaphor

The word "organism" is not decorative. It is structurally accurate.

A biological organism is a self-organizing system with:
- An identity that persists across changes in constituent matter
- A boundary (membrane) that separates inside from outside
- Internal structure (organs, cells) with specialized functions
- An interface to the environment (sensory, motor, communicative)
- The ability to grow, shrink, heal, reproduce, and die
- Metabolism — it consumes resources and produces effects

A Scynapse Component has all of these properties:
- **Persistent identity** — the Component exists as a virtual entity regardless of which Nodes run it
- **Membrane** — the boundary between internal types and exposed surface (see next section)
- **Internal structure** — Types with different roles, some internal, some exposed
- **Interface** — the OOP surface exposed through the membrane
- **Lifecycle** — instances appear, migrate, deactivate; the Component grows and shrinks with demand
- **Resource metabolism** — it runs on the compute/memory/network resources of its hosting Nodes

What makes this more than a metaphor is the self-organization. A Component's instances don't just happen to be running on multiple Nodes — they actively coordinate, discover each other, maintain coherence, and respond to changes in the fabric. This is the behavior of a living system, not a deployed artifact.

### The Membrane

The membrane is a single concept, not three bolted together. It is simultaneously:

- **The security boundary** — what the security system (CCaps, assertions, trust model) protects
- **The visibility boundary** — what other Components can see and what is hidden
- **The API boundary** — what other Components can interact with

These are one thing because they must be. A type that is "visible but not callable" or "callable but not secured" is a design error. The membrane is the line between inside and outside, and all aspects of that line — visibility, accessibility, security — are facets of the same boundary.

#### What the Membrane Exposes: An OOP Surface

What's exposed through the membrane is not a REST API or a flat set of endpoints. It is a **full object-oriented surface**: Types with methods, properties, events, and other OOP member kinds. Interaction with a Component looks like:

```
Component.Type.Instance.Member
```

For example (conceptual, for illustration):
```
InventoryService.IInventoryGrain.warehouse-7.GetStockLevel()
InventoryService.IInventoryGrain.warehouse-7.StockChanged  (event)
InventoryService.IInventoryGrain.warehouse-7.Location       (property)
```

This is the Orleans insight taken further: distributed programming as object-oriented programming, but now the "objects" are Types within Components, and the interaction surface preserves the full richness of OOP — not just methods/functions but events, properties, and any other member kind the type system supports.

#### Hierarchical Authority

Types exposed through the membrane can have their own boundaries — a Type might expose some members publicly and keep others restricted. But the **Component's membrane has authority over all boundaries within it.** The Component can:

- Override a Type's visibility (make a normally-public Type internal)
- Restrict a Type's capabilities (limit what actions a Type's members allow)
- Shape the boundary conditions for all its constituent Types

And Types can do the same for their sub-structures. This creates a **hierarchy of authority over boundaries**: Component > Type > Instance. Parent boundaries have shaping power over child boundaries. A Type cannot expose more than its Component allows. An Instance cannot expose more than its Type allows.

This aligns with the security system: the capability model naturally supports hierarchical scope narrowing (attenuation). A CCap granted at the Component level can be narrowed to a specific Type, then to a specific method. The hierarchy of authority in the membrane IS the hierarchy of attenuation in the security model. One concept, one mechanism.

### Types as the Execution Unit

Within a Component, **Types are the unit of execution** — analogous to grain types in Orleans. A Type defines behavior, state, and interface. Instances of a Type are the live activations that handle calls.

Some Types are **fully internal** — invisible outside the Component, used only by other Types within the same Component. Some are **fully exposed** — visible and callable from outside through the membrane. Some are **partially exposed** — some members public, some private to the Component.

This maps naturally onto existing OOP visibility concepts (public, internal, protected) but at a distributed-systems scale. The novelty is that these visibility boundaries are enforced by the security system across the fabric, not just by the compiler within a process.

### The Recursive Insight: Infrastructure is Components Too

Here is where the vision becomes self-similar.

The fabric — the substrate of Nodes running the Scynapse runtime — needs infrastructure: naming, discovery, persistence, routing, package management, source code hosting, monitoring, diagnostics. In a traditional platform, these are "the platform" — built-in, monolithic, different in kind from the applications running on top.

**In Scynapse, the infrastructure is made of Components.** Mandatory, foundational, kernel-bound Components that are part of the runtime — but still Components. They have membranes. They have Types. They expose an OOP surface. They live in the fabric. They ARE the fabric.

This means:

- The **Distributed Repository Component** (a potential foundational Component) provides source code hosting for all other Components. It doesn't live "outside" the fabric — it IS a Component in the fabric, running on every Node, mandatory and foundational.
- The **Distributed Package System Component** (potential) handles Component distribution, versioning, and survival. It's a Component like any other, but mandatory.
- The **Persistence Component** (potential) provides durable state. It's a Component.
- The **Naming/Discovery Component** (potential — related to the CNS concept) resolves names to instances. It's a Component.

The space and what lives in it are the same kind of thing. The container and the contained are the same nature. The infrastructure that creates the cyberspace IS organisms in that cyberspace — just mandatory ones, present everywhere, built over the runtime kernel.

This is not a theoretical elegance. It has practical consequences:

1. **Infrastructure evolves like everything else.** A better persistence model can be deployed as a new Component version. The infrastructure is not frozen in the platform.
2. **Infrastructure is distributed like everything else.** The naming system, the package system, the repository — all get the same distribution, resilience, and self-organization properties as application Components.
3. **The programming model is uniform.** Interacting with infrastructure uses the same OOP surface as interacting with application Components. No separate SDK for "platform services" vs. "application services."
4. **The security model is uniform.** Infrastructure Components are protected by the same CCap system as application Components. Access to the package system or the naming service requires capabilities, just like access to an application grain.

---

## Part 5: Component Anatomy — Deeper

### The Component as Holistic Software

A traditional piece of software is fragmented across systems: source code in GitHub, binaries in a package registry, configuration in a deployment system, state in a database, runtime in a container orchestrator. The "software" is a ghost that haunts multiple systems — it has no single, self-describing, self-contained existence.

A Scynapse Component is holistic. It aims to be the complete embodiment of a piece of software:

- **Source code** — the Component IS its own distributed source repository. Not "the Component's source code is hosted somewhere" but "the living Component in the fabric carries its source code with it." The source is part of what the Component is.
- **Binaries** — compiled artifacts, if applicable. Some Components may be source-only (interpreted, JIT-compiled, or compiled on-demand by the fabric).
- **Metadata** — type declarations, dependency manifests, version information, security policies, membrane declarations.
- **State** — persistent data owned by the Component's instances.
- **Runtime** — the live instances running on Nodes.

A Component can come with:
- Binaries and source code
- Only binaries (closed-source)
- Only source code (compiled by the fabric)
- Neither (purely declarative — interfaces, schemas, policies)

All of these are valid Component forms. The key is that whatever the Component IS, it is self-describing and self-contained within the fabric.

**This makes the Component the new "Software" / "Application."** Not a library, not a service, not a package — a living, complete, self-organizing unit of software that carries everything it needs to exist, be understood, be built, be run, and be evolved.

### Interdependency

Components do not exist in isolation. They use each other.

**Mandatory dependencies:** Every Component depends (directly or indirectly, explicitly or implicitly) on the mandatory foundational/infrastructure Components of the Scynapse platform. You cannot opt out of the naming system or the security system. These are like an operating system's kernel services.

**Optional dependencies:** Components may depend on other non-foundational Components. An "OrderManagement" Component might depend on an "InventoryService" Component. These dependencies are declared in the Component's metadata and are part of its identity.

**Ecosystem evolution:** As Components depend on other Components, ecosystems emerge. A popular Component becomes infrastructure for the Components that depend on it. This is how platforms and standards form — not by decree but by adoption. The dependency graph IS the ecosystem topology.

### Survival Guarantees

If Component A depends on Component B, and Component B disappears from the fabric (all instances gone, all Nodes that hosted it offline), Component A is broken. In a distributed system with no central authority, this is not a hypothetical — it's an inevitability.

**Survival guarantees are therefore essential.** A Component's "package" — its distributable artifact (source, binaries, metadata) — must be resilient enough that any Component depending on it can, in the worst case, find and spawn the version it needs even if no instances are currently alive in the fabric.

This is a hard problem. Potential approaches include:

- **Distributed package storage** via a mandatory infrastructure Component that replicates package artifacts across Nodes
- **Seeding** — Nodes that use a Component automatically retain its package, similar to BitTorrent
- **External backup** — pragmatic use of stable external systems (GitHub, package registries, cloud storage) as backup for the distributed package store
- **Version pinning** — dependency declarations include version constraints, and the package system retains compatible versions

The specific mechanisms are open design questions. But the requirement is non-negotiable: **if a Component can die permanently and take its dependents with it, no one will adopt the platform.** The fabric must provide survival guarantees or it's a cemetery.

### Users Become Servers

A consequence of the homogeneous fabric: **every Node that uses a Component becomes a server of that Component.** If your Node runs instances of Component X to consume its services, your Node is also serving Component X to the fabric.

This creates a virtuous cycle:
- A Component becomes popular → more Nodes use it → more Nodes serve it → more resources available → better performance and resilience → more attractive → more popular
- An unpopular Component has few Nodes → fragile → but its foundational dependency Components are everywhere and can provide minimal survival infrastructure

This is analogous to BitTorrent's economics: seeders are also downloaders. But applied to live, stateful, interactive software, not static files.

### Mandatory Infrastructure Components and Their Role

Mandatory foundational Components don't add their features *into* each application Component. Instead, they **augment** all Components in the fabric by providing capabilities that Components can use without implementing themselves.

For example (these are potential designs, not commitments):

- A **Distributed Repository Component** could provide source code hosting, versioning, and retrieval for all Components. Application Components don't implement their own repository — the infrastructure Component provides it.
- A **Distributed Package System Component** could handle artifact storage, dependency resolution, version management, and distribution. Application Components declare dependencies; the package system resolves and provisions them.
- A **Persistence Infrastructure Component** could provide durable state storage. Application Components declare state schemas; the persistence Component handles storage, replication, and retrieval.
- A **Naming/Discovery Component** could resolve Component and Type names to live instances. Application Components register themselves; the naming Component makes them findable.

Any Component IS technically free to bring its own implementation of any of these (its own repository tech, its own persistence engine), but the default path — the one that makes distributed programming easy — is to use the platform's foundational Components. This is how "distributed systems is hard" becomes "distributed systems is easy out-of-the-box": the hard parts are provided as foundational infrastructure, systematically and by design.

---

## Part 6: Pragmatic Decentralization

Scynapse embraces decentralization as a natural consequence of its design — organisms are independent, distributed, self-organizing. But Scynapse is not dogmatically decentralized.

### The Spectrum, Not the Pole

A Component that runs on a single dedicated server is still a Scynapse Component. A cluster of Nodes under a single organization's administration is a valid Scynapse deployment. The vision does not require every deployment to be a fully decentralized, peer-to-peer, trustless network.

What the vision requires is that the *architecture supports* decentralization — that Components *can* be independent, *can* operate across trust boundaries, *can* survive the loss of any single Node or organization. Whether a specific deployment exercises these capabilities is a choice, not a mandate.

### Classical Resources Are Welcome

Scynapse is designed to perform well in adversity — nodes going offline, networks partitioning, hostile actors present. But it does not refuse the strength of classical computing resources:

- **Stable servers** — a Component backed by dedicated servers in a data center is perfectly valid and will perform better than one relying entirely on ephemeral edge nodes.
- **Existing repositories** — Component source code's distributed self-repository should still, by default, prefer backing up to and syncing with services like GitHub. Belt and suspenders.
- **Existing package systems** — NuGet, npm, or any stable package registry can serve as a backup or distribution channel for Component artifacts.
- **Cloud services** — Azure, AWS, etc. are reliable infrastructure. Scynapse doesn't replace them; it can run on them.

The design principle is: **optimize for the worst case (adversity, partition, decentralization) but leverage the best case (stability, reliable infrastructure, centralized services) when available.** The platform should be robust when the world is hostile and performant when the world is cooperative.

### Blockchain and Related Technologies

Some "blockchain-related technologies" may be justified for specific problems — consensus in adversarial environments, immutable audit trails, verifiable timestamps. Scynapse is open to using them where they are the right tool.

But Scynapse is not a blockchain platform. Most of what the fabric needs (routing, naming, persistence, package management) does not require the overhead and constraints of blockchain consensus. The design space includes blockchain-derived techniques without being defined by them.

---

## Part 7: The Security System's Relationship to the Vision

The security system built in v1 is not just "Orleans with auth bolted on." It was designed with the Component vision in mind, even though it currently operates on Orleans's grain paradigm.

### What Was Designed to Carry Forward

**Ed25519 identity:** Every entity has a keypair. The public key IS the identity. No registry, no central authority. This is exactly what a decentralized fabric of organisms needs — self-sovereign identity.

**The Signed Assertion as universal primitive:** One format for identity, capability, delegation, relation, revocation, impersonation. This generality was chosen specifically because the Component Model will need all of these. A Component's membrane declarations, trust policies, type exposures, and inter-Component delegations can all be expressed as Signed Assertions.

**Capability-based authorization (CCaps):** Bearer tokens that are self-contained, delegatable, attenuatable, and verifiable without a central authority. This is the only authorization model that works in a decentralized fabric where there is no single authority that could maintain a global ACL.

**Attenuation (scope narrowing):** Each delegation can only narrow, never widen. This maps directly onto the hierarchical authority model of the membrane: a Component grants a capability, which can be narrowed to a Type, which can be narrowed to a method.

**The subject namespace:** Dot-separated hierarchical names with NATS-style wildcards. Designed to extend from `scynapse.app.{grain}.{method}` (v1) to `scynapse.component.{type}.{grain}.{method}` (Component Model) to potentially `scynapse.{org}.{component}.{type}.{instance}.{member}` (full CNS). The naming hierarchy IS the security hierarchy IS the routing hierarchy. One namespace, not three.

**Content-addressed assertions:** Every assertion is identified by its Blake2b-256 hash. Assertions are immutable. References are by hash. This is how you build verifiable trust in a system where there is no central authority to "look up" whether an assertion is valid — you verify it cryptographically, locally, from its content.

### What Changes in Phase 2

The Phase 2 transition (from Orleans paradigm to Component Model) involves **interface swaps behind stable abstractions**, not rewrites:

| Aspect | v1 (Orleans paradigm) | v2 (Component Model) |
|--------|----------------------|----------------------|
| Trust boundary | Grain type | Component (membrane) |
| Policy declaration | `[SecurityPolicy]` attributes | Component type definition metadata |
| Resource namespace | `scynapse.app.{grain}.{method}` | `scynapse.component.{type}.{grain}.{method}` (or similar) |
| Policy provider | `AttributeBasedPolicyProvider` | `ComponentModelPolicyProvider` (potential) |
| Assertion store | InMemory | Distributed (CNS-backed or similar) |
| Default security policy | AllowAnonymous (Orleans compatibility) | RequireAuth (Component-native) |

The cryptographic primitives, the assertion format, the verification algorithm, the attenuation checker, the subject namespace pattern, and the call filter architecture all carry forward unchanged.

### Alignment with NATS

The security system's alignment with NATS's security model is deliberate and structural, not superficial:

| NATS | Scynapse |
|------|----------|
| Subject (dot-separated, `*`/`>` wildcards) | Subject namespace URI |
| Account (security boundary) | Component (membrane) |
| NKey (Ed25519 identity) | ScynapseKeyPair |
| JWT (signed authorization) | Signed Assertion |
| `nsc` CLI | `scy` CLI |
| Publish/subscribe permissions on subjects | CCap action/resource on subject namespace |

If Scynapse ever uses NATS for infrastructure messaging (which is an explored direction), the subject patterns and security models could interoperate structurally. A NATS account maps to a Scynapse Component. A NATS subject maps to a Component resource. The mapping is architectural.

---

## Part 8: Open Problems and Explored Directions

Each subsection describes a **problem** that must be solved for the vision to be realized, followed by any **explored directions** (clearly marked as speculative). These are the active frontiers of Scynapse's design space.

### Naming and Discovery (CNS — Scynapse Name System)

**The problem:** In a decentralized fabric, how do Components, Types, and Instances find each other? How does "InventoryService.IInventoryGrain.warehouse-7" resolve to a live instance on a specific Node?

In v1, name resolution is compile-time: `GrainResourceInference` derives resource URIs from .NET type names. This works for a single Orleans cluster but not for a dynamic, decentralized fabric where Components appear and disappear at runtime.

**Explored direction:** A distributed name system (called CNS — Scynapse Name System in design discussions) that resolves hierarchical dot-separated names to live instances across the fabric. The subject namespace already in use for security (`scynapse.app.IOrderGrain.PlaceOrder`) is designed to become CNS-resolvable.

**What's open:** Everything about the CNS design is open. Whether it's a DHT, a gossip protocol, a hierarchical delegation system, a NATS-inspired subject routing mesh, or something else entirely — all open. The requirement is: Components must be able to discover and reach each other by name, at runtime, across the fabric, without a central registry.

### Routing

**The problem:** Once you know WHERE a Component instance lives (naming/discovery), how do messages get there? In Orleans, the runtime handles routing within a cluster. In a decentralized fabric spanning heterogeneous Nodes, routing is a fundamentally harder problem — Nodes may be behind NATs, on different networks, intermittently connected.

**Explored direction:** One concept explored is a "chainmail" routing fabric where each Component type's instances form a hash ring, and contact points between rings form a routing mesh. This is speculative — the metaphor is suggestive but no implementation exists and the problem remains fully open.

**Other potential inspirations:** NATS's subject-based routing, libp2p's peer-to-peer networking, Kademlia DHT routing, gossip protocols. The routing solution likely draws from multiple sources.

**What's open:** The entire routing design.

### Component Lifecycle

**The problem:** How does a Component come into existence in the fabric, grow, shrink, migrate, hibernate, and die? What triggers spawning new instances? What happens when a Node hosting instances goes offline?

Orleans answers this for individual grains (virtual actors: activate on demand, deactivate on idle, reactivate on next call). Scynapse needs to answer it for Components — which are collections of related Types, not individual actors.

**What's open:** This is a Phase 2 design problem. The Orleans grain lifecycle model provides a starting point but likely needs significant extension for Component-level lifecycle management.

### Component Packaging and Distribution

**The problem:** How are Component artifacts (source, binaries, metadata) packaged, versioned, distributed across the fabric, and made available for spawning?

**Explored direction:** A mandatory infrastructure Component (a "Distributed Package System") that handles packaging and distribution. Could draw inspiration from NuGet (for .NET package conventions), IPFS (for content-addressed distribution), BitTorrent (for peer-to-peer seeding), and container registries (for artifact versioning).

**What's open:** The entire packaging design. Format, distribution protocol, versioning semantics, dependency resolution — all open.

### Component Survival

**The problem:** If all instances of a Component die and all Nodes that hosted it go offline, how does a dependent Component resurrect it? The package artifact must be recoverable from somewhere.

**Explored direction:** Mandatory infrastructure Components provide baseline survival by replicating artifacts across the fabric. External systems (GitHub, package registries, cloud storage) serve as backup. Nodes that use a Component retain its package (seeding).

**What's open:** The specific mechanisms, the guarantees (eventual? strong?), the economics of storage (who pays for replicating artifacts?).

### Persistence

**The problem:** How do Component instances persist state in a decentralized fabric? Orleans provides persistence via pluggable storage providers (Azure Table, SQL, etc.). A decentralized fabric cannot assume access to any particular storage service.

**Explored direction:** A Persistence Infrastructure Component that provides durable state storage. Could be backed by classical databases when available, distributed storage when not.

**What's open:** The entire persistence design for decentralized scenarios.

### Federation and Cross-Organization Trust

**The problem:** How do Components owned by different organizations interact? How does Organization A's Component trust Organization B's Component? This is the "inter-cluster" problem that Orleans doesn't address at all.

**Explored direction:** The assertion model already supports multi-organization trust chains (Organization A delegates to Organization B). Cross-organization CCaps could enable federated access. The trust model is designed to be self-similar — the same mechanism works within an organization and across organizations.

**What's open:** Federation governance, trust bootstrap between organizations, dispute resolution, trust revocation at scale.

### Stream and Event Security

**The problem (known v1 limitation):** Orleans streams bypass the grain call filter pipeline. Stream publications and subscriptions are not secured. For the Component vision, where the membrane is the unified security/visibility/API boundary, streams that bypass the membrane are a hole.

**Explored direction:** Stream-level security filters, or redesigning the event/stream mechanism to flow through the same security pipeline as grain calls.

**What's open:** Whether streams as an Orleans concept survive into the Component Model, or whether Component-level events replace them entirely.

### Democratic Anonymity

**The problem:** In some contexts, participants want pseudonymous interaction — verifiable identity without revealing real identity. Think: anonymous voting, whistleblower channels, privacy-preserving transactions.

**Explored direction:** The assertion format already reserves space for threshold de-anonymization (Shamir's Secret Sharing). Pseudonymous identities would use the same Ed25519 keys and CCaps as named identities. The binding between pseudonym and real identity would be encrypted and sharded across trustees — recoverable only with a threshold of trustees cooperating.

**What's open:** The entire anonymity design. Whether it's needed for v2, whether Shamir's is the right primitive, what "democratic" means in governance terms.

### Key Distribution and Discovery

**The problem:** How do entities learn each other's public keys? In v1, keys are pre-shared via the Scy CLI and configuration files. In a dynamic fabric, keys need to propagate without pre-sharing.

**Explored direction:** Keys propagate through Component type-networks (discovery via the naming system), TOFU (Trust On First Use) as a fallback, web-of-trust as an alternative, CNS registration as the primary mechanism.

**What's open:** The key distribution mechanism is tied to the CNS design and will be resolved together with it.

### Built-In Capability Vocabulary

**The problem:** What are the universal "verbs" in the Scynapse capability model? V1 uses arbitrary strings (`"read"`, `"write"`, `"admin"`). The Component Model may need a standardized vocabulary of actions.

**Explored direction:** Universal verbs like `invoke`, `subscribe`, `admin`, `delegate`, `query`, `mutate`. But this depends on the Component interface story, which is itself open.

**What's open:** Whether a standardized vocabulary is needed at all, or whether arbitrary strings plus convention is sufficient.

---

## Part 9: What Carries Forward from v1

Not everything in v1 will survive the transition to the Component Model. Here is what is designed to carry forward and what is likely to change.

### Carries Forward (Designed for Longevity)

| What | Why |
|------|-----|
| Ed25519 identity primitive | Self-sovereign identity is foundational. No registry dependency. |
| Signed Assertion format | Universal primitive for all trust operations. CBOR serialization, Blake2b-256 content addressing. |
| Verification algorithm | Recursive chain walk with attenuation. Sound and general. |
| Subject namespace scheme | Dot-separated, NATS-style wildcards. Extends naturally. |
| Capability-based auth model (CCaps) | The only authorization model that works without central authority. |
| Attenuation (scope narrowing) | Maps directly onto Component > Type > Instance hierarchy. |
| Content-addressed assertions | Enables verification without central lookup. |
| `Scynapse.Security` library | Zero Orleans dependency. Reusable in any context. |
| Scy CLI patterns | Topology-aware provisioning. Command structure extends with Component commands. |
| Key type system | Organization, Domain, Node, ComponentType, Instance, User, Encryption, Seed. |

### Changes (Orleans-Specific, Will Be Reworked)

| What | Why It Changes |
|------|---------------|
| `ScynapseIncomingCallFilter` / `ScynapseOutgoingCallFilter` | Orleans-specific call filter interfaces. The pattern (enforcement at call boundary) survives; the Orleans types don't. |
| `AttributeBasedPolicyProvider` | Security policy from C# attributes is an Orleans-era pattern. Components will declare policy in their type definitions. |
| `GrainResourceInference` | Derives URIs from .NET reflection on grain types. Component Model will have its own type metadata. |
| `InMemoryCCapWallet` | The wallet concept survives; the implementation will need distribution. |
| `InMemoryAssertionStore` / `InMemoryNonceStore` | Will be replaced by distributed stores (likely backed by infrastructure Components). |
| Orleans `RequestContext` for security data | The concept (ambient security context flowing with calls) survives; the Orleans-specific mechanism will change. |
| `[SecurityPolicy]` / `[RequireCapability]` attributes | Declarative policy survives; the mechanism (C# attributes on grain interfaces) is Orleans-specific. |

### The Migration Path

The v1 architecture was explicitly designed for interface swaps, not rewrites. The key abstractions — `IAssertionStore`, `IGrainSecurityPolicyProvider`, `IAttenuationChecker`, `ICCapWallet` — are interfaces specifically so their implementations can be swapped when the Component Model arrives. This was a deliberate design decision, documented in the architecture.

---

## Part 10: Glossary

| Term | Definition |
|------|-----------|
| **Component** | The fundamental unit of software in Scynapse. Simultaneously an artifact (code/binaries/metadata), a virtual entity (identity persists across activations), and runtime instances (live on Nodes). A Component IS the network it forms. |
| **Node** | A process running the Scynapse runtime. Every participant is a Node — servers, clients, edge devices. No fundamental distinction between "server" and "client." |
| **Fabric** | The substrate of interconnected Nodes. The cyberspace in which Components live. |
| **Membrane** | The unified boundary of a Component: security, visibility, and API in one concept. Determines what is internal and what is exposed. |
| **Type** | The execution unit within a Component. Analogous to a grain type in Orleans. Has members (methods, properties, events). Can be internal, exposed, or partially exposed through the membrane. |
| **Instance** | A live activation of a Type on a specific Node. Analogous to a grain activation in Orleans. |
| **CCap (Crypto-Capability)** | A Signed Assertion granting specific permissions on specific resources. Bearer token, self-contained, delegatable, attenuatable, verifiable without central authority. |
| **Signed Assertion** | The universal primitive for trust operations: identity, capability, delegation, relation, revocation, impersonation. CBOR-serialized, Ed25519-signed, Blake2b-256 content-addressed. |
| **Subject Namespace** | Dot-separated hierarchical resource names with `*`/`>` wildcards. Used for security resource matching, naming, and (future) routing. NATS-compatible by design. |
| **CNS (Scynapse Name System)** | A potential distributed name system for resolving names to live instances. Not yet designed — an open problem. |
| **Chainmail** | A speculative routing concept where Component instances form hash rings with inter-ring contact points. Not designed, not committed — the routing problem is open. |
| **Attenuation** | The principle that delegated capabilities can only narrow, never widen. Each level in a delegation chain can restrict but not expand the scope of the parent. |
| **Grain** | Orleans's virtual actor abstraction. The current execution unit in Scynapse v1. Will be conceptually subsumed by Component Types in the Component Model. |
| **Silo** | Orleans's host process. The current Node concept in Scynapse v1. |
| **Scy** | The CLI tool for Scynapse security provisioning. Topology-aware, inspired by NATS's `nsc`. |
| **Orleans** | Microsoft's distributed actor framework. Scynapse's foundation. |
| **Foundational Component** | A mandatory infrastructure Component that is part of the Scynapse platform itself. Provides capabilities (persistence, naming, packaging, etc.) that augment all other Components. |
| **Trust Root** | An Organization key whose self-signed identity assertion anchors a trust hierarchy. All delegation chains terminate at a trusted root. |
| **Bearer Proof** | Proof that a CCap presenter owns the subject key in the CCap. Implemented as signing the CCap's content hash with the subject's private key. |

---

*This document reflects the state of Scynapse as of 2026-03-07. V1 implementation is on branch `claude/review-scynapse-security-UG35t`. 213 tests passing. The vision described here is the design space being explored for V2 and beyond. Everything not yet implemented is open to better designs and solutions.*
