# M0-B Message Field Matrix (Draft)

## 1. Purpose

This document makes M0-B actionable by defining required, optional, and conditional fields for each message type.

Requirement labels:

1. `R` required
2. `O` optional
3. `C` conditional (required only if condition holds)

Compatibility tags:

1. `A` adapted from Orleans concept
2. `N` Scynapse-native

---

## 2. Global Envelope Rules

Envelope fields that apply to all messages:

| Field | Req | Tag | Rule |
|---|---|---|---|
| `msg_type` | R | A | Must match family/type exactly |
| `msg_id` | R | A | Unique within sender replay window |
| `trace_id` | R | A | Stable across a relation flow |
| `timestamp` | R | A | UTC |
| `from.node_id` | R | A | Node identity key or equivalent id |
| `from.name_anchor` | C | N | Required when CNS policy/path context is relevant |
| `intent` | R | N | `resolve|invoke|observe|policy` |
| `target_scope` | C | N | Required for scope-addressed operations |
| `relation_id` | C | N | Required after relation establishment |
| `route_mode` | C | N | Required for route/handshake/observe data path |
| `disclosure_level` | C | N | Required when endpoint visibility matters |
| `proofs.capability_refs` | C | N | Required for capability-gated actions |
| `proofs.bearer_proof` | C | N | Required when bearer ownership proof is required |
| `proofs.attestation_refs` | O | N | Trust evidence references |
| `ttl_ms` | R | A | Operation expiry bound |

---

## 3. Resolve Family

### 3.1 ResolveRequest

| Field | Req | Tag | Rule |
|---|---|---|---|
| `expr_raw` | R | N | Original expression string |
| `expr_norm` | O | N | Canonicalized expression |
| `operation_class` | R | N | `meta|value|endpoint|invoke|observe` |
| `preferred_route_mode` | O | N | Hint only; policy may override |
| `selector_hints` | O | N | Disambiguation hints |
| `cursor_or_revision` | O | A | Incremental refresh support |

### 3.2 ResolveResponse

| Field | Req | Tag | Rule |
|---|---|---|---|
| `resolved_scope` | R | N | Canonical target scope |
| `candidate_bindings` | R | A | Policy-filtered candidate set (can be empty) |
| `effective_policy_ref` | R | N | Policy revision reference |
| `disclosure_constraints` | R | N | Allowed visibility level(s) |
| `referral_hints` | O | A | Next authoritative path hints |
| `decision_code` | R | N | `Resolved|Ambiguous|Denied|RefreshRequired` |

### 3.3 ResolveReferral

| Field | Req | Tag | Rule |
|---|---|---|---|
| `referral_scope` | R | A | Next authority scope |
| `referral_expiry` | R | A | Absolute expiry |
| `relay_requirements` | O | N | Parent/relay constraints |
| `policy_proof_refs` | O | N | Why referral is authoritative |

### 3.4 ResolveDeny

| Field | Req | Tag | Rule |
|---|---|---|---|
| `deny_code` | R | N | From deterministic taxonomy |
| `reason` | R | A | Stable human-readable reason |
| `retryable` | R | A | bool |
| `remediation` | O | N | Action hint |

---

## 4. Handshake Family

### 4.1 HandshakeInit

| Field | Req | Tag | Rule |
|---|---|---|---|
| `requested_ops` | R | N | Requested operation classes |
| `requested_scope` | R | N | Relation scope |
| `requested_disclosure_level` | R | N | Desired visibility class |
| `proposed_route_mode` | R | N | Initial mode (normally mediated) |

### 4.2 HandshakeChallenge

| Field | Req | Tag | Rule |
|---|---|---|---|
| `challenge_nonce` | R | A | Replay-safe challenge |
| `required_proofs` | R | N | Capability/attestation requirements |
| `expires_at` | R | A | Challenge expiry |

### 4.3 HandshakeProof

| Field | Req | Tag | Rule |
|---|---|---|---|
| `challenge_nonce` | R | A | Must match challenge |
| `bearer_proof` | C | N | Required when capability bearer proof is requested |
| `capability_refs` | C | N | Required when gated ops requested |
| `attestation_refs` | O | N | Supplemental trust refs |

### 4.4 HandshakeAccept

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_token` | R | N | Signed relation contract |
| `route_mode` | R | N | Active route mode |
| `disclosure_level` | R | N | Granted disclosure level |
| `expires_at` | R | N | Relation token expiry |
| `fallback_route_ref` | C | N | Required if direct-upgrade is granted |

### 4.5 HandshakeDeny

| Field | Req | Tag | Rule |
|---|---|---|---|
| `deny_code` | R | N | Deterministic deny code |
| `policy_ref` | O | N | Relevant policy revision |
| `retryable` | R | A | bool |

---

## 5. Route/Upgrade Family

### 5.1 RouteEstablish

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_id` | R | N | Existing relation |
| `route_mode` | R | N | `parent_mediated|relay_mediated|anonymous_relay` |
| `relay_path` | C | N | Required for mediated modes |
| `keepalive_ms` | O | A | Liveness interval hint |

