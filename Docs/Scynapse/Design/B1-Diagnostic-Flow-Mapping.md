# B1 Diagnostic Flow Mapping

Date: 2026-03-09  
Author: Codex  
Inputs: `F5-Current-State-Audit.md`, `F6-Current-State-Audit.md`, runtime/security code scan  
Purpose: B0.5 diagnostic mapping pass from `Scynapse-Spec-To-Code-Bridge-Plan.md`

---

## 1. Plain-Language Summary

What we did:

1. Mapped one representative conformance intent flow to current production security/runtime behavior.
2. Identified direct analogs, partial analogs, and missing analogs.
3. Produced concrete deltas to feed the B1 vertical spike.

Why it matters:

1. We can now compare harness semantics to real code behavior without guessing.
2. It prevents opening new micro-slices when the next constraint is implementation evidence.

What happens next:

1. Run the B1 spike with test-only trace instrumentation.
2. Validate pass + deny variants against deterministic comparison rules.
3. Feed mismatches into closure decision and next-phase planning.

---

## 2. Diagnostic Target Flow

Representative flow selected:

1. Authenticated client call to secured grain method requiring capability (`read` action).
2. Validation path through outgoing and incoming Orleans security call filters.
3. Deterministic terminal outcome capture (allow/deny).

Production anchors:

1. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseOutgoingCallFilter.cs`
2. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseIncomingCallFilter.cs`
3. `src/Scynapse/test/Scynapse.Security.Integration.Tests/ScynapseSecurityIntegrationTests.cs`
4. `src/Scynapse/src/Scynapse.Security/Verification/AssertionVerifier.cs`
5. `src/Scynapse/src/Scynapse.Security/Verification/InMemoryCCapWallet.cs`
6. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseSecurityException.cs`

---

## 3. Conformance-to-Production Mapping (B0.5)

| Conformance intent | Production analog | Status | Notes |
|---|---|---|---|
| Operation starts with explicit protocol envelope/messages | Grain call enters outgoing filter (`Invoke`) | Partial | Production currently uses call pipeline, not message envelope/protocol frames. |
| Caller identity attachment | `RequestContext` keys set by outgoing filter | Direct analog | Caller key always attached; original caller key preserved across grain-to-grain calls. |
| Token/capability selection | `ICCapWallet.FindCapability(resource, action)` | Direct analog | Wallet lookup is deterministic but currently hides some deny-class distinctions (see Section 5). |
| Proof verification gate | `AssertionVerifier.VerifyAsync` in incoming filter | Direct analog | Chain/signature/time/replay/revocation checks exist. |
| Capability/resource/action authorization gate | Incoming filter `RequireCapability` checks + `SubjectNameMatcher` | Direct analog | Uses wildcard resource/action semantics. |
| Terminal deny mapping with stable machine-checkable IDs | `ScynapseSecurityException(FailureCode)` | Partial | Has enum failure codes; does not yet map to E-series deterministic IDs. |
| Multi-step mediated handshake (`Init/Challenge/Proof/Accept`) | None (single-step per-call verification) | Missing analog | Current implementation performs per-call verification, not multi-message handshake protocol. |
| Direct/mediated route transitions | None in call filters | Missing analog | No runtime mediation/direct-upgrade path yet. |

---

## 4. Evidence from Current Integration Tests

Covered pass/deny paths (real pipeline):

1. `SecuredGrainCall_WithValidCCap_Succeeds` (pass path)
2. `SecuredGrainCall_NoCCap_IsRejected` (deny path)
3. `SecuredGrainCall_WrongAction_IsRejected` (deny path)
4. `SecuredGrainCall_ExpiredCCap_IsRejected` (deny path)
5. `CrossSilo_GrainToGrain_WithNodeTrust_Succeeds` (hybrid node-trust path)

Observed diagnostic nuance:

1. `WrongAction` deny is realized as missing capability (wallet does not attach non-matching CCap), not as explicit `InsufficientCapability` at incoming filter.
2. `ExpiredCCap` deny is also realized as missing capability (wallet filters expired token before send), not as explicit expired-failure emission.

Implication:

1. Current integration coverage validates deny behavior, but not all semantic deny classes at incoming filter level.

---

## 5. Mismatch and Coverage Gaps

## G1: Error Surface Mismatch

1. Harness uses deterministic E-series IDs per risk class.
2. Production emits `SecurityFailureCode` enum + message.
3. No canonical mapping table currently exists between them.

## G2: Deny-Class Collapsing in Current Test Paths

1. Wallet pre-filtering causes multiple causes (`wrong action`, `expired`) to collapse into `MissingCapability` at ingress.
2. This reduces observability for conformance-level deny taxonomy comparison.

## G3: Trace Granularity Gap

1. Harness provides explicit gate-by-gate state trace.
2. Production integration tests currently assert outcomes only; no normalized gate trace emitted.

## G4: Handshake Layer Gap

1. Conformance protocol models mediated handshake and upgrade lifecycle.
2. Production security path is per-call call-filter pipeline.
3. Bridge needs explicit boundary doc to avoid false mismatch conclusions.

---

## 6. Proposed B1 Trace Schema (Test-Only, Minimal)

Normalized event classes for B1 comparison:

1. `OutgoingContextStart`
2. `OutgoingWalletLookup(resource, action, found)`
3. `OutgoingContextAttached(hasCallerKey, hasCCap, hasBearerProof)`
4. `IncomingPolicyResolved(requiresAuth, allowAnonymous, requiresCallerCapability)`
5. `IncomingNodeTrustEvaluated(isTrustedNode, strictMode)`
6. `IncomingCCapDeserialize(success)`
7. `IncomingChainVerify(success, failureReason?)`
8. `IncomingBearerVerify(success)`
9. `IncomingCapabilityMatch(success, requiredAction, requiredResource)`
10. `IncomingTerminal(outcome, failureCode?)`

Notes:

1. Keep instrumentation test-only and non-invasive.
2. Use deterministic event ordering index for comparison.

---

## 7. Delta Set for B1 Execution

Priority 0 (must do for bridge comparability):

1. Add test-only normalized trace emission around outgoing/incoming filter gates.
2. Define canonical mapping table: `SecurityFailureCode` + verifier reason -> deterministic comparison tokens.
3. Add one integration deny scenario that reaches `InsufficientCapability` directly (not wallet-collapse path).

Priority 1 (high leverage):

1. Add one integration deny scenario for explicit chain verification failure class.
2. Add one integration assertion for exact caller identity equality in `WhoAmI` test path.
3. Emit scenario-level trace artifact from integration tests for machine comparison.

Priority 2 (next step after spike readout):

1. Decide whether to expose deterministic E-series IDs in production or maintain a stable bridge mapping layer.
2. Define explicit protocol boundary statement for handshake-layer semantics not yet present in runtime.

---

## 8. Decision Impact

This diagnostic pass supports:

1. closing M1 without further micro-slice expansion, if B1 confirms expected comparability boundaries, or
2. opening exactly one bounded extension only if B1 reveals a distinct uncovered risk class per `M1-Exit-Criteria.md` extension rule.

Current recommendation:

1. proceed with B1 spike as next execution step.
