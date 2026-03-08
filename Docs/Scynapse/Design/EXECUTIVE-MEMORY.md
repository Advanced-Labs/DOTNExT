# Scynapse Executive Memory (Re-Entry File)

Last updated: 2026-03-08

## 1. Mission Snapshot

Current mission: finish M0 foundation without drift by carrying a closed S1 baseline into bounded S2 direct-upgrade validation.

Pre-compaction checkpoint status (2026-03-08):

1. active branch: `codex/s1-prototype`
2. continuity files synchronized
3. no in-scope uncommitted changes at checkpoint handoff

Current active path:

1. M0-A complete draft
2. M0-B structured draft with wire-lock decisions
3. S1 prototype closed with deterministic message-driven conformance baseline
4. S2 direct-upgrade slice kickoff (profile-gated, fallback-preserving)

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

### Doing

1. freeze S1 baseline in a focused commit and cut implementation branch `codex/s2-direct-upgrade`

### Next

1. add S2 fixture profile support (`S1` default, explicit `S2`)
2. implement S2 direct-upgrade gate ordering and deterministic deny IDs
3. validate fallback continuity (`RelayedSession`) on all S2 reject paths

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
5. latest entry in `SESSION-LOG.md`

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
5. Wire lock:
   - `M0-B-Wire-Lock-Open-Decisions.md`
   - `M0-B-Wire-Examples.md`

---

## 7. Session Exit Checklist (Mandatory)

1. update this file if `Doing`/`Next`/open decisions changed
2. update milestone checkpoint if plan-level status changed
3. append concise session entry in `SESSION-LOG.md`
4. link new design artifacts from the relevant skeleton/index docs
