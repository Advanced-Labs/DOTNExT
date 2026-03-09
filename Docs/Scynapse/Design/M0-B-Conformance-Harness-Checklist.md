# M0-B Conformance Harness Checklist (Draft)

## 1. Purpose

Define the conformance harness requirements for validating M0-B protocol behavior across:

1. field validity
2. state transitions
3. deterministic error mapping
4. security/disclosure gates
5. audit completeness

---

## 2. Inputs (Normative for Harness)

1. `Docs/Scynapse/Design/M0-B-Protocol-Skeleton.md`
2. `Docs/Scynapse/Design/M0-B-Message-Field-Matrix.md`
3. `Docs/Scynapse/Design/M0-B-Error-Mapping.md`
4. `Docs/Scynapse/Design/M0-B-State-Transition-Matrix.md`
5. `Docs/Scynapse/Design/M0-B-Protocol-Test-Vectors.md`
6. `Docs/Scynapse/Design/M0-B-Orleans-Compatibility-Profile.md`

---

## 3. Harness Layers

### 3.1 L1 Envelope and Schema Validation

Checklist:

1. enforce required global envelope fields
2. enforce type constraints and enum domains
3. enforce conditional fields (`C`) only when trigger condition holds
4. reject unknown required-by-policy fields as protocol violations

### 3.2 L2 Message Field Rules

Checklist:

1. apply per-message `R/O/C` matrix
2. verify message family-specific invariants
3. validate cross-field dependencies (for example `direct_upgraded` requires fallback ref)

### 3.3 L3 State Machine Validation

Checklist:

1. track current state per operation/relation/subscription context
2. validate transition existence before handling each message
3. on invalid transition, emit deterministic mapped code
4. assert terminal-state behavior (`Deny`, `Completed`, `ObserveClosed`)

### 3.4 L4 Error Mapping Validation

Checklist:

1. verify deny code is allowed for message type
2. verify retryability default matches mapping
3. verify required remediation hint for each deny code
4. reject silent failures and unknown code substitutions

### 3.5 L5 Security and Disclosure Gate Validation

Checklist:

1. enforce capability gating for endpoint/value/policy-sensitive operations
2. enforce disclosure level caps against relation token and policy
3. enforce encrypted endpoint grant requirement when directory mode is encrypted
4. enforce mediated-first and conditional direct-upgrade gates

### 3.6 L6 Observation Semantics Validation

Checklist:

1. validate `follow_moves` defaults and behavior by scope type
2. validate cursor/replay semantics
3. validate monotonic revisions and dedupe by `event_id`
4. validate deterministic `ObserveGap` on replay expiry

### 3.7 L7 Audit Completeness Validation

Checklist:

1. for `Regulated` profile, verify required audit fields are present
2. verify trace continuity across relayed and upgraded routes
3. verify payload hash presence when raw payload is omitted

---

## 4. Fixture Contract (Minimum)

Each harness case should include:

```json
{
  "id": "TV-001",
  "slice_profile": "S1",
  "expect_conformance": "pass",
  "expected_error_ids": [],
  "expected_error_contains": [],
  "preconditions": [],
  "messages": [],
  "expected_state_trace": [],
  "expected_outcome": {
    "success": true,
    "deny_code": null
  },
  "assertions": []
}
```

Required assertion categories:

1. field assertions
2. state assertions
3. error assertions
4. disclosure/security assertions
5. audit assertions (when profile requires)

Failure oracle preference:

1. `expected_error_ids` is preferred for deterministic machine-checkable fail vectors.
2. `expected_error_contains` is compatibility fallback for legacy token matching.

S2 extension note:

1. `slice_profile: "S2"` enables direct-upgrade gate evaluation semantics.
2. S2 `RouteUpgradeProbe` fixtures must include:
   - `policy_allowed` (bool)
   - `disclosure_allowed` (bool)
   - `grant_status` (`active|missing|expired|not_required`)
   - `trust_sufficient` (bool)

S3 extension note:

1. `slice_profile: "S3"` enables encrypted endpoint grant/disclosure validation.
2. S3 `ResolveRequest` fixtures with `operation_class=endpoint` must include:
   - `endpoint_directory_mode` (`plaintext|encrypted`)
   - `endpoint_grant_status` (`active|missing|expired|not_required`)
   - `endpoint_disclosure_allowed` (bool)
3. encrypted endpoint disclosure with active grant requires `GrantPresent` proof path before `ResolveResponse`.

S4 extension note:

1. `slice_profile: "S4"` enables observation/replay lifecycle validation.
2. S4 observe fixtures use:
   - `ObserveOpen.scope_mode` (`exact|subtree`)
   - `ObserveOpen.follow_moves` (optional bool; default by scope mode)
   - `ObserveGap.cause` (`RetentionExpired|PolicyDenied|TransportLoss`)
   - `ObserveResume.replay_available` (bool)

S5 extension note:

1. `slice_profile: "S5"` enables policy inheritance hard-lock validation.
2. S5 policy fixtures use:
   - `PolicyDelta.parent_hard_lock` (bool)
   - `PolicyDelta.child_weaken_attempt` (bool)
   - `PolicyDelta.override_granted` (bool)
   - `PolicyDeny.deny_code` (deterministic match required when hard-lock violation occurs)

M1-S1 extension note:

