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

## 10. Review Checklist

1. Are any fields tagged `A` but still carrying hidden silo/cluster assumptions?
2. Are all `N` fields tied to M0-A invariants?
3. Are all `D` concepts explicitly blocked in implementation notes?
4. Does each message family have enough data for deterministic deny/error behavior?
