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
├── For AI/                          ← AI agent prompts & project docs (THIS PROJECT)
│   ├── DOTNExT-Index.md             ← Master documentation index
│   ├── DOTNExT-Master-Reference.md  ← Comprehensive technical reference
│   ├── DOTNExT-Agent-Roles.md       ← Role definitions & architecture
│   ├── DOTNExT-Workflows.md         ← Step-by-step workflow scenarios
│   ├── DOTNExT-Terminology.md       ← Shared language & conventions
│   ├── SAGE-role-prompt.md          ← Platform R&D Expert
│   ├── BUILD-role-prompt.md         ← Build Master
│   ├── DEPLOY-role-prompt.md        ← Deployment Operations
│   ├── TEST-role-prompt.md          ← Test Runner
│   ├── REPO-role-prompt.md          ← Repository Manager
│   └── CODE-role-prompt.md          ← Implementer
│
├── Repo Map/                        ← AI-generated repository analysis (VALUABLE)
│   ├── README.md                    ← Start here for repo understanding
│   ├── SUMMARY.md                   ← Condensed overview
│   ├── 00-Repository-Overview.md    ← High-level structure
│   ├── 01-Directory-Structure.md    ← Folder organization
│   ├── 02-CoreCLR-Guide.md          ← CLR internals guide
│   ├── 03-Mono-Runtime-Guide.md     ← Mono runtime guide
│   ├── 04-Libraries-Guide.md        ← BCL libraries guide
│   ├── 05-Native-And-Hosting.md     ← Native code & hosting
│   ├── 06-Build-System.md           ← Build infrastructure
│   ├── 07-Testing-Guide.md          ← Testing approaches
│   ├── 08-Feature-Location-Reference.md  ← WHERE to find things
│   ├── 09-Contribution-Workflows.md ← How to contribute
│   ├── 10-Architecture-Concepts.md  ← Architectural patterns
│   ├── 11-Major-Subsystem-Integration.md ← How subsystems connect
│   ├── 12-Component-Dependencies.md ← Dependency graph
│   ├── 13-Modification-Impact-Zones.md   ← WHAT changes affect WHAT
│   └── 14-Extension-Points-Catalog.md    ← WHERE to extend
│
├── Async+/                          ← OUR MODIFICATIONS: Async+ enhancements
│   └── Async+.md                    ← Async+ feature documentation
│
├── New Orleans/                     ← OUR MODIFICATIONS: Orleans fork
│   ├── New Orleans.md               ← Overview of our Orleans changes
│   ├── New Orleans Features/        ← Our new features
│   │   ├── DynamicGrainAccess.md    ← Dynamic grain access feature
│   │   ├── OrleansAsync+.md         ← Async+ integration with Orleans
│   │   └── PluginGrainArchitecture.md ← Plugin grain system
│   ├── Original Orleans Internals/  ← Reference docs (DO NOT MODIFY)
│   └── Researches/                  ← Research notes
│
├── New Roslyn/                      ← OUR MODIFICATIONS: Roslyn fork
│   └── AI Analysis of Original Roslyn REPO Sources/  ← AI-generated analysis
│
├── New dotnet-runtime/              ← OUR MODIFICATIONS: Runtime fork
│   └── [modification docs go here]
│
└── Pre Fork Docs - All projects/    ← ORIGINAL DOCS (NEVER MODIFY)
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

## ⚡ DOTNExT Modifications

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
- `/Docs/New Orleans/New Orleans.md` - Orleans modifications overview
- `/Docs/New Orleans/New Orleans Features/` - Individual feature docs

**For AI agent operations:**
- `/Docs/For AI/DOTNExT-Master-Reference.md` - Technical reference
- `/Docs/For AI/DOTNExT-Workflows.md` - Step-by-step procedures
- `/Docs/For AI/[ROLE]-role-prompt.md` - Role-specific instructions

---

## Context Continuity System

### The Problem

Claude Code sessions have limited context windows. When context fills up or gets "compacted" (summarized), critical details are lost. This breaks continuity across sessions and within long sessions.

### The Solution: `/Contexts/` Folder

This repository maintains a `/Contexts/` folder structure for persistent, shared context:

```
D:\Dev\DOTNExT\Contexts\
├── 001 - 2025-12-04/
│   ├── STATUS.md                 # Overall context state
│   ├── SAGE/
│   │   └── state.md              # SAGE role state
│   ├── BUILD/
│   ├── DEPLOY/
│   ├── TEST/
│   ├── REPO/
│   ├── CODE/
│   └── shared/
├── 002 - 2025-12-06/
│   └── ...
└── LATEST.txt                    # Points to active context folder
```

