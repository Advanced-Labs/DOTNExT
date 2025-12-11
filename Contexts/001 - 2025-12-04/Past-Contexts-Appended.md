# Past Contexts Archive

This file contains archived context snapshots. New entries are appended at the bottom.

Use `.\Manage-Contexts.ps1 -Action archive` to archive the current context.

---


---

# Archived: 2025-12-04 22:35

# Current Context

**Context:** 001 - 2025-12-04
**Initialized:** 2025-12-04 22:30
**Last Updated:** 2025-12-04 22:30

---

## Current Focus

Establishing hybrid development workflows for core .NET components (runtime, libs, Roslyn, SDK) so developers can focus on development rather than DevOps operations.

---

## Active State

**What we're working on:**
- Setting up the context continuity system (just completed restructuring)
- Planning hybrid workflows that combine corerun, VSIX, and SDK dogfood approaches

**Recent progress:**
- 2025-12-04 22:30 - Restructured context system (simplified from role-based to file-based)
- 2025-12-04 22:30 - Updated CLAUDE.md with new context instructions + environment info
- 2025-12-04 22:30 - Updated Manage-Contexts.ps1 with simplified structure + archive action

**Issues/Blockers:**
- None currently

---

## Key Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| Simplified context structure | Role-based subfolders too complex, hard to read/update | 2025-12-04 |
| Keep completed tasks visible | History valuable for understanding what was done | 2025-12-04 |
| Topic-specific .md files | Avoid bloating single files, easier to navigate | 2025-12-04 |
| Archive via PowerShell command | Saves context window vs manual transcription | 2025-12-04 |

---

## Topic Files

| File | Purpose |
|------|---------|
| CurrentPlan.md | Active tasks and planning |
| Hybrid-Workflow-Research.md | (To create) Research on corerun/VSIX/SDK workflows |

---

## Critical Survival Info

> Everything below this line MUST survive context window death.
> If starting fresh, read this section first.

### Project Goal
Establish organized hybrid development workflows for DOTNExT (custom .NET fork) so developers can build/test modifications to runtime, Roslyn, SDK without manual DevOps overhead.

### Key Workflow Types (from prior research)

**TIER 1 - corerun workflow (runtime/BCL):**
- Build: `build.cmd -subset clr+libs -c Release`
- Generate Core_Root: `src\tests\build.cmd generatelayoutonly`
- Test: `corerun.exe app.dll`

**TIER 3 - SDK dogfood workflow:**
- Build SDK, use `eng\dogfood.cmd` or set `DOTNET_ROOT`

**TIER 4 - VSIX workflow (Roslyn):**
- Build: `Build.cmd -restore -build -c Release -deployExtensions`
- Test: `devenv.exe /rootSuffix Exp`

### Context System
- Files: `Current-Context.md`, `CurrentPlan.md`, `Past-Contexts-Appended.md`
- Topic files created as needed with descriptive names
- Archive command: `.\Manage-Contexts.ps1 -Action archive`
- Never delete completed tasks from CurrentPlan.md

### Environment
- Claude Code CLI on Windows 11
- PowerShell primary, Git Bash available
- Working directory: `D:\Dev\DOTNExT\`

---

*Update frequently. This file is your reboot point.*
