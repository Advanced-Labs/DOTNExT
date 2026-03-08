# Scynapse Executive Memory (Re-Entry File)

Last updated: 2026-03-08

## 1. Mission Snapshot

Current mission: finish M0 foundation without drift by carrying stable S1/S2/S3/S4 conformance baselines into bounded S5 planning.

Pre-compaction checkpoint status (2026-03-08):

1. active branch: `codex/s4-observe-replay`
2. continuity files synchronized
3. no in-scope uncommitted changes at checkpoint handoff

Current active path:

1. M0-A complete draft
2. M0-B structured draft with wire-lock decisions
3. S1 prototype closed with deterministic message-driven conformance baseline
4. S2 direct-upgrade slice implemented (profile-gated, fallback-preserving)
5. S3 endpoint-grant slice implemented (encrypted endpoint disclosure gate semantics)
6. S4 observation/replay slice implemented (observe lifecycle and replay-expiry semantics)

---

## 2. Done / Doing / Next

### Done

1. M0-A contracts baseline (`M0-A-Fabric-Contracts.md`)
2. M0-B protocol skeleton and supporting matrices
3. Orleans reuse/adaptation matrix and compatibility profile
4. S1 task board + fixture pack + conformance checklist
5. branch baseline commit pushed: `9f6a66743a` on `codex/m0-design-foundation`
6. root `AGENTS.md` created with mandatory orientation protocol
7. methodology continuity layer established (`METHODOLOGY.md`, this file, `SESSION-LOG.md`)
8. continuity layer commit pushed: `6b9b472793` on `codex/m0-design-foundation`
9. pre-compaction refresh protocol added and pushed: `3ee989e892` on `codex/m0-design-foundation`
10. implementation branch created and pushed: `codex/s1-prototype`
11. S1 prototype harness scaffolded under `src/Scynapse/playground/FabricS1Prototype`
12. first S1 harness run executed against `TV-001/002/003/012/013` with 5/5 pass
13. S1 wire-lock decisions `D1`, `D2`, `D4`, `D6` locked and propagated to protocol/field/decision docs
14. S1 harness upgraded to derive observed state traces from message flow and compare against expected traces
15. S1 fixture pack extended with expected-fail negative vectors (`TV-101`, `TV-102`, `TV-103`)
16. S1 harness supports `expect_conformance` mode (`pass`/`fail`) with 8/8 effective pass
17. wire examples synchronized to locked S1 profile (`M0-B-Wire-Examples.md`)
18. expected-fail vectors now support `expected_error_contains` for reason-level rejection checks
19. S1 harness replaced heuristic trace derivation with explicit message-driven operation context execution
20. structured machine-checkable error IDs added to harness output (`[Layer:ErrorId]`)
21. fixture contract extended with `expected_error_ids` (preferred expected-fail oracle)
22. transition-edge negative vectors added (`TV-104`..`TV-109`)
23. repeated harness execution confirms deterministic/reproducible 14/14 effective pass
24. Orleans compatibility profile updated with S1 hardening behavior `A/N/D` classification notes
25. S1 closure artifact added (`M0-S1-Closure.md`) with lock/defer register and handoff boundaries
26. S1 closure commit pushed on `codex/s1-prototype` (`04e297587d`)
27. S2 implementation branch created: `codex/s2-direct-upgrade`
28. fixture profile contract extended with `slice_profile` (`S1` default, explicit `S2`)
29. `RouteUpgradeProbe` S2 gate fields added (`policy_allowed`, `disclosure_allowed`, `grant_status`, `trust_sufficient`)
30. profile-aware conformance execution implemented (`S1` mediated-only posture, `S2` gate-evaluated direct-upgrade)
31. deterministic S2 error IDs added for reject-code mismatch and invalid-accept paths
32. isolated S2 fixture pack added (`Docs/Scynapse/Design/Fixtures/S2`): TV-004, TV-014, TV-201..TV-206
33. regression results stable: S1 14/14 effective pass, S2 8/8 effective pass, repeated runs reproducible
34. S2 implementation commit pushed on `codex/s2-direct-upgrade` (`7bfa560428`)
35. S3 implementation branch created: `codex/s3-endpoint-grants`
36. S3 profile support added for encrypted endpoint disclosure grant validation (`slice_profile: S3`)
37. S3 endpoint fixtures added (`Docs/Scynapse/Design/Fixtures/S3`): TV-005, TV-006, TV-301, TV-302
38. deterministic S3 error IDs added for grant-path ordering and proof-path failures
39. regression results stable: S1 14/14 effective pass, S2 8/8 effective pass, S3 4/4 effective pass with repeated S3 run reproducibility
40. S4 implementation branch created: `codex/s4-observe-replay`
41. S4 profile support added for observation/replay conformance (`slice_profile: S4`)
42. S4 fixture pack added (`Docs/Scynapse/Design/Fixtures/S4`): TV-007..TV-010
43. observe/replay lifecycle transitions implemented in harness (`ObserveOpen/Ack/Event/Gap/Resume/Close`)
44. S4 regression results stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4 with repeated S4 run reproducibility

