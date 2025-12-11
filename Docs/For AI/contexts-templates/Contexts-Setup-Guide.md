# Contexts Folder Setup & Management Guide

## Overview

The `/Contexts/` folder system provides persistent, shared context that survives Claude Code session limits and context compaction. This document explains how to set it up and manage it.

---

## Initial Setup

### 1. Create the Contexts Folder Structure

Run this PowerShell script from the VMR root (`D:\Dev\DOTNExT\`):

```powershell
# Create Contexts folder and initial structure
$ContextsRoot = "D:\Dev\DOTNExT\Contexts"
$FirstContext = "001 - $(Get-Date -Format 'yyyy-MM-dd')"

# Create main folder
New-Item -ItemType Directory -Path $ContextsRoot -Force

# Create first context folder with all subfolders
$Subfolders = @("SAGE", "BUILD", "DEPLOY", "TEST", "REPO", "CODE", "shared")
foreach ($folder in $Subfolders) {
    New-Item -ItemType Directory -Path "$ContextsRoot\$FirstContext\$folder" -Force
}

# Create LATEST.txt pointer
$FirstContext | Out-File -FilePath "$ContextsRoot\LATEST.txt" -Encoding UTF8

Write-Host "Created context folder: $ContextsRoot\$FirstContext"
Write-Host "LATEST.txt points to: $FirstContext"
```

### 2. Initialize STATUS.md

Copy the STATUS template to the new context folder and fill it in:

```powershell
# Copy STATUS template (assuming templates are in place)
Copy-Item "D:\Dev\DOTNExT\Docs\For AI\contexts-templates\STATUS-template.md" `
          "D:\Dev\DOTNExT\Contexts\001 - $(Get-Date -Format 'yyyy-MM-dd')\STATUS.md"
```

### 3. Initialize Role State Files

For each role that will be active, create a state.md:

```powershell
$Roles = @("SAGE", "BUILD", "DEPLOY", "TEST", "REPO", "CODE")
$ContextFolder = "D:\Dev\DOTNExT\Contexts\001 - $(Get-Date -Format 'yyyy-MM-dd')"

foreach ($role in $Roles) {
    $StatePath = "$ContextFolder\$role\state.md"
    Copy-Item "D:\Dev\DOTNExT\Docs\For AI\contexts-templates\role-state-template.md" $StatePath
    # Replace placeholder with actual role name
    (Get-Content $StatePath) -replace '\[ROLE\]', $role | Set-Content $StatePath
}
```

---

## Creating a New Context Folder

**Only Louis should trigger this.** When Louis says "start new context" or "new context folder":

### PowerShell Script

```powershell
# Create new context folder
$ContextsRoot = "D:\Dev\DOTNExT\Contexts"

# Find next sequence number
$ExistingFolders = Get-ChildItem -Path $ContextsRoot -Directory | 
    Where-Object { $_.Name -match '^\d{3}' } |
    Sort-Object Name -Descending

if ($ExistingFolders) {
    $LastNum = [int]($ExistingFolders[0].Name.Substring(0, 3))
    $NextNum = $LastNum + 1
} else {
    $NextNum = 1
}

$NewFolder = "{0:D3} - {1}" -f $NextNum, (Get-Date -Format 'yyyy-MM-dd')
$NewPath = "$ContextsRoot\$NewFolder"

# Create structure
$Subfolders = @("SAGE", "BUILD", "DEPLOY", "TEST", "REPO", "CODE", "shared")
foreach ($folder in $Subfolders) {
    New-Item -ItemType Directory -Path "$NewPath\$folder" -Force
}

# Initialize STATUS.md
Copy-Item "$ContextsRoot\..\Docs\For AI\contexts-templates\STATUS-template.md" "$NewPath\STATUS.md"

# Update LATEST.txt
$NewFolder | Out-File -FilePath "$ContextsRoot\LATEST.txt" -Encoding UTF8

Write-Host "Created new context: $NewPath"
Write-Host "Updated LATEST.txt to: $NewFolder"
Write-Host ""
Write-Host "Remember to:"
Write-Host "1. Review previous context folder for carryover items"
Write-Host "2. Initialize STATUS.md with current state"
Write-Host "3. Create role state.md files as needed"
```

### What to Carry Forward

When creating a new context folder, review the previous one for:

1. **Unfinished work** → Copy relevant state to new folder
2. **Active decisions** → Transcribe decisions that still apply
3. **Blockers** → Carry forward if still blocked
4. **Handoff notes** → Important for continuity

**Never move files** - always copy or transcribe. The old folder is a historical record.

---

## Finding the Active Context

### Quick Command

```powershell
# Get active context folder name
Get-Content "D:\Dev\DOTNExT\Contexts\LATEST.txt"

# Get full path to active context
$Latest = Get-Content "D:\Dev\DOTNExT\Contexts\LATEST.txt"
"D:\Dev\DOTNExT\Contexts\$Latest"
```

### Alternative (sort by name)

```powershell
Get-ChildItem "D:\Dev\DOTNExT\Contexts" -Directory | 
    Where-Object { $_.Name -match '^\d{3}' } |
    Sort-Object Name -Descending | 
    Select-Object -First 1 -ExpandProperty FullName
```

---

## Agent Instructions Summary

### On Session Start

1. Run: `Get-Content "D:\Dev\DOTNExT\Contexts\LATEST.txt"` to find active context
2. Read: `Contexts/<active>/STATUS.md` for overall state
3. Read: `Contexts/<active>/<your-role>/state.md` for role-specific state
4. Check: `Contexts/<active>/shared/` for cross-role info
5. Confirm: Tell Louis "I've recontextualized from [context folder]. Current state: [summary]"

### During Work

1. Update your role's `state.md` after significant actions
2. Update `STATUS.md` when overall state changes
3. Put cross-role info in `shared/`
4. Log decisions with rationale

### After Compaction

1. Immediately run the context-finding command
2. Re-read all relevant context files
3. Note in your state.md that compaction occurred
4. Continue work with restored context

### When Louis Requests New Context

1. Run the new context folder script
2. Review previous context folder
3. Copy/transcribe relevant information
4. Initialize new STATUS.md
5. Confirm to Louis: "New context [number] created. Carried forward: [what]"

---

## Folder Structure Reference

```
D:\Dev\DOTNExT\Contexts\
├── LATEST.txt                    # Contains name of active context folder
├── 001 - 2025-12-04/
│   ├── STATUS.md                 # Overall context state
│   ├── SAGE/
│   │   ├── state.md              # SAGE role state
│   │   └── [other files]
│   ├── BUILD/
│   │   ├── state.md
│   │   └── [build logs, notes]
│   ├── DEPLOY/
│   │   ├── state.md
│   │   └── [deployment records]
│   ├── TEST/
│   │   ├── state.md
│   │   └── [test results]
│   ├── REPO/
│   │   ├── state.md
│   │   └── [git operation logs]
│   ├── CODE/
│   │   ├── state.md
│   │   └── [implementation notes]
│   └── shared/
│       ├── handoffs.md           # Cross-role handoff notes
│       └── [shared resources]
├── 002 - 2025-12-06/
│   └── [same structure]
└── [...]
```

---

## Best Practices

### For Louis

- Create new context folders when focus shifts significantly
- Review context folders periodically to ensure they're being maintained
- Use context folders to onboard new agent sessions quickly

### For Agents

- **Update context proactively** - Don't wait until you're about to lose context
- **Be specific** - Future readers need details, not vague summaries
- **Log decisions with WHY** - The rationale is often more valuable than the decision
- **Note what went wrong** - Mistakes are learning opportunities for future sessions
- **Include file paths** - "I modified a file" is useless; "I modified src/runtime/src/coreclr/jit/optimizer.cpp line 342" is useful

### For Recovery

- When recovering from compaction, explicitly state what you recovered
- If context seems incomplete, ask Louis rather than guessing
- The previous context folder often has valuable information even after a new one is created

---

## Troubleshooting

**LATEST.txt is missing:**
```powershell
# Recreate by finding most recent folder
$Latest = Get-ChildItem "D:\Dev\DOTNExT\Contexts" -Directory | 
    Where-Object { $_.Name -match '^\d{3}' } |
    Sort-Object Name -Descending | 
    Select-Object -First 1 -ExpandProperty Name
$Latest | Out-File "D:\Dev\DOTNExT\Contexts\LATEST.txt" -Encoding UTF8
```

**Context folder is empty:**
- Check if templates were copied
- May need to initialize from templates
- Ask Louis for current state if unclear

**Multiple agents updated same file:**
- Context files may have conflicts
- Louis should resolve by reviewing both versions
- Consider more granular files for busy contexts

---

*The context system is only as good as the discipline of those using it. Update early, update often.*
