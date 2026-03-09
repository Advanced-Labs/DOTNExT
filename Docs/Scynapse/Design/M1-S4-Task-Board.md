# M1-S4 Task Board (Strict Failure Mapping Slice)

Date: 2026-03-08  
Branch: `codex/m1-s4-security-failure-mapping`

## 1. Scope

Extend the M1 security-adapter bridge with deterministic strict verification failure mapping:

1. add profile `slice_profile: "M1-S4"`
2. add strict failure-mode controls on `HandshakeProof`
3. map strict verification outcomes to stable M1-S4 IDs
4. preserve all prior slice behavior and regression stability

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S4-W1-T1`: add `M1-S4` profile recognition and routing in conformance engine.
2. `M1-S4-W1-T2`: extend security-adapter field validation for strict failure modes.
3. `M1-S4-W1-T3`: keep M1-S3 strict/mock behavior unchanged while extending depth.

Exit criteria:

1. M1-S4 profile executes without regressing M1-S3 behavior.

### W2 Strict Failure-Mode Mapping

1. `M1-S4-W2-T1`: extend adapter session with strict failure-mode injection.
2. `M1-S4-W2-T2`: cover deterministic failure reasons:
   - expired assertion
   - revoked assertion
   - unresolvable proof chain
   - not-yet-valid assertion
3. `M1-S4-W2-T3`: map each reason to stable IDs (`E3081`..`E3084`).

Exit criteria:

1. strict failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S4-W3-T1`: add isolated fixture pack `Fixtures/M1-S4`.
2. `M1-S4-W3-T2`: add pass vector + strict failure vectors + schema-negative vector.
3. `M1-S4-W3-T3`: rerun S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S4-W4-T1`: sync protocol/vector/matrix/checklist/compatibility docs.
2. `M1-S4-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S4 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S4` strict failure mapping implemented with stable error IDs.
2. fixture pack `TV-901..TV-906` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
