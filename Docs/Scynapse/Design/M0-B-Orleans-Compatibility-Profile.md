# M0-B Orleans Compatibility Profile (Draft)

## 1. Purpose

This profile maps M0-B protocol fields to their Orleans lineage, with explicit classification:

1. `Adapted`: inherited concept, remapped to Scynapse semantics.
2. `Native`: Scynapse-specific concept with no direct Orleans equivalent.
3. `Deprecated`: Orleans concept intentionally not carried forward.

---

## 2. Classification Legend

| Tag | Meaning |
|---|---|
| `A` | Adapted from Orleans concept |
| `N` | Scynapse-native |
| `D` | Orleans concept deprecated in Scynapse protocol |

---

## 3. Common Envelope Field Profile

| Field | Tag | Orleans Lineage | Scynapse Interpretation |
|---|---|---|---|
| `msg_type` | A | Message kind/direction split | Unified message family identifier |
| `msg_id` | A | Request/response correlation primitives | Unique message id for dedupe and tracing |
| `trace_id` | A | RequestContext tracing patterns | Cross-hop relation trace identity |
| `timestamp` | A | Message metadata timestamps | Audit and replay ordering anchor |
| `from.node_id` | A | `SiloAddress` identity role | Node identity key, topology-agnostic |
| `from.name_anchor` | N | none | CNS anchor context for policy/path evaluation |
| `intent` | N | implicit call kind | Explicit operation class (`resolve/invoke/observe/policy`) |
| `target_scope` | N | `GrainAddress` target identity | CNS scope target (name graph, not silo address) |
| `relation_id` | N | none | Established mediated/direct relation handle |
| `route_mode` | N | none | Policy-shaped route state (`parent_mediated`, etc.) |
| `disclosure_level` | N | none | Endpoint visibility contract (`hidden`, etc.) |
| `proofs.capability_refs` | N | none | Capability-based authorization proofs |
| `proofs.bearer_proof` | N | none | Subject-possession proof for capability use |
| `proofs.attestation_refs` | N | partial cert/trust metadata | Relation trust evidence references |
| `ttl_ms` | A | Message TTL/timeout options | Operation expiry bound |

---

## 4. Resolve Family Profile

| Field | Tag | Orleans Lineage | Notes |
|---|---|---|---|
| `expr_raw` | N | none | User/tool-facing CNS expression form |
| `expr_norm` | N | none | Canonicalized resolver form for deterministic routing |
| `operation_class` | N | implicit method semantics | Explicit right class (`meta/value/endpoint/invoke/observe`) |
| `preferred_route_mode` | N | none | Caller hint constrained by policy |
| `cursor_or_revision` | A | stream/checkpoint patterns | Resolve-refresh and incremental consistency support |
| `candidate_bindings` | A | directory lookup results | Policy-filtered endpoint candidates |
| `referral` | A | forwarding/locator retry patterns | Scoped authority handoff in CNS graph |
| `effective_policy_ref` | N | none | Required for explainable policy outcomes |

---

## 5. Handshake and Route Profile

| Field | Tag | Orleans Lineage | Notes |
|---|---|---|---|
| `relation_token` | N | none | Signed relation contract with route and scope constraints |
| `participants` | A | sender/target addresses | Node identities under relation context |
| `ops` | N | none | Allowed operation classes for this relation |
| `route_upgrade_probe` fields | N | none | Direct-upgrade gates and consent proofs |
| `fallback_route_ref` | N | none | Mandatory mediated fallback continuity |
| `renewal_nonce` | A | challenge-response patterns | Replay-safe token renewal |

---

## 6. Observation Family Profile

| Field | Tag | Orleans Lineage | Notes |
|---|---|---|---|
| `subscription_mode` | A | stream subscription modes | Extended with predicate and mixed modes |
| `profile` | N | none | Explicit cost/guarantee level (`Lite/Standard/Rich/Regulated`) |
| `follow_moves` | N | none | Dynamic graph rename/move tracking behavior |
| `event_id` | A | stream sequence tokens/concepts | Deduplication and idempotent consumer support |
| `revision` | A | version/checkpoint ideas | Monotonic scope version for replay/resume |
| `delivery_class` | N | none | `meta/value/policy/binding` gate |
| `observe_gap` cause | N | partial timeout concepts | Deterministic replay failure reasoning |

---

## 7. Explicit Orleans Deprecations

| Orleans Concept | Tag | Replacement |
|---|---|---|
| `SiloAddress` as primary routing identity | D | Node identity + CNS scope + relation token |
| global `Cluster` as universal execution domain | D | per-Varia Hive participation across Nodes |
| silo/client split and gateway client model | D | Node-only participants (no silo-less clients) |
| client-specific grain locator assumptions | D | unified resolve/handshake path for all Nodes |
| implicit direct addressing from lookup | D | mediated-first + disclosure-gated upgrade |

