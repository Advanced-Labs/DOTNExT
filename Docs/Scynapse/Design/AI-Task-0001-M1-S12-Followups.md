# AI Task 0001: M1-S12 Followups (Claude Packet)

Date: 2026-03-09  
Owner: GPT lead  
Assignee: Claude

## 1. Objective

Produce a bounded design-ready candidate for the next slice after M1-S12, with deterministic gate-order and vector coverage specification, without changing runtime code.

---

## 2. Scope In

1. `Docs/Scynapse/Design/*` design and planning docs.
2. fixture design definitions (documentation only).
3. continuity alignment notes for future implementation.

## 3. Scope Out

1. no edits under `src/Scynapse/*` runtime/harness code.
2. no edits to existing fixture JSON packs.
3. no branch/commit operations.

---

## 4. Inputs (Read First)

1. `Scynapse-Plan-Report-For-Claude.md`
2. `M1-S12-Closure.md`
3. `M1-Status-Checkpoint.md`
4. `M0-B-Protocol-Skeleton.md`
5. `M0-B-Protocol-Test-Vectors.md`
6. `M0-B-Conformance-Harness-Checklist.md`
7. `M0-B-Message-Field-Matrix.md`

---

## 5. Required Outputs

Create these files:

1. `Docs/Scynapse/Design/M1-S13-Candidate-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S13-Candidate-Vector-Pack.md`
3. `Docs/Scynapse/Design/M1-S13-Claude-Readback.md`

Output requirements:

1. candidate profile name must be explicit (`slice_profile: "M1-S13"` unless strongly justified otherwise).
2. candidate must define exact `HandshakeAccept` gate order extension from M1-S12.
3. candidate must define deterministic ID budget:
   - schema IDs: at least 5
   - runtime IDs: at least 3
   - each runtime ID mapped to deterministic deny code.
4. vector pack must define at least 8 vectors:
   - 2 pass vectors
   - 6 fail vectors using exact expected error IDs
   - include at least 1 precedence vector.
5. readback must include:
   - assumptions,
   - top 3 risks,
   - regression packs to run unchanged,
   - why this candidate is bounded enough for harness-first execution.

---

## 6. Deterministic Acceptance Gates (Lead Review)

1. no out-of-scope file edits occurred.
2. all 3 required output files exist with non-placeholder content.
3. gate order is unambiguous and append-only from M1-S12 (no silent reorder of prior gates).
4. each fail vector maps to explicit expected error IDs (no substring-only oracle).
5. deny mappings are explicit for each runtime ID.

---

## 7. Response Format (Claude Must Use)

1. `Change Summary`
2. `Files Created`
3. `Design Decisions`
4. `Risks/Assumptions`
5. `Lead Review Checklist Self-Assessment`

---

## 8. Completion Note

When complete, Claude should state:  
`AI-Task-0001 ready for GPT lead review.`
