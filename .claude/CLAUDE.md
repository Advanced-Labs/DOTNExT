# CLAUDE.md - DOTNExT Project Intelligence

> This file provides foundational context for all Claude Code CLI sessions working in this repository.
> Read this first. Always.

---

## Project Identity

**Project:** DOTNExT
**Type:** Custom fork of the .NET platform (VMR - Virtual Monolithic Repository)
**Location:** `D:\Dev\DOTNExT\`
**GitHub Origin:** `Advanced-Labs/DOTNExT`
**Purpose:** Development OF the .NET platform itself, not development WITH .NET

This repository contains the complete source code for the .NET SDK, runtime, compilers, and frameworks. You are working on the platform that other developers use to build software.

---

## Environment

### Detecting Your Environment

Check the platform to determine which environment you're in:
- **Windows**: `$env:OS` equals "Windows_NT", paths like `D:\Dev\DOTNExT\`
- **Linux**: Running in Claude Code Web session, paths like `/home/user/DOTNExT/`

### Windows Environment (Primary)

**You are running in:**
- **Claude Code CLI** on **Windows 11**
- **PowerShell** as the primary shell
- **Git Bash** is also available when needed
- All paths use Windows format: `D:\Dev\DOTNExT\`

**Shell preference:** Use PowerShell for most operations. Git Bash available for Unix-style commands when necessary.

### Linux Environment (Claude Code Web Sessions)

**When running in Claude Code Web:**
- Linux environment with Bash shell
- Restricted network (proxy blocks Azure DevOps feeds)
- Paths use Linux format: `/home/user/DOTNExT/`

**Automatic Setup:**
A SessionStart hook (`/.claude/hooks/session-start.sh`) runs automatically and:
- Sets required environment variables
- Checks for missing system dependencies
- Provides guidance if setup is needed

**If the hook reports missing packages, run the full setup:**
```bash
./.claude/scripts/setup-dotnext-env.sh
```

This script:
1. Installs system dependencies (libkrb5-dev, libicu-dev, liblttng-ust-dev)
2. Downloads Arcade SDK packages via wget (bypasses proxy)
3. Installs SDKs to the repo's local `.dotnet/` directory (with case fix for Linux)
4. Sets up local NuGet feed at `/tmp/nuget-feed/`

**Build commands for Linux:**
```bash
# Native CLR build (C++ only - works offline)
cd src/runtime/src/coreclr
./build-runtime.sh -component runtime -c Debug

