# M1-S10 Task Board (Reference Grant Claim Binding Slice)

Date: 2026-03-08  
Branch: `codex/m1-s10-reference-grant-claim-binding`

## 1. Scope

Extend M1-S9 reference-grant proof checks with deterministic claim-binding behavior:

1. add profile `slice_profile: "M1-S10"`
2. enforce claim-binding source contract on `HandshakeInit`
3. enforce active-grant claim-binding contract on `HandshakeAccept`
4. enforce deterministic runtime deny mapping for subject/scope/action mismatch
5. preserve M1-S5 -> M1-S7 -> M1-S8 -> M1-S9 -> M1-S10 -> M1-S6 gate order

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S10-W1-T1`: add `M1-S10` profile recognition and routing in conformance engine.
2. `M1-S10-W1-T2`: preserve M1-S1/M1-S5/M1-S6/M1-S7/M1-S8/M1-S9 checks under M1-S10.
3. `M1-S10-W1-T3`: enforce deterministic gate order in `HandshakeAccept`.

Exit criteria:

1. M1-S10 executes with deterministic gate order and no regressions in prior slices.

### W2 Claim-Binding Schema and Runtime

1. `M1-S10-W2-T1`: enforce `HandshakeInit` source fields (`requester_subject_ref`, `requested_scope`, `requested_ops`).
2. `M1-S10-W2-T2`: enforce active-grant claim fields (`reference_grant_claim_subject_ref`, `reference_grant_claim_scope`, `reference_grant_claim_action`).
3. `M1-S10-W2-T3`: forbid claim fields for non-active grant status.
4. `M1-S10-W2-T4`: map subject/scope/action mismatches to deterministic deny IDs/codes.

Exit criteria:

1. claim-binding failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S10-W3-T1`: add isolated fixture pack `Fixtures/M1-S10`.
2. `M1-S10-W3-T2`: cover schema and runtime failures for claim-binding paths.
3. `M1-S10-W3-T3`: rerun S1..S5 + M1-S1..M1-S10.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S10-W4-T1`: sync protocol/matrix/checklist/vector/compatibility/wire docs.
2. `M1-S10-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S10 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S10` claim-binding checks implemented with stable deterministic error surface.
2. fixture pack `TV-1501..TV-1514` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
