# Scynapse Plan Report For Claude

Date: 2026-03-09  
Prepared by: GPT lead agent (Codex)

## 1. Purpose

This report is the re-entry and alignment document for Claude collaboration. It explains:

1. what Scynapse is trying to become,
2. which protocol/design paradigms are currently locked,
3. how the implementation slices progressed from S1 through M1-S12,
4. what is open next and how to contribute without destabilizing the baseline.

Use this with:

1. `AI-Collab-Operating-Model.md`
2. `AI-Task-0001-M1-S12-Followups.md`
3. `EXECUTIVE-MEMORY.md`

---

## 2. Plain-English Summary

Scynapse is designing a distributed execution and naming fabric where every participant is a node, not a siloed server/client split.  
The current plan intentionally hardens behavior in small slices so we can prove safety and determinism before scaling architecture complexity.

In plain terms:

1. we already proved mediated-first interactions and deterministic denials,
2. we then added bounded direct upgrade behavior with strict gate order,
3. we progressively hardened reference-token and grant-validation logic,
4. we now have issuer-binding checks in place (M1-S12),
5. every failure path is machine-checkable via stable error IDs.

Current baseline is deterministic and fully green: `135/135` effective pass across `S1..S5 + M1-S1..M1-S12`.

---

## 3. Architecture Intent (Paradigm View)

## 3.1 Topology and Runtime Model

1. no silo-less clients; all participants are nodes.
2. per-component isolation is central:
   - `Varia` as component model term,
   - `Varion` for virtual object units,
   - `Cell`/`Hive` vocabulary for component-scoped memory/runtime distribution.
3. direct communication is conditional, policy-gated, and may remain mediated when required.

## 3.2 Trust, Routing, and Policy

1. mediated-first lifecycle is the default invariant.
2. direct upgrade is optional and must never bypass fallback continuity.
3. parent policy inheritance is hard by default; future delegated weakening is explicit and bounded.
4. capability and disclosure semantics are first-class constraints, not optional afterthoughts.

## 3.3 CNS Model

1. CNS is dynamic, not static: names, members, and signatures can evolve over time.
2. observation/subscription semantics are native and policy-aware.
3. ambiguity resolves fail-closed (`AmbiguousResolution`), not best-guess.
4. developer ergonomics target both simple address-like paths and advanced expression chains.

---

## 4. Conformance Framework

The harness is the control point for deterministic behavior before production refactors.

Layer model:

1. `L1` envelope/schema shape
2. `L2` field contract correctness
3. `L3` transition/gate legality
4. `L4` deterministic deny mapping correctness

Core invariants:

1. explicit message-driven state transitions (not heuristic trace inference),
2. terminal behavior enforcement,
3. stable machine-checkable error IDs,
4. exact `expected_error_ids` for fail vectors (`expected_error_contains` only as compatibility fallback).

---

## 5. Slice Progression and Rationale

## 5.1 Foundation Slices

1. `S1`: mediated resolve + handshake determinism and transition hardening.
2. `S2`: direct-upgrade gating and fallback continuity.
3. `S3`: encrypted endpoint disclosure grant semantics.
4. `S4`: observe/replay lifecycle behavior.
5. `S5`: policy inheritance hard-lock behavior.

## 5.2 M1 Hardening Slices

1. `M1-S1`: wire closure for deferred decisions (`D3/D5/D7/D8`).
2. `M1-S2`: runtime bridge route-data behavior.
3. `M1-S3`: bounded security adapter integration (`mock|strict`).
4. `M1-S4`: strict failure-mode determinism (`expired|revoked|unresolvable|not_yet_valid`).
5. `M1-S5`: inline relation-token CID integrity.
6. `M1-S6`: reference lookup unresolved/rebinding/CID guard.
7. `M1-S7`: reference grant status guard.
8. `M1-S8`: reference grant proof binding.
9. `M1-S9`: reference grant freshness/replay.
10. `M1-S10`: request-context to grant-claim binding.
11. `M1-S11`: challenge/proof/accept nonce binding.
12. `M1-S12`: requested-issuer to active-grant issuer binding.

Design pattern across these slices:

1. add one bounded contract,
2. add deterministic schema/runtime IDs,
3. add isolated fixture pack,
4. rerun full regression before closure.

---

## 6. Current State (Post M1-S12)

1. branch: `codex/m1-s12-grant-issuer-binding`
2. harness baseline: `135/135` effective pass
3. deterministic gate order for `HandshakeAccept` in `M1-S12`:
   - `M1-S5 -> M1-S7 -> M1-S8 -> M1-S9 -> M1-S10 -> M1-S11 -> M1-S12 -> M1-S6`
4. new M1-S12 deterministic IDs:
   - schema: `E3200..E3204`
   - runtime: `E3210` (`TrustInsufficient`)

---

## 7. Deferred/Boundary Notes

1. this work is harness/design scope only, not production runtime refactor.
2. no scope pull-in of large federation/persistence rewrites.
3. wire-lock authority remains `M0-B-Wire-Lock-Open-Decisions.md`.
4. production crypto-capabilities and distributed grant-chain propagation remain future slices.

---

## 8. Near-Term Roadmap

1. choose next bounded M1 slice from M1-S12 closure baseline.
2. preserve full baseline behavior/error-ID stability.
3. continue harness-first hardening before production-grade implementation pivots.
4. keep docs/checkpoints/continuity synchronized per `AGENTS.md` protocol.

---

## 9. How Claude Should Engage

Claude should operate through file-based packets only, with deterministic deliverables and explicit acceptance criteria.

Start here:

1. read `AI-Collab-Operating-Model.md`
2. execute `AI-Task-0001-M1-S12-Followups.md`
3. produce outputs in the exact response format requested in that task file

All contributions are reviewed by GPT lead before merge recommendation.
