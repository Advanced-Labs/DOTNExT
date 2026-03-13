# F5-F6-B1 Decision Delta Set (For Louis)

Date: 2026-03-09  
Prepared by: Codex  
Inputs: `F5-Current-State-Audit.md`, `F6-Current-State-Audit.md`, `B1-Diagnostic-Flow-Mapping.md`

---

## 1. Plain-Language Summary

What we did:

1. Consolidated security baseline (`F5`), runtime/lifecycle baseline (`F6`), and first implementation mapping (`B1`).
2. Converted findings into concrete decision points and execution deltas.
3. Aligned next-step recommendation to M1 closure governance.

Why it matters:

1. We now have enough implementation evidence to avoid blind design drift.
2. The next work can be constrained to a real bridge spike rather than additional speculative micro-slices.

What happens next:

1. Execute B1 vertical spike with trace instrumentation and explicit mismatch triage.
2. Use spike evidence to decide M1 closure (or a single bounded extension, if justified).

---

## 2. Consolidated Baseline

## 2.1 F5 (Security)

Locked commitments (high confidence, production-proven):

1. Signed assertions, chain verification, attenuation, replay primitives.
2. Orleans call-filter security enforcement pattern.
3. Subject-style resource matching and CCap wallet pattern.

Known bridge pressure points:

1. Deterministic E-series vs runtime failure-surface mismatch.
2. Handshake protocol semantics are richer in harness than current runtime.

## 2.2 F6 (Runtime/Lifecycle)

Locked/runtime-carry primitives:

1. activation/deactivation lifecycle model,
2. placement/rebalancing framework,
3. serialization pipeline,
4. lifecycle observation patterns.

Open architecture gaps (not yet designed/implemented end-to-end):

1. Component abstraction and boundaries (`Varia/Varion/Cell/Hive`),
2. node unification,
3. mediation layer,
4. per-component isolation,
5. decentralized CNS discovery/routing integration.

## 2.3 B1 Diagnostic Mapping

Observed now:

1. direct analogs exist for per-call authz/authn checks,
2. error taxonomy and trace granularity are only partial analogs,
3. handshake/route lifecycle is a missing analog in production path.

---

## 3. Decision Matrix

## D-01: M1 Closure Posture

Decision:

1. Keep M1 finite; do not open unbounded M1-S13+ chain by default.

Rationale:

1. Current risk is comparability/bridge evidence, not another unconstrained conformance dimension.

Required action:

1. Use B1 spike evidence as the closure discriminator per `M1-Exit-Criteria.md`.

## D-02: Immediate Execution Priority

Decision:

1. Execute `M1-Vertical-Spike-B1-Task-Board.md` now.

Rationale:

1. F5/F6/B1 evidence is sufficient to start implementation-constrained validation.

## D-03: Error Surface Strategy

Decision required:

1. Choose one of:
   - A) runtime adopts deterministic E-series IDs,
   - B) maintain runtime-native codes and define stable bridge mapping table.

Current recommendation:

1. start with B (mapping layer) during spike, revisit A with spike evidence.

## D-04: Trace Strategy

Decision:

1. Add test-only normalized trace emission in integration path.

Rationale:

1. Needed for deterministic comparability without production behavior churn.

## D-05: Deny-Class Coverage Expansion

Decision:

1. Add integration scenarios that directly exercise:
   - `InsufficientCapability` (not wallet-collapsed to missing capability),
   - chain verification failure class.

Rationale:

1. Current tests validate denial outcomes but collapse several semantic causes.

## D-06: Orleans Relationship Guardrail

Decision:

1. Continue assuming Orleans internals are modifiable starting material.

Rationale:

1. F6 confirms Component Model goals require deep runtime changes, not API-only adaptation.

## D-07: Legacy Feature Reuse Policy

Decision:

1. Treat GTD as discard-tier; treat dynamic grain/StateTask/plugin mechanisms as reference inputs, not architecture commitments.

Rationale:

1. They are useful problem evidence but target architecture has changed.

---

## 4. Delta Backlog (Actionable)

Priority 0:

1. Deliver test-only trace schema and event emission hooks for selected B1 flow.
2. Create runtime-failure to comparison-token mapping table.
3. Add one pass + two fail scenarios with explicit expected terminal tokens.

Priority 1:

1. Expand deny-class coverage to avoid wallet-collapsed ambiguity.
2. Add strict identity equality assertion in `WhoAmI` path.
3. Publish B1 closure readout with mismatch triage categories.

Priority 2:

1. Decide long-term error-surface convergence strategy (E-series in runtime vs mapping permanence).
2. Formalize handshake-layer boundary contract between conformance protocol and current runtime.

---

## 5. Recommended Next 3 Steps

1. Approve and start `codex/m1-b1-vertical-spike` execution scope (bounded).
2. Run B1 W1-W5 with trace + comparability + readout deliverables.
3. Re-evaluate M1 closure with B1 evidence against gates G1-G7.

---

## 6. Risk Notes

1. Biggest risk is scope drift into broad runtime redesign during spike.
2. Second risk is ambiguous trace format that blocks deterministic comparison.
3. Third risk is spending more time on new slices than on bridge evidence.

Mitigation:

1. Keep B1 to one representative flow + bounded deny variants.
2. Keep instrumentation test-only.
3. Use explicit mismatch triage classes (`harness`, `implementation`, `contract ambiguity`).

---

## 7. Closure Recommendation

Current recommendation:

1. Do not open a new M1 micro-slice yet.
2. Execute B1 spike first.
3. Revisit M1 closure immediately after B1 readout.
