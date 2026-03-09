# M1-S7 Task Board (Reference Lookup Grant Guard Slice)

Date: 2026-03-08  
Branch: `codex/m1-s7-reference-grant-guard`

## 1. Scope

Extend M1-S6 reference-token safety with deterministic capability-grant guard behavior:

1. add profile `slice_profile: "M1-S7"`
2. enforce reference grant status contract for `HandshakeAccept` reference transport
3. enforce typed grant reference when grant status is active
4. deny deterministically on missing/expired/revoked grant states before lookup-resolution checks

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S7-W1-T1`: add `M1-S7` profile recognition and routing in conformance engine.
2. `M1-S7-W1-T2`: preserve M1-S6 lookup/rebinding behavior unchanged while adding grant guard depth.
3. `M1-S7-W1-T3`: apply M1-S1 token-boundary checks for M1-S7 handshake accepts.

Exit criteria:

1. M1-S7 profile executes without regressing M1-S6 reference lookup guard behavior.

### W2 Grant Guard Enforcement

1. `M1-S7-W2-T1`: enforce `reference_grant_status` contract (`active|missing|expired|revoked|not_required`) for reference transport.
2. `M1-S7-W2-T2`: enforce typed `reference_grant_ref` when `reference_grant_status=active`.
3. `M1-S7-W2-T3`: map missing/expired/revoked grant outcomes to deterministic deny paths.

Exit criteria:

1. reference grant guard failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S7-W3-T1`: add isolated fixture pack `Fixtures/M1-S7`.
2. `M1-S7-W3-T2`: add pass/fail vectors for status/typed-ref/runtime-deny paths.
3. `M1-S7-W3-T3`: rerun S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S7-W4-T1`: sync protocol/vector/matrix/checklist/compatibility docs.
2. `M1-S7-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S7 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S7` reference lookup grant checks implemented with stable deterministic error surface.
2. fixture pack `TV-1201..TV-1207` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
