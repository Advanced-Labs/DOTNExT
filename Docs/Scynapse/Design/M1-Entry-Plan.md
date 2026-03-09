# M1 Entry Plan (Wire Closure + Runtime Bridge)

Date: 2026-03-08  
Planning branch: `codex/m0-exit-m1-entry`

## 1. M1 Mission

Carry M0's deterministic protocol semantics into a wire-accurate, runtime-adjacent execution slice without regressing S1..S5 behavior.

---

## 2. Scope

### In Scope

1. resolve deferred wire decisions `D3`, `D5`, `D7`, `D8`.
2. add canonical wire/codec conformance checks aligned with locked M0 defaults (`D1`, `D2`, `D4`, `D6`).
3. introduce a bounded runtime bridge harness path (message-driven node simulation) that preserves mediated-first behavior.
4. keep S1..S5 fixture packs as hard regression baseline.

### Out of Scope

1. production runtime replacement in Orleans core.
2. full CNS global routing architecture.
3. distributed persistence implementation for assertion/nonce stores.
4. large-scale federation/governance mechanisms.

---

## 3. Proposed M1 Slices

### M1-S1 (First): Deferred Wire Closure

Primary goal:

1. close `D3`, `D5`, `D7`, `D8` with deterministic fixture and error-ID coverage.

Deliverables:

1. updated lock file with final outcomes for `D3`, `D5`, `D7`, `D8`.
2. field-matrix and wire-example updates for closed decisions.
3. new vectors covering:
   - typed identifier validity/invalidity
   - `expr_norm_v` compatibility paths
   - deny envelope required-field policy
   - relation token boundary (embed/reference) conformance

### M1-S2: Runtime Bridge Harness

Primary goal:

1. execute protocol flows through a node/message-pump simulation layer instead of fixture-only transition playback.

Deliverables:

1. deterministic node lifecycle hooks for requester/mediator/target roles.
2. mediated handshake and direct-upgrade behavior preserved under runtime simulation.
3. replay/observe/policy slices runnable through same bridge.

### M1-S3: Security-Adapter Bridge (Bounded)

Primary goal:

1. connect conformance paths to existing `Scynapse.Security` verification primitives where practical, without production refactor.

Deliverables:

1. adapter interfaces for proof validation and nonce replay checks.
2. fixture-selectable strict/mock verification modes.
3. deterministic deny mapping preserved when strict mode is active.

### M1-S4: Strict Failure Mapping (Bounded)

Primary goal:

1. extend strict verification to deterministic temporal/revocation/proof-chain failure reasons.

Deliverables:

1. `strict_failure_mode` fixture control for strict verification paths.
2. deterministic strict failure IDs for expired/revoked/unresolvable/not-yet-valid outcomes.
3. baseline stability preserved across S1..S5 + M1-S1 + M1-S2 + M1-S3.

---

## 4. Non-Negotiable Guardrails

1. S1..S5 semantics remain stable unless an explicit migration note is approved.
2. no reintroduction of silo/client topology assumptions.
3. direct upgrade remains gate-driven and never bypasses mediated fallback continuity.
4. all new fail vectors must validate exact error IDs first.
5. deferred decisions resolved in M1-S1 must include Orleans compatibility classification (`A/N/D`).

---

## 5. Acceptance Criteria for M1 Entry

1. M0 exit review is complete and linked (`M0-Exit-Review.md`).
2. M1-S1 task board is present with bounded scope and vector ownership.
3. S1..S5 baseline rerun is green before first M1 implementation commit.
4. continuity docs (`EXECUTIVE-MEMORY`, milestone checkpoint, `SESSION-LOG`) are synchronized to M1 start.

---

## 6. Initial Branch Strategy

1. keep `codex/s5-policy-inheritance` as S5 implementation checkpoint.
2. use this planning branch (`codex/m0-exit-m1-entry`) for M0 exit + M1 entry artifacts.
3. create `codex/m1-s1-wire-closure` from this branch for first M1 implementation pass.

---

## 7. Immediate Next Action

1. `M1-S1` execution complete (`M1-S1-Closure.md`).
2. `M1-S2` runtime-bridge execution complete (`M1-S2-Closure.md`).
3. `M1-S3` security-adapter bridge execution complete (`M1-S3-Closure.md`).
4. `M1-S4` strict failure-mapping execution complete (`M1-S4-Closure.md`).
5. open next bounded M1 slice task board from this closure baseline.
