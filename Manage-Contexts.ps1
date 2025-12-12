<#
.SYNOPSIS
    DOTNExT Context Manager - Manages context folders for AI-assisted development.

.DESCRIPTION
    Manages the context continuity system that allows AI agents to survive context window
    resets. Creates simplified context folders with clear, AI-friendly file structure.

.PARAMETER Action
    The action to perform:
    - init: Initialize the Contexts system (first-time setup)
    - new: Create a new context folder
    - archive: Archive Current-Context.md to Past-Contexts-Appended.md
    - status: Show current context status
    - latest: Show the active context folder path (for scripts/AI)
    - list: List all context folders
    - reboot: Full context dump for AI resurrection (prints plan, context, file list, orientation)

.EXAMPLE
    .\Manage-Contexts.ps1 -Action init
    Initializes the Contexts folder system.

.EXAMPLE
    .\Manage-Contexts.ps1 -Action archive
    Appends Current-Context.md to Past-Contexts-Appended.md and reinitializes it.

.EXAMPLE
    .\Manage-Contexts.ps1 -Action latest
    Outputs just the path (for AI to capture).

.NOTES
    Environment: PowerShell on Windows 11, Claude Code CLI
    Part of the DOTNExT multi-session AI development workflow.
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("init", "new", "archive", "status", "latest", "list", "reboot")]
    [string]$Action
)

# Configuration
$VMRRoot = "D:\Dev\DOTNExT"
$ContextsRoot = "$VMRRoot\Contexts"

function Get-LatestContextFolder {
    if (Test-Path "$ContextsRoot\LATEST.txt") {
        $Latest = (Get-Content "$ContextsRoot\LATEST.txt" -First 1).Trim()
        $Path = "$ContextsRoot\$Latest"
        if (Test-Path $Path) {
            return $Path
        }
    }

    # Fallback: find by sorting
    $Folders = Get-ChildItem -Path $ContextsRoot -Directory |
        Where-Object { $_.Name -match '^\d{3}' } |
        Sort-Object Name -Descending

    if ($Folders) {
        return $Folders[0].FullName
    }

    return $null
}

function Get-NextSequenceNumber {
    $Folders = Get-ChildItem -Path $ContextsRoot -Directory |
        Where-Object { $_.Name -match '^\d{3}' } |
        Sort-Object Name -Descending

    if ($Folders) {
        $LastNum = [int]($Folders[0].Name.Substring(0, 3))
        return $LastNum + 1
    }

    return 1
}

function New-CurrentContextContent {
    param([string]$FolderName, [string]$Focus = "[Define focus]")

    return @"
# Current Context

**Context:** $FolderName
**Initialized:** $(Get-Date -Format 'yyyy-MM-dd HH:mm')
**Last Updated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm')

---

## Current Focus

$Focus

---

## Active State

**What we're working on:**
- [Describe current work]

**Recent progress:**
- $(Get-Date -Format 'yyyy-MM-dd HH:mm') - Context initialized

**Issues/Blockers:**
- None yet

---

## Key Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| | | |

---

## Topic Files

| File | Purpose |
|------|---------|
| CurrentPlan.md | Active tasks and planning |
| | |

---

## Critical Survival Info

> Everything below this line MUST survive context window death.
> If starting fresh, read this section first.

[Add critical facts, decisions, directions here]

---

*Update frequently. This file is your reboot point.*
"@
}

function New-CurrentPlanContent {
    param([string]$FolderName)

    return @"
# Current Plan

**Context:** $FolderName
**Last Updated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm')

---

## Active Tasks

### In Progress
- [ ] [Task description]

### Pending
- [ ] [Task description]

### Completed (keep for history)
- [x] $(Get-Date -Format 'yyyy-MM-dd') - Context system initialized

---

## Research Needed

- [ ] [Research item]

---

## Notes

[Planning notes, dependencies, sequencing considerations]

---

*Update often. Don't delete completed items - they're valuable history.*
"@
}

