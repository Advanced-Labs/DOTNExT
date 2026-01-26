# How to Work on VAYRON

> **Purpose:** Work protocol for AI agents and human contributors working on VAYRON.
> **Read this before starting any implementation work.**

---

## 1. The Journal System

### Why Journals Exist

Context is lost between sessions. Journals preserve:
- What was done
- What was learned
- Where to resume
- Decisions made along the way

**Without journals, work is repeated or forgotten.**

---

### 1.1 Main Journal (`/Docs/VAYRON/journal.md`)

The main journal tracks **cross-phase progress** and **high-level milestones**.

**Update frequency:** After completing any task, add 2-3 lines.

**Format:**

```markdown
## [Date] - [Task ID or Description]

Completed [task]. [Key outcome or learning]. Next: [what comes next].

---
```

**Example entries:**

```markdown
## 2026-01-26 - T01 Header Bit Infrastructure

Completed T01. Bit 31 repurposed as BIT_SBLK_DDS_NONDEFAULT. Verified no conflicts on x64/ARM64. Next: T02 OpsRoot Side Table.

---

## 2026-01-27 - T02 OpsRoot Side Table

Completed T02. Generation tag safety net implemented. Found SyncBlock recycle point at SyncBlockCache::GCWeakPtrScan - added to IMP-001 notes. Next: T03/T04 can run in parallel.

---

## 2026-01-28 - Phase 1 Complete

All Phase 1 tasks complete. 47 tests passing. Performance baseline: 15ns overhead for routed objects. Ready to begin Phase 2.

---
```

**Rules:**
- Keep entries short (2-3 lines)
- Always state what's next
- Note any discoveries or decisions
- Date every entry

---

### 1.2 Phase Journals (`/Docs/VAYRON/Phase{N}/phase{N}-journal.md`)

Each phase has its own journal for **detailed task-level tracking**.

**Update frequency:** After each work session, even if task not complete.

**Format:**

```markdown
# Phase {N} Journal

## [Date] - [Session Focus]

### What I Did
- [Specific action]
- [Specific action]

### What I Learned
- [Discovery or decision]

### Blockers / Issues
- [Problem encountered, if any]

### Next Session
- [Specific next steps]

---
```

**Example:**

```markdown
# Phase 1 Journal

## 2026-01-26 - T02 OpsRoot Side Table (Session 1)

### What I Did
- Created dds/ directory structure
- Implemented OpsRootEntry with generation tag
- Wrote Get() and Set() methods

### What I Learned
- SHash requires custom traits class
- CrstExplicitInit needs CrstType enum entry

### Blockers / Issues
- Need to add CrstOpsRootTable to CrstTypes.h

### Next Session
- Add CrstType entry
- Implement OnSyncBlockRecycled
- Write unit tests

---

## 2026-01-26 - T02 OpsRoot Side Table (Session 2)

### What I Did
- Added CrstOpsRootTable to CrstTypes.h
- Implemented OnSyncBlockRecycled
- Wrote 5 unit tests, all passing

### What I Learned
- Generation increment must be atomic (used Interlocked)

### Blockers / Issues
- None

### Next Session
- Move to T03 or T04 (parallel track)

---
```

---

### 1.3 Creating Journals

**Main journal:** Create `/Docs/VAYRON/journal.md` if it doesn't exist.

**Phase journal:** Create `/Docs/VAYRON/Phase{N}/phase{N}-journal.md` when starting work on that phase.

**Template for new phase journal:**

```markdown
# Phase {N} Journal

> Phase goal: [One sentence describing phase goal]
> Started: [Date]
> Status: In Progress

---

## [Date] - [First Session]

### What I Did
- [Actions]

### What I Learned
- [Discoveries]

### Next Session
- [Next steps]

---
```

---

## 2. Task Workflow

### 2.1 Starting a Task

1. **Read the task file** (`Phase{N}/Tasks/T##-Task-Name.md`)
2. **Read the phase journal** to understand current state
3. **Update phase journal** with session start
4. **Do the work**
5. **Update phase journal** with session end
6. **If task complete:** Move to Completed/, update main journal

### 2.2 Completing a Task

1. Verify all acceptance criteria in task file
2. Add "Implementation Notes" section to task file:

```markdown
---

## Implementation Notes

**Completed:** [Date]
**Commits:** [commit hashes]

### What Was Done
- [Summary]

### Deviations from Plan
- [Any changes from spec]

### Issues Encountered
- [Problems and resolutions]
```

3. Move task file to `Phase{N}/Tasks/Completed/`
4. Update phase journal
5. Update main journal (2-3 lines)
6. Commit with message: `[Phase{N}/T##] Complete [task name]`

### 2.3 Resuming Work

