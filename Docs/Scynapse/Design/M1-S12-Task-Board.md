# M1-S12 Task Board (Reference Grant Issuer Binding Slice)

Date: 2026-03-09  
Branch: `codex/m1-s12-grant-issuer-binding`

## 1. Scope

Extend M1-S11 reference-grant challenge-session nonce-binding behavior with deterministic grant issuer-binding:

1. add profile `slice_profile: "M1-S12"`
2. enforce `HandshakeInit.requested_grant_issuer_ref` contract
3. enforce active reference-grant `HandshakeAccept.reference_grant_claim_issuer_ref` contract
4. enforce non-active grant issuer-claim field forbiddance
5. enforce deterministic runtime issuer mismatch deny mapping
6. preserve deterministic gate order: M1-S5 -> M1-S7 -> M1-S8 -> M1-S9 -> M1-S10 -> M1-S11 -> M1-S12 -> M1-S6

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S12-W1-T1`: add `M1-S12` profile recognition and routing in conformance engine.
2. `M1-S12-W1-T2`: preserve M1-S1/M1-S5/M1-S6/M1-S7/M1-S8/M1-S9/M1-S10/M1-S11 checks under M1-S12.
3. `M1-S12-W1-T3`: enforce deterministic M1-S12 gate ordering in `HandshakeAccept`.

Exit criteria:

1. M1-S12 executes with deterministic gate order and no regressions in prior slices.

### W2 Issuer-Binding Schema and Runtime

1. `M1-S12-W2-T1`: enforce typed `HandshakeInit.requested_grant_issuer_ref` required in M1-S12.
2. `M1-S12-W2-T2`: enforce typed active-grant `HandshakeAccept.reference_grant_claim_issuer_ref` required in M1-S12.
3. `M1-S12-W2-T3`: enforce issuer-claim forbiddance on non-active grant statuses.
4. `M1-S12-W2-T4`: map issuer mismatch to deterministic runtime deny ID.

Exit criteria:

1. issuer-binding failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S12-W3-T1`: add isolated fixture pack `Fixtures/M1-S12`.
2. `M1-S12-W3-T2`: cover schema and runtime failures for issuer-binding and precedence paths.
3. `M1-S12-W3-T3`: rerun S1..S5 + M1-S1..M1-S12.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S12-W4-T1`: sync protocol/matrix/checklist/vector/compatibility/wire docs.
2. `M1-S12-W4-T2`: update continuity checkpoints and session log.
3. `M1-S12-W4-T3`: add Claude collaboration bootstrap docs and first task packet.

Exit criteria:

1. M1-S12 closure state and collaboration handoff model are deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S12` issuer-binding checks implemented with stable deterministic error surface.
2. fixture pack `TV-1701..TV-1710` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.
5. Claude collaboration docs added with clear lead/review workflow.

---

## 4. Progress Snapshot (2026-03-09)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
