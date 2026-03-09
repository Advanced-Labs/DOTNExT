# AI Collaboration Operating Model (GPT Lead + Claude Implementer)

Date: 2026-03-09  
Scope: Scynapse design/harness workflow

## 1. Roles

1. GPT lead (Codex):
   - owns plan sequencing, risk control, and final acceptance decisions,
   - issues bounded task packets,
   - reviews Claude outputs against deterministic criteria.
2. Claude implementer:
   - executes assigned packet scope only,
   - reports assumptions and constraints clearly,
   - submits patch + validation evidence in required format.
3. Human user:
   - final decision authority on priorities, pivots, and merge direction.

---

## 2. Collaboration Channel

1. collaboration is file-mediated through repository documents only.
2. no model-to-model runtime channel is assumed.
3. canonical packet location:
   - `Docs/Scynapse/Design/AI-Task-*.md`

---

## 3. Task Packet Template

Each packet must include:

1. `Objective`: one bounded deliverable statement.
2. `Scope In`: explicit files/systems allowed.
3. `Scope Out`: explicit no-touch boundaries.
4. `Inputs`: source docs/artifacts to consult.
5. `Expected Outputs`: files and exact changes required.
6. `Deterministic Acceptance Gates`: machine-checkable pass/fail criteria.
7. `Validation Commands`: exact command list and expected status.
8. `Response Format`: required structure for Claude’s return.

---

## 4. Claude Response Format (Required)

Claude should respond using these sections:

1. `Change Summary`
2. `Files Modified`
3. `Validation Evidence`
4. `Open Risks/Assumptions`
5. `Ready-for-Review Checklist`

If blocked, Claude must provide:

1. exact blocker,
2. attempted mitigation,
3. minimal decision needed from GPT lead/human.

---

## 5. Review Rubric (GPT Lead)

## 5.1 Correctness

1. does implementation match packet objective exactly?
2. are deterministic gate orders and IDs preserved?
3. are all required vectors/tests present and passing?

## 5.2 Scope Control

1. no unauthorized file/domain changes,
2. no hidden feature creep,
3. no mutation of locked behavior unless packet permits.

## 5.3 Regression Safety

1. prior baseline packs remain green,
2. no ID churn outside packet scope,
3. deterministic outcomes are reproducible across reruns.

## 5.4 Documentation Continuity

1. checkpoints and continuity docs updated,
2. artifact linkage complete in skeleton/index docs,
3. session log entry added.

---

## 6. Acceptance Gates

A packet is accepted only if all are true:

1. all packet-specific tests pass,
2. required regression suites pass,
3. deterministic ID assertions match exact expected IDs,
4. docs and continuity artifacts are synchronized,
5. no unresolved high-severity review findings remain.

---

## 7. Handoff Loop

1. GPT lead writes/updates task packet.
2. Claude executes and returns in required format.
3. GPT lead reviews and records findings.
4. if accepted:
   - checkpoint docs updated,
   - next packet issued.
5. if not accepted:
   - GPT lead issues revision packet with narrowed delta.

---

## 8. Escalation Rules

Escalate to human decision when:

1. packet implies architecture pivot,
2. deterministic behavior conflicts with existing locked semantics,
3. security/policy intent is ambiguous,
4. scope expansion would materially delay current milestone.
