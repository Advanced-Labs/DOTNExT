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
| `msg_id` | R | A | Unique within sender replay window; canonical typed form `mid:<value>` in M1-S1 strict profile |
| `trace_id` | R | A | Stable across a relation flow; canonical typed form `trc:<value>` in M1-S1 strict profile |
| `timestamp` | R | A | Unix epoch milliseconds (`int64`, UTC) on wire |
| `from.node_id` | R | A | Node identity key or equivalent id; canonical typed form `nid:<value>` in M1-S1 strict profile |
| `from.name_anchor` | C | N | Required when CNS policy/path context is relevant |
| `intent` | R | N | enum code on wire; text label in debug rendering |
| `target_scope` | C | N | Required for scope-addressed operations |
| `relation_id` | C | N | Required after relation establishment; canonical typed form `rid:<value>` in M1-S1 strict profile |
| `route_mode` | C | N | enum code on wire; text label in debug rendering |
| `disclosure_level` | C | N | enum code on wire; text label in debug rendering |
| `proofs.capability_refs` | C | N | array of digest tuples (`[alg_code, digest_bstr]`) |
| `proofs.bearer_proof` | C | N | Required when bearer ownership proof is required |
| `proofs.attestation_refs` | O | N | array of digest tuples (`[alg_code, digest_bstr]`) |
| `ttl_ms` | R | A | Operation expiry bound |

### 2.1 S1 Wire-Lock Profile (`D1`, `D2`, `D4`, `D6`)

Locked enum codebooks for S1:

1. `intent`
   - `0=resolve`
   - `1=invoke`
   - `2=observe`
   - `3=policy`
2. `operation_class`
   - `0=meta`
   - `1=value`
   - `2=endpoint`
   - `3=invoke`
   - `4=observe`
3. `route_mode`
   - `0=parent_mediated`
   - `1=relay_mediated`
   - `2=anonymous_relay`
   - `3=direct_upgraded`
4. `disclosure_level`
   - `0=hidden`
   - `1=mediator_visible`
   - `2=mutual_visible`
5. `deny_code`
   - `1=PathNotFound`
   - `2=PolicyDenied`
   - `3=DisclosureDenied`
   - `4=TrustInsufficient`
   - `5=UpgradeRejected`
   - `6=MediatorUnavailable`
   - `7=GrantMissing`
   - `8=GrantExpired`
   - `9=ReplayWindowExpired`
   - `10=AmbiguousResolution`

Locked proof digest algorithm codes for S1:

1. `1=sha256`
2. `2=sha384`
3. `3=sha512`

---

### 2.2 Key Dictionary `v1` (Frozen for S1 Field Set)

Family ranges:

1. `1-31` envelope/common
2. `32-63` resolve
3. `64-96` handshake
4. `97-127` route/upgrade
5. `128-159` observe (reserved in S1)
6. `160-191` policy/grant (reserved in S1)

Envelope/common keys:

| Key | Field |
|---|---|
| `1` | `msg_type` |
| `2` | `msg_id` |
| `3` | `trace_id` |
| `4` | `timestamp` |
| `5` | `from` |
| `6` | `intent` |
| `7` | `target_scope` |
| `8` | `relation_id` |
| `9` | `route_mode` |
| `10` | `disclosure_level` |
| `11` | `proofs` |
| `12` | `ttl_ms` |

Nested envelope keys:

1. `from`: `1=node_id`, `2=name_anchor`
2. `proofs`: `1=capability_refs`, `2=bearer_proof`, `3=attestation_refs`

Resolve keys (S1-assigned):

| Key | Field |
|---|---|
| `33` | `expr_raw` |
| `34` | `expr_norm` |
| `35` | `operation_class` |
| `36` | `preferred_route_mode` |
| `37` | `selector_hints` |
| `38` | `cursor_or_revision` |
| `39` | `resolved_scope` |
| `40` | `candidate_bindings` |
| `41` | `effective_policy_ref` |
| `42` | `disclosure_constraints` |
| `43` | `referral_hints` |
| `44` | `decision_code` |
| `45` | `referral_scope` |
| `46` | `referral_expiry` |
| `47` | `relay_requirements` |
| `48` | `policy_proof_refs` |
| `49` | `deny_code` |
| `50` | `reason` |
| `51` | `retryable` |
| `52` | `remediation` |
| `53` | `expr_norm_v` |

Handshake keys (S1-assigned):