1. Read main journal (last few entries)
2. Read phase journal for the phase you're working on
3. Check task status in `Phase{N}/Tasks/README.md`
4. Continue from where you left off

---

## 3. Working with Backlogs

### 3.1 What Backlogs Are

`/Docs/VAYRON/Backlogs/` contains work items **not yet assigned to any phase**.

```
Backlogs/
├── README.md
└── Improvements/
    ├── IMP-001-SyncBlock-Recycle-Hook.md
    ├── IMP-002-JIT-Helper-Interception.md
    └── ...
```

**Improvements/** contains future enhancements that were identified during phase planning but explicitly deferred.

### 3.2 How to Work with Backlogs

#### Adding to Backlog

When you discover something that should be done later:

1. Create a new file in appropriate subfolder
2. Use naming convention: `IMP-###-Short-Name.md`
3. Include:
   - Origin (what decision/task created this)
   - Target phase (when it makes sense)
   - Current state vs proposed
   - Implementation tasks

**Example:** While implementing T02, you find a better way to handle SyncBlock recycling but it's not needed for Phase 1. Create `IMP-001-SyncBlock-Recycle-Hook.md`.

#### Picking from Backlog

When a backlog item is scheduled for a phase:

1. Move the file to `/Docs/VAYRON/Phase{N}/Tasks/`
2. Rename to fit task numbering: `T##-Task-Name.md`
3. Update phase's task README
4. Update backlog README to show it's been picked

### 3.3 How NOT to Work with Backlogs

**DO NOT:**
- ❌ Implement backlog items without assigning to a phase first
- ❌ Add items that belong in current phase's tasks
- ❌ Delete items without implementing or documenting why
- ❌ Modify items without noting the change and reason

**Backlog items are not a dumping ground.** They are tracked future work with clear origins and purposes.

### 3.4 Backlog vs Task

| Aspect | Backlog Item | Task |
|--------|--------------|------|
| Assigned to phase | No | Yes |
| Has due date | No | Implicit (phase completion) |
| Blocking current work | No | Possibly |
| Implementation planned | Roughly | Detailed |

---

## 4. Work Protocol Summary

### Before Starting Any Work

1. ✅ Read this document
2. ✅ Read main journal (recent entries)
3. ✅ Read phase journal (if exists)
4. ✅ Identify current task from `Phase{N}/Tasks/README.md`

### During Work

1. ✅ Follow task file instructions
2. ✅ Update phase journal each session
3. ✅ Note discoveries and decisions
4. ✅ Create backlog items for deferred work

### After Completing a Task

1. ✅ Verify acceptance criteria
2. ✅ Add implementation notes to task file
3. ✅ Move task to Completed/
4. ✅ Update phase journal
5. ✅ Update main journal (2-3 lines)
6. ✅ Commit with proper message

### When Stopping Mid-Task

1. ✅ Update phase journal with current state
2. ✅ Note exactly where to resume
3. ✅ Commit any work in progress (WIP commit OK)

---

## 5. Journal Maintenance Checklist

Use this checklist to ensure journals stay useful:

**Main Journal (`journal.md`):**
- [ ] Entry for every completed task
- [ ] Entry for every major decision
- [ ] Entry for every phase transition
- [ ] Entries are 2-3 lines, not essays
- [ ] "Next" is always stated

**Phase Journal (`phase{N}-journal.md`):**
- [ ] Entry for every work session
- [ ] "What I Did" is specific
- [ ] "What I Learned" captures discoveries
- [ ] "Next Session" is actionable
- [ ] Blockers are documented

---

## 6. Quick Reference

### File Locations

| File | Purpose |
|------|---------|
| `/Docs/VAYRON/journal.md` | Cross-phase progress |
| `/Docs/VAYRON/Phase{N}/phase{N}-journal.md` | Detailed phase tracking |
| `/Docs/VAYRON/Phase{N}/Tasks/README.md` | Task status and order |
| `/Docs/VAYRON/Phase{N}/Tasks/T##-*.md` | Task specifications |
| `/Docs/VAYRON/Phase{N}/Tasks/Completed/` | Finished tasks |
| `/Docs/VAYRON/Backlogs/Improvements/` | Deferred improvements |

### Commit Message Format

```
[Phase{N}/T##] Brief description

Longer description if needed.

https://claude.ai/code/session_XXX
```

### Journal Entry Format (Main)

```markdown
## [Date] - [Task or Milestone]

[2-3 sentences: what done, key outcome, what's next]

---
```

### Journal Entry Format (Phase)

```markdown
## [Date] - [Session Focus]

### What I Did
### What I Learned
### Blockers / Issues
### Next Session

---
```

---

*Keep the journals updated. Future you (or the next AI) will thank you.*
