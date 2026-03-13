# Scynapse Plan Report For Claude

Date: 2026-03-09  
Prepared by: GPT lead agent (Codex)  
Audience: Claude Opus 4.6 + human project lead

## 1. What This Document Is For

This is the canonical re-entry brief for Scynapse design/harness work.

It defines:

1. what Scynapse is trying to become,
2. where the current milestone work is heading,
3. the exact execution path from current state to next gates,
4. what remains fundamentally under-designed and must be designed next.

Read alongside:

1. `EXECUTIVE-MEMORY.md`
2. `M1-Status-Checkpoint.md`
3. `M0-Status-Checkpoint.md`
4. `M1-S12-Closure.md`

---

## 2. Plain-English Project Direction

Scynapse is building a distributed cybernetic fabric where all participants are nodes and where routing, naming, policy, and trust are first-class runtime concerns.

The immediate strategy is deliberate:

1. lock deterministic protocol behavior first,
2. prove it with machine-checkable conformance vectors,
3. harden edge cases slice-by-slice,
4. only then widen toward production runtime/security implementation.

Current maturity:

1. deterministic baseline is green at `135/135` vectors (`S1..S5 + M1-S1..M1-S12`),
2. direct path behavior is profile-gated, never unconditional,
3. grant/reference chain is progressively bound (status -> proof -> freshness -> claims -> nonce -> issuer).

---

## 3. Paradigm/Framework Commitments (Locked Intent)

## 3.1 Topology and Component Model

1. no silo-less clients; nodes are the only participant form.
2. per-component isolation remains central:
   - `Varia` (component),
   - `Varion` (virtual object units),
   - `Cell`/`Hive` semantics for component-scoped distribution.

## 3.2 Trust/Routing/Policy

1. mediated-first by default.
2. direct upgrade is conditional and gate-checked.
3. fallback continuity is mandatory on upgrade rejection.
4. parent policy power is hard by default (future delegated weakening remains explicit/future-slice).

## 3.3 CNS Behavior

1. dynamic namespace, not static registry.
2. observation and subscription are native.
3. ambiguity is fail-closed (`AmbiguousResolution`).
4. language ergonomics must serve both simple users and deep agentic/developer workflows.

---

## 4. Current Technical State (Post M1-S12)

1. active implementation branch: `codex/m1-s12-grant-issuer-binding`
2. conformance engine supports `slice_profile: "M1-S12"`.
3. active `HandshakeAccept` gate order:
   - `M1-S5 -> M1-S7 -> M1-S8 -> M1-S9 -> M1-S10 -> M1-S11 -> M1-S12 -> M1-S6`
4. M1-S12 deterministic IDs:
   - schema: `E3200..E3204`
   - runtime: `E3210_M1S12_REFERENCE_GRANT_ISSUER_MISMATCH` -> `TrustInsufficient`
5. full regression baseline remains green and reproducible (`135/135`).

---

## 5. Where This Is Leading (Plan Destination)

## 5.1 End of Current Track

The current track is aiming to complete an M1 hardening envelope where reference-token and reference-grant acceptance is fully deterministic and precedence-safe in harness space.

Success condition for this track:

1. all pre-runtime protocol-critical gates have deterministic schema/runtime IDs,
2. precedence between slices is explicit and regression-tested,
3. future runtime/security implementation can reuse this as executable contract.

## 5.2 Why This Matters

This reduces the chance of:

1. security regressions hidden in ambiguous logic,
2. policy bypass through ordering bugs,
3. context-loss drift during long AI-assisted design/programming cycles.

---

## 6. Exact Execution Path From Here

This is the intended sequence unless human decision changes priorities.

## Step 1: Candidate Definition (Now)

1. generate next bounded slice candidate (`M1-S13`) with deterministic gate-order extension proposal.
2. output only docs/design artifacts first (no runtime edits yet).
3. acceptance gates are packetized in `AI-Task-0001-M1-S12-Followups.md`.

## Step 2: Lead Review and Lock

1. Codex reviews Claude candidate for:
   - scope discipline,
   - gate-order append-only behavior,
   - ID budget adequacy,
   - vector completeness.
2. accepted candidate becomes next implementation task board.

## Step 3: Harness-First Implementation

1. implement new slice in conformance engine only.
2. add isolated fixture pack.
3. rerun full baseline + new pack.

## Step 4: Closure and Continuity

1. update closure doc, checkpoints, executive memory, session log.
2. issue next packet.

Operational rule:

1. no production runtime/security refactor until this contract-hardening sequence reaches approved transition point.

---

## 7. Design Reality Check: What Is Still Not Designed

The harness hardening work is intentionally narrow. It proves deterministic protocol behavior for selected flows, but it does not mean Scynapse is fully designed.

Source-grounded unresolved areas from `Scynapse-Vision.md` Part 8 ("Open Problems and Explored Directions"):

1. naming and discovery (CNS)
2. routing
3. component lifecycle
4. component packaging and distribution
5. component survival
6. persistence
7. federation and cross-organization trust
8. stream and event security
9. democratic anonymity
10. key distribution and discovery
11. built-in capability vocabulary

Additional unresolved pressure points from `Scynapse-V1.md` Part 7 ("Known Limitations") and Part 8 ("Future Roadmap"):

1. TLS-level assertion verification path is deferred and not yet end-to-end hardened.
2. streams/events still bypass the call-filter security membrane.
3. assertion/nonce stores are in-memory in v1; distributed stores are still future work.
4. online key rotation is not yet designed/implemented.
5. default-open compatibility behavior (`AllowAnonymous`) must be transitioned to component-native secure defaults.
6. multiple v1 implementation details are Orleans-specific and require component-native replacements.