# Output location
ls artifacts/bin/coreclr/linux.x64.Debug/
```

**Check setup status:**
```bash
./.claude/scripts/setup-dotnext-env.sh --status
```

**Build Capabilities in Linux/Web Environment:**

| Build Type | Status | Notes |
|------------|--------|-------|
| Native CLR (C++) | ✅ Works | Full offline build via CMake |
| Managed (C#) | ❌ Requires TAI | Proxy blocks .NET SDK's NuGet client |

**Why managed builds don't work:** The environment's proxy requires authentication that works with `wget` but not with the .NET SDK's HttpClient. Even nuget.org is blocked for NuGet restore operations. This is a fundamental infrastructure limitation.

**Workflow recommendation:**
1. Use local native builds to verify C++ code (TDS infrastructure, intrinsics, QCalls)
2. Coordinate with TAI for managed code (C#) verification and full integration testing
3. TAI has full network access on Windows and can run complete builds

---

## Team Structure

### The Orchestrator: Louis

**Louis is the human orchestrator of this project.** He is also known as "the User" in your interactions.

Louis's roles:
- Primary developer and architect
- Project manager and decision maker
- **Central relay between all agents** - All handoffs go through Louis
- Escalation handler - When you need another role, you request it from Louis

**Critical:** You do not switch roles yourself. You do not directly communicate with other agents. When you need work done by another role, you make an explicit request to Louis, who will route it appropriately.

### Agent Roles

This project uses specialized Claude Code CLI agents. Each role has focused expertise:

| Code | Role | Primary Function |
|------|------|------------------|
| SAGE | Platform R&D Expert | Workflow guidance, troubleshooting, "the expert" |
| BLD | Build Master | Execute builds for all components |
| DEP | Deploy Operations | Environment setup, artifact placement |
| TST | Test Runner | Test execution and validation |
| GIT | Repo Manager | Git operations, VMR management |
| IMP | Code Implementer | Write and debug code |

**If you don't know which role you are:** Ask Louis. If Louis hasn't specified, default to SAGE behavior (helpful expert) until clarified.

---

## Documentation System

### Overview

The `/Docs/` folder has a strict organizational principle:

```
D:\Dev\DOTNExT\Docs\
│
├── For AI/                          <- AI agent prompts & project docs (THIS PROJECT)
│   ├── DOTNExT-Index.md             <- Master documentation index
│   ├── DOTNExT-Master-Reference.md  <- Comprehensive technical reference
│   ├── DOTNExT-Agent-Roles.md       <- Role definitions & architecture
│   ├── DOTNExT-Workflows.md         <- Step-by-step workflow scenarios
│   ├── DOTNExT-Terminology.md       <- Shared language & conventions
│   ├── SAGE-role-prompt.md          <- Platform R&D Expert
│   ├── BUILD-role-prompt.md         <- Build Master
│   ├── DEPLOY-role-prompt.md        <- Deployment Operations
│   ├── TEST-role-prompt.md          <- Test Runner
│   ├── REPO-role-prompt.md          <- Repository Manager
│   └── CODE-role-prompt.md          <- Implementer
│
├── Repo Map/                        <- AI-generated repository analysis (VALUABLE)
│   ├── README.md                    <- Start here for repo understanding
│   ├── SUMMARY.md                   <- Condensed overview
│   ├── 00-Repository-Overview.md    <- High-level structure
│   ├── 01-Directory-Structure.md    <- Folder organization
│   ├── 02-CoreCLR-Guide.md          <- CLR internals guide
│   ├── 03-Mono-Runtime-Guide.md     <- Mono runtime guide
│   ├── 04-Libraries-Guide.md        <- BCL libraries guide
│   ├── 05-Native-And-Hosting.md     <- Native code & hosting
│   ├── 06-Build-System.md           <- Build infrastructure
│   ├── 07-Testing-Guide.md          <- Testing approaches
│   ├── 08-Feature-Location-Reference.md  <- WHERE to find things
│   ├── 09-Contribution-Workflows.md <- How to contribute
│   ├── 10-Architecture-Concepts.md  <- Architectural patterns
│   ├── 11-Major-Subsystem-Integration.md <- How subsystems connect
│   ├── 12-Component-Dependencies.md <- Dependency graph
│   ├── 13-Modification-Impact-Zones.md   <- WHAT changes affect WHAT
│   └── 14-Extension-Points-Catalog.md    <- WHERE to extend
│
├── Async+/                          <- OUR MODIFICATIONS: Async+ enhancements
│   └── Async+.md                    <- Async+ feature documentation
│
├── Scynapse/                     <- OUR MODIFICATIONS: Orleans fork
│   ├── Scynapse.md               <- Overview of our Orleans changes
│   ├── Scynapse Features/        <- Our new features
│   │   ├── DynamicGrainAccess.md    <- Dynamic grain access feature
│   │   ├── OrleansAsync+.md         <- Async+ integration with Orleans
│   │   └── PluginGrainArchitecture.md <- Plugin grain system
│   ├── Original Orleans Internals/  <- Reference docs (DO NOT MODIFY)
│   └── Researches/                  <- Research notes
│
├── New Roslyn/                      <- OUR MODIFICATIONS: Roslyn fork
│   └── AI Analysis of Original Roslyn REPO Sources/  <- AI-generated analysis
│
├── New dotnet-runtime/              <- OUR MODIFICATIONS: Runtime fork
│   └── [modification docs go here]
│
└── Pre Fork Docs - All projects/    <- ORIGINAL DOCS (NEVER MODIFY)
    ├── aspire/
    ├── aspnetcore/
    ├── efcore/
    ├── fsharp/
    ├── msbuild/
    ├── nuget-client/
    ├── roslyn/
    ├── runtime/
    ├── sdk/
    ├── winforms/
    ├── wpf/
    └── [other projects]/