function Initialize-ContextsSystem {
    Write-Host "Initializing DOTNExT Contexts System..." -ForegroundColor Cyan
    Write-Host ""

    # Create Contexts folder
    if (-not (Test-Path $ContextsRoot)) {
        New-Item -ItemType Directory -Path $ContextsRoot -Force | Out-Null
        Write-Host "Created: $ContextsRoot" -ForegroundColor Green
    } else {
        Write-Host "Exists: $ContextsRoot" -ForegroundColor Yellow
    }

    # Create first context folder
    $FirstContext = "001 - $(Get-Date -Format 'yyyy-MM-dd')"
    $FirstContextPath = "$ContextsRoot\$FirstContext"

    if (-not (Test-Path $FirstContextPath)) {
        New-Item -ItemType Directory -Path $FirstContextPath -Force | Out-Null
        New-Item -ItemType Directory -Path "$FirstContextPath\artifacts" -Force | Out-Null

        Write-Host "Created: $FirstContextPath" -ForegroundColor Green

        # Create files
        New-CurrentContextContent -FolderName $FirstContext | Out-File -FilePath "$FirstContextPath\Current-Context.md" -Encoding UTF8
        New-CurrentPlanContent -FolderName $FirstContext | Out-File -FilePath "$FirstContextPath\CurrentPlan.md" -Encoding UTF8
        "" | Out-File -FilePath "$FirstContextPath\Past-Contexts-Appended.md" -Encoding UTF8

        Write-Host "Created: Current-Context.md, CurrentPlan.md, Past-Contexts-Appended.md" -ForegroundColor Green
    } else {
        Write-Host "Exists: $FirstContextPath" -ForegroundColor Yellow
    }

    # Create LATEST.txt
    $FirstContext | Out-File -FilePath "$ContextsRoot\LATEST.txt" -Encoding UTF8 -NoNewline
    Write-Host "Updated: LATEST.txt -> $FirstContext" -ForegroundColor Green

    Write-Host ""
    Write-Host "Contexts system initialized!" -ForegroundColor Cyan
    Write-Host "Active context: $FirstContextPath" -ForegroundColor White
}

function New-ContextFolder {
    Write-Host "Creating new context folder..." -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Path $ContextsRoot)) {
        Write-Host "ERROR: Contexts system not initialized. Run with -Action init first." -ForegroundColor Red
        return
    }

    $PreviousContext = Get-LatestContextFolder
    $PreviousName = if ($PreviousContext) { Split-Path $PreviousContext -Leaf } else { "none" }

    # Create new folder
    $NextNum = Get-NextSequenceNumber
    $NewFolder = "{0:D3} - {1}" -f $NextNum, (Get-Date -Format 'yyyy-MM-dd')
    $NewPath = "$ContextsRoot\$NewFolder"

    New-Item -ItemType Directory -Path $NewPath -Force | Out-Null
    New-Item -ItemType Directory -Path "$NewPath\artifacts" -Force | Out-Null

    Write-Host "Created: $NewPath" -ForegroundColor Green

    # Create files with reference to previous
    $Content = New-CurrentContextContent -FolderName $NewFolder -Focus "[Continued from $PreviousName - review previous context]"
    $Content | Out-File -FilePath "$NewPath\Current-Context.md" -Encoding UTF8
    New-CurrentPlanContent -FolderName $NewFolder | Out-File -FilePath "$NewPath\CurrentPlan.md" -Encoding UTF8
    "" | Out-File -FilePath "$NewPath\Past-Contexts-Appended.md" -Encoding UTF8

    Write-Host "Created: Current-Context.md, CurrentPlan.md, Past-Contexts-Appended.md" -ForegroundColor Green

    # Update LATEST.txt
    $NewFolder | Out-File -FilePath "$ContextsRoot\LATEST.txt" -Encoding UTF8 -NoNewline
    Write-Host "Updated: LATEST.txt -> $NewFolder" -ForegroundColor Green

    Write-Host ""
    Write-Host "New context created!" -ForegroundColor Cyan
    Write-Host "Previous: $PreviousContext" -ForegroundColor Gray
    Write-Host "Active: $NewPath" -ForegroundColor White
}

