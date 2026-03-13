# Scynapse Design Methodology (Continuity-First)

## 1. Status and Origin

This methodology is project-local for Scynapse.

1. It is not an official OpenAI/Codex methodology.
2. It exists to keep design/program direction stable across:
   - long threads
   - compaction events
   - handoff to other agents
   - new-thread restarts

---

## 2. Primary Goals

1. Preserve intent, not just artifacts.
2. Make re-entry deterministic for humans and agents.
3. Separate durable decisions from temporary discussion.
4. Keep context-window usage efficient by storing executive state in files.

### 2.1 Decision Maturity Tiers (R&D Alignment)

Scynapse is R&D-first. Not all decisions have the same commitment level.

1. `Locked Commitments`
   - proven by code/tests and treated as stability anchors.
   - examples: working v1 security primitives and verified implementations.
2. `Design Baselines`
   - current working design used for planning/prototyping/conformance.
   - can be revised if implementation evidence demands change.
3. `Explored Directions`
   - hypothesis space under active exploration.
   - no commitment implied until promoted to baseline or locked commitment.

Rule:

1. Every major design artifact should state which tier its decisions belong to.
2. "Locked" language should be used for `Locked Commitments` by default; use "baseline" for provisional design.

---

## 3. Artifact Taxonomy and ID System

### 3.1 Milestones and Phases

1. `M*` = milestone (`M0`, `M1`, ...).
2. `M*-A/B/C...` = milestone phase (A = contracts/invariants, B = protocol/spec hardening, later letters as needed).

### 3.2 Execution Planning Units

1. `S*` = implementation slice (`S1`, `S2`, ...).
2. `W*` = workstream (`W1`, `W2`, ...).
3. `T*` = task inside a workstream (`S1-W4-T2`).

### 3.3 Verification and Decisions

1. `TV-*` = test vector ID (`TV-001`, ...).
2. `D*` = explicit open/locked decision item (`D1`, `D2`, ...).
3. `G*` = conformance gate/checkpoint layer.

---

## 4. File Roles (Single Source of Truth by Layer)

1. `EXECUTIVE-MEMORY.md`
   - top re-entry file
   - current objective, done/doing/next, open decisions, guardrails
2. `M0-Status-Checkpoint.md` (or corresponding milestone checkpoint)
   - milestone-level status and plan continuity
3. `SESSION-LOG.md`
   - append-only session timeline and handoff breadcrumbs
4. milestone design artifacts (`M0-A-*`, `M0-B-*`, etc.)
   - durable specs, matrices, vectors, examples

Rule:

1. If a detail can change day to day, keep it in `EXECUTIVE-MEMORY.md` or `SESSION-LOG.md`.
2. If a detail is intended as durable design contract, keep it in a milestone artifact.

---

## 5. Re-Entry Protocol (New Thread or Post-Compaction)

Do this in order before any design/code action:

1. Read `EXECUTIVE-MEMORY.md`.
2. Read active milestone checkpoint (`M0-Status-Checkpoint.md` or `M1-Status-Checkpoint.md`).
3. Read active phase skeleton (`M0-B-Protocol-Skeleton.md` for current phase).
4. Read `SESSION-LOG.md` newest entry.
5. If multi-agent collaboration is active, read:
   - `AI-Collab-Operating-Model.md`
   - latest `Codex-Reply-*-To-Claude.md`
   - latest `Claude-Reply-*-To-Codex.md`
   - latest audit/mapping artifacts (`F*-Current-State-Audit.md`, `B*-Diagnostic-Flow-Mapping.md`)
6. Confirm:
   - current `Doing`
   - immediate `Next`
   - open decision IDs blocking progress

If any mismatch exists between files:

1. checkpoint + phase docs are design authority
2. update `EXECUTIVE-MEMORY.md` to match
3. append discrepancy note in `SESSION-LOG.md`

### 5.1 Important Reading Pack (Persistent)

These docs are mandatory context anchors for Scynapse architecture/security/CNS/runtime-evolution work and should be re-scanned after compaction before major planning/coding pivots:

1. `Docs/Scynapse/Scynapse-Vision.md`
2. `Docs/Scynapse/Scynapse-V1.md`
3. `Docs/Scynapse/Scynapse Security Development/scynapse-security-architecture_3.md`
4. `Docs/Scynapse/Scynapse Security Development/scynapse-security-implementation-guide-v2_1.md`
5. `Docs/Scynapse/Scynapse Security Development/scynapse-security-phase1-review.md`
6. `Docs/Scynapse/Scynapse Security Development/scynapse-security-phase1-completion-guide-v4.md`
7. `Docs/Scynapse/Scynapse Features/StatePropertyAccess.md`
8. `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/DynamicGrainAccess.md`
9. `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/PluginGrainArchitecture.md`
10. `Docs/Scynapse/Scynapse Features/Dynamic Orleans Grain System/Scynapse v0 - Dynamic Grain Features.md`
11. `Docs/Scynapse/Original Orleans Internals/OrleansDistributedGrainDirectory.md`

---

## 6. Hierarchical Context Model

### L0: Repository Operating Rules

1. `AGENTS.md`

### L1: Product Vision and Scope

1. `Docs/Scynapse/Scynapse-Vision.md`
2. `Docs/Scynapse/Scynapse-V1.md`

### L2: Milestone Contracts and Protocol

1. `M0-A-*`
2. `M0-B-*`
3. `M0-Status-Checkpoint.md`
4. `M1-Status-Checkpoint.md` (when M1 work is active)

### L3: Execution Plan

1. `M0-Implementation-Slice-Plan.md`
2. `M0-S1-Task-Board.md`
3. active M1 task board when M1 work is active (for example `M1-S1-Task-Board.md`)

### L4: Verification Assets

1. `M0-B-Protocol-Test-Vectors.md`
2. `Fixtures/S1/*`

### L5: Session Continuity

1. `EXECUTIVE-MEMORY.md`
2. `SESSION-LOG.md`

---

## 7. Session Hygiene Rules

At end of each significant working block:

1. update `EXECUTIVE-MEMORY.md` (`Done`, `Doing`, `Next`, blockers)
2. update active milestone checkpoint when plan-level status changed
3. append one concise entry to `SESSION-LOG.md`
4. ensure new artifacts are linked from relevant index/skeleton docs

### 7.1 Outward Communication Rule (Plain-Language Layer)

For cross-agent or lead-facing documents, include a plain-language layer in addition to technical detail.

Minimum plain-language summary:

1. `What we did`
2. `Why it matters`
3. `What happens next`

Technical IDs/slice notation remain required for implementation artifacts, but outward-facing docs must be readable without prior methodology immersion.

---

## 8. Compaction-Resilience Practices

1. Assume model memory can be incomplete after compaction.
2. Never rely on thread history alone for project state.
3. Keep executive state in short, high-signal files.
4. Prefer references to canonical docs over repeating full content.
5. Record decisions with IDs (`D*`) and status (`open`, `locked`, `deferred`).

### 8.1 Pre-Compaction Refresh Playbook

Trigger:

1. user reports context is close to compaction, or
2. agent judges thread complexity/history length as high-risk.

Actions (in order):

1. update `EXECUTIVE-MEMORY.md` with precise `Done`, `Doing`, `Next`, branch, and open decision state.
2. reconcile `M0-Status-Checkpoint.md` and `M1-Status-Checkpoint.md` with executive memory.
3. reconcile `AI-Collab-Operating-Model.md` and latest cross-agent exchange state if collaboration is active.
4. append concise timeline entry to `SESSION-LOG.md`.
5. ensure new artifacts are referenced from active index docs and immediate reading order sections.
6. create a focused continuity commit before resuming feature work.

---

## 9. Branching and Commit Hygiene (Scynapse Design Work)

1. keep design baseline branch stable (current: `codex/m0-design-foundation`)
2. create focused implementation branch per slice (example: `codex/s1-prototype`)
3. commit only relevant files for the active scope
4. avoid bundling unrelated workspace artifacts

---

## 10. Definition of "On Track"

The work is on-track when:

1. `EXECUTIVE-MEMORY.md`, milestone checkpoint, and task board agree
2. open decisions are explicit and bounded
3. next executable step is unambiguous
4. test vectors and conformance gates map to planned slice

---

## 11. Multi-Agent Collaboration Mode

When another AI agent (for example Claude) is introduced:

1. project lead (Louis) remains final decision authority.
2. agents collaborate as peers with dynamic/contextual task authority.
3. collaboration is file-mediated through shared artifacts.
4. required control files:
   - `Scynapse-Plan-Report-For-Claude.md`
   - `AI-Collab-Operating-Model.md`
   - active task packet(s) when needed (`AI-Task-*.md`)
5. routine bounded work should proceed without requiring project-lead approval for every detail.
6. escalate to project lead for pivots, unresolved disagreements, or non-obvious risk/tradeoff decisions.