| Key | Field |
|---|---|
| `65` | `requested_ops` |
| `66` | `requested_scope` |
| `67` | `requested_disclosure_level` |
| `68` | `proposed_route_mode` |
| `69` | `challenge_nonce` |
| `70` | `required_proofs` |
| `71` | `expires_at` |
| `72` | `bearer_proof` |
| `73` | `capability_refs` |
| `74` | `attestation_refs` |
| `75` | `relation_token` |
| `76` | `route_mode` |
| `77` | `disclosure_level` |
| `78` | `fallback_route_ref` |
| `79` | `deny_code` |
| `80` | `policy_ref` |
| `81` | `retryable` |
| `82` | `token_transport` |
| `83` | `relation_token_ref` |
| `84` | `relation_token_cid` |
| `85` | `relation_token_blob` |
| `86` | `verification_mode` |
| `87` | `proof_ref` |
| `88` | `replay_probe` |
| `89` | `force_bad_signature` |
| `90` | `mock_signature_valid` |
| `91` | `mock_replay_detected` |
| `92` | `strict_failure_mode` |
| `93` | `reference_lookup_status` |
| `94` | `reference_lookup_cid` |
| `95` | `reference_grant_status` |
| `96` | `reference_grant_ref` |

Route/upgrade keys (S1-assigned):

| Key | Field |
|---|---|
| `97` | `relation_id` |
| `98` | `route_mode` |
| `99` | `relay_path` |
| `100` | `keepalive_ms` |
| `101` | `upgrade_target_mode` |
| `102` | `endpoint_disclosure_grant_ref` |
| `103` | `consent_proof` |
| `104` | `fallback_route_ref` |
| `105` | `decision_code` |
| `106` | `reason` |
| `107` | `active_route_ref` |
| `108` | `close_reason` |
| `109` | `transport_path` |
| `110` | `payload_class` |

---

### 2.3 M1-S1 Strict Wire-Closure Conventions

Typed identifier convention:

1. canonical format: `<prefix>:<value>`
2. locked prefixes: `nid`, `rid`, `gid`, `pid`, `tid`, `rte`, `mid`, `trc`
3. value constraints:
   - ASCII
   - starts with alphanumeric
   - remaining chars from `[A-Za-z0-9._-]`
   - length `3..128`

Relation token CID convention:

1. canonical format: `sha256:<hex>`
2. used with `relation_token_ref` in `HandshakeAccept` strict boundary mode

---

## 3. Resolve Family

### 3.1 ResolveRequest

| Field | Req | Tag | Rule |
|---|---|---|---|
| `expr_raw` | R | N | Original expression string |
| `expr_norm` | O | N | Canonicalized expression |
| `expr_norm_v` | C | N | Required when `expr_norm` is present; supported version set currently `{1}` |
| `operation_class` | R | N | `meta|value|endpoint|invoke|observe` |
| `preferred_route_mode` | O | N | Hint only; policy may override |
| `selector_hints` | O | N | Disambiguation hints |
| `cursor_or_revision` | O | A | Incremental refresh support |

### 3.2 ResolveResponse

| Field | Req | Tag | Rule |
|---|---|---|---|
| `resolved_scope` | R | N | Canonical target scope |
| `expr_norm` | O | N | Canonicalized resolved expression when provided |
| `expr_norm_v` | C | N | Required when `expr_norm` is present; supported version set currently `{1}` |
| `candidate_bindings` | R | A | Policy-filtered candidate set (can be empty) |
| `effective_policy_ref` | R | N | Policy revision reference |
| `disclosure_constraints` | R | N | Allowed visibility level(s) |
| `referral_hints` | O | A | Next authoritative path hints |
| `decision_code` | R | N | `Resolved|Ambiguous|Denied|RefreshRequired` |

### 3.3 ResolveReferral

| Field | Req | Tag | Rule |
|---|---|---|---|
| `referral_scope` | R | A | Next authority scope |
| `expr_norm` | O | N | Canonicalized expression associated with referral |
| `expr_norm_v` | C | N | Required when `expr_norm` is present; supported version set currently `{1}` |
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
| `policy_ref` | C | N | Required when deny code is policy-causal (`PolicyDenied|DisclosureDenied|GrantMissing|GrantExpired`) |

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
| `verification_mode` | C | N | M1-S3 profile: required (`mock|strict`) |
| `proof_ref` | C | N | M1-S3 strict mode: required stable proof reference |
| `replay_probe` | O | N | M1-S3 strict mode: force deterministic nonce replay path |
| `force_bad_signature` | O | N | M1-S3 strict mode: force deterministic invalid-signature path |
| `mock_signature_valid` | O | N | M1-S3 mock mode: signature verdict flag (default `true`) |
| `mock_replay_detected` | O | N | M1-S3 mock mode: replay verdict flag (default `false`) |
| `strict_failure_mode` | O | N | M1-S4 strict mode: optional deterministic failure injection (`none|expired|revoked|unresolvable_proof|not_yet_valid`) |