function Invoke-ArchiveContext {
    Write-Host "Archiving Current-Context.md..." -ForegroundColor Cyan
    Write-Host ""

    $LatestPath = Get-LatestContextFolder
    if (-not $LatestPath) {
        Write-Host "ERROR: No active context folder found." -ForegroundColor Red
        return
    }

    $CurrentFile = "$LatestPath\Current-Context.md"
    $ArchiveFile = "$LatestPath\Past-Contexts-Appended.md"

    if (-not (Test-Path $CurrentFile)) {
        Write-Host "ERROR: Current-Context.md not found at $LatestPath" -ForegroundColor Red
        return
    }

    # Append header and content to archive
    $ArchiveHeader = "`n`n---`n`n# Archived: $(Get-Date -Format 'yyyy-MM-dd HH:mm')`n"
    $ArchiveHeader | Add-Content -Path $ArchiveFile -Encoding UTF8
    Get-Content $CurrentFile | Add-Content -Path $ArchiveFile -Encoding UTF8

    Write-Host "Appended to: $ArchiveFile" -ForegroundColor Green

    # Reinitialize Current-Context.md
    $FolderName = Split-Path $LatestPath -Leaf
    New-CurrentContextContent -FolderName $FolderName -Focus "[Fresh start - check Past-Contexts-Appended.md for history]" | Out-File -FilePath $CurrentFile -Encoding UTF8

    Write-Host "Reinitialized: $CurrentFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "Archive complete!" -ForegroundColor Cyan
}

