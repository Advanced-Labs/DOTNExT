# M1-S6 Task Board (Reference Token Resolution/Rebinding Guard Slice)

Date: 2026-03-08  
Branch: `codex/m1-s6-reference-token-guard`

## 1. Scope

Extend M1-S5 relation-token integrity with deterministic reference-token lookup guard behavior:

1. add profile `slice_profile: "M1-S6"`
2. enforce reference lookup status contract for `HandshakeAccept` reference transport
3. deny deterministically on unresolved, rebinding, and CID mismatch reference outcomes
4. preserve all prior slice behavior and regression stability

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S6-W1-T1`: add `M1-S6` profile recognition and routing in conformance engine.
2. `M1-S6-W1-T2`: keep M1-S5 inline integrity behavior unchanged while extending reference-path depth.
3. `M1-S6-W1-T3`: apply M1-S1 token-boundary checks for M1-S6 handshake accepts.

Exit criteria:

1. M1-S6 profile executes without regressing M1-S5 token-boundary and inline integrity behavior.

### W2 Reference Token Guard Enforcement

1. `M1-S6-W2-T1`: enforce `reference_lookup_status` contract (`resolved|missing|rebinding_detected`) for reference transport.
2. `M1-S6-W2-T2`: enforce resolved lookup CID presence/format and equality with `relation_token_cid`.
3. `M1-S6-W2-T3`: map unresolved/rebinding/mismatch outcomes to deterministic deny paths.

Exit criteria:

1. reference-token guard failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S6-W3-T1`: add isolated fixture pack `Fixtures/M1-S6`.
2. `M1-S6-W3-T2`: add pass/fail vectors for resolved/missing/rebinding/mismatch/schema paths.
3. `M1-S6-W3-T3`: rerun S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S6-W4-T1`: sync protocol/vector/matrix/checklist/compatibility docs.
2. `M1-S6-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S6 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S6` reference-token resolution/rebinding checks implemented with stable deterministic error surface.
2. fixture pack `TV-1101..TV-1105` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
