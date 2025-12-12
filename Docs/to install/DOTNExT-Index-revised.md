# DOTNExT Documentation Index

## Document Purpose

Master index of all DOTNExT project documentation. This is where to start.

**Created:** 2025-12-04  
**Updated:** 2025-12-05  
**Project:** DOTNExT - Custom .NET Platform Fork  
**GitHub:** `Advanced-Labs/DOTNExT`

---

## Documentation Philosophy

### Original vs Modified Documentation

This project maintains a strict separation:

| Category | Location | Rule |
|----------|----------|------|
| **Original upstream docs** | `/Docs/Pre Fork Docs - All projects/` | NEVER MODIFY |
| **Original internals refs** | `/Docs/*/Original * Internals/` | NEVER MODIFY |
| **Our modifications** | `/Docs/New */`, `/Docs/Async+/`, etc. | Document here |
| **AI agent docs** | `/Docs/For AI/` | Agent prompts & project docs |
| **Repo analysis** | `/Docs/Repo Map/` | AI-generated repo understanding |

### Modification Documentation Format

When documenting OUR changes to .NET, always:

1. Create new files in the appropriate modification folder
2. If based on an original doc, COPY it (don't modify the original)
3. Add a **"⚡ DOTNExT Modifications"** section at the TOP listing:
   - What was changed
   - Where to find the changes in the document
   - Why the changes were made

---

## Complete Documentation Structure

```
D:\Dev\DOTNExT\
├── CLAUDE.md                              ← ROOT: AI agent constitution
├── Manage-Contexts.ps1                    ← ROOT: Context management script
│
├── Contexts/                              ← Context continuity system
│   ├── LATEST.txt
│   └── ### - YYYY-MM-DD/
│       ├── STATUS.md
│       ├── SAGE/, BUILD/, DEPLOY/, TEST/, REPO/, CODE/
│       └── shared/
│
└── Docs/
    │
    ├── For AI/                            ← AI AGENT DOCUMENTATION
    │   ├── DOTNExT-Index.md               ← You are here
    │   ├── DOTNExT-Master-Reference.md    ← Technical reference
    │   ├── DOTNExT-Agent-Roles.md         ← Role definitions
    │   ├── DOTNExT-Workflows.md           ← Step-by-step procedures
    │   ├── DOTNExT-Terminology.md         ← Shared language
    │   ├── SAGE-role-prompt.md            ← Platform R&D Expert
    │   ├── BUILD-role-prompt.md           ← Build Master
    │   ├── DEPLOY-role-prompt.md          ← Deployment Operations
    │   ├── TEST-role-prompt.md            ← Test Runner
    │   ├── REPO-role-prompt.md            ← Repository Manager
    │   └── CODE-role-prompt.md            ← Implementer
    │
    ├── Repo Map/                          ← AI-GENERATED REPO ANALYSIS
    │   ├── README.md                      ← Start here
    │   ├── SUMMARY.md                     ← Quick overview
    │   ├── 00-Repository-Overview.md      ← High-level structure
    │   ├── 01-Directory-Structure.md      ← Folder organization
    │   ├── 02-CoreCLR-Guide.md            ← CLR internals
    │   ├── 03-Mono-Runtime-Guide.md       ← Mono runtime
    │   ├── 04-Libraries-Guide.md          ← BCL libraries
    │   ├── 05-Native-And-Hosting.md       ← Native & hosting
    │   ├── 06-Build-System.md             ← Build infrastructure
    │   ├── 07-Testing-Guide.md            ← Testing approaches
    │   ├── 08-Feature-Location-Reference.md   ← ★ WHERE to find things
    │   ├── 09-Contribution-Workflows.md   ← How to contribute
    │   ├── 10-Architecture-Concepts.md    ← Architectural patterns
    │   ├── 11-Major-Subsystem-Integration.md  ← Subsystem connections
    │   ├── 12-Component-Dependencies.md   ← Dependency graph
    │   ├── 13-Modification-Impact-Zones.md    ← ★ Change impact analysis
    │   └── 14-Extension-Points-Catalog.md     ← ★ Where to extend
    │
    ├── Async+/                            ← OUR MODIFICATION: Async+
    │   └── Async+.md                      ← Async+ feature documentation
    │
    ├── New Orleans/                       ← OUR MODIFICATION: Orleans
    │   ├── New Orleans.md                 ← Overview of our changes
    │   ├── New Orleans Features/          ← Our new features
    │   │   ├── DynamicGrainAccess.md      ← Dynamic grain access
    │   │   ├── OrleansAsync+.md           ← Async+ in Orleans
    │   │   └── PluginGrainArchitecture.md ← Plugin grain system
    │   ├── Original Orleans Internals/    ← Reference (DO NOT MODIFY)
    │   │   ├── 00-index.md
    │   │   ├── 01-paradigms-and-concepts.md
    │   │   ├── 02-systems-and-subsystems.md
    │   │   ├── ... (12 files)
    │   │   └── OrleansDistributedGrainDirectory.md
    │   └── Researches/                    ← Research notes
    │
    ├── New Roslyn/                        ← OUR MODIFICATION: Roslyn
    │   └── AI Analysis of Original Roslyn REPO Sources/
    │       ├── README.md
    │       ├── 01-repository-structure.md
    │       ├── 02-component-guide.md
    │       ├── 03-feature-location-guide.md
    │       ├── 04-architecture.md
    │       ├── 05-developer-guide.md
    │       └── 06-compiler-internals.md
    │
    ├── New dotnet-runtime/                ← OUR MODIFICATION: Runtime
    │   └── [modification docs as created]
    │
    └── Pre Fork Docs - All projects/      ← ORIGINAL DOCS (NEVER MODIFY)
        ├── aspire/                        ← Aspire docs
        ├── aspnetcore/                    ← ASP.NET Core docs
        ├── command-line-api/              ← CLI API docs
        ├── deployment-tools/              ← Deployment docs
        ├── efcore/                        ← EF Core docs
        ├── fsharp/                        ← F# docs (extensive)
        ├── msbuild/                       ← MSBuild docs (extensive)
        ├── nuget-client/                  ← NuGet docs
        ├── roslyn/                        ← Roslyn docs (extensive)
        ├── runtime/                       ← Runtime docs (extensive)
        ├── sdk/                           ← SDK docs (extensive)
        ├── source-build/                  ← Source build docs
        ├── templating/                    ← Templating docs
        ├── vstest/                        ← VSTest docs (extensive)
        ├── winforms/                      ← WinForms docs
        └── wpf/                           ← WPF docs
```

---

## Quick Reference by Task

### "I need to understand the repo structure"
1. `/Docs/Repo Map/README.md` - Start here
2. `/Docs/Repo Map/00-Repository-Overview.md` - High-level view
3. `/Docs/Repo Map/01-Directory-Structure.md` - Folder layout

### "I need to find where a feature is implemented"
1. `/Docs/Repo Map/08-Feature-Location-Reference.md` - Feature locations
2. `/Docs/Repo Map/02-CoreCLR-Guide.md` - If CLR-related
3. `/Docs/Repo Map/04-Libraries-Guide.md` - If BCL-related

### "I'm about to make a change and want to understand impact"
1. `/Docs/Repo Map/13-Modification-Impact-Zones.md` - Impact analysis
2. `/Docs/Repo Map/12-Component-Dependencies.md` - Dependencies
3. `/Docs/Repo Map/11-Major-Subsystem-Integration.md` - Subsystem connections

### "I want to extend .NET with a new feature"
1. `/Docs/Repo Map/14-Extension-Points-Catalog.md` - Extension points
2. `/Docs/Repo Map/10-Architecture-Concepts.md` - Patterns to follow

### "I need to understand our modifications"
1. `/Docs/Async+/Async+.md` - Async+ feature
2. `/Docs/New Orleans/New Orleans.md` - Orleans overview
3. `/Docs/New Orleans/New Orleans Features/` - Specific features

### "I'm an AI agent and need role guidance"
1. `/CLAUDE.md` - Foundation (READ FIRST)
2. `/Docs/For AI/DOTNExT-Master-Reference.md` - Technical details
3. `/Docs/For AI/[ROLE]-role-prompt.md` - Your role's instructions

### "I need original upstream documentation"
1. `/Docs/Pre Fork Docs - All projects/[component]/` - Find the component
2. Remember: **NEVER MODIFY** these files

---

## Document Descriptions

### AI Agent Documentation (`/Docs/For AI/`)

| Document | Purpose |
|----------|---------|
| **DOTNExT-Index.md** | This file - navigation and organization |
| **DOTNExT-Master-Reference.md** | Comprehensive technical reference: VMR structure, build commands, environment variables, testing workflows, troubleshooting |
| **DOTNExT-Agent-Roles.md** | Role definitions, responsibilities, boundaries, escalation protocols, handoff procedures |
| **DOTNExT-Workflows.md** | 7 step-by-step workflow scenarios for common development tasks |
| **DOTNExT-Terminology.md** | Shared language, command shortcuts, state indicators |
| **Role prompts** | Detailed instructions for each specialized role |

### Repo Map (`/Docs/Repo Map/`)

AI-generated comprehensive analysis of the .NET runtime repository. **Highly valuable** for understanding the codebase.

| Document | Purpose |
|----------|---------|
| **README.md** | Entry point and overview |
| **SUMMARY.md** | Condensed quick reference |
| **00-Repository-Overview.md** | High-level repo structure |
| **01-Directory-Structure.md** | Detailed folder layout |
| **02-CoreCLR-Guide.md** | CLR/JIT/GC internals |
| **03-Mono-Runtime-Guide.md** | Mono runtime details |
| **04-Libraries-Guide.md** | BCL organization |
| **05-Native-And-Hosting.md** | Native code and hosting |
| **06-Build-System.md** | Build infrastructure |
| **07-Testing-Guide.md** | Testing approaches |
| **08-Feature-Location-Reference.md** | ★ Feature location finder |
| **09-Contribution-Workflows.md** | Contribution processes |
| **10-Architecture-Concepts.md** | Design patterns |
| **11-Major-Subsystem-Integration.md** | How parts connect |
| **12-Component-Dependencies.md** | Dependency mapping |
| **13-Modification-Impact-Zones.md** | ★ Change impact analysis |
| **14-Extension-Points-Catalog.md** | ★ Extension points |

### Our Modifications

| Location | Contents |
|----------|----------|
| `/Docs/Async+/` | Async+ enhancement feature |
| `/Docs/New Orleans/` | Orleans fork with custom features |
| `/Docs/New Orleans/New Orleans Features/` | DynamicGrainAccess, OrleansAsync+, PluginGrainArchitecture |
| `/Docs/New Roslyn/` | Roslyn modifications (when added) |
| `/Docs/New dotnet-runtime/` | Runtime modifications (when added) |

---

## Context Continuity System

The `/Contexts/` folder provides persistent context that survives session limits:

```
/Contexts/
├── LATEST.txt              ← Points to active context
└── 001 - 2025-12-04/       ← Context folders
    ├── STATUS.md           ← Overall state
    ├── SAGE/state.md       ← Role-specific state
    ├── BUILD/state.md
    ├── DEPLOY/state.md
    ├── TEST/state.md
    ├── REPO/state.md
    ├── CODE/state.md
    └── shared/             ← Cross-role information
```

**Key rules:**
- Only Louis creates new context folders
- Always check active context on session start
- Update context after significant progress
- Never move files between contexts (copy instead)

See `CLAUDE.md` for full context system documentation.

---

## Maintenance Notes

### Adding New Modification Documentation

1. Create in appropriate `/Docs/New */` folder
2. Use the modification header format (see CLAUDE.md)
3. Add entry to this index

### Updating Role Prompts

1. Edit in `/Docs/For AI/`
2. Consider if CLAUDE.md needs updates
3. Note changes in active context folder

### When Upstream Docs Change

1. Update files in `/Docs/Pre Fork Docs - All projects/`
2. Check if our modification docs need updates
3. Do NOT modify in-place; note changes in modification docs

---

*Last updated: 2025-12-05*
