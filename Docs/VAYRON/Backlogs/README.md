# VAYRON Backlogs

> **Purpose:** Track future work items not yet assigned to a specific phase.

---

## Structure

```
Backlogs/
├── README.md                    ← This file
└── Improvements/                ← Future improvements from phase decisions
    ├── IMP-001-*.md             ← Individual improvement specs
    ├── IMP-002-*.md
    └── ...
```

---

## Workflow

### Adding Items

1. Create a new file in the appropriate subfolder
2. Use the naming convention: `IMP-###-Short-Name.md`
3. Include origin (which decision created this backlog item)
4. Include target phase (when it makes sense to implement)

### Picking Items for a Phase

When an item is selected for implementation:

1. Move the file to `/Docs/VAYRON/Phase{N}/Tasks/`
2. Rename to fit task numbering: `T##-Task-Name.md`
3. Update the phase's main document to reference the task

### Completing Items

When a task is completed:

1. Move to `/Docs/VAYRON/Phase{N}/Tasks/Completed/`
2. Add implementation notes at the bottom of the file
3. Reference the relevant commits

---

## Current Backlog

### Improvements (from Phase 1 Gap Closures)

| ID | Name | Origin | Target Phase |
|----|------|--------|--------------|
| IMP-001 | SyncBlock Recycle Hook | Generation tag safety net | Phase 1 WP2 Hardening |
| IMP-002 | JIT Helper Interception | No JIT surgery | Phase 2.5 |
| IMP-003 | VContext Threading | Null context placeholder | Phase 2 |
| IMP-004 | Custom GC Scanning | Default scanning only | Phase 3+ |
| IMP-005 | Dynamic Driver Loading | Static only | Phase 4+ |

---

## Priority Levels

| Priority | Meaning |
|----------|---------|
| **Critical** | Blocks current work; must resolve immediately |
| **High** | Important for upcoming phase; schedule soon |
| **Medium** | Valuable improvement; schedule when capacity allows |
| **Low** | Nice to have; schedule opportunistically |

---

*Items in backlog are not committed to any schedule. See individual phase docs for committed work.*
