# M0 S3 Task Board (Encrypted Endpoint Grants)

## 1. Scope

S3 extends conformance coverage for encrypted endpoint disclosure and grant requirements:

1. endpoint resolve request profiling (`operation_class=endpoint`)
2. encrypted directory grant requirements and deterministic deny behavior
3. grant proof path handling (`GrantPresent`) before endpoint disclosure

Reference vectors:

1. TV-005
2. TV-006
3. TV-301
4. TV-302

---

## 2. Workstreams

### W1 Fixture/Profile Contract

1. `S3-W1-T1`: add isolated `Fixtures/S3` pack
2. `S3-W1-T2`: define endpoint resolve fields for S3 profile
3. `S3-W1-T3`: preserve S1/S2 fixture compatibility

Exit criteria:

1. S3 fixtures parse cleanly
2. no schema regressions in S1/S2 fixtures

### W2 Endpoint Gate Rules

1. `S3-W2-T1`: parse endpoint resolve preconditions from `ResolveRequest`
2. `S3-W2-T2`: enforce disclosure and grant checks for encrypted endpoint responses
3. `S3-W2-T3`: map deterministic deny codes (`DisclosureDenied`, `GrantMissing`, `GrantExpired`)

Exit criteria:

1. TV-005 success path is accepted
2. TV-006 deny path is accepted with deterministic outcome

### W3 Grant Proof Path

1. `S3-W3-T1`: support `GrantPresent` conformance message
2. `S3-W3-T2`: reject grant messages outside valid resolve context
3. `S3-W3-T3`: reject endpoint disclosure when active grant proof path is missing

Exit criteria:

1. TV-301 fails by exact error ID
2. TV-302 fails by exact error ID

### W4 Regression and Stability

1. `S3-W4-T1`: re-run S1 regression set
2. `S3-W4-T2`: re-run S2 direct-upgrade set
3. `S3-W4-T3`: run S3 set and repeat for reproducibility

Exit criteria:

1. S1 and S2 remain green
2. S3 vectors are deterministic and reproducible

---

## 3. Definition of Done (S3)

1. TV-005 and TV-006 pass with deterministic expected outcomes
2. TV-301 and TV-302 are expected-fail with exact machine-checkable IDs
3. S1 and S2 regressions stay unchanged
4. deferred wire decisions (`D3`, `D5`, `D7`, `D8`) remain untouched

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete (isolated `Fixtures/S3` pack and endpoint resolve field contract added)
2. W2: complete (encrypted endpoint grant/disclosure gates and deterministic denies implemented)
3. W3: complete (`GrantPresent` proof-path handling with exact fail IDs for invalid flows)
4. W4: complete (S1 14/14 effective pass, S2 8/8 effective pass, S3 4/4 effective pass, repeated S3 run stable)
