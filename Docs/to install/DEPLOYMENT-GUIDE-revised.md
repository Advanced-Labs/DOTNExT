# DOTNExT Documentation Deployment Guide

## Overview

This guide explains how to deploy all DOTNExT documentation and systems to your VMR repository.

**Created:** 2025-12-04  
**Updated:** 2025-12-05  
**GitHub:** `Advanced-Labs/DOTNExT`

---

## Files Created

### Root Files (VMR Root: `D:\Dev\DOTNExT\`)

| File | Size | Description |
|------|------|-------------|
| **CLAUDE.md** | 10K | Agent constitution - Claude Code reads this automatically |
| **Manage-Contexts.ps1** | 11K | PowerShell script to manage context system |

### AI Agent Documentation (`D:\Dev\DOTNExT\Docs\For AI\`)

| File | Size | Description |
|------|------|-------------|
| **DOTNExT-Index.md** | 8K | Master documentation index |
| **DOTNExT-Master-Reference.md** | 14K | Comprehensive technical reference |
| **DOTNExT-Agent-Roles.md** | 17K | Role definitions & architecture |
| **DOTNExT-Workflows.md** | 12K | Step-by-step workflow scenarios |
| **DOTNExT-Terminology.md** | 6.5K | Shared language & conventions |
| **SAGE-role-prompt.md** | 9K | Platform R&D Expert |
| **BUILD-role-prompt.md** | 6K | Build Master |
| **DEPLOY-role-prompt.md** | 8K | Deployment Operations |
| **TEST-role-prompt.md** | 6.5K | Test Runner |
| **REPO-role-prompt.md** | 6.5K | Repository Manager |
| **CODE-role-prompt.md** | 7K | Implementer |

### Context Templates (`D:\Dev\DOTNExT\Docs\For AI\contexts-templates\`)

| File | Size | Description |
|------|------|-------------|
| **Contexts-Setup-Guide.md** | 8K | Context system management |
| **STATUS-template.md** | 2K | Template for context STATUS.md |
| **role-state-template.md** | 3K | Template for role state files |

**Total:** ~125KB of documentation and tooling

---

## Deployment Steps

### Step 1: Create Directory Structure

```powershell
# Run from D:\Dev\DOTNExT (VMR root)
New-Item -ItemType Directory -Path "Docs\For AI\contexts-templates" -Force
New-Item -ItemType Directory -Path "Contexts" -Force
```

### Step 2: Copy Root Files