function Show-ContextStatus {
    Write-Host "DOTNExT Context Status" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    Write-Host ""

    $LatestPath = Get-LatestContextFolder

    if (-not $LatestPath) {
        Write-Host "No context folders found. Run: .\Manage-Contexts.ps1 -Action init" -ForegroundColor Red
        return
    }

    $LatestName = Split-Path $LatestPath -Leaf
    Write-Host "Active Context: $LatestName" -ForegroundColor White
    Write-Host "Path: $LatestPath" -ForegroundColor Gray
    Write-Host ""

    Write-Host "Files:" -ForegroundColor White
    $Files = @("Current-Context.md", "CurrentPlan.md", "Past-Contexts-Appended.md")
    foreach ($file in $Files) {
        $FilePath = "$LatestPath\$file"
        if (Test-Path $FilePath) {
            $Size = (Get-Item $FilePath).Length
            $ModTime = (Get-Item $FilePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
            Write-Host "  [OK] $file ($Size bytes, modified: $ModTime)" -ForegroundColor Green
        } else {
            Write-Host "  [--] $file (missing)" -ForegroundColor Yellow
        }
    }

    # Check for topic files
    $TopicFiles = Get-ChildItem -Path $LatestPath -Filter "*.md" |
        Where-Object { $_.Name -notin $Files }

    if ($TopicFiles) {
        Write-Host ""
        Write-Host "Topic Files:" -ForegroundColor White
        foreach ($tf in $TopicFiles) {
            Write-Host "  $($tf.Name)" -ForegroundColor Cyan
        }
    }

    # Check artifacts
    $ArtifactsPath = "$LatestPath\artifacts"
    if (Test-Path $ArtifactsPath) {
        $ArtifactCount = (Get-ChildItem $ArtifactsPath -File).Count
        Write-Host ""
        Write-Host "Artifacts: $ArtifactCount file(s)" -ForegroundColor Gray
    }
}

function Show-LatestContext {
    $LatestPath = Get-LatestContextFolder
    if ($LatestPath) {
        # Output just the path, no extra text (for AI/script capture)
        Write-Output $LatestPath
    } else {
        Write-Error "No context folders found."
    }
}

function Show-ContextList {
    Write-Host "DOTNExT Context Folders" -ForegroundColor Cyan
    Write-Host "=======================" -ForegroundColor Cyan
    Write-Host ""

    $Folders = Get-ChildItem -Path $ContextsRoot -Directory |
        Where-Object { $_.Name -match '^\d{3}' } |
        Sort-Object Name -Descending

    $LatestPath = Get-LatestContextFolder
    $LatestName = if ($LatestPath) { Split-Path $LatestPath -Leaf } else { "" }

    foreach ($folder in $Folders) {
        $Marker = if ($folder.Name -eq $LatestName) { " <- ACTIVE" } else { "" }
        $FileCount = (Get-ChildItem "$($folder.FullName)\*.md" -ErrorAction SilentlyContinue).Count
        Write-Host "$($folder.Name) [$FileCount .md files]$Marker" -ForegroundColor $(if ($Marker) { "Green" } else { "White" })
    }

    if (-not $Folders) {
        Write-Host "No context folders found. Run: .\Manage-Contexts.ps1 -Action init" -ForegroundColor Yellow
    }
}

function Invoke-AIReboot {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Magenta
    Write-Host "           DOTNExT AI CONTEXT REBOOT                            " -ForegroundColor Magenta
    Write-Host "================================================================" -ForegroundColor Magenta
    Write-Host ""

    $LatestPath = Get-LatestContextFolder
    if (-not $LatestPath) {
        Write-Host "ERROR: No active context folder found." -ForegroundColor Red
        return
    }

    $LatestName = Split-Path $LatestPath -Leaf
    Write-Host "Active Context: $LatestName" -ForegroundColor Green
    Write-Host "Path: $LatestPath" -ForegroundColor Gray
    Write-Host ""

    # 1. Print CurrentPlan.md
    $PlanFile = "$LatestPath\CurrentPlan.md"
    if (Test-Path $PlanFile) {
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host " CurrentPlan.md" -ForegroundColor Cyan
        Write-Host "================================================================" -ForegroundColor Cyan
        Get-Content $PlanFile
        Write-Host ""
    }

    # 2. Print Current-Context.md
    $ContextFile = "$LatestPath\Current-Context.md"
    if (Test-Path $ContextFile) {
        Write-Host "================================================================" -ForegroundColor Yellow
        Write-Host " Current-Context.md" -ForegroundColor Yellow
        Write-Host "================================================================" -ForegroundColor Yellow
        Get-Content $ContextFile
        Write-Host ""
    }

    # 3. List all files in context folder
    Write-Host "================================================================" -ForegroundColor Blue
    Write-Host " All Files in Context Folder" -ForegroundColor Blue
    Write-Host "================================================================" -ForegroundColor Blue

    $AllFiles = Get-ChildItem -Path $LatestPath -File -Recurse
    foreach ($file in $AllFiles) {
        $RelPath = $file.FullName.Replace("$LatestPath\", "")
        $Size = "{0:N0}" -f $file.Length
        Write-Host "  $RelPath ($Size bytes)" -ForegroundColor White
    }
    Write-Host ""

    # 4. Orientation instructions
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host " AI ORIENTATION" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "You have been rebooted - new session or context compaction." -ForegroundColor Gray
    Write-Host ""
    Write-Host "WHAT TO DO NOW:" -ForegroundColor White
    Write-Host "  1. You just read CurrentPlan.md - this shows your active tasks" -ForegroundColor Gray
    Write-Host "  2. You just read Current-Context.md - this contains everything you knew" -ForegroundColor Gray
    Write-Host "  3. If there are topic-specific .md files listed above, read them as needed" -ForegroundColor Gray
    Write-Host "  4. The artifacts/ folder contains reference docs if needed" -ForegroundColor Gray
    Write-Host ""
    Write-Host "CONTEXT COMMANDS:" -ForegroundColor White
    Write-Host "  .\Manage-Contexts.ps1 -Action status   # Quick status check" -ForegroundColor Gray
    Write-Host "  .\Manage-Contexts.ps1 -Action archive  # Archive current context and reset" -ForegroundColor Gray
    Write-Host ""
    Write-Host "ENVIRONMENT:" -ForegroundColor White
    Write-Host "  Claude Code CLI on Windows 11, PowerShell" -ForegroundColor Gray
    Write-Host "  Working directory: D:\Dev\DOTNExT\" -ForegroundColor Gray
    Write-Host "  Project: DOTNExT (.NET 9 platform fork)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "RESUME WORK:" -ForegroundColor White
    Write-Host "  Check CurrentPlan.md 'Next Session' or 'In Progress' sections." -ForegroundColor Gray
    Write-Host "  Continue where you left off." -ForegroundColor Gray
    Write-Host ""
}

# Main execution
switch ($Action) {
    "init" { Initialize-ContextsSystem }
    "new" { New-ContextFolder }
    "archive" { Invoke-ArchiveContext }
    "status" { Show-ContextStatus }
    "latest" { Show-LatestContext }
    "list" { Show-ContextList }
    "reboot" { Invoke-AIReboot }
}