```

### CRITICAL: Documentation Rules

**1. NEVER modify files in `/Pre Fork Docs - All projects/`**
These are original upstream docs preserved for reference. They must remain unchanged.

**2. NEVER modify files in `/Original X Internals/` folders**
These are reference copies of original documentation.

**3. When documenting OUR modifications:**
- Create new files in the appropriate `New X/` or feature folder
- If modifying an existing concept, COPY the original doc to the modification folder
- At the TOP of any modified doc, add a "DOTNExT Modifications" section listing:
  - What was changed
  - Where in the document to find the changes
  - Why the changes were made

**4. Modification doc header format:**
```markdown
# [Topic Name]

## DOTNExT Modifications

This document is based on the original [component] documentation with the following modifications:

| Modification | Section | Reason |
|--------------|---------|--------|
| [Change 1] | [Section name or line] | [Why] |
| [Change 2] | [Section name or line] | [Why] |

---

[Rest of document...]
```

### Key Documentation Files

**For understanding the repo structure:**
- `/Docs/Repo Map/08-Feature-Location-Reference.md` - WHERE to find features
- `/Docs/Repo Map/13-Modification-Impact-Zones.md` - Impact of changes
- `/Docs/Repo Map/14-Extension-Points-Catalog.md` - Where to extend

**For understanding our modifications:**
- `/Docs/Async+/Async+.md` - Async+ feature
- `/Docs/Scynapse/Scynapse.md` - Orleans modifications overview
- `/Docs/Scynapse/Scynapse Features/` - Individual feature docs

**For AI agent operations:**
- `/Docs/For AI/DOTNExT-Master-Reference.md` - Technical reference
- `/Docs/For AI/DOTNExT-Workflows.md` - Step-by-step procedures
- `/Docs/For AI/[ROLE]-role-prompt.md` - Role-specific instructions

---

## Context Continuity System

### The Problem

Claude Code sessions have limited context windows. When context fills up or gets "compacted" (summarized), critical details are lost. This breaks continuity across sessions and within long sessions.

### The Solution: `/Contexts/` Folder

The repository maintains a simplified context folder system designed for AI efficiency:

```
D:\Dev\DOTNExT\Contexts\
├── LATEST.txt                       <- Contains name of active context folder
└── 001 - 2025-12-04/                <- Active context folder
    ├── Current-Context.md           <- THE REBOOT FILE - read this first
    ├── CurrentPlan.md               <- Active tasks, planning, history
    ├── Past-Contexts-Appended.md    <- Archived contexts (append-only)
    ├── [Topic-Specific].md          <- Created as needed, descriptive names
    └── artifacts/                   <- Supporting files when needed
```

### Context Files Explained

**`Current-Context.md`** - The survival file:
- What we're focused on NOW
- Recent progress and issues
- Key decisions made
- Pointers to topic-specific files
- **Critical Survival Info section** - Everything that MUST survive context window death
- **Read this first on session start or after compaction**

**`CurrentPlan.md`** - The action file:
- Active tasks (in progress, pending)
- Completed tasks (KEEP these - valuable history)
- Research items
- Planning notes
- **Update frequently, never delete completed items**

**`Past-Contexts-Appended.md`** - The archive:
- Append-only historical record
- Archived via PowerShell command (not manual copy)
- Read via grep or paging when needed, not every session

**Topic-specific `.md` files** - Avoid bloat:
- Create when a topic needs dedicated space
- Use clear, descriptive names: `Hybrid-Workflow-Research.md`, `VSIX-Setup-Notes.md`
- Reference these from `Current-Context.md`
- Keep `Current-Context.md` focused as a hub/index

### Finding the Active Context

**Use the management script:**
```powershell
# Get active context path (for scripts/AI capture)
.\Manage-Contexts.ps1 -Action latest

