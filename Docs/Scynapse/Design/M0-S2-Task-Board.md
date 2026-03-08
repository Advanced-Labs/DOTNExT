# M0 S2 Task Board (Direct Upgrade Path)

## 1. Scope

S2 extends the executable slice with conditional direct upgrade while preserving relayed fallback continuity:

1. profile-aware direct-upgrade path (`S1` forbidden posture, `S2` gate-evaluated)
2. deterministic gate-order handling for upgrade denials
3. fallback continuity preservation on reject path

Reference vectors:

1. TV-004
2. TV-014
3. TV-201
4. TV-202
5. TV-203
6. TV-204
7. TV-205
8. TV-206

---

## 2. Workstreams

### W1 Fixture/Profile Contract

1. `S2-W1-T1`: add fixture-level `slice_profile` (`S1` default, explicit `S2`)
2. `S2-W1-T2`: define `RouteUpgradeProbe` S2 gate fields
3. `S2-W1-T3`: add isolated `Fixtures/S2` pack

Exit criteria:

1. S2 fixtures parse with no schema drift from S1 baseline
2. profile defaults preserve S1 behavior

### W2 Gate Evaluation Engine

1. `S2-W2-T1`: implement profile-aware probe handling (`S1`/`S2`)
2. `S2-W2-T2`: implement gate order:
   - `PolicyDenied -> DisclosureDenied -> GrantMissing/GrantExpired -> TrustInsufficient -> UpgradeRejected`
3. `S2-W2-T3`: carry deterministic expected reject code into response validation

Exit criteria:

1. gate-deny vectors emit deterministic decision paths
2. no gate-order nondeterminism across repeated runs

### W3 Accept/Reject Semantics

1. `S2-W3-T1`: allow `RouteUpgradeAccept` only when all S2 gates pass
2. `S2-W3-T2`: reject invalid accepts with structured deterministic error ID
3. `S2-W3-T3`: validate reject code consistency against gate outcome

Exit criteria:

1. invalid accept vectors fail with exact expected error ID
2. reject-code mismatches fail deterministically

### W4 Fallback Continuity

1. `S2-W4-T1`: ensure reject path restores `RelayedSession`
2. `S2-W4-T2`: ensure operation reaches `Completed` over fallback route
3. `S2-W4-T3`: keep relayed fallback untouched when direct path is denied

Exit criteria:

1. TV-014 and gate-deny vectors show `RelayedSession` continuity and completion

### W5 Deterministic Error Surface

1. `S2-W5-T1`: add S2-specific structured error IDs for invalid accept and reject mismatch paths
2. `S2-W5-T2`: keep `expected_error_ids` as primary fail oracle
3. `S2-W5-T3`: retain `expected_error_contains` fallback compatibility

Exit criteria:

1. fail vectors rely on exact IDs, not substring-only checks

### W6 Regression and Gate Validation

1. `S2-W6-T1`: re-run full S1 set unchanged
2. `S2-W6-T2`: run S2 vectors including gate-deny and invalid-accept cases
3. `S2-W6-T3`: confirm reproducibility across repeated runs

Exit criteria:

1. S1 remains green
2. S2 vectors satisfy deterministic outcomes and fallback guarantees

---

## 3. Dependency Order

1. W1 -> W2 -> W3
2. W2 + W3 -> W4
3. W1 + W2 + W3 + W4 -> W5 + W6

---

## 4. Definition of Done (S2)

1. S1 regression set remains green with unchanged expected behavior
2. TV-004 and TV-014 pass in S2 fixture pack
3. S2 gate vectors produce deterministic, reproducible outcomes
4. invalid accept path fails via exact machine-checkable error ID
5. deferred wire decisions (`D3`, `D5`, `D7`, `D8`) remain untouched

---

## 5. Progress Snapshot (2026-03-08)

1. W1: complete (fixture-level profile contract and isolated `Fixtures/S2` pack added)
2. W2: complete (profile-aware gate-order evaluation implemented)
3. W3: complete (accept/reject semantics hardened with deterministic error IDs)
4. W4: complete (reject path restores `RelayedSession` and completes over fallback)
5. W5: complete (`expected_error_ids` remains primary fail oracle; compatibility fallback retained)
6. W6: complete (S1 regression 14/14 effective pass; S2 pack 8/8 effective pass; repeated runs stable)
