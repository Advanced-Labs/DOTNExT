# AGENTS.md

Repository-wide guidance for AI coding agents.

## Scope

These instructions apply to the whole repository.

## Scynapse Orientation Protocol (Required)

If a task touches `Docs/Scynapse` or Scynapse runtime/design work:

1. Read `Docs/Scynapse/Design/EXECUTIVE-MEMORY.md` first.
2. Read `Docs/Scynapse/Design/METHODOLOGY.md` second.
3. Read `Docs/Scynapse/Design/M0-Status-Checkpoint.md` third.
4. If present, read `Docs/Scynapse/Design/M1-Status-Checkpoint.md` before coding.
5. Use these files as the authoritative re-entry context before coding or drafting.

## Session Hygiene (Required for Scynapse Work)

At the end of each significant session:

1. Update `M0-Status-Checkpoint.md` (done/doing/next).
2. Update `EXECUTIVE-MEMORY.md` if current focus, priorities, or open decisions changed.
3. Append a short entry to `SESSION-LOG.md`.
4. Ensure new artifacts are linked from the relevant index/skeleton docs.

## Compaction Warning Protocol (Required for Scynapse Work)

When user indicates context is nearing compaction, or thread complexity is high enough that compaction risk is likely:

1. Pause new design/coding changes unless critical.
2. Perform a continuity refresh pass in this order:
   - `EXECUTIVE-MEMORY.md` (done/doing/next, open decisions, branch status)
   - `M0-Status-Checkpoint.md` (milestone-level status alignment)
   - `SESSION-LOG.md` (append current session summary)
3. Link any newly created artifacts from checkpoint/skeleton/index docs.
4. Commit continuity updates in a focused commit before resuming implementation.

## Naming Conventions (Scynapse)

The active shorthand system is documented in:

1. `Docs/Scynapse/Design/METHODOLOGY.md`

Quick map:

1. `M*` = milestone (`M0`, `M1`, etc)
2. `S*` = implementation slice (`S1`, `S2`, etc)
3. `TV-*` = test vector id
4. `W*` = workstream id

## Non-Scynapse Tasks

If task scope does not involve Scynapse, follow local instructions (including deeper `agents.md` files) and repository norms.