# Show status with file info
.\Manage-Contexts.ps1 -Action status

# List all context folders
.\Manage-Contexts.ps1 -Action list
```

Or read `D:\Dev\DOTNExT\Contexts\LATEST.txt` directly.

### Context Lifecycle

**On session start:**
1. Get active context path: `.\Manage-Contexts.ps1 -Action latest`
2. Read `Current-Context.md` (the reboot file)
3. Read `CurrentPlan.md` to see active tasks
4. Read any topic files referenced in Current-Context.md

**During work:**
- Update `Current-Context.md` with significant progress, decisions, issues
- Update `CurrentPlan.md` as tasks progress (mark done, add new)
- Create topic-specific `.md` files when content grows large
- Put supporting files in `artifacts/`

**When Current-Context.md gets stale/bloated:**
Archive it and start fresh:
```powershell
.\Manage-Contexts.ps1 -Action archive
```
This appends the current content to `Past-Contexts-Appended.md` and reinitializes `Current-Context.md`.

**When starting a new context folder** (only when Louis requests):
```powershell
.\Manage-Contexts.ps1 -Action new
```

### Context Rules

1. **Only Louis can request new context folders.** Use `-Action new` only when Louis says so.
2. **Update context files frequently.** Every significant progress, decision, or issue.
3. **Keep completed tasks visible.** Don't delete from CurrentPlan.md - history matters.
4. **Create topic files freely.** Better than one bloated file. Use descriptive names.
5. **Archive rather than delete.** Use `-Action archive` to preserve and reset.
6. **Never assume context survived.** After compaction, re-read from Contexts/.

---

## VMR Structure Quick Reference

This is a Virtual Monolithic Repository containing:

```
D:\Dev\DOTNExT\
├── src/
│   ├── runtime/        # CLR, JIT, GC, BCL (System.*)
│   ├── roslyn/         # C#/VB compilers
│   ├── sdk/            # dotnet CLI, MSBuild tasks
│   ├── aspnetcore/     # ASP.NET Core
│   ├── wpf/            # Windows Presentation Foundation
│   ├── winforms/       # Windows Forms
│   ├── msbuild/        # MSBuild engine
│   ├── efcore/         # Entity Framework Core
│   ├── Scynapse/     # Orleans fork (Louis's)
│   └── ...             # Many more components
├── eng/                # Build infrastructure (Arcade)
├── Contexts/           # Context continuity system
├── Docs/               # Documentation (see structure above)
└── CLAUDE.md           # This file
```

**For detailed repo understanding:** Read `/Docs/Repo Map/` files, especially:
- `00-Repository-Overview.md` for high-level orientation
- `08-Feature-Location-Reference.md` for finding specific features
- `13-Modification-Impact-Zones.md` before making changes

---

## Core Workflows (Summary)

**For runtime/BCL changes:** corerun workflow
- Build: `build.cmd -subset clr+libs -c Release`
- Generate Core_Root: `src\tests\build.cmd generatelayoutonly`
- Test with: `corerun.exe app.dll`

**For Roslyn/compiler changes:** VSIX workflow
- Build: `Build.cmd -restore -build -c Release -deployExtensions`
- Deploy to VS experimental instance
- Test with: `devenv.exe /rootSuffix Exp`

**For SDK changes:** Dogfood workflow
- Build: `build.cmd -c Release`
- Use: `eng\dogfood.cmd` or set `DOTNET_ROOT`

**For full details:** See `/Docs/For AI/DOTNExT-Workflows.md`

---

## Escalation Protocol

When you need to hand off work or need another role:

```
REQUEST TO LOUIS: [What you need]