### Finding the Active Context

**Always determine the active context folder first.** Run this PowerShell command:

```powershell
Get-ChildItem -Path "D:\Dev\DOTNExT\Contexts" -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
```

Or check `D:\Dev\DOTNExT\Contexts\LATEST.txt` which contains the active folder name.

### Context Folder Rules

1. **Only Louis can create new context folders.** If Louis says "start new context" or "new context folder", create one with the next sequence number and current date.

2. **On session start or after compaction:**
   - Identify the active context folder
   - Read `STATUS.md` in that folder for current state
   - Read your role's subfolder for role-specific context
   - Read `shared/` for cross-cutting information

3. **During work:**
   - Update your role's subfolder with significant progress, decisions, issues
   - Update `STATUS.md` with major state changes
   - Put cross-role information in `shared/`

4. **When compaction occurs** (you receive a "summary of previous conversation"):
   - Immediately identify and read the active context folder
   - This restores context that compaction destroyed
   - Note in your role folder that compaction occurred

5. **Never move files between context folders.** Copy or transcribe instead. Previous context folders are historical records.

6. **Check previous context folder** when starting a new one. It may contain:
   - Unfinished work
   - Decisions that still apply
   - Context worth carrying forward

### Context Folder Contents

**STATUS.md** (in context root):
```markdown
# Context Status
Last Updated: [timestamp]
Active Roles: [which roles are currently engaged]
Current Focus: [what the team is working on]
Blockers: [any blocking issues]
Next Steps: [planned actions]
```

**Role subfolders** contain:
- `state.md` - Current state of work in this role
- `decisions.md` - Decisions made and rationale
- `issues.md` - Problems encountered
- Other files as needed

**shared/** contains:
- Cross-cutting information
- Handoff notes between roles
- Shared resources

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
│   ├── NewOrleans/     # Orleans fork (Louis's)
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

1. **Read active context folder** on session start and after compaction
2. **Update context folder** with significant progress
3. **Use escalation protocol** when you need another role
4. **Check documentation** before making assumptions
5. **Report status clearly** using standard terminology
6. **Stay within your role's boundaries** - do what you're good at
7. **Preserve original docs** - never modify Pre Fork or Original docs
8. **Document modifications properly** - header format with changes listed at top

### Never Do

1. **Never switch roles yourself** - Louis orchestrates role changes
2. **Never assume context survived compaction** - Always reread from Contexts/
3. **Never move files from old context folders** - Copy or transcribe
4. **Never create new context folders** without Louis's request
5. **Never make architectural decisions** without Louis's approval
6. **Never proceed when blocked** - Escalate instead
7. **Never modify files in Pre Fork Docs** - These are upstream originals
8. **Never modify Original X Internals docs** - These are reference copies

---

## Command Quick Reference

```powershell
# Find active context folder
Get-ChildItem "D:\Dev\DOTNExT\Contexts" -Directory | Sort-Object Name -Descending | Select-Object -First 1

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

---

## Session Startup Checklist

When you begin a session or recover from compaction:

1. [ ] Determine your role (ask Louis if unclear)
2. [ ] Find active context folder (run PowerShell command)
3. [ ] Read `Contexts/<active>/STATUS.md`
4. [ ] Read `Contexts/<active>/<your-role>/state.md`
5. [ ] Read `Contexts/<active>/shared/` if relevant
6. [ ] Review previous context folder if this is a new one
7. [ ] Confirm with Louis: "I've recontextualized. Current state is [X]. Ready to continue."

---

## Getting Help

- **Repo structure questions:** `/Docs/Repo Map/` files
- **Workflow questions:** SAGE role or `/Docs/For AI/DOTNExT-Workflows.md`
- **Technical details:** `/Docs/For AI/DOTNExT-Master-Reference.md`
- **Our modifications:** `/Docs/Async+/`, `/Docs/New Orleans/`, `/Docs/New Roslyn/`
- **Terminology:** `/Docs/For AI/DOTNExT-Terminology.md`
- **Stuck or confused:** Escalate to Louis with clear description

---

*You are part of a team working on something ambitious. Stay focused, stay in your lane, keep context alive, communicate clearly, and respect the documentation organization. Louis is counting on you.*
