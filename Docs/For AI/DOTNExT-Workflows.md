# DOTNExT Development Workflow Scenarios

## Document Purpose

Step-by-step workflows for common development scenarios in the DOTNExT project. Each workflow shows the sequence of roles and actions.

**Last Updated:** 2026-01-08

---

# Quick Commands Reference

Most common operations at a glance. Run from VMR root (`D:\Dev\DOTNExT`).

## One-Liners

| Task | Command |
|------|---------|
| **Launch VS with DOTNExT** | `.\vsdotnext.cmd` |
| **Launch VS with solution** | `.\vsdotnext.cmd path\to\solution.sln` |
| **Full rebuild (runtime + roslyn)** | `.\Update-DOTNExT.ps1` |
| **Rebuild Roslyn only** | `.\Update-DOTNExT.ps1 -SkipRuntime` |
| **Fix Roslyn VS issues** | `.\fix-roslyn-deploy.ps1` |
| **Quick Roslyn redeploy** | `.\deploy-roslyn-only.ps1` |
| **Test with corerun** | `$env:CORE_ROOT\corerun.exe MyApp.dll` |

## Build Commands by Component

```powershell
# Runtime (CLR + BCL)
cd src\runtime
.\build.cmd -subset clr+libs -c Release
.\src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release

# Roslyn (with VS deployment)
cd src\roslyn
.\Build.cmd -restore -c Release -deployExtensions

# SDK
cd src\sdk
.\build.cmd -c Release

# WPF
cd src\wpf
.\build.cmd -c Release -pack
```

## Key Scripts at VMR Root

| Script | Purpose |
|--------|---------|
| `vsdotnext.cmd` | Launch VS2022 with DOTNExT environment |
| `Update-DOTNExT.ps1` | Full build/deploy (runtime + roslyn + optional SDK) |
| `fix-roslyn-deploy.ps1` | Fix Roslyn package load failures |
| `deploy-roslyn-only.ps1` | Redeploy existing VSIX without rebuild |

## Key Environment Variables

| Variable | Purpose |
|----------|---------|
| `CORE_ROOT` | Path to Core_Root with corerun.exe |
| `DOTNET_ROOT` | Path to custom SDK |
| `DOTNET_MULTILEVEL_LOOKUP=0` | Prevent fallback to system SDK |
| `VSDebugger_ValidateDotnetDebugLibSignatures=0` | **Required** for F5 debugging with custom runtime |

---

# Workflow 1: Modify Runtime (CLR/JIT/GC) and Test

**Goal:** Make a change to native runtime code and verify it works.

**Components:** runtime (clr subset)

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  REPO   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│ (write) │   │(commit?)│   │ (clr)   │   │(corerun)│   │(corerun)│
└─────────┘   └─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

### Step 1: CODE - Implement Change

Location: `src/runtime/src/coreclr/...`

```
Modify the relevant C++ files in:
- jit/ for JIT changes
- gc/ for GC changes
- vm/ for VM changes
```

**Completion:** "Code changes complete. Ready for build."

### Step 2: BUILD - Build Runtime

```powershell
cd D:\Dev\DOTNExT\src\runtime

# Build CLR + Libraries (needed for Core_Root)
.\build.cmd -subset clr+libs -c Release

# Generate Core_Root
.\src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
```

**Validation:**
- Exit code 0
- `artifacts/bin/coreclr/windows.x64.Release/coreclr.dll` exists
- `artifacts/tests/coreclr/windows.x64.Release/Tests/Core_Root/corerun.exe` exists

**Completion:** "Build complete. Core_Root generated. Ready for deployment."

### Step 3: DEPLOY - Set Up Core_Root Environment

```powershell
$CoreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"
$env:CORE_ROOT = $CoreRoot
$env:PATH = "$CoreRoot;$env:PATH"
```

**Validation:**
```powershell
& "$env:CORE_ROOT\corerun.exe" --help
# Should show corerun usage
```

**Completion:** "Core_Root deployed. Environment configured. Ready for testing."

### Step 4: TEST - Verify Change

```powershell
# Run your test application
& "$env:CORE_ROOT\corerun.exe" D:\Test\MyTestApp\bin\Release\net9.0\MyTestApp.dll

# Or run runtime tests
.\artifacts\tests\coreclr\windows.x64.Release\...\TestScript.cmd
```

**Completion:** "Tests passed. Change verified." or "Tests failed: [details]"

### Step 5: REPO - Commit (if tests pass)

```bash
git add -A
git commit -m "Description of runtime change"
```

