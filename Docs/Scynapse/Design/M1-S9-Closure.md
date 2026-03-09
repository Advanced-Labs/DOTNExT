# M1-S9 Closure (Reference Grant Proof Freshness + Replay Slice)

Date: 2026-03-08  
Implementation branch: `codex/m1-s9-grant-proof-freshness-replay`

## 1. Scope Closed

M1-S9 delivered deterministic reference grant proof freshness/replay checks on top of M1-S8 behavior:

1. profile `M1-S9` extending strict verification, runtime bridge handling, M1-S5 token integrity, M1-S7 grant status, M1-S8 proof binding, and M1-S6 lookup/rebinding guards
2. deterministic active-grant freshness/replay contract on `HandshakeAccept` reference transport
3. deterministic stale/replay failure mapping to stable deny codes
4. deterministic schema restrictions for freshness/replay fields when grant status is non-active

---

## 2. Deterministic Validation Results

Closure pass harness results:

1. S1: 14/14 effective pass
2. S2: 8/8 effective pass
3. S3: 4/4 effective pass
4. S4: 4/4 effective pass
5. S5: 3/3 effective pass
6. M1-S1: 10/10 effective pass
7. M1-S2: 6/6 effective pass
8. M1-S3: 5/5 effective pass
9. M1-S4: 6/6 effective pass
10. M1-S5: 4/4 effective pass
11. M1-S6: 5/5 effective pass
12. M1-S7: 7/7 effective pass
13. M1-S8: 13/13 effective pass
14. M1-S9: 10/10 effective pass

Total closure baseline: 99/99 effective pass.

---

## 3. New Deterministic IDs

Schema IDs:

1. `E3140_M1S9_REFERENCE_GRANT_PROOF_FRESHNESS_STATUS_REQUIRED`
2. `E3141_M1S9_REFERENCE_GRANT_PROOF_FRESHNESS_STATUS_INVALID`
3. `E3142_M1S9_REFERENCE_GRANT_PROOF_REPLAY_STATUS_REQUIRED`
4. `E3143_M1S9_REFERENCE_GRANT_PROOF_REPLAY_STATUS_INVALID`
5. `E3144_M1S9_REFERENCE_GRANT_PROOF_FRESHNESS_FIELDS_FORBIDDEN`

Runtime IDs:

1. `E3150_M1S9_REFERENCE_GRANT_PROOF_FRESHNESS_STALE`
2. `E3151_M1S9_REFERENCE_GRANT_PROOF_REPLAY_DETECTED`

---

## 4. Artifacts Updated

1. `src/Scynapse/playground/FabricS1Prototype/ConformanceEngine.cs`
2. `src/Scynapse/playground/FabricS1Prototype/README.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S9/*`
4. `Docs/Scynapse/Design/M1-S9-Task-Board.md`
5. protocol/matrix/checklist/vector/compatibility docs synchronized for M1-S9 semantics
6. continuity/checkpoint files synchronized for next-slice handoff

---

## 5. Next Step

1. define and execute the next bounded M1 slice from M1-S9 closure baseline
2. preserve S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9 deterministic behavior and error-ID stability
