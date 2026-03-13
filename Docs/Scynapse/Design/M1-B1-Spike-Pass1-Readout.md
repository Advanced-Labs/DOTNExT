# M1-B1 Spike Pass 1 Readout

Date: 2026-03-09  
Branch: `codex/m1-b1-vertical-spike`  
Scope: W4/W5 diagnostic closure (pass 1)

---

## 1. Plain-Language Summary

What we did:

1. Compared new integration trace outputs to conformance-intent expectations for one representative secured-call flow.
2. Classified mismatches into three buckets:
   - harness mismatch,
   - implementation mismatch,
   - contract ambiguity.
3. Produced a closure recommendation for M1 governance.

Why it matters:

1. We now have implementation evidence, not only design inference.
2. The remaining gap is mostly comparability contract and handshake-boundary semantics, not missing basic enforcement capability.

What happens next:

1. Decide M1 closure posture using this readout plus `M1-Exit-Criteria.md`.
2. If we continue B1, focus on comparator automation and stable error-surface mapping.

---

## 2. Comparison Basis

Inputs:

1. `B1-Diagnostic-Flow-Mapping.md`
2. `F5-F6-B1-Decision-Delta-Set.md`
3. Integration trace events emitted by:
   - `ScynapseOutgoingCallFilter`
   - `ScynapseIncomingCallFilter`
4. Integration tests in:
   - `ScynapseSecurityIntegrationTests.cs`

Observed integration coverage in pass 1:

1. pass flow (`SecuredGrainCall_WithValidCCap_Succeeds`) with trace assertions,
2. deny flow (`ResourceMismatch`) reaching `InsufficientCapability`,
3. deny flow (`BrokenProofChain`) reaching `ChainVerificationFailed`.

---

## 3. Mismatch Triage

| ID | Finding | Classification | Impact | Action |
|---|---|---|---|---|
| B1-M01 | Runtime has no multi-message mediated handshake (`Init/Challenge/Proof/Accept`) equivalent in production path | Contract ambiguity | High for direct conformance parity claims | Keep explicit boundary: per-call filter pipeline is current runtime layer; handshake remains design/harness layer. |
| B1-M02 | Runtime failure surface uses `SecurityFailureCode` + verifier reason; harness uses E-series deterministic IDs | Contract ambiguity (can become implementation decision) | High for machine-compare interoperability | Define stable bridge mapping table now; defer runtime E-series adoption decision until post-spike governance. |
| B1-M03 | Historical deny-class collapse (`MissingCapability`) for some negative paths | Implementation mismatch (partially resolved) | Medium | Added explicit `InsufficientCapability` scenario; keep additional deny-path expansions in follow-up. |
| B1-M04 | Trace schema did not exist in production path before spike | Implementation mismatch (resolved in pass 1) | Medium | Completed with optional test-only trace sink/events. |
| B1-M05 | Comparator is still assertion-based in tests, not generalized oracle comparator | Harness/tooling mismatch | Medium | Add comparator utility in next pass if M1 remains open for bridge hardening. |

No critical harness defect discovered in pass 1.

---

## 4. Gate Readout (Against B1 Task Board)

W1 (Spike contract definition):

1. complete (pass 1 artifacts exist and are traceable).

W2 (Integration instrumentation):

1. complete (test-only normalized trace sink + events added and validated).

W3 (Scenario set):

1. complete for pass-1 minimum set (one pass + two deny variants).

W4 (Conformance comparison):

1. partial complete (manual comparative triage performed, generalized comparator pending).

W5 (Gap-analysis readout):

1. complete for pass 1 (this document).

---

## 5. Decision Recommendation

Primary recommendation:

1. Do not open a new unconstrained M1 micro-slice.
2. Use this B1 pass-1 evidence to evaluate M1 closure readiness.
3. If further bridge work is approved before closure, keep it bounded to:
   - comparator automation,
   - stable error-surface mapping,
   - no architecture-wide runtime redesign.

M1 extension rule check (`M1-Exit-Criteria.md`, section 4):

1. Distinct uncovered risk class exists: generalized comparator automation (yes, bounded).
2. Can risk be covered by bridge/spike work instead of new micro-slice? yes.
3. Therefore: prefer bridge continuation over new M1-S* slice.

---

## 6. Follow-Up Delta Candidates (Bounded)

1. Add machine comparator utility for normalized trace vs expected token set.
2. Add explicit mapping file: `SecurityFailureCode`/verifier reason -> bridge comparison token IDs.
3. Expand one additional deny class where feasible without broad redesign.

These can be executed as B1 follow-through tasks, not new M1 protocol slices.