Cross-cutting design areas that remain under-specified:

1. final architecture vocabulary and boundaries (`Varia`/`Varion`/`Cell`/`Hive`) as a complete ontology.
2. CNS language design (grammar, typing, dynamic/static bridge, IDE/language-server interaction).
3. mediation/direct-upgrade policy model, including disclosure grants and fallback continuity rules.
4. parent/child policy inheritance with future delegated override semantics.
5. trust/credential lifecycle semantics at scale (revocation, rotation, replay windows, cache invalidation).
6. observability and operator contracts (audit evidence, policy decision introspection, incident forensics).

Bottom line:

1. we have a strong protocol-hardening scaffold,
2. we do not yet have a full end-to-end Scynapse systems design.

---

## 8. Design Fronts That Must Be Completed

To move from deterministic harness slices to buildable architecture, the next design work should be organized into explicit fronts derived from Vision Part 8 + v1 limitations.

## F1: Semantic Core and Terminology Freeze

1. lock official meanings and boundaries for `Varia`, `Varion`, `Cell`, `Hive`, "virtual object," and runtime object distinctions.
2. produce one normative glossary and cross-map Orleans legacy terms to Scynapse terms.

## F2: CNS Semantics and Language

1. define CNS object model and graph invariants.
2. define expression grammar (addressing, calls, indexing, predicates, variables, subscriptions).
3. define static/dynamic typing bridge and IDE/language-server integration contract.

## F3: Routing and Mediation Semantics

1. define mediated-first baseline, direct-upgrade eligibility, and fallback continuity as architecture rules.
2. define anonymous relay modes and trust-vouching options.
3. define resolution-walk behavior and bounded-cost heuristics.

## F4: Policy Model and Governance

1. define parent policy inheritance, conflict resolution, and exception/delegation paths.
2. define policy evaluation phases for resolve/handshake/session operations.
3. define policy audit evidence requirements.

## F5: Trust, Capabilities, and Credential Lifecycle

1. define grant issuance/lookup/refresh/revocation models.
2. define issuer authority model and delegation boundaries.
3. define key rotation and cache invalidation behavior under partition conditions.

## F6: Runtime and Lifecycle Model

1. define component admission and resource contract model.
2. define object activation/deactivation, migration, and recovery semantics.
3. define failure domains and survival targets.

## F7: Data, Streams, and State Guarantees

1. define persistence tiers and consistency profiles.
2. define event/stream authorization and replay controls.
3. define state versioning and evolution rules.

## F8: Federation and Operational Plane

1. define cross-org trust/federation contracts.
2. define observability, audit, incident response, and operator workflows.
3. define minimum deployable reference topology for real environments.

## F9: Packaging, Distribution, and Survival

1. define component package format, dependency semantics, and version compatibility rules.
2. define artifact distribution protocol (content addressing, replication, seeding, fetch policy).
3. define survival guarantees and resurrection paths when hosting nodes disappear.

## F10: Privacy, Anonymity, and Capability Vocabulary

1. define pseudonymous identity model and threshold de-anonymization governance.
2. define key discovery/bootstrap modes and anti-spoofing trust policy.
3. define capability verb vocabulary strategy (standardized core verbs vs open convention model).

## F11: Developer Surface and Tooling

1. define primary developer-facing API model for Varia/Varion/CNS operations.
2. define configuration and policy authoring ergonomics.
3. define diagnostics/observability UX for developers and operators.
4. define IDE/language-server and shell integration expectations for CNS exploration.

---

## 9. Proposed Design Sequence (After Current M1 Track)

This is the recommended design-first sequence before broad production coding.

## Phase A: Semantic and Control-Plane Foundation

1. complete `F1 + F2 + F3 + F4` first.
2. produce normative specs with explicit invariants and failure semantics.
3. output conformance vectors for each invariant before runtime implementation.

## Phase B: Trust and Runtime Core

1. complete `F5 + F6`.
2. lock lifecycle/trust interactions under failure and partition scenarios.
3. produce executable harness scenarios for critical failure modes.

## Phase C: Data/Federation/Operations

1. complete `F7 + F8 + F9 + F10 + F11`.
2. lock operational contracts required for multi-organization deployment.
3. define pilot architecture and go/no-go criteria for implementation scaling.

---

## 10. Design Exit Criteria Before Broad Coding

Broad production coding should begin only when:

1. each major front (`F1..F11`) has a versioned spec with explicit invariants.
2. unresolved architecture decisions are reduced to a tracked, bounded set with named owners.
3. critical security/policy paths have deterministic conformance vectors and stable error taxonomy.
4. lifecycle and failure semantics are simulation-tested in harness.
5. migration path from v1 assumptions to the target Scynapse model is documented and reviewed.

---

## 11. Immediate Planning Focus

The next planning output should not be only protocol micro-slices. It should include an architecture decision backlog spanning all fronts above.

Immediate expected artifacts:

1. `Scynapse-Open-Design-Questions.md`:
   - ranked unresolved questions across `F1..F11`.
2. `Scynapse-Decision-Backlog.md`:
   - decision records with status (`proposed`/`approved`/`deferred`), impact, and dependencies.
3. `Scynapse-Design-Roadmap.md`:
   - sequence from current M1 hardening to phase-based architecture completion.

This keeps us honest:

1. harness progress is real and valuable,
2. but it is one layer of a much larger design program that remains to be completed.
