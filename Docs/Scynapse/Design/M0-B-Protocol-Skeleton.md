# Scynapse M0-B Protocol Skeleton (Draft)

## 1. Purpose

This document converts M0-A fabric contracts into a protocol-work scaffold for M0-B.

Scope:

1. message families
2. common envelope fields
3. relation and route lifecycle artifacts
4. deterministic failure classes
5. scenario checklist for implementation and tests

This is a design skeleton with S1 and M1-S1 wire-lock decisions applied (`D1`, `D2`, `D3`, `D4`, `D5`, `D6`, `D7`, `D8`).

---

## 2. Inputs from M0-A

This draft assumes:

1. mediated-first interaction with optional direct-upgrade
2. endpoint disclosure levels (`hidden`, `mediator_visible`, `mutual_visible`; conceptual aliases exist in M0-A)
3. parent-policy primacy with hard inheritance by default
4. capability-compatible rights model for resolve/observe/disclosure

Reference:

- `Docs/Scynapse/Design/METHODOLOGY.md`
- `Docs/Scynapse/Design/EXECUTIVE-MEMORY.md`
- `Docs/Scynapse/Design/SESSION-LOG.md`
- `Docs/Scynapse/Design/M0-A-Fabric-Contracts.md`
- `Docs/Scynapse/Design/M0-Orleans-Reuse-Matrix.md`
- `Docs/Scynapse/Design/M0-B-Orleans-Compatibility-Profile.md`
- `Docs/Scynapse/Design/M0-B-Message-Field-Matrix.md`
- `Docs/Scynapse/Design/M0-B-Error-Mapping.md`
- `Docs/Scynapse/Design/M0-B-State-Transition-Matrix.md`
- `Docs/Scynapse/Design/M0-B-Protocol-Test-Vectors.md`
- `Docs/Scynapse/Design/M0-B-Conformance-Harness-Checklist.md`
- `Docs/Scynapse/Design/M0-B-Wire-Examples.md`
- `Docs/Scynapse/Design/M0-Cross-Doc-Consistency-Report.md`
- `Docs/Scynapse/Design/M0-Implementation-Slice-Plan.md`
- `Docs/Scynapse/Design/M0-S1-Task-Board.md`
- `Docs/Scynapse/Design/M0-S1-Closure.md`
- `Docs/Scynapse/Design/M0-S2-Task-Board.md`
- `Docs/Scynapse/Design/M0-S3-Task-Board.md`
- `Docs/Scynapse/Design/M0-S4-Task-Board.md`
- `Docs/Scynapse/Design/M0-S5-Task-Board.md`
- `Docs/Scynapse/Design/M0-Conformance-Closure.md`
- `Docs/Scynapse/Design/M0-Exit-Review.md`
- `Docs/Scynapse/Design/M1-Entry-Plan.md`
- `Docs/Scynapse/Design/M1-S1-Task-Board.md`
- `Docs/Scynapse/Design/M1-S1-Closure.md`
- `Docs/Scynapse/Design/M1-Status-Checkpoint.md`
- `Docs/Scynapse/Design/M1-S2-Task-Board.md`
- `Docs/Scynapse/Design/M1-S2-Closure.md`
- `Docs/Scynapse/Design/M1-S3-Task-Board.md`
- `Docs/Scynapse/Design/M1-S3-Closure.md`
- `Docs/Scynapse/Design/M1-S4-Task-Board.md`
- `Docs/Scynapse/Design/M1-S4-Closure.md`
- `Docs/Scynapse/Design/M1-S5-Task-Board.md`
- `Docs/Scynapse/Design/M1-S5-Closure.md`
- `Docs/Scynapse/Design/M1-S6-Task-Board.md`
- `Docs/Scynapse/Design/M1-S6-Closure.md`
- `Docs/Scynapse/Design/M1-S7-Task-Board.md`
- `Docs/Scynapse/Design/M1-S7-Closure.md`
- `Docs/Scynapse/Design/M1-S8-Task-Board.md`
- `Docs/Scynapse/Design/M1-S8-Closure.md`
- `Docs/Scynapse/Design/M1-S9-Task-Board.md`
- `Docs/Scynapse/Design/M1-S9-Closure.md`
- `Docs/Scynapse/Design/M1-S10-Task-Board.md`
- `Docs/Scynapse/Design/M1-S10-Closure.md`
- `Docs/Scynapse/Design/M1-S11-Task-Board.md`
- `Docs/Scynapse/Design/M1-S11-Closure.md`
- `Docs/Scynapse/Design/M0-B-Wire-Lock-Open-Decisions.md`
- `Docs/Scynapse/Design/M0-Status-Checkpoint.md`