Copy to VMR root (`D:\Dev\DOTNExT\`):
- `CLAUDE.md` ← **Critical: Claude Code CLI reads this automatically**
- `Manage-Contexts.ps1`

### Step 3: Copy AI Agent Documentation

Copy to `D:\Dev\DOTNExT\Docs\For AI\`:
- All `DOTNExT-*.md` files
- All `*-role-prompt.md` files

### Step 4: Copy Context Templates

Copy to `D:\Dev\DOTNExT\Docs\For AI\contexts-templates\`:
- `Contexts-Setup-Guide.md`
- `STATUS-template.md`
- `role-state-template.md`

### Step 5: Initialize Contexts System

```powershell
cd D:\Dev\DOTNExT
.\Manage-Contexts.ps1 -Action init
```

This creates:
- `D:\Dev\DOTNExT\Contexts\` folder
- First context folder `001 - YYYY-MM-DD`
- `LATEST.txt` pointer
- Initial `STATUS.md`

---

## Final Folder Structure

After deployment:

```
D:\Dev\DOTNExT\
├── CLAUDE.md                              ← ROOT: Agent constitution
├── Manage-Contexts.ps1                    ← ROOT: Context management
│
├── Contexts/                              ← Context continuity system
│   ├── LATEST.txt
│   └── 001 - YYYY-MM-DD/
│       ├── STATUS.md
│       ├── SAGE/, BUILD/, DEPLOY/, TEST/, REPO/, CODE/
│       └── shared/
│
└── Docs/
    ├── For AI/                            ← AI AGENT DOCUMENTATION
    │   ├── DOTNExT-Index.md
    │   ├── DOTNExT-Master-Reference.md
    │   ├── DOTNExT-Agent-Roles.md
    │   ├── DOTNExT-Workflows.md
    │   ├── DOTNExT-Terminology.md
    │   ├── SAGE-role-prompt.md
    │   ├── BUILD-role-prompt.md
    │   ├── DEPLOY-role-prompt.md
    │   ├── TEST-role-prompt.md
    │   ├── REPO-role-prompt.md
    │   ├── CODE-role-prompt.md
    │   └── contexts-templates/
    │       ├── Contexts-Setup-Guide.md
    │       ├── STATUS-template.md
    │       └── role-state-template.md
    │
    ├── Repo Map/                          ← AI-GENERATED REPO ANALYSIS
    │   └── (already exists)
    │
    ├── Async+/                            ← OUR MODIFICATIONS
    │   └── (already exists)
    │
    ├── New Orleans/                       ← OUR MODIFICATIONS
    │   └── (already exists)
    │
    ├── New Roslyn/                        ← OUR MODIFICATIONS
    │   └── (already exists)
    │
    └── Pre Fork Docs - All projects/      ← ORIGINAL DOCS (NEVER MODIFY)
        └── (already exists)
```

---

## Verification Checklist

After deployment, verify:

- [ ] `D:\Dev\DOTNExT\CLAUDE.md` exists
- [ ] `D:\Dev\DOTNExT\Manage-Contexts.ps1` exists
- [ ] `D:\Dev\DOTNExT\Docs\For AI\` contains all docs
- [ ] `D:\Dev\DOTNExT\Docs\For AI\contexts-templates\` contains 3 template files
- [ ] `D:\Dev\DOTNExT\Contexts\` exists with initial context folder
- [ ] `D:\Dev\DOTNExT\Contexts\LATEST.txt` points to context folder

**Quick test:**
```powershell
cd D:\Dev\DOTNExT
.\Manage-Contexts.ps1 -Action status
```

Should show the active context and folder structure.

---

## Testing with Claude Code CLI

### Test 1: Verify CLAUDE.md is Read

1. Open Claude Code CLI in `D:\Dev\DOTNExT`
2. Ask: "What project is this?"
3. Claude should know it's DOTNExT and mention the multi-agent system

### Test 2: Verify Context System

1. Open Claude Code CLI
2. Ask: "What's the active context folder?"
3. Claude should run the PowerShell command and report the active folder

### Test 3: Verify Documentation Awareness

1. Ask: "Where are the Repo Map docs?"
2. Claude should respond with `/Docs/Repo Map/`
3. Ask: "What files document our modifications?"
4. Claude should mention Async+, New Orleans, New Roslyn

### Test 4: Verify Role Loading

1. Tell Claude: "You are now the SAGE role"
2. Ask Claude to read `/Docs/For AI/SAGE-role-prompt.md`
3. Verify Claude understands SAGE responsibilities and documentation locations

---

## Using the System

### Starting a Work Session

1. Open Claude Code CLI(s) in VMR root
2. Assign roles as needed: "You are the BUILD role"
3. Have each agent read their role prompt
4. Verify they've read the active context

### Creating a New Context

```powershell
.\Manage-Contexts.ps1 -Action new
```

Then tell agents: "New context created. Recontextualize from [folder name]"

### Checking Status

```powershell
.\Manage-Contexts.ps1 -Action status
```

### Listing All Contexts

```powershell
.\Manage-Contexts.ps1 -Action list
```

---

## Documentation Rules Reminder

**NEVER MODIFY:**
- `/Docs/Pre Fork Docs - All projects/*` - Original upstream docs
- `/Docs/*/Original * Internals/*` - Reference copies

**Document our modifications in:**
- `/Docs/New */` folders (runtime, roslyn, etc.)
- `/Docs/Async+/` for Async+ features
- Use modification header format (changes listed at TOP)

**Modification Header Format:**
```markdown
# [Topic Name]

## ⚡ DOTNExT Modifications

| Modification | Section | Reason |
|--------------|---------|--------|
| [Change 1] | [Section] | [Why] |

---
[Rest of document...]
```

---

## Maintenance

### Updating Documentation

If documentation needs updating:
1. Edit the file in `Docs/For AI/`
2. Consider updating CLAUDE.md if changes affect all agents
3. Note significant changes in the active context's STATUS.md

### Archiving Old Contexts

Old context folders can be compressed and archived:
```powershell
# Archive contexts older than 30 days
$Cutoff = (Get-Date).AddDays(-30)
Get-ChildItem "D:\Dev\DOTNExT\Contexts" -Directory | 
    Where-Object { $_.Name -match '^\d{3}' } |
    Where-Object { $_.CreationTime -lt $Cutoff } |
    ForEach-Object {
        Compress-Archive -Path $_.FullName -DestinationPath "$($_.FullName).zip"
    }
```

---

## Quick Reference

| Task | Command/Action |
|------|----------------|
| Initialize system | `.\Manage-Contexts.ps1 -Action init` |
| New context folder | `.\Manage-Contexts.ps1 -Action new` |
| Check status | `.\Manage-Contexts.ps1 -Action status` |
| Find active context | `.\Manage-Contexts.ps1 -Action latest` |
| List all contexts | `.\Manage-Contexts.ps1 -Action list` |
| Assign role | Tell Claude: "You are the [ROLE] role" |
| Load role prompt | Have Claude read `/Docs/For AI/[ROLE]-role-prompt.md` |

---

*This guide is part of the DOTNExT project documentation suite.*
