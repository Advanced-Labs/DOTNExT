# Phase 1 Tasks

> **Purpose:** Ordered implementation tasks for Phase 1 (DDS/SAL Microkernel).
> **Status:** Ready for Implementation

---

## Task Order

Tasks must be completed in order. Dependencies are explicit.

| Task | Name | Work Package | Dependencies | Status |
|------|------|--------------|--------------|--------|
| T01 | Header Bit Infrastructure | WP1 | None | Pending |
| T02 | OpsRoot Side Table | WP2 | T01 | Pending |
| T03 | Device Interfaces | WP3 | None | Pending |
| T04 | Default Drivers | WP4 | T03 | Pending |
| T05 | Field Access Interception | WP5 | T01, T02, T03, T04 | Pending |
| T06 | GC Integration | WP6 | T02 | Pending |
| T07 | Managed API Surface | WP7 | T01-T06 | Pending |
| T08 | Test Suite | WP8 | T07 | Pending |

---

## Parallel Tracks

Some tasks can be done in parallel:

```
Track A: Infrastructure          Track B: Interfaces
─────────────────────            ───────────────────
T01 Header Bit                   T03 Device Interfaces
       │                                │
       ▼                                ▼
T02 OpsRoot Side Table           T04 Default Drivers
       │                                │
       └────────────┬───────────────────┘
                    │
                    ▼
            T05 Field Access Interception
                    │
                    ▼
            T06 GC Integration
                    │
                    ▼
            T07 Managed API Surface
                    │
                    ▼
            T08 Test Suite
```

---

## Directory Structure

```
Tasks/
├── README.md                     ← This file
├── T01-Header-Bit-Infrastructure.md
├── T02-OpsRoot-Side-Table.md
├── T03-Device-Interfaces.md
├── T04-Default-Drivers.md
├── T05-Field-Access-Interception.md
├── T06-GC-Integration.md
├── T07-Managed-API-Surface.md
├── T08-Test-Suite.md
└── Completed/                    ← Move completed tasks here
    └── (completed task files with implementation notes)
```

---

## Workflow

### Starting a Task

1. Read the task file thoroughly
2. Read referenced sections in main Phase 1 document
3. Read CLR-Integration-Reference.md for CLR details
4. Mark task as "In Progress" in this README
5. Create a working branch if needed

### Completing a Task

1. Verify all acceptance criteria are met
2. Add "Implementation Notes" section to task file
3. Move task file to `Completed/` directory
4. Update this README status
5. Commit with reference to task: `[Phase1/T01] Implement header bit infrastructure`

### Implementation Notes Format

Add to completed task file:
```markdown
---

## Implementation Notes

**Completed:** YYYY-MM-DD
**Commits:** abc1234, def5678

### What Was Done
- [Summary of implementation]

### Deviations from Plan
- [Any changes from original spec]

### Issues Encountered
- [Problems and how they were resolved]

### Follow-up Items
- [Anything discovered that needs future work]
```

---

## References

- Main Document: `../01-Phase1-DDS-Microkernel-and-Persistence.md`
- CLR Reference: `../CLR-Integration-Reference.md`
- Gap Decisions: Main Doc Part VI
- Backlogs: `../../Backlogs/`