---

# Workflow 2: Modify BCL Library and Test

**Goal:** Change a managed library (e.g., System.Linq) and verify it works.

**Components:** runtime (libs subset)

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│ (write) │   │ (libs)  │   │(corerun)│   │(corerun)│
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

### Step 1: CODE - Implement Change

Location: `src/runtime/src/libraries/System.Linq/...`

```
Modify C# files in the relevant library.
Follow existing patterns and conventions.
```

### Step 2: BUILD - Build Libraries

```powershell
cd D:\Dev\DOTNExT\src\runtime

# If only libs changed, can skip clr
.\build.cmd -subset libs -c Release

# Regenerate Core_Root to include new libs
.\src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
```

### Step 3: DEPLOY - Set Up Environment

Same as Workflow 1, Step 3.

### Step 4: TEST - Verify Change

```powershell
# Test app that uses the modified library
& "$env:CORE_ROOT\corerun.exe" MyLinqTest.dll
```

---

# Workflow 3: Modify Roslyn Compiler and Test in VS

**Goal:** Add a language feature or fix compiler bug, test in Visual Studio.

**Components:** roslyn

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │  TEST   │
│ (write) │   │ (vsix)  │   │ (vs)    │
└─────────┘   └─────────┘   └─────────┘
```

Note: `-deployExtensions` handles deployment automatically during build.

## Steps

### Step 1: CODE - Implement Change

Location: `src/roslyn/src/Compilers/CSharp/...`

```
For language features:
- Parser changes: Syntax/
- Binding: Binder/
- Lowering: Lowering/
- Emit: Emit/

For IDE features:
- src/roslyn/src/Features/CSharp/
```

### Step 2: BUILD - Build and Deploy to VS

```powershell
cd D:\Dev\DOTNExT\src\roslyn

# Build with VSIX generation AND auto-deploy to experimental hive
.\Build.cmd -restore -c Release -deployExtensions
```

**Important flags:**
- `-restore` - **Required**. Restores NuGet dependencies. Omitting causes package load failures.
- `-c Release` - Configuration (use Release for testing, Debug for development)
- `-deployExtensions` - Auto-deploys VSIX to RoslynDev experimental hive

**Validation:**
- Exit code 0
- `artifacts/VSSetup/Release/Roslyn.VisualStudio.Setup.vsix` exists
- `artifacts/VSSetup/Release/Roslyn.Compilers.Extension.vsix` exists

**Completion:** "Roslyn build complete. VSIX deployed to RoslynDev hive."

### Step 3: TEST - Launch VS and Verify

**Preferred method - use the launcher script:**
```powershell
# From VMR root
.\vsdotnext.cmd

# Or with a specific solution
.\vsdotnext.cmd D:\Dev\DOTNExT\test-isdonext\test-isdonext.sln
```

**Alternative - manual launch:**
```powershell
# Set required env var for debugging custom runtime
$env:VSDebugger_ValidateDotnetDebugLibSignatures = "0"

# Launch VS with RoslynDev experimental hive
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" /rootSuffix RoslynDev
```

**In VS, verify:**
1. No package load error popups
2. Syntax highlighting works (not all-white text)
3. IntelliSense recognizes new syntax/features
4. Build succeeds
5. F5 debugging works (select "CoreRun Debug" profile if available)

### Troubleshooting

If you get "RoslynPackage did not load correctly" or similar errors:

```powershell
# Quick fix from VMR root
.\fix-roslyn-deploy.ps1
```

See Troubleshooting section below for details.

---

# Workflow 4: Modify SDK and Test

**Goal:** Change SDK tooling (CLI, MSBuild tasks, templates) and verify.

**Components:** sdk

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│ (write) │   │ (sdk)   │   │ (sdk)   │   │(dotnet) │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

### Step 1: CODE - Implement Change

Location: `src/sdk/src/...`

```
- CLI commands: Cli/
- MSBuild tasks: Tasks/
- Templates: template_feed/
```

### Step 2: BUILD - Build SDK

```powershell
cd D:\Dev\DOTNExT\src\sdk

