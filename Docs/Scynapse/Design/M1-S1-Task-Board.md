# M1-S1 Task Board (Deferred Wire Closure)

Date: 2026-03-08  
Branch target: `codex/m1-s1-wire-closure` (next implementation branch)

## 1. Scope

Close deferred wire decisions and bind them to deterministic conformance coverage:

1. `D3` identifier typed-string strictness
2. `D5` normalization versioning (`expr_norm_v`)
3. `D7` deny envelope required-field policy
4. `D8` relation token serialization boundary

---

## 2. Workstreams

### W1 Decision Finalization

1. `M1-S1-W1-T1`: decide final policy for `D3` and document acceptable identifier forms.
2. `M1-S1-W1-T2`: decide final policy for `D5` (`expr_norm_v` required/optional + compatibility behavior).
3. `M1-S1-W1-T3`: decide final policy for `D7` (always required vs conditional fields for deny envelope).
4. `M1-S1-W1-T4`: decide final policy for `D8` (relation token embed/reference boundary).

Exit criteria:

1. `M0-B-Wire-Lock-Open-Decisions.md` shows `LOCKED` for `D3`, `D5`, `D7`, `D8`.

### W2 Contract Propagation

1. `M1-S1-W2-T1`: propagate decisions to `M0-B-Protocol-Skeleton.md`.
2. `M1-S1-W2-T2`: update `M0-B-Message-Field-Matrix.md` field constraints and key usage.
3. `M1-S1-W2-T3`: update `M0-B-Wire-Examples.md` for canonical wire/debug rendering.
4. `M1-S1-W2-T4`: refresh compatibility tags in `M0-B-Orleans-Compatibility-Profile.md`.

Exit criteria:

1. no cross-doc inconsistencies for D3/D5/D7/D8 outcomes.

### W3 Fixture and Conformance Coverage

1. `M1-S1-W3-T1`: add new vectors for identifier strictness pass/fail cases.
2. `M1-S1-W3-T2`: add `expr_norm_v` compatibility vectors.
3. `M1-S1-W3-T3`: add deny envelope field-policy vectors.
4. `M1-S1-W3-T4`: add relation token boundary vectors.
5. `M1-S1-W3-T5`: ensure fail vectors validate exact `expected_error_ids`.

Exit criteria:

1. all new fail vectors are machine-checkable by exact IDs.

### W4 Regression and Stability

1. `M1-S1-W4-T1`: rerun S1 pack.
2. `M1-S1-W4-T2`: rerun S2 pack.
3. `M1-S1-W4-T3`: rerun S3 pack.
4. `M1-S1-W4-T4`: rerun S4 pack.
5. `M1-S1-W4-T5`: rerun S5 pack.
6. `M1-S1-W4-T6`: run new M1-S1 vectors and repeat for reproducibility.

Exit criteria:

1. S1..S5 remain green.
2. M1-S1 vectors pass deterministically across repeated runs.

---

## 3. Definition of Done

1. decisions `D3`, `D5`, `D7`, `D8` are locked and synchronized across contract docs.
2. new vectors and harness behavior are deterministic and reproducible.
3. no S1..S5 regression.
4. continuity docs updated at session close.