---

## 8. Compatibility Guardrails

1. No field in M0-B may require silo/client topology assumptions.
2. Every endpoint-disclosure field must have policy and capability gating.
3. Every adapted Orleans field must include its Scynapse semantic delta.
4. Deprecated Orleans concepts must not re-enter via helper APIs or "temporary" shortcuts.
5. Protocol review checklists should include a `Tag` column (`A/N/D`) before lock.

---

## 9. S1 Hardening Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Explicit message-driven operation context state machine | `A` | adapts Orleans request-pipeline/state discipline into protocol-level transition handling |
| Resolve/handshake transition validation by state matrix | `N` | Scynapse-native CNS and mediated-flow semantics; no Orleans equivalent |
| Terminal-state rejection (`Deny`/`Completed`) | `A` | conceptually aligned with Orleans terminal operation handling but tightened for protocol determinism |
| S1 mediated-only direct-upgrade rejection | `N` | Scynapse-native policy posture; Orleans has no mediated/direct route contract at this layer |
| Structured error IDs and deterministic deny mapping | `N` | Scynapse conformance surface for machine-checkable protocol correctness |
| Silo/client split assumptions in ordering/validation paths | `D` | explicitly blocked; all flows are Node-to-Node with unified semantics |

---

## 10. S2 Direct-Upgrade Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Profile-aware direct-upgrade handling (`S1` forbid posture, `S2` evaluate gates) | `N` | Scynapse-native multi-slice protocol profile model |
| Deterministic S2 gate-order evaluation (`Policy -> Disclosure -> Grant -> Trust -> UpgradeRejected`) | `N` | no Orleans equivalent at protocol route-upgrade layer |
| Reject-path fallback restoration to `RelayedSession` | `A` | adapts Orleans-style resilience/fallback intent, but implemented in Scynapse relation-route semantics |
| Invalid accept with failed gates blocked by stable machine error ID | `N` | Scynapse conformance determinism requirement |
| Route behavior requiring silo/client topology hints | `D` | remains explicitly blocked in S2, same as S1 |

---

## 11. S3 Endpoint-Grant Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Endpoint resolve profile (`operation_class=endpoint`) with encrypted-directory gates | `N` | Scynapse-native disclosure/grant model over CNS endpoint semantics |
| `GrantPresent` proof-path requirement before encrypted endpoint disclosure | `N` | no Orleans equivalent in grain directory semantics |
| Deterministic endpoint deny mapping (`GrantMissing`, `GrantExpired`, `DisclosureDenied`) | `N` | protocol-level conformance surface for endpoint disclosure safety |
| Endpoint grant handling via silo/client locator shortcuts | `D` | explicitly blocked; remains node-unified and policy-gated |

---

## 12. S4 Observation/Replay Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Observe lifecycle context (`ObserveOpen/Ack/Event/Gap/Resume/Close`) | `A` | adapts stream/subscription lifecycle discipline while binding to CNS semantics |
| `follow_moves` scope-aware behavior modeling | `N` | Scynapse-native dynamic namespace observation semantics |
| Replay-expiry deterministic mapping (`ReplayWindowExpired`) | `A` | conceptually aligned with stream replay window behavior, tightened for deterministic protocol conformance |
| Subscription semantics requiring silo/client gateway assumptions | `D` | explicitly blocked; remains node-unified routing and policy model |

---

## 13. S5 Policy Inheritance Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Policy inheritance hard-lock conformance (`PolicyDelta` -> deterministic deny) | `N` | Scynapse-native parent-policy authority model over CNS hierarchy |
| Deterministic policy deny validation (`PolicyDeny` ordering and code consistency) | `N` | protocol-level machine-checkable conformance behavior |
| Policy inheritance enforced through silo/client role assumptions | `D` | explicitly blocked; remains node-unified policy semantics |

---

## 14. M1-S1 Wire-Closure Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Typed identifier lock (`<prefix>:<value>`) for id/ref surfaces | `N` | Scynapse-native deterministic identity/reference encoding contract |
| `expr_norm` with required supported `expr_norm_v` | `N` | Scynapse-native normalization compatibility control point |
| Policy-causal deny envelope requiring `policy_ref` | `A` | adapts explicit policy-causality reasoning into deterministic wire contract |
| Relation token boundary (`token_transport`, ref/cid/blob rules) | `N` | Scynapse-native transport optimization with explicit verification anchors |
| Reintroduction of implicit silo/gateway id assumptions through identifier shortcuts | `D` | explicitly blocked; typed id prefixes remain topology-neutral |

---

