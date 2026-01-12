# Current Context

**Context:** 002 - 2025-12-12
**Initialized:** 2025-12-12 14:48
**Last Updated:** 2026-01-08

---

## Current Focus

VS2022 DOTNExT integration - fixed and working.

---

## Active State

**What we're working on:**
- VS2022 integration with custom Roslyn compiler

**Recent progress:**
- 2026-01-08 - Fixed VS2022 Roslyn package load failures
- 2025-12-12 14:48 - Context initialized

**Issues/Blockers:**
- None - VS2022 working

---

## Key Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| Use `-Restore` flag always | Required for proper NuGet dependency resolution | 2026-01-08 |
| Clear experimental hive on deploy issues | Corrupted hive causes package load failures | 2026-01-08 |

---

## Topic Files

| File | Purpose |
|------|---------|
| CurrentPlan.md | Active tasks and planning |

---

## Session Summary: 2026-01-08

### Fixed: VS2022 Roslyn Package Load Failures

**Symptoms:**
- "RoslynPackage did not load correctly" popup
- "CSharpPackage did not load correctly" popup
- No syntax highlighting (all-white text)
- Cannot set breakpoints

**Root Cause:**
Corrupted VS experimental hive. Found 3 stale folders in `%LocalAppData%\Microsoft\VisualStudio\`:
- `17.0_cb1e5d3bRoslynDev`
- `18.0_f601aad6RoslynDev`
- `RoslynDev`

**Solution:**
1. Kill VS processes
2. Delete all `*RoslynDev*` folders from LocalAppData
3. Rebuild Roslyn: `Build.cmd -restore -c Release -deployExtensions`
4. Launch: `vsdotnext.cmd`

**Scripts created:**
- `deploy-roslyn-only.ps1` - Quick redeploy (clears hive, installs existing VSIX)
- `fix-roslyn-deploy.ps1` - Full rebuild + deploy

---

## Critical Survival Info

> Everything below this line MUST survive context window death.

### VS2022 Integration Status: WORKING

**Launch command:** `.\vsdotnext.cmd [solution.sln]`

**If Roslyn packages fail to load:**
```powershell
.\fix-roslyn-deploy.ps1
```

**Key files:**
- `vsdotnext.cmd` - VS launcher with DOTNExT environment
- `Update-DOTNExT.ps1` - Full build/deploy script
- `fix-roslyn-deploy.ps1` - Quick Roslyn fix
- `deploy-roslyn-only.ps1` - Deploy without rebuild

**Critical env var for debugging:** `VSDebugger_ValidateDotnetDebugLibSignatures=0`

---

*Update frequently. This file is your reboot point.*