### Doing

1. final S4 continuity synchronization and branch push preparation

### Next

1. preserve S1/S2/S3/S4 fixture and error-ID stability as new slices are added
2. open bounded S5 planning for policy inheritance hard-lock vector (`TV-011`)
3. continue holding deferred wire decisions (`D3`, `D5`, `D7`, `D8`) outside current scope

---

## 3. Non-Negotiable Invariants (Do Not Regress)

1. Node unification: no silo-less client role.
2. Per-Varia isolation by `Cell`; distributed runtime by `Hive`.
3. Mediated-first lifecycle; direct path only as conditional upgrade.
4. Parent policy hard inheritance by default.
5. Disclosure and routing are policy-governed and capability-compatible.
6. Ambiguity defaults to fail-closed (`AmbiguousResolution`).
7. CNS is dynamic and observable.

---

## 4. Open Decision Register (Current)

S1 locked:

1. `D1` enum encoding strategy
2. `D2` timestamp wire representation
3. `D4` proof reference encoding
4. `D6` body key dictionary freeze policy

S2+ deferrable:

1. `D3` typed identifier strictness
2. `D5` normalization versioning details
3. `D7` deny envelope required-field policy
4. `D8` relation token serialization boundary optimization

Authority file:

1. `M0-B-Wire-Lock-Open-Decisions.md`

---

## 5. Immediate Reading Order for Any New Agent

1. `METHODOLOGY.md`
2. `M0-Status-Checkpoint.md`
3. `M0-B-Protocol-Skeleton.md`
4. `M0-S1-Task-Board.md`
5. `M0-S2-Task-Board.md`
6. `M0-S3-Task-Board.md`
7. `M0-S4-Task-Board.md`
8. latest entry in `SESSION-LOG.md`

---

## 6. Active Artifact Map

1. Contracts: `M0-A-Fabric-Contracts.md`
2. Protocol scaffold: `M0-B-Protocol-Skeleton.md`
3. Compatibility and migration:
   - `M0-B-Orleans-Compatibility-Profile.md`
   - `M0-Orleans-Reuse-Matrix.md`
4. Validation and execution:
   - `M0-B-Protocol-Test-Vectors.md`
   - `M0-B-Conformance-Harness-Checklist.md`
   - `M0-Implementation-Slice-Plan.md`
   - `M0-S1-Task-Board.md`
   - `M0-S1-Closure.md`
   - `M0-S2-Task-Board.md`
   - `Fixtures/S2/README.md`
   - `M0-S3-Task-Board.md`
   - `Fixtures/S3/README.md`
   - `M0-S4-Task-Board.md`
   - `Fixtures/S4/README.md`
5. Wire lock:
   - `M0-B-Wire-Lock-Open-Decisions.md`
   - `M0-B-Wire-Examples.md`

---

## 7. Session Exit Checklist (Mandatory)

1. update this file if `Doing`/`Next`/open decisions changed
2. update milestone checkpoint if plan-level status changed
3. append concise session entry in `SESSION-LOG.md`
4. link new design artifacts from the relevant skeleton/index docs
