# AI Collaboration Operating Model (Dynamic Committee Mode)

Date: 2026-03-09  
Scope: Scynapse multi-agent collaboration workflow

## 1. Authority Model

1. Project lead (Louis) is final authority on priorities, direction, and decisions.
2. Codex and Claude operate as peer committee members with different strengths.
3. Tactical authority is dynamic and contextual:
   1. it can shift by task type, phase, and evidence,
   2. it is not permanently assigned to one agent.
4. Routine bounded work should proceed without asking for approval on every detail.
5. Escalation to project lead is required only for non-obvious risk, architecture pivots, or conflicting recommendations.

---

## 2. Working Principle

1. We optimize for progress + learning in an R&D setting.
2. Neither agent gates the other's work by default.
3. Both agents review each other through their domain lens:
   1. spec/conformance consistency,
   2. implementation/codebase reality.
4. Disagreement is surfaced with explicit tradeoffs for project-lead decision.

---

## 3. Collaboration Channel

1. collaboration remains file-mediated through repository documents.
2. no direct model-to-model runtime channel is assumed.
3. shared work artifacts live under:
   1. `Docs/Scynapse/Design/`

---

## 4. Packet and Artifact Format

Any agent may issue a bounded work packet. A packet should include:

1. `Objective`
2. `Scope In`
3. `Scope Out`
4. `Inputs`
5. `Expected Outputs`
6. `Validation/Evidence`
7. `Risks/Decision Points`

Agent responses should include:

1. `What changed`
2. `Evidence`
3. `Open risks`
4. `Decision asks (if any)`

---

## 5. Plain-Language Requirement

For any cross-agent or lead-facing document, include:

1. `What we did`
2. `Why it matters`
3. `What happens next`

Technical detail remains required in implementation artifacts, but outward docs must be readable without deep internal notation context.

---

## 6. Acceptance and Decision Flow

1. Bounded routine work can be accepted by committee consensus and recorded in continuity files.
2. Project-lead decision is required for:
   1. architecture pivots,
   2. priority changes,
   3. scope expansions with schedule/risk impact,
   4. unresolved agent disagreements.
3. Acceptance decisions should be recorded with enough rationale for post-compaction re-entry.

---

## 7. Non-Blocking Parallelism

1. Agents should not block each other on routine work.
2. If a dependency is missing, proceed in parallel with explicit marker:
   1. `pending implementation validation`, or
   2. `pending spec clarification`.
3. Reconcile later through documented findings and decision points.

---

## 8. Continuity Requirements

After significant collaborative blocks:

1. update `EXECUTIVE-MEMORY.md`,
2. update active milestone checkpoint (if plan-level state changed),
3. append `SESSION-LOG.md` entry with outcomes and next actions.
