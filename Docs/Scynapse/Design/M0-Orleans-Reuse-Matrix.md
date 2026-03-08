# M0 Orleans Reuse Matrix (Draft)

## 1. Purpose

This document answers: which Orleans internals should be kept, adapted, or redesigned for Scynapse M0 and beyond.

It is intentionally practical and tied to current M0 work (`M0-A` contracts and `M0-B` protocol skeleton).

---

## 2. Decision Classes

1. `Keep`: can carry forward with minimal semantic change.
2. `Adapt`: valuable mechanism, but requires interface and model remapping.
3. `Redesign`: core assumptions conflict with Scynapse model; replace.

---

## 3. Reuse Matrix

| Area | Orleans Baseline | M0 Decision | Why |
|---|---|---|---|
| Turn-based execution and per-activation queue | `WorkItemGroup`, `OrleansTaskScheduler`, turn guarantees | `Adapt` | The single-turn model is still strong, but execution units change from grain/silo assumptions to Varion/Cell/Hive contexts. |
| Request context propagation | `RequestContext` flows across grain boundaries | `Adapt` | Keep ambient context propagation semantics; evolve into relation/disclosure-aware context for mediated-first routing. |
| Message envelope and correlation | `Message` with routing metadata, correlation, TTL | `Adapt` | Keep request/response correlation patterns, replace address identity and add route/disclosure/policy fields. |
| Connection lifecycle | TCP/TLS handshake, preamble, reconnect | `Adapt` | Keep transport lifecycle mechanics, but remove silo/client assumptions and tie establishment to relation contracts. |
| Placement strategy abstraction | `PlacementStrategy` + `IPlacementDirector` | `Adapt` | Strategy pattern is reusable, but target should be Cell/Hive placement and policy-scoped placement constraints. |
| Grain directory DHT patterns | consistent hashing, snapshot versions, view-change transfer | `Adapt` | Strong fit for CNS internals (versioned snapshots, ownership, transfer), but object model and authority semantics differ. |
| Membership versioning and gossip merge ideas | snapshot merge/version progression | `Adapt` | Versioned convergence patterns are useful for CNS/directory convergence. Must detach from cluster/silo worldview. |
| Serialization/codegen performance model | generated codecs, version tolerance | `Adapt` | Keep for in-process/runtime performance and compatibility strategies. Wire canonical choice for M0-B is CBOR profile. |
| Persistence abstraction and optimistic concurrency | `IStorage<TState>`, ETag conflict handling | `Adapt` | Useful abstraction and conflict model for Persistence Infrastructure Component design. |
| Timers/reminders split | transient timer vs durable reminder | `Adapt` | Useful conceptual split, but delivery/ownership and policy mediation should be Varia-native. |
| Streams subsystem | Orleans stream providers and pub/sub | `Redesign` | Current Orleans stream path bypasses Scynapse call-filter security expectations; needs unified membrane/policy path. |
| Silo/cluster/client topology | silo hosts grains; clients connect via gateways | `Redesign` | Conflicts with Scynapse Node unification and no silo-less clients. Replace with Node-first, per-Varia Hive participation. |
| Client-specific locator/gateway assumptions | `ClientGrainLocator`, `GatewayConnection` | `Redesign` | Not compatible with Node-only participant model. |

---

## 4. Evidence Pointers (Orleans Internals)

### 4.1 Topology assumptions to redesign

1. Silos and cluster assumptions:  
   `01-paradigms-and-concepts.md` lines 168-187.
2. Membership table centered on `SiloAddress` and external table join protocol:  
   `06-clustering-membership.md` lines 13-20 and 389-413.
3. Client/gateway connection split:  
   `07-messaging-networking.md` lines 84-87.

### 4.2 Mechanisms worth adapting

1. Request context propagation:  
   `01-paradigms-and-concepts.md` lines 247-259.
2. Scheduler and turn execution model:  
   `05-runtime-activation.md` lines 384-395 and 499-503.
3. Placement strategy extension point:  
   `11-key-abstractions.md` lines 337-379.
4. DHT directory and versioned membership transfer patterns:  
   `OrleansDistributedGrainDirectory.md` lines 41-52, 174-204, 408-427.
5. Messaging correlation/timeouts and envelope:  
   `07-messaging-networking.md` lines 17-34 and 153-165.
6. Serialization version tolerance and performance profile:  
   `09-serialization.md` lines 112-173 and 317-322.
7. Persistence abstraction + optimistic concurrency:  
   `08-persistence-state.md` lines 11-22 and 175-213.

### 4.3 Mechanism requiring redesign for security alignment

1. Streams subsystem baseline:  
   `10-additional-systems.md` lines 202-272.
2. Scynapse limitation that streams bypass call-filter security path:  
   `Scynapse-Context-Combined.md` lines 1501-1503.

---

## 5. Immediate M0 Implications

1. M0-B message contracts should preserve Orleans strengths:
   - correlation ids
   - timeout semantics
   - reconnect/fallback discipline
2. M0-B must not inherit Orleans topology assumptions:
   - no client/gateway primary model
   - no silo-centric identity model
3. CNS internals can borrow from distributed directory techniques:
   - versioned snapshots
   - ownership transfer flows
   - deterministic retry on stale versions
4. Stream/observe protocol in Scynapse should be policy-first from the start.

---

## 6. Recommended Next Step

Completed:

1. Orleans compatibility profile created: `M0-B-Orleans-Compatibility-Profile.md`

Current next step:

1. enforce compatibility tags in implementation review checklists
2. prevent deprecated topology assumptions from reappearing in helper APIs