### 5.2 RouteUpgradeProbe

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_id` | R | N | Existing relation |
| `upgrade_target_mode` | R | N | `direct_upgraded` |
| `endpoint_disclosure_grant_ref` | C | N | Required when endpoint directory is encrypted/gated |
| `consent_proof` | R | N | Endpoint consent proof |
| `fallback_route_ref` | R | N | Must remain valid |

### 5.3 RouteUpgradeAccept / RouteUpgradeReject

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_id` | R | N | Existing relation |
| `decision_code` | R | N | `UpgradeAccepted|UpgradeRejected` |
| `reason` | C | A | Required for reject |
| `active_route_ref` | R | N | Route to use after decision |

### 5.4 RouteKeepAlive / RouteClose

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_id` | R | N | Existing relation |
| `route_mode` | R | N | Current active route |
| `close_reason` | C | A | Required for `RouteClose` |

---

## 6. Observation Family

### 6.1 ObserveOpen

| Field | Req | Tag | Rule |
|---|---|---|---|
| `scope` | R | N | Observed scope |
| `subscription_mode` | R | A | `OnChange|IntervalSnapshot|Predicate|Mixed` |
| `profile` | R | N | `Lite|Standard|Rich|Regulated` |
| `follow_moves` | R | N | Defaults from M0-A section 8.10 |
| `filters` | O | N | event class/predicate filters |
| `replay_cursor` | O | A | resume point |

### 6.2 ObserveAck

| Field | Req | Tag | Rule |
|---|---|---|---|
| `subscription_id` | R | N | Assigned subscription handle |
| `accepted_scope` | R | N | Effective scope (may be narrowed) |
| `effective_profile` | R | N | Final granted profile |
| `start_cursor` | O | A | Initial cursor if replay granted |

### 6.3 ObserveEvent

| Field | Req | Tag | Rule |
|---|---|---|---|
| `subscription_id` | R | N | Target subscription |
| `event_id` | R | A | Deduplication id |
| `revision` | R | A | Monotonic revision per scope |
| `event_type` | R | N | change class |
| `delivery_class` | R | N | `meta|value|policy|binding` |
| `payload_ref_or_inline` | R | N | Payload carrier |

### 6.4 ObserveGap

| Field | Req | Tag | Rule |
|---|---|---|---|
| `subscription_id` | R | N | Target subscription |
| `missing_revision_range` | R | A | Gap interval |
| `cause` | R | N | `RetentionExpired|PolicyDenied|TransportLoss` |
| `recovery_hints` | O | N | Resume or restart guidance |

### 6.5 ObserveResume / ObserveClose

| Field | Req | Tag | Rule |
|---|---|---|---|
| `subscription_id` | R | N | Target subscription |
| `cursor` | C | A | Required for `ObserveResume` |
| `close_reason` | C | A | Required for `ObserveClose` |

---

## 7. Policy and Grant Family

### 7.1 GrantPresent / GrantRefused

| Field | Req | Tag | Rule |
|---|---|---|---|
| `grant_action` | R | N | e.g. `resolve.endpoint` |
| `grant_scope` | R | N | Exact/subtree scope |
| `grant_expiry` | R | N | Absolute expiry |
| `grant_ref` | C | N | Required for `GrantPresent` |
| `deny_code` | C | N | Required for `GrantRefused` |

### 7.2 PolicySnapshot / PolicyDelta

| Field | Req | Tag | Rule |
|---|---|---|---|
| `policy_ref` | R | N | Policy identity |
| `policy_revision` | R | N | Monotonic policy revision |
| `scope` | R | N | Covered scope |
| `payload_ref_or_inline` | R | N | Snapshot/delta payload |
| `prev_revision` | C | A | Required for delta chaining |

---

## 8. Cross-Message Validation Rules

1. `relation_id` must resolve to an unexpired relation token when present.
2. `disclosure_level` must not exceed relation token and policy envelope limits.
3. `direct_upgraded` route mode is invalid unless upgrade gates are satisfied.
4. Observation events without subscription authorization must be denied, not dropped silently.
5. Any `expr_raw`/`expr_norm` mismatch must emit an auditable warning or deny based on policy.

---

## 9. Next Hardening Step

Completed:

1. deterministic error mapping by message type (`M0-B-Error-Mapping.md`)

Current next step:

1. keep this matrix synchronized with wire examples and conformance harness fixtures
2. mark each field with wire-lock status when CBOR key dictionary is finalized
