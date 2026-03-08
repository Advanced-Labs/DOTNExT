# M0 S4 Task Board (Observation + Replay)

## 1. Scope

S4 extends conformance coverage for observation semantics and replay recovery:

1. observe lifecycle (`ObserveOpen`, `ObserveAck`, `ObserveEvent`)
2. gap and resume handling (`ObserveGap`, `ObserveResume`)
3. deterministic replay-expiry behavior (`ReplayWindowExpired`)

Reference vectors:

1. TV-007
2. TV-008
3. TV-009
4. TV-010

---

## 2. Workstreams

### W1 Fixture/Profile Contract

1. `S4-W1-T1`: add isolated `Fixtures/S4` pack
2. `S4-W1-T2`: define observe message field contract for S4 profile
3. `S4-W1-T3`: preserve S1/S2/S3 fixture compatibility

Exit criteria:

1. S4 fixtures parse cleanly
2. no schema regressions in previous slices

### W2 Observe Lifecycle Engine

1. `S4-W2-T1`: implement `ObserveOpen -> ObservePendingAck -> ObserveActive`
2. `S4-W2-T2`: enforce event gating (`ObserveEvent` valid only in `ObserveActive`)
3. `S4-W2-T3`: enforce terminal/invalid ordering behavior deterministically

Exit criteria:

1. TV-007 and TV-008 pass
2. invalid ordering is deterministic

### W3 Replay Lifecycle Engine

1. `S4-W3-T1`: implement `ObserveGap` transition from active stream
2. `S4-W3-T2`: implement `ObserveResume` success path (`ObserveResuming -> ObserveActive`)
3. `S4-W3-T3`: implement replay-expiry mapping to `ReplayWindowExpired`

Exit criteria:

1. TV-009 passes
2. TV-010 passes with deterministic deny semantics

### W4 Regression and Stability

1. `S4-W4-T1`: re-run S1 regression set
2. `S4-W4-T2`: re-run S2 direct-upgrade set
3. `S4-W4-T3`: re-run S3 endpoint-grant set
4. `S4-W4-T4`: run S4 set and repeat for reproducibility

Exit criteria:

1. S1/S2/S3 remain green
2. S4 vectors are deterministic and reproducible

---

## 3. Definition of Done (S4)

1. TV-007..TV-010 pass with deterministic outcomes
2. replay-expiry behavior maps to `ReplayWindowExpired` with retryability semantics
3. S1/S2/S3 regressions remain unchanged
4. deferred wire decisions (`D3`, `D5`, `D7`, `D8`) remain untouched

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete (isolated `Fixtures/S4` pack and observe field contract added)
2. W2: complete (observe lifecycle transitions and gating implemented)
3. W3: complete (replay resume success/expiry behavior implemented)
4. W4: complete (S1 14/14, S2 8/8, S3 4/4, S4 4/4; repeated S4 run stable)
