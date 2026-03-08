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
2. Read active milestone checkpoint (`M0-Status-Checkpoint.md`).
3. Read active phase skeleton (`M0-B-Protocol-Skeleton.md` for current phase).
4. Read `SESSION-LOG.md` newest entry.
5. Confirm:
   - current `Doing`
   - immediate `Next`
   - open decision IDs blocking progress

If any mismatch exists between files:

1. checkpoint + phase docs are design authority
2. update `EXECUTIVE-MEMORY.md` to match
3. append discrepancy note in `SESSION-LOG.md`

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

### L3: Execution Plan

1. `M0-Implementation-Slice-Plan.md`
2. `M0-S1-Task-Board.md`

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
2. reconcile `M0-Status-Checkpoint.md` with executive memory.
3. append concise timeline entry to `SESSION-LOG.md`.
4. ensure new artifacts are referenced from `M0-B-Protocol-Skeleton.md` and/or other active index docs.
5. create a focused continuity commit before resuming feature work.

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