.\build.cmd -c Release
```

**Validation:**
- `artifacts/bin/redist/Release/dotnet/dotnet.exe` exists

### Step 3: DEPLOY - Set Up Custom SDK

```powershell
$SDKPath = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet"
$env:DOTNET_ROOT = $SDKPath
$env:DOTNET_MULTILEVEL_LOOKUP = "0"
$env:PATH = "$SDKPath;$env:PATH"
```

**Validation:**
```powershell
dotnet --info
# Should show custom SDK path
```

### Step 4: TEST - Verify SDK Functionality

```powershell
# Test your specific changes
dotnet new console -n TestApp
cd TestApp
dotnet build
dotnet run
```

---

# Workflow 5: Modify WPF/WinForms and Test

**Goal:** Change desktop framework and verify in an application.

**Components:** wpf or winforms

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│ (write) │   │(wpf/wfm)│   │ (copy)  │   │ (app)   │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

### Step 1: CODE - Implement Change

Location: `src/wpf/src/...` or `src/winforms/src/...`

### Step 2: BUILD - Build Framework

```powershell
cd D:\Dev\DOTNExT\src\wpf  # or winforms

.\build.cmd -c Release -pack
```

### Step 3: DEPLOY - Copy to Published App

```powershell
# Create and publish test app
cd D:\Test\WpfTestApp
dotnet publish -r win-x64 --self-contained -o publish

# Copy built assemblies
cd D:\Dev\DOTNExT\src\wpf
.\eng\copy-wpf.ps1 -destination D:\Test\WpfTestApp\publish
```

### Step 4: TEST - Run Application

```powershell
# Run from publish folder (NOT via dotnet run)
D:\Test\WpfTestApp\publish\WpfTestApp.exe
```

---

# Workflow 6: Cross-Component Change (Runtime + SDK)

**Goal:** Make a change that spans multiple components.

**Example:** Add new runtime API and expose it in SDK

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│(runtime)│   │(runtime)│   │(corerun)│   │(corerun)│
└─────────┘   └─────────┘   └─────────┘   └─────────┘
     │
     ▼
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│  (sdk)  │   │  (sdk)  │   │  (sdk)  │   │(dotnet) │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

1. **Implement runtime change first** (lower layer)
2. **Build and test runtime in isolation** (corerun)
3. **Implement SDK change** (depends on runtime)
4. **Build SDK** (uses updated runtime)
5. **Test full stack** (dotnet CLI with new SDK)

**Key:** Build and test lower layers before upper layers.

---

# Workflow 7: Full Stack Rebuild (Update-DOTNExT)

**Goal:** Rebuild everything and ensure VS2022 integration works.

**Use when:** Starting fresh, after git pull, or when things are broken.

## Quick Command

```powershell
# From VMR root - full rebuild
.\Update-DOTNExT.ps1

# Skip runtime (Roslyn only)
.\Update-DOTNExT.ps1 -SkipRuntime

# Include SDK (adds build time)
.\Update-DOTNExT.ps1 -IncludeSDK

# Rebuild and launch VS
.\Update-DOTNExT.ps1 -LaunchVS
```

## What It Does

1. Stops stale processes (devenv, VBCSCompiler, MSBuild)
2. Builds runtime (`clr+libs`) and generates Core_Root
3. Builds Roslyn with `-restore -deployExtensions`
4. Deploys VSIX to RoslynDev experimental hive
5. Sets environment variables (session + persistent)
6. Generates/updates `vsdotnext.cmd` launcher
7. Validates the setup

## Script Options

| Flag | Effect |
|------|--------|
| `-SkipRuntime` | Skip runtime build |
| `-SkipRoslyn` | Skip Roslyn build |
| `-IncludeSDK` | Also build SDK (slow) |
| `-SkipDeploy` | Skip VSIX deployment |
| `-NoBuild` | Skip builds, just configure environment |
| `-LaunchVS` | Launch VS after completion |
| `-Configuration Debug` | Use Debug instead of Release |

## Timing

| Scenario | Approximate Time |
|----------|------------------|
| Full (runtime + roslyn) | 30-60 min |
| Roslyn only (`-SkipRuntime`) | 10-20 min |
| With SDK (`-IncludeSDK`) | 45-90 min |
| No build (`-NoBuild`) | < 1 min |

---

# Workflow 8: Sync with Upstream

**Goal:** Bring in latest changes from official .NET repos.

## Sequence

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  REPO   │ → │  BUILD  │ → │  TEST   │ → │  REPO   │
│ (sync)  │   │ (full)  │   │(verify) │   │(commit) │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

## Steps

### Step 1: REPO - Fetch and Merge Upstream

```bash
# Assuming upstream remote is configured
git fetch upstream
git merge upstream/main
# Resolve any conflicts
```

### Step 2: BUILD - Verify Build Still Works

```powershell
# Full rebuild recommended after sync
.\Update-DOTNExT.ps1
```

### Step 3: TEST - Run Smoke Tests

```powershell
# Basic verification that nothing broke
& "$env:CORE_ROOT\corerun.exe" HelloWorld.dll

