# M1-S3 Task Board (Security-Adapter Bridge Slice)

Date: 2026-03-08  
Branch: `codex/m1-s3-security-adapter`

## 1. Scope

Implement bounded security-adapter bridge behavior on top of M1-S2 baseline:

1. `slice_profile: "M1-S3"` handshake proof security validation
2. fixture-selectable verification modes (`mock`, `strict`)
3. strict-mode reuse of existing `Scynapse.Security` primitives
4. deterministic deny mapping for signature/replay failures

---

## 2. Workstreams

### W1 Harness Profile and Field Validation

1. `M1-S3-W1-T1`: add `M1-S3` profile support in conformance engine.
2. `M1-S3-W1-T2`: add `HandshakeProof` field checks for `verification_mode`.
3. `M1-S3-W1-T3`: validate strict/mock mode-specific fields.

Exit criteria:

1. invalid/ambiguous M1-S3 proof payloads fail deterministically.

### W2 Security Adapter Integration

1. `M1-S3-W2-T1`: add bounded adapter session in harness using `Scynapse.Security`.
2. `M1-S3-W2-T2`: implement strict proof verification path via `AssertionVerifier`.
3. `M1-S3-W2-T3`: enforce deterministic replay/signature deny IDs.
4. `M1-S3-W2-T4`: keep handshake state transition semantics unchanged.

Exit criteria:

1. strict mode executes real verification primitives and maps failures deterministically.

### W3 Fixture Coverage

1. `M1-S3-W3-T1`: add fixture pack `Fixtures/M1-S3`.
2. `M1-S3-W3-T2`: add strict pass/fail vectors.
3. `M1-S3-W3-T3`: add mock pass/fail vectors.

Exit criteria:

1. M1-S3 vectors are reproducible and exact error-ID checks pass.

### W4 Documentation and Regression

1. `M1-S3-W4-T1`: sync matrix/skeleton/checklist/vector/compatibility docs.
2. `M1-S3-W4-T2`: rerun S1/S2/S3/S4/S5 + M1-S1 + M1-S2 + M1-S3 packs.
3. `M1-S3-W4-T3`: update continuity and closure artifacts.

Exit criteria:

1. all baseline packs remain green while M1-S3 is green.

---

## 3. Definition of Done

1. `M1-S3` security-adapter behavior is implemented with deterministic IDs.
2. fixture pack `TV-801..TV-805` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs are synchronized for handoff.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
