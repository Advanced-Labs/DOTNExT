# M1-S2 Task Board (Runtime Bridge Slice)

Date: 2026-03-08  
Branch: `codex/m1-s1-wire-closure`

## 1. Scope

Implement bounded runtime-bridge semantics on top of locked M1-S1 wire baseline:

1. runtime bridge profile (`slice_profile: "M1-S2"`)
2. route data transfer validation (`RouteData`)
3. mediated/direct transport-path conformance checks
4. deterministic runtime transit assertions in fixtures

---

## 2. Workstreams

### W1 Runtime Bridge Engine Extension

1. `M1-S2-W1-T1`: add `M1-S2` profile support in conformance engine.
2. `M1-S2-W1-T2`: keep S2 direct-upgrade gates active under M1-S2.
3. `M1-S2-W1-T3`: add runtime bridge transit trace capture.

Exit criteria:

1. bridge traces are produced deterministically for `RouteData`.

### W2 RouteData Conformance Rules

1. `M1-S2-W2-T1`: add `RouteData` message type and field checks.
2. `M1-S2-W2-T2`: enforce transport-path validity by active session mode.
3. `M1-S2-W2-T3`: emit deterministic deny IDs for invalid data-path attempts.

Exit criteria:

1. invalid transport paths fail with exact machine-checkable IDs.

### W3 Fixture and Assertion Coverage

1. `M1-S2-W3-T1`: add fixture pack `Fixtures/M1-S2`.
2. `M1-S2-W3-T2`: add pass vectors for mediated/direct/fallback continuity.
3. `M1-S2-W3-T3`: add fail vectors for direct-while-mediated, mediated-after-direct, and data-before-session.
4. `M1-S2-W3-T4`: add runtime assertions (`bridge_transit_contains`, `bridge_transit_count_equals`).

Exit criteria:

1. M1-S2 vectors are deterministic and reproducible.

### W4 Documentation and Regression

1. `M1-S2-W4-T1`: sync protocol/matrix/checklist/vector docs for runtime bridge semantics.
2. `M1-S2-W4-T2`: rerun S1/S2/S3/S4/S5 + M1-S1 + M1-S2 packs.

Exit criteria:

1. baseline packs remain green while M1-S2 pack is green.

---

## 3. Definition of Done

1. `M1-S2` runtime bridge behavior is implemented with deterministic IDs.
2. fixture pack `TV-701..TV-706` passes effective conformance.
3. S1/S2/S3/S4/S5 + M1-S1 regressions remain green.
4. continuity docs updated for M1-S2 closure handoff.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