---

## 3. Message Families (v0)

### 3.1 Resolve

1. `ResolveRequest`
2. `ResolveResponse`
3. `ResolveReferral`
4. `ResolveDeny`

### 3.2 Relation and Handshake

1. `HandshakeInit`
2. `HandshakeChallenge`
3. `HandshakeProof`
4. `HandshakeAccept`
5. `HandshakeDeny`

### 3.3 Route and Upgrade

1. `RouteEstablish` (mediated path setup)
2. `RouteUpgradeProbe` (direct-upgrade attempt)
3. `RouteUpgradeAccept`
4. `RouteUpgradeReject`
5. `RouteKeepAlive`
6. `RouteClose`
7. `RouteData` (runtime-bridge validation/control-plane payload path marker)

### 3.4 Observation

1. `ObserveOpen`
2. `ObserveAck`
3. `ObserveEvent`
4. `ObserveGap`
5. `ObserveResume`
6. `ObserveClose`

### 3.5 Policy and Grants

1. `GrantPresent`
2. `GrantRefused`
3. `PolicySnapshot`
4. `PolicyDelta`

---

## 4. Common Envelope (conceptual debug rendering)

```json
{
  "msg_type": "ResolveRequest|HandshakeInit|...",
  "msg_id": "unique",
  "trace_id": "correlation id",
  "timestamp": 1736092800000,
  "from": {
    "node_id": "public key or node id",
    "name_anchor": "<root>.A.B"
  },
  "intent": 0,
  "target_scope": "<root>.Path",
  "relation_id": "optional",
  "route_mode": 0,
  "disclosure_level": 1,
  "proofs": {
    "capability_refs": [[1, "0x..."]],
    "bearer_proof": "optional",
    "attestation_refs": [[1, "0x..."]]
  },
  "ttl_ms": 30000
}
```

Wire-lock notes:

1. enum fields in the envelope/body use compact unsigned integer codes on wire (`D1`).
2. `timestamp` and other temporal fields use Unix epoch milliseconds (`int64`, UTC) on wire (`D2`).
3. proof refs use digest tuples (`[alg_code, digest]`) on wire (`D4`).
4. this JSON block is readability-oriented debug rendering, not canonical wire bytes.

---

## 5. Resolve Contract (minimum fields)

### 5.1 ResolveRequest

1. expression string (or normalized path form)
2. operation class (`meta`, `value`, `endpoint`, `invoke`, `observe`)
3. caller anchor and preferred route mode
4. optional cursor/requested revision for cached refresh

### 5.2 ResolveResponse

1. resolved canonical name/scope
2. candidate binding set (policy-filtered)
3. effective policy reference and revision
4. disclosure constraints
5. referral hints (if authoritative answer is elsewhere)

### 5.3 ResolveReferral

1. next authoritative scope
2. referral expiry
3. optional relay requirements
4. optional policy proof references

---

## 6. Handshake and Relation Contract

### 6.1 Relation Token (conceptual)

```json
{
  "relation_id": "unique",
  "participants": ["nodeA", "nodeB"],
  "route_mode": 0,
  "scope": "<root>.Path or subtree",
  "ops": ["invoke", "observe.meta", "resolve.endpoint"],
  "disclosure_level": 1,
  "issued_at": 1736092800000,
  "expires_at": 1736093700000,
  "issuer_chain_refs": []
}
```

### 6.2 Upgrade Rule

`RouteUpgradeProbe` is valid only when all M0-A upgrade gates pass, including:

1. policy allowance
2. endpoint consent
3. trust checks
4. endpoint-disclosure grant when encrypted registration is used
5. relayed fallback continuity

---

## 7. Observation Contract

### 7.1 ObserveOpen

1. scope and filter
2. subscription mode (`OnChange`, `IntervalSnapshot`, `Predicate`, `Mixed`)
3. profile (`Lite`, `Standard`, `Rich`, `Regulated`)
4. requested replay cursor/window
5. `follow_moves` flag

### 7.2 ObserveEvent

Must carry:

1. event id
2. monotonic revision
3. event type
4. payload reference or inline payload
5. policy/relation context needed for verification

### 7.3 ObserveGap

Must carry:

1. scope
2. missed revision interval
3. cause (`RetentionExpired`, `PolicyDenied`, `TransportLoss`)
4. recovery hints