## 15. M1-S2 Runtime-Bridge Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Runtime bridge profile with deterministic data-path checks (`M1-S2`) | `N` | Scynapse-native runtime-adjacent conformance layer |
| `RouteData` mediated/direct session-path enforcement | `N` | no Orleans equivalent at this protocol layer |
| Direct-upgrade gate reuse from S2 into runtime profile | `A` | adapts existing gate-order semantics into runtime transit context |
| Bridge transit trace assertions for deterministic flow proofs | `N` | Scynapse-native conformance observability |
| Gateway-style silo/client transport shortcuts in runtime bridge | `D` | explicitly blocked; remains node-unified transport semantics |

---

## 16. M1-S3 Security-Adapter Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| `HandshakeProof` security adapter mode (`mock|strict`) in `M1-S3` profile | `N` | Scynapse-native conformance harness extension for verification realism |
| Strict-mode proof verification via `Scynapse.Security.Verification.AssertionVerifier` | `A` | adapts existing security primitives into bounded harness validation paths |
| Strict-mode nonce replay deterministic deny mapping | `A` | adapts nonce replay semantics from verification layer into protocol conformance IDs |
| Mock-mode deterministic security-failure simulation with stable IDs | `N` | Scynapse-native fixture control for bounded protocol validation |
| Security validation requiring silo/client/gateway trust shortcuts | `D` | explicitly blocked; remains node-unified proof semantics |

---

## 17. M1-S4 Strict Failure-Mapping Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| Strict failure-mode extension on `HandshakeProof` (`strict_failure_mode`) in `M1-S4` | `N` | Scynapse-native deterministic conformance control for strict verification paths |
| Deterministic strict temporal/revocation/proof-chain failure ID mapping (`E3081`..`E3084`) | `A` | adapts verification-layer failure semantics into stable protocol conformance IDs |
| Invalid strict-failure-mode schema rejection (`E3080`) | `N` | Scynapse-native schema hardening for deterministic fail surfaces |
| Strict failure behavior requiring silo/client trust shortcuts | `D` | explicitly blocked; remains node-unified proof and policy semantics |

---

## 18. M1-S5 Relation-Token Integrity Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| M1-S1 token-boundary contract reuse in `M1-S5` handshake accept path | `A` | adapts prior wire-closure boundaries into runtime/security integrated slice |
| Inline token CID integrity enforcement (`relation_token_cid == sha256(relation_token_blob)`) | `N` | Scynapse-native deterministic relation-token integrity check |
| Deterministic inline CID mismatch deny mapping (`E3091`) | `N` | machine-checkable conformance behavior for integrity violations |
| Token integrity relying on silo/client gateway trust shortcuts | `D` | explicitly blocked; remains node-unified relation semantics |

---

## 19. M1-S6 Reference-Token Guard Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| M1-S5 token-boundary/integrity reuse in `M1-S6` reference-transport handshake path | `A` | adapts prior integrity constraints into reference lookup safety layer |
| Reference lookup status contract on `HandshakeAccept` (`resolved|missing|rebinding_detected`) | `N` | Scynapse-native deterministic guard for reference token resolution state |
| Resolved lookup CID equality with relation token CID (`reference_lookup_cid == relation_token_cid`) | `N` | Scynapse-native anti-rebinding/consistency check for reference transport |
| Deterministic reference deny mapping (`E3101`, `E3102`, `E3103`) | `N` | machine-checkable conformance behavior for unresolved/rebinding/mismatch outcomes |
| Reference token guard relying on silo/client gateway trust shortcuts | `D` | explicitly blocked; remains node-unified, policy-gated relation semantics |

---

## 20. M1-S7 Reference-Grant Guard Behavior Classification

| Hardened Behavior | Tag | Notes |
|---|---|---|
| M1-S6 reference lookup guard reuse in `M1-S7` handshake accept path | `A` | adapts prior reference integrity checks into grant-gated disclosure model |
| Reference grant status contract on `HandshakeAccept` (`active|missing|expired|revoked|not_required`) | `N` | Scynapse-native deterministic grant-state gate for reference resolution |
| Active-grant typed reference requirement (`reference_grant_ref`) | `N` | Scynapse-native capability-causality anchor for auditable grant binding |
| Deterministic reference-grant deny mapping (`E3111`, `E3112`, `E3113`) | `N` | machine-checkable conformance behavior for grant gate failures |
| Reference grant handling relying on silo/client trust shortcuts | `D` | explicitly blocked; remains node-unified and capability-gated by policy semantics |

---

## 21. Review Checklist

1. Are any fields tagged `A` but still carrying hidden silo/cluster assumptions?
2. Are all `N` fields tied to M0-A invariants?
3. Are all `D` concepts explicitly blocked in implementation notes?
4. Does each message family have enough data for deterministic deny/error behavior?
