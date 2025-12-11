# DOTNExT Development Workflow Scenarios

## Document Purpose

Step-by-step workflows for common development scenarios in the DOTNExT project. Each workflow shows the sequence of roles and actions.

**Last Updated:** 2025-12-04

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
build.cmd -subset clr+libs -c Release

# Generate Core_Root
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
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
build.cmd -subset libs -c Release

# Regenerate Core_Root to include new libs
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
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
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│  CODE   │ → │  BUILD  │ → │ DEPLOY  │ → │  TEST   │
│ (write) │   │ (vsix)  │   │ (vsexp) │   │ (vsexp) │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
```

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

### Step 2: BUILD - Build with VSIX

```powershell
cd D:\Dev\DOTNExT\src\roslyn

# Build with VSIX generation
Build.cmd -restore -build -c Release -deployExtensions
```

**Validation:**
- Exit code 0
- `artifacts/VSSetup/Release/Roslyn.VisualStudio.Setup.vsix` exists
- `artifacts/VSSetup/Release/Roslyn.Compilers.Extension.vsix` exists

### Step 3: DEPLOY - Install to VS Experimental

```powershell
# Kill VS processes
taskkill /F /IM devenv.exe 2>$null
taskkill /F /IM VBCSCompiler.exe 2>$null
Start-Sleep -Seconds 2

# Install VSIX to experimental
$VSInstallDir = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise"
$VSIXInstaller = "$VSInstallDir\Common7\IDE\VSIXInstaller.exe"

& $VSIXInstaller /quiet /experimental `
    "D:\Dev\DOTNExT\src\roslyn\artifacts\VSSetup\Release\Roslyn.Compilers.Extension.vsix"
    
& $VSIXInstaller /quiet /experimental `
    "D:\Dev\DOTNExT\src\roslyn\artifacts\VSSetup\Release\Roslyn.VisualStudio.Setup.vsix"
```

### Step 4: TEST - Verify in VS Experimental

```powershell
# Launch VS experimental
devenv.exe /rootSuffix Exp
```

Then manually:
1. Open/create project using new feature
2. Verify IntelliSense recognizes new syntax
3. Verify build succeeds
4. Verify debugging works

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

build.cmd -c Release
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

build.cmd -c Release -pack
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

# Workflow 7: Sync with Upstream

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
# Build key components
cd src/runtime
build.cmd -subset clr+libs -c Release
```

### Step 3: TEST - Run Smoke Tests

```powershell
# Basic verification that nothing broke
corerun.exe HelloWorld.dll
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
| Upstream sync | 7 (Sync) |

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