---

## 8. Deterministic Failure Taxonomy (v0)

1. `PathNotFound`
2. `PolicyDenied`
3. `DisclosureDenied`
4. `TrustInsufficient`
5. `UpgradeRejected`
6. `MediatorUnavailable`
7. `GrantMissing`
8. `GrantExpired`
9. `ReplayWindowExpired`
10. `AmbiguousResolution`

Failure responses should include:

1. machine code
2. stable human-readable reason
3. retryability hint
4. optional remediation pointer

---

## 9. Security and Privacy Baselines

1. metadata visibility and endpoint visibility are distinct permissions.
2. endpoint coordinates should never be emitted when disclosure level is `hidden`.
3. encrypted endpoint registration should be supported without plaintext CNS storage.
4. grants and relation tokens must be time-bounded.
5. all privileged operations should be auditable through event traces.

---

## 10. Implementation Scenarios (for tests)

1. cold resolve miss requiring full up/down namespace walk
2. parent-mediated handshake with no direct-upgrade allowed
3. relayed session upgraded to direct after consent and proof checks
4. encrypted endpoint resolve with valid grant
5. encrypted endpoint resolve denied due to missing grant
6. observe subtree with `follow_moves=true` and rename event
7. observe exact path with `follow_moves=false` and rename event
8. replay resume successful within retention window
9. replay resume fails with deterministic gap signal
10. parent hard policy blocks child weakening attempt

---

## 11. M0-B Locked Defaults (S1 Lock Pass)

1. Canonical serialization profile:
   - Wire canonical format is CBOR (deterministic/canonical profile).
   - Optional JSON rendering is tooling-only and not authoritative on wire.
2. Expression transport form:
   - `expr_raw` is required for request portability and audit readability.
   - `expr_norm` is optional in requests, recommended in responses/referrals when available.
   - If both are present and disagree, `expr_norm` wins and mismatch is audited.
3. Relation token authority and rotation:
   - Relation token is signed by the authority that established the active route mode (parent mediator or approved relay authority).
   - Token lifetime is short by default (recommended 15 minutes).
   - Refresh uses explicit renewal flow; stale tokens are denied, not auto-upgraded.
4. Multi-candidate ambiguity policy:
   - Default is fail-closed with `AmbiguousResolution`.
   - Caller may supply explicit disambiguation strategy or selector hints.
   - Silent first-match behavior is disallowed in protocol default.
5. Minimum audit payload for `Regulated` profile:
   - `trace_id`, `msg_id`, `timestamp`
   - actor id and relation id
   - target scope and operation class
   - decision code and policy revision
   - grant/capability references used
   - route mode and disclosure level
   - payload reference hash (not raw sensitive payload by default)
6. Enum wire representation (`D1`):
   - operational enums are encoded as compact unsigned integer codes on wire.
   - canonical text labels remain mandatory for debug/tooling rendering.
   - unknown enum code is a schema/protocol failure.
7. Timestamp wire representation (`D2`):
   - temporal fields use Unix epoch milliseconds (`int64`, UTC) on wire.
   - RFC3339 text is debug/tooling-only.
8. Proof reference wire representation (`D4`):
   - `capability_refs` and `attestation_refs` use digest tuples (`[alg_code, digest_bstr]`) on wire.
   - `bearer_proof` remains opaque proof payload when present.
9. Key dictionary stability (`D6`):
   - dictionary `v1` is frozen for S1 field set.
   - family key ranges are reserved for forward-compatible growth.
   - field-level key assignments are canonicalized in `M0-B-Message-Field-Matrix.md`.
10. Identifier encoding strictness (`D3`):
   - canonical id/ref format is typed-string `<prefix>:<value>`.
   - locked prefix set: `nid`, `rid`, `gid`, `pid`, `tid`, `rte`, `mid`, `trc`.
11. Normalization versioning (`D5`):
   - `expr_norm` requires `expr_norm_v`.
   - supported `expr_norm_v` set currently `{1}`.
   - `expr_norm_v` without `expr_norm` is invalid.
12. Deny envelope policy reference (`D7`):
   - for policy-causal deny codes (`PolicyDenied`, `DisclosureDenied`, `GrantMissing`, `GrantExpired`), `policy_ref` is required.
13. Relation token serialization boundary (`D8`):
   - `HandshakeAccept` must declare `token_transport` (`reference|inline`).
   - `relation_token_ref` + `relation_token_cid` are required in both modes.
   - `relation_token_blob` is required only for `inline` and forbidden for `reference`.
