# M0 S5 Task Board (Policy Inheritance Hard-Lock)

## 1. Scope

S5 extends conformance coverage for parent-policy hard inheritance:

1. policy delta request handling (`PolicyDelta`)
2. deterministic deny on hard-lock weakening attempts
3. deterministic policy deny code validation (`PolicyDeny`)

Reference vectors:

1. TV-011
2. TV-501
3. TV-502

---

## 2. Workstreams

### W1 Fixture/Profile Contract

1. `S5-W1-T1`: add isolated `Fixtures/S5` pack
2. `S5-W1-T2`: define policy message field contract for S5 profile
3. `S5-W1-T3`: preserve S1..S4 fixture compatibility

Exit criteria:

1. S5 fixtures parse cleanly
2. no schema regressions in previous slices

### W2 Policy Lifecycle Engine

1. `S5-W2-T1`: implement `PolicyDelta` operation context handling
2. `S5-W2-T2`: map hard-lock weakening attempt to deterministic required deny
3. `S5-W2-T3`: enforce policy-operation isolation from other operation families

Exit criteria:

1. TV-011 passes with deterministic deny semantics

### W3 Deterministic Deny Validation

1. `S5-W3-T1`: implement `PolicyDeny` ordering checks
2. `S5-W3-T2`: enforce expected deny code consistency on hard-lock failures
3. `S5-W3-T3`: validate exact structured error IDs for fail vectors

Exit criteria:

1. TV-501 and TV-502 are expected-fail with exact error IDs

### W4 Regression and Stability

1. `S5-W4-T1`: re-run S1 regression set
2. `S5-W4-T2`: re-run S2 direct-upgrade set
3. `S5-W4-T3`: re-run S3 endpoint-grant set
4. `S5-W4-T4`: re-run S4 observation/replay set
5. `S5-W4-T5`: run S5 set and repeat for reproducibility

Exit criteria:

1. S1..S4 remain green
2. S5 vectors are deterministic and reproducible

---

## 3. Definition of Done (S5)

1. TV-011 passes with deterministic policy hard-lock deny behavior
2. TV-501 and TV-502 fail as expected via exact machine-checkable IDs
3. S1..S4 regressions remain unchanged
4. deferred wire decisions (`D3`, `D5`, `D7`, `D8`) remain untouched

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete (isolated `Fixtures/S5` pack and policy field contract added)
2. W2: complete (policy operation context + hard-lock deny requirement implemented)
3. W3: complete (policy deny ordering and code-match deterministic checks implemented)
4. W4: complete (S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3; repeated S5 run stable)