1. `slice_profile: "M1-S1"` enables deferred wire-closure checks for `D3`, `D5`, `D7`, `D8`.
2. M1-S1 typed identifier checks enforce `<prefix>:<value>` for id/ref fields.
3. `expr_norm` requires integer `expr_norm_v` and supported version membership.
4. policy-causal deny codes require `policy_ref`.
5. `HandshakeAccept` relation token boundary checks enforce:
   - `token_transport` (`reference|inline`)
   - required `relation_token_ref` + `relation_token_cid`
   - conditional `relation_token_blob` (inline-only)

M1-S2 extension note:

1. `slice_profile: "M1-S2"` enables runtime-bridge session transport checks.
2. M1-S2 introduces `RouteData` control vectors with required fields:
   - `route_mode`
   - `transport_path` (`mediated|direct`)
3. active session mode governs allowed transport path:
   - `RelayedSession` -> `mediated`
   - `DirectSession` -> `direct`
4. bridge transit assertions are machine-checkable:
   - `bridge_transit_contains`
   - `bridge_transit_count_equals`

M1-S3 extension note:

1. `slice_profile: "M1-S3"` enables security-adapter handshake proof validation.
2. M1-S3 `HandshakeProof` fixtures require:
   - `verification_mode` (`mock|strict`)
3. strict mode fields:
   - `proof_ref` (required)
   - optional `replay_probe` (bool)
   - optional `force_bad_signature` (bool)
4. mock mode fields:
   - optional `mock_signature_valid` (bool)
   - optional `mock_replay_detected` (bool)
5. deterministic deny IDs must be emitted for signature and replay failures.

M1-S4 extension note:

1. `slice_profile: "M1-S4"` extends strict security-adapter mapping depth.
2. M1-S4 strict mode supports optional:
   - `strict_failure_mode` (`none|expired|revoked|unresolvable_proof|not_yet_valid`)
3. deterministic strict failure IDs:
   - `E3081_M1S4_PROOF_EXPIRED`
   - `E3082_M1S4_PROOF_REVOKED`
   - `E3083_M1S4_PROOF_CHAIN_UNRESOLVABLE`
   - `E3084_M1S4_PROOF_NOT_YET_VALID`
4. invalid `strict_failure_mode` must fail schema validation with:
   - `E3080_M1S4_STRICT_FAILURE_MODE_INVALID`

M1-S5 extension note:

1. `slice_profile: "M1-S5"` extends M1-S4 with relation-token integrity checks.
2. M1-S5 `HandshakeAccept` reuses M1-S1 token-boundary requirements:
   - `token_transport` (`reference|inline`)
   - `relation_token_ref`
   - `relation_token_cid`
   - `relation_token_blob` (inline-only)
3. inline transport deterministic integrity rule:
   - `relation_token_cid == sha256(relation_token_blob)`
4. deterministic mismatch deny ID:
   - `E3091_M1S5_TOKEN_CID_MISMATCH`

---

## 5. Conformance Gates (Pass/Fail)

### 5.1 Gate G1: Field Conformance

Pass criteria:

1. 100% of required-field assertions pass
2. 0 tolerated unknown enum/value coercions

### 5.2 Gate G2: State Conformance

Pass criteria:

1. 100% of valid transitions accepted
2. 100% of invalid transitions rejected with mapped deterministic code

### 5.3 Gate G3: Error Conformance

Pass criteria:

1. deny code always in allowed set for message type
2. retryability and remediation semantics match mapping document

### 5.4 Gate G4: Security/Disclosure Conformance

Pass criteria:

1. no endpoint disclosure while `disclosure_level=hidden`
2. no direct upgrade without all gates in S2 profile
3. no gated operation accepted without required grants/proofs
4. S1 profile rejects direct-upgrade accept path deterministically
5. S2 profile applies gate order deterministically:
   - `PolicyDenied -> DisclosureDenied -> GrantMissing/GrantExpired -> TrustInsufficient -> UpgradeRejected`
6. S3 profile denies encrypted endpoint disclosure deterministically when grant/disclosure gates are unmet.
7. M1-S3 strict/mock handshake proof failures map deterministically (`invalid signature`, `nonce replay`) without bypassing existing handshake state rules.
8. M1-S4 strict temporal/revocation/proof-chain failures map deterministically to stable strict-failure IDs, with invalid mode rejected at schema level.
9. M1-S5 inline relation-token CID integrity mismatches map deterministically to `E3091` without bypassing existing handshake state rules.

### 5.5 Gate G5: Observation Conformance

Pass criteria:

1. rename/move semantics match scope defaults
2. replay success/failure behavior matches vectors
3. monotonic revision guarantees hold per scope

### 5.6 Gate G6: Audit Conformance

Pass criteria:

1. regulated audit fields complete
2. trace continuity preserved across flow

---

## 6. Minimum Required Vector Execution

Must run at least:

1. all vectors in `M0-B-Protocol-Test-Vectors.md` section 3
2. one additional negative test per message family not already covered
3. one randomized invalid-order message sequence per family

---

## 7. Reporting Format

Each run should produce:

1. per-vector status (`PASS/FAIL`)
2. failing error IDs/categories and human-readable reason
3. observed vs expected state trace
4. observed vs expected deny code
5. coverage summary by harness layer (L1-L7)

---

## 8. Exit Criteria for M0-B Conformance Readiness

1. all required vectors pass across all gates
2. no unresolved deterministic-code mismatches
3. no unresolved state transition mismatches
4. no critical security/disclosure assertion failures
5. coverage report generated and archived for review