14. M1-S3 security-adapter handshake proof profile:
   - `HandshakeProof` in `M1-S3` requires `verification_mode` (`mock|strict`).
   - strict mode integrates bounded verification with nonce replay checks.
   - strict/mock failure paths map to deterministic deny IDs.
15. M1-S4 strict failure mapping profile:
   - `HandshakeProof` in `M1-S4` extends strict mode with optional `strict_failure_mode`.
   - supported failure modes: `none`, `expired`, `revoked`, `unresolvable_proof`, `not_yet_valid`.
   - strict failure paths map deterministically to `E3081`..`E3084`; invalid mode is schema error `E3080`.
16. M1-S5 relation-token integrity profile:
   - `HandshakeAccept` in `M1-S5` uses M1-S1 token-boundary contract fields.
   - `token_transport=inline` requires `relation_token_cid` to match `sha256(relation_token_blob)`.
   - CID mismatch maps deterministically to `E3091_M1S5_TOKEN_CID_MISMATCH`.
17. M1-S6 reference-token guard profile:
   - `HandshakeAccept` in `M1-S6` extends M1-S5 with reference lookup status checks.
   - `token_transport=reference` requires `reference_lookup_status` in `resolved|missing|rebinding_detected`.
   - resolved lookup requires `reference_lookup_cid` and equality with `relation_token_cid`.
   - deterministic deny IDs: `E3101` (unresolved), `E3102` (CID mismatch), `E3103` (rebinding).
18. M1-S7 reference-grant guard profile:
   - `HandshakeAccept` in `M1-S7` extends M1-S6 with reference grant status checks.
   - `token_transport=reference` requires `reference_grant_status` in `active|missing|expired|revoked|not_required`.
   - `reference_grant_status=active` requires typed `reference_grant_ref`.
   - deterministic deny IDs: `E3111` (grant missing), `E3112` (grant expired), `E3113` (grant revoked).
19. M1-S8 reference-grant proof binding profile:
   - `HandshakeAccept` in `M1-S8` extends M1-S7 with active-grant proof verification checks.
   - `reference_grant_status=active` requires `reference_grant_verification_mode` in `mock|strict`.
   - strict mode requires typed `reference_grant_proof_ref` and optional deterministic failure control `reference_grant_strict_failure_mode`.
   - mock mode requires boolean `reference_grant_mock_valid`.
   - deterministic runtime deny IDs: `E3130`..`E3135`.
20. M1-S9 reference-grant freshness/replay profile:
   - `HandshakeAccept` in `M1-S9` extends M1-S8 with active-grant freshness/replay checks.
   - `reference_grant_status=active` requires `reference_grant_proof_freshness_status` in `fresh|stale`.
   - `reference_grant_status=active` requires `reference_grant_proof_replay_status` in `clear|replayed`.
   - stale freshness maps deterministically to `E3150` (`GrantExpired`).
   - replayed proof maps deterministically to `E3151` (`TrustInsufficient`).
21. M1-S10 reference-grant claim-binding profile:
- `M1-S10` extends M1-S9 with subject/scope/action binding checks for active reference grant acceptance.
- `HandshakeInit` must provide claim-binding source fields: `requester_subject_ref`, `requested_scope`, `requested_ops`.
- `HandshakeAccept` with active reference grant must provide claim fields: `reference_grant_claim_subject_ref`, `reference_grant_claim_scope`, `reference_grant_claim_action`.
- mismatch deny mapping is deterministic:
  - `E3170` subject mismatch -> `TrustInsufficient`
  - `E3171` scope mismatch -> `PolicyDenied`
  - `E3172` action mismatch -> `PolicyDenied`
22. M1-S11 reference-grant challenge-session nonce-binding profile:
- `M1-S11` extends M1-S10 with challenge/proof/accept nonce-binding checks for active reference grant acceptance.
- `HandshakeChallenge` must provide `challenge_nonce` (non-empty string).
- `HandshakeProof` must provide `challenge_nonce` (non-empty string) and match `HandshakeChallenge.challenge_nonce`.
- `HandshakeAccept` with active reference grant must provide `reference_grant_challenge_nonce` (non-empty string) and match `HandshakeProof.challenge_nonce`.
- mismatch deny mapping is deterministic:
  - `E3190` proof/challenge nonce mismatch -> `TrustInsufficient`
  - `E3191` accept/proof nonce mismatch -> `TrustInsufficient`