# Launch VS and verify
.\vsdotnext.cmd
```

### Step 4: REPO - Commit Merge

```bash
git add -A
git commit -m "Sync with upstream main"
```

---

# Quick Reference: Which Workflow for Which Change?

| Change Type | Workflow |
|-------------|----------|
| CLR/JIT/GC native code | 1 (Runtime corerun) |
| BCL managed libraries | 2 (BCL corerun) |
| C# language feature | 3 (Roslyn VSIX) |
| VB language feature | 3 (Roslyn VSIX) |
| Analyzer/code fix | 3 (Roslyn VSIX) |
| CLI command | 4 (SDK) |
| MSBuild task | 4 (SDK) |
| Project template | 4 (SDK) |
| WPF control/feature | 5 (WPF) |
| WinForms control/feature | 5 (WinForms) |
| ASP.NET Core | Similar to 5 |
| Cross-layer feature | 6 (Multi-component) |
| Full rebuild / fresh start | 7 (Update-DOTNExT) |
| Upstream sync | 8 (Sync) |

---

# Troubleshooting

## VS2022 Roslyn Package Load Failures

**Symptoms:**
- "RoslynPackage did not load correctly" popup
- "CSharpPackage did not load correctly" popup
- No syntax highlighting (all-white text in editor)
- Cannot set breakpoints
- IntelliSense not working

**Root Cause:**
Corrupted VS experimental hive (RoslynDev). Stale cached data from previous deployments prevents packages from loading.

**Quick Fix:**
```powershell
# Run the fix script from VMR root
.\fix-roslyn-deploy.ps1
```

**Manual Fix:**
```powershell
# 1. Kill VS processes
Stop-Process -Name devenv, VBCSCompiler, MSBuild -Force -ErrorAction SilentlyContinue

# 2. Clear corrupted hive folders
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
Get-ChildItem "$localAppData\Microsoft\VisualStudio" -Directory |
    Where-Object { $_.Name -like "*RoslynDev*" } |
    Remove-Item -Recurse -Force

# 3. Rebuild Roslyn with restore + deploy
cd D:\Dev\DOTNExT\src\roslyn
.\Build.cmd -restore -c Release -deployExtensions

# 4. Launch VS
.\vsdotnext.cmd
```

**Key Points:**
- `-restore` flag is **required** - ensures all NuGet dependencies are fresh
- `-deployExtensions` deploys VSIX to experimental hive automatically
- Always kill VS processes before clearing hive
- The experimental hive location: `%LocalAppData%\Microsoft\VisualStudio\*RoslynDev*`

**Available Fix Scripts:**

| Script | Purpose | Time |
|--------|---------|------|
| `fix-roslyn-deploy.ps1` | Full rebuild + deploy | ~1-2 min |
| `deploy-roslyn-only.ps1` | Deploy existing VSIX (no rebuild) | ~30 sec |
| `Update-DOTNExT.ps1` | Complete rebuild (runtime + roslyn) | 30-60 min |

## F5 Debugging Not Working

**Symptom:** MissingMethodException or debugging fails with custom runtime.

**Root Cause:** VS validates signatures of .NET debug libraries. Custom builds are unsigned.

**Fix:** Ensure this environment variable is set BEFORE launching VS:
```powershell
$env:VSDebugger_ValidateDotnetDebugLibSignatures = "0"
```

The `vsdotnext.cmd` launcher sets this automatically. If launching VS manually, set this first.

## IntelliSense Shows Red Squiggles on Valid Code

**Symptom:** Code using custom APIs (e.g., `Environment.IsDotnext`) shows errors.

**Root Cause:** VS uses NuGet cached targeting packs, not your custom SDK.

**Fix:** Ensure your test project has:
1. `global.json` requesting your custom SDK version
2. `Directory.Build.props` overriding `NetCoreTargetingPackRoot`
3. Matching targeting pack version in FrameworkReference

See `test-isdonext` project for working example.

---

# Role Quick Reference

| Code | Role | Primary Use |
|------|------|-------------|
| SAGE | Expert | Questions, troubleshooting, guidance |
| BLD | Build | Execute builds |
| DEP | Deploy | Set up environments |
| TST | Test | Run tests |
| GIT | Repo | Git operations |
| IMP | Code | Write code |

---

*This document is part of the DOTNExT project documentation suite.*
