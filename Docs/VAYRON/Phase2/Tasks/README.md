# Phase 2 Tasks

> **Purpose:** Ordered implementation tasks for Phase 2 (StorageDevice + Voron-backed Persistence).
> **Status:** Ready for Implementation

---

## Phase 2 Goal

**Make this work end-to-end:**
1. Create a virtual/routed object
2. Mutate fields
3. Shutdown process (clean or crash-tolerant)
4. Restart
5. Re-load by VUID
6. Observe fields persisted

---

## Task Order

| Task | Name | Work Package | Dependencies | Status |
|------|------|--------------|--------------|--------|
| T01 | VContext Enhancement | WP2.0 | Phase 1 | Pending |
| T02 | VUID Infrastructure | WP2.0 | T01 | Pending |
| T03 | Dirty Tracking | WP2.0 | T01 | Pending |
| T04 | Voron Embedding | WP2.1 | None | Pending |
| T05 | Storage_Voron Driver | WP2.2 | T01, T02, T04 | Pending |
| T06 | Body Encoder | WP2.3 | None | Pending |
| T07 | FieldAccess_Persist Driver | WP2.4 | T03, T05 | Pending |
| T08 | Driver Registry | WP2.0 | T05, T07 | Pending |
| T09 | VKernel Managed API | WP2.5 | T05, T07, T08 | Pending |
| T10 | Test Suite | WP2.6 | T09 | Pending |

---

## Parallel Tracks

Some tasks can be done in parallel:

```
Track A: Infrastructure          Track B: Storage             Track C: Drivers
─────────────────────            ────────────────             ───────────────
T01 VContext Enhancement         T04 Voron Embedding          T06 Body Encoder
       │                               │                            │
       ├── T02 VUID Infrastructure     │                            │
       │                               │                            │
       └── T03 Dirty Tracking          │                            │
              │                        │                            │
              └────────────────────────┴────────────────────────────┘
                                       │
                                       ▼
                             T05 Storage_Voron Driver
                                       │
                                       ▼
                             T07 FieldAccess_Persist Driver
                                       │
                                       ▼
                             T08 Driver Registry
                                       │
                                       ▼
                             T09 VKernel Managed API
                                       │
                                       ▼
                             T10 Test Suite
```

---

## Directory Structure

```
Tasks/
├── README.md                     ← This file
├── T01-VContext-Enhancement.md
├── T02-VUID-Infrastructure.md
├── T03-Dirty-Tracking.md
├── T04-Voron-Embedding.md
├── T05-Storage-Voron-Driver.md
├── T06-Body-Encoder.md
├── T07-FieldAccess-Persist-Driver.md
├── T08-Driver-Registry.md
├── T09-VKernel-Managed-API.md
├── T10-Test-Suite.md
└── Completed/                    ← Move completed tasks here
```

---

## Workflow

### Starting a Task

1. Read the task file thoroughly
2. Read the Phase 2 main document for context
3. Read Voron-Integration-Guide.md for Voron details
4. Mark task as "In Progress" in this README
5. Create journal entry in phase2-journal.md

### Completing a Task

1. Verify all acceptance criteria are met
2. Add "Implementation Notes" section to task file
3. Move task file to `Completed/` directory
4. Update this README status
5. Commit with reference: `[Phase2/T01] Implement VContext enhancement`

---

## References

- Main Document: `../02-Phase2-StorageDevice-Voron.md`
- Voron Guide: `../Voron-Integration-Guide.md`
- Phase 1 Reference: `../../Phase1/`
- Platform Vision: `../../VAYRON-R1-Platform-Vision.md`