Completed: [What you accomplished]
Blocker/Need: [Why you're escalating]
Recommended: [Which role should handle this]
Context: [Relevant details]
```

**Example:**
```
REQUEST TO LOUIS: Build complete, ready for deployment.

Completed: Built runtime (clr+libs) Release configuration.
           Core_Root generated successfully.
Blocker/Need: Environment setup required before testing.
Recommended: DEP role to configure CORE_ROOT and validate.
Context: Testing JIT loop optimization. Test app at D:\Test\JitTest\
```

---

## Critical Behaviors

### Always Do

1. **Read context files** on session start and after compaction
2. **Update context files** with significant progress, decisions, issues
3. **Use escalation protocol** when you need another role
4. **Check documentation** before making assumptions
5. **Report status clearly** using standard terminology
6. **Stay within your role's boundaries** - do what you're good at
7. **Preserve original docs** - never modify Pre Fork or Original docs
8. **Create topic files** when content grows large - avoid bloated single files

### Never Do

1. **Never switch roles yourself** - Louis orchestrates role changes
2. **Never assume context survived compaction** - Always re-read from Contexts/
3. **Never delete completed tasks** from CurrentPlan.md - history matters
4. **Never create new context folders** without Louis's request
5. **Never make architectural decisions** without Louis's approval
6. **Never proceed when blocked** - Escalate instead
7. **Never modify files in Pre Fork Docs** - These are upstream originals
8. **Never modify Original X Internals docs** - These are reference copies

---

## Command Quick Reference

### Windows (PowerShell)

```powershell
# Context management
.\Manage-Contexts.ps1 -Action reboot    # RECOMMENDED: Full context dump for resurrection
.\Manage-Contexts.ps1 -Action latest    # Get active context path
.\Manage-Contexts.ps1 -Action status    # Show context status
.\Manage-Contexts.ps1 -Action archive   # Archive Current-Context and reset
.\Manage-Contexts.ps1 -Action new       # Create new context folder (Louis only)
.\Manage-Contexts.ps1 -Action list      # List all context folders

# Check git status
git status

# Build runtime
cd D:\Dev\DOTNExT\src\runtime
build.cmd -subset clr+libs -c Release

# Generate Core_Root
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release

# Build Roslyn with VSIX
cd D:\Dev\DOTNExT\src\roslyn
Build.cmd -restore -build -c Release -deployExtensions

# Verify SDK being used
dotnet --info
```

### Linux (Claude Code Web Sessions)

```bash
# Setup build environment (run once per session if needed)
./.claude/scripts/setup-dotnext-env.sh

# Check environment status
./.claude/scripts/setup-dotnext-env.sh --status

# Build native CLR runtime (C++)
cd src/runtime/src/coreclr
./build-runtime.sh -component runtime -c Debug

# Build output location
ls src/runtime/artifacts/bin/coreclr/linux.x64.Debug/

# Check git status
git status
```

---

## Session Startup / Context Compaction Recovery

**IMPORTANT:** If you receive a message starting with "This is a summary of the conversation so far" or similar - you have been compacted. Run the reboot command immediately.

**Quick Reboot (recommended):**
```powershell
.\Manage-Contexts.ps1 -Action reboot
```
This prints: CurrentPlan.md, Current-Context.md, file list, and orientation instructions.

**Manual Checklist:**
1. [ ] Run reboot command above OR manually read context files
2. [ ] Get active context: `.\Manage-Contexts.ps1 -Action latest`
3. [ ] Read `Current-Context.md` (the reboot file)
4. [ ] Read `CurrentPlan.md` for active tasks
5. [ ] Check for topic files mentioned in Current-Context.md
6. [ ] Confirm with Louis: "I've recontextualized. Current state is [X]. Ready to continue."

---

## Getting Help

- **Repo structure questions:** `/Docs/Repo Map/` files
- **Workflow questions:** SAGE role or `/Docs/For AI/DOTNExT-Workflows.md`
- **Technical details:** `/Docs/For AI/DOTNExT-Master-Reference.md`
- **Our modifications:** `/Docs/Async+/`, `/Docs/Scynapse/`, `/Docs/New Roslyn/`
- **Terminology:** `/Docs/For AI/DOTNExT-Terminology.md`
- **Stuck or confused:** Escalate to Louis with clear description

---

*You are part of a team working on something ambitious. Stay focused, stay in your lane, keep context alive, communicate clearly, and respect the documentation organization. Louis is counting on you.*
