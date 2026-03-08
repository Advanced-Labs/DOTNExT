# Scynapse M0-B Protocol Skeleton (Draft)

## 1. Purpose

This document converts M0-A fabric contracts into a protocol-work scaffold for M0-B.

Scope:

1. message families
2. common envelope fields
3. relation and route lifecycle artifacts
4. deterministic failure classes
5. scenario checklist for implementation and tests

This is a design skeleton, not a locked wire format.

---

## 2. Inputs from M0-A

This draft assumes:

1. mediated-first interaction with optional direct-upgrade
2. endpoint disclosure levels (`hidden`, `mediator_visible`, `mutual_visible`; conceptual aliases exist in M0-A)
3. parent-policy primacy with hard inheritance by default
4. capability-compatible rights model for resolve/observe/disclosure

Reference:

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

## 4. Common Envelope (conceptual)

```json
{
  "msg_type": "ResolveRequest|HandshakeInit|...",
  "msg_id": "unique",
  "trace_id": "correlation id",
  "timestamp": "utc",
  "from": {
    "node_id": "public key or node id",
    "name_anchor": "<root>.A.B"
  },
  "intent": "resolve|invoke|observe|policy",
  "target_scope": "<root>.Path",
  "relation_id": "optional",
  "route_mode": "parent_mediated|relay_mediated|anonymous_relay|direct_upgraded",
  "disclosure_level": "hidden|mediator_visible|mutual_visible",
  "proofs": {
    "capability_refs": [],
    "bearer_proof": "optional",
    "attestation_refs": []
  },
  "ttl_ms": 30000
}
```

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
  "route_mode": "parent_mediated|relay_mediated|direct_upgraded",
  "scope": "<root>.Path or subtree",
  "ops": ["invoke", "observe.meta", "resolve.endpoint"],
  "disclosure_level": "hidden|mediator_visible|mutual_visible",
  "issued_at": "utc",
  "expires_at": "utc",
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

## 11. M0-B Locked Defaults (Current Draft)

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