### 4.4 HandshakeAccept

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_token` | C | N | Legacy/debug alias; optional when `relation_token_ref` + `relation_token_cid` are used |
| `route_mode` | R | N | Active route mode |
| `disclosure_level` | R | N | Granted disclosure level |
| `expires_at` | R | N | Relation token expiry |
| `fallback_route_ref` | C | N | Required if direct-upgrade is granted |
| `token_transport` | C | N | M1-S1/M1-S5/M1-S6 profile: required (`reference|inline`) |
| `relation_token_ref` | C | N | M1-S1/M1-S5/M1-S6 profile: required typed identifier |
| `relation_token_cid` | C | N | M1-S1/M1-S5/M1-S6 profile: required `sha256:<hex>` digest id |
| `relation_token_blob` | C | N | M1-S1/M1-S5/M1-S6 profile: required for `inline`, forbidden for `reference` |
| `reference_lookup_status` | C | N | M1-S6 profile + `token_transport=reference`: required (`resolved|missing|rebinding_detected`) |
| `reference_lookup_cid` | C | N | M1-S6 profile + `token_transport=reference` + `reference_lookup_status=resolved`: required `sha256:<hex>` |
| `reference_grant_status` | C | N | M1-S7 profile + `token_transport=reference`: required (`active|missing|expired|revoked|not_required`) |
| `reference_grant_ref` | C | N | M1-S7 profile + `reference_grant_status=active`: required typed identifier |

### 4.5 HandshakeDeny

| Field | Req | Tag | Rule |
|---|---|---|---|
| `deny_code` | R | N | Deterministic deny code |
| `policy_ref` | C | N | Required when deny code is policy-causal (`PolicyDenied|DisclosureDenied|GrantMissing|GrantExpired`) |
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

### 5.5 RouteData (M1-S2/M1-S3 Runtime Bridge)

| Field | Req | Tag | Rule |
|---|---|---|---|
| `relation_id` | C | N | Recommended when data flow is relation-bound |
| `route_mode` | R | N | Declared active route mode for this data transfer |
| `transport_path` | R | N | `mediated|direct`; must match active session mode in M1-S2/M1-S3 |
| `payload_class` | O | N | lightweight payload marker (`invoke|event|meta`) |

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
6. When `expr_norm` is present, `expr_norm_v` must be present and supported.
7. Policy-causal deny codes require `policy_ref`.
8. In M1-S1 strict profile, id/ref fields use typed identifiers (`<prefix>:<value>`).
9. In M1-S1 strict profile, `HandshakeAccept` relation token boundary fields are mandatory and mode-consistent.
10. In M1-S3 profile, `HandshakeProof.verification_mode` governs strict/mock security adapter behavior and deterministic deny mapping.
11. In M1-S4 profile, `HandshakeProof.strict_failure_mode` is valid only in strict mode and must be from the locked domain.
12. In M1-S5 profile, inline `HandshakeAccept` token CID must match `sha256(relation_token_blob)` or deterministic deny is emitted.
13. In M1-S6 profile, reference `HandshakeAccept` requires lookup status contract, and resolved lookup CID must match `relation_token_cid` or deterministic deny is emitted.
14. In M1-S7 profile, reference `HandshakeAccept` requires grant status contract, and active grant requires typed `reference_grant_ref`; missing/expired/revoked grant states emit deterministic deny.

---

## 9. Next Hardening Step

Completed:

1. deterministic error mapping by message type (`M0-B-Error-Mapping.md`)
2. S1 wire-lock profile for enum/timestamp/proof/dictionary (`D1`, `D2`, `D4`, `D6`)

Current next step:

1. keep this matrix synchronized with wire examples and conformance harness fixtures
2. preserve M1-S1 wire-closure constraints while extending runtime bridge and security-adapter slices, including M1-S4 strict failure mapping, M1-S5 token-integrity constraints, M1-S6 reference-token guard constraints, and M1-S7 reference-grant guard constraints
