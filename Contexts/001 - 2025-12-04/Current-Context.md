# Current Context

**Context:** 001 - 2025-12-04
**Last Updated:** 2025-12-05 21:30

---

## Current Focus

**VS2022 FULL INTEGRATION - COMPLETE!** All workflows operational.

---

## Session Summary (2025-12-05)

### Major Achievement: VS2022 F5 Debugging with Custom Runtime

After extensive research and troubleshooting, we achieved full VS2022 integration:
- IntelliSense recognizes `Environment.IsDotnext`
- F5 debugging runs with custom runtime
- `Environment.IsDotnext = True` in debugger output

### All Workflows Now Working

| Workflow | Status | Test |
|----------|--------|------|
| corerun (CLI) | ✅ WORKING | `test-isdonext\run-test.ps1` |
| VS2022 IntelliSense | ✅ WORKING | No red squiggles on `Environment.IsDotnext` |
| VS2022 Build | ✅ WORKING | Build succeeds |
| VS2022 F5 Debug | ✅ WORKING | Shows `Environment.IsDotnext = True` |

---

## The Solution (Key Discoveries)

### IntelliSense Fix

**Problem:** VS uses NuGet cache for targeting packs, ignoring our custom SDK.

**Root Causes Found:**
1. VMR's `global.json` requests SDK version not in custom location → fallback to system
2. VS resolves SDK BEFORE evaluating project properties
3. Targeting pack version mismatch (VS wanted 9.0.11, we had 9.0.10)

**Solution Files for Test Project:**
- `Directory.Build.props` - Override `NetCoreTargetingPackRoot`, skip Arcade
- `Directory.Build.targets` - Empty file to block VMR inheritance
- `global.json` - Request `9.0.112-dev` (our custom SDK)
- `.csproj` - `TargetingPackVersion="9.0.10"` on FrameworkReference
- Created `9.0.11` folder in custom SDK packs (copy of 9.0.10)

### F5 Debugging Fix

**Problem:** `MissingMethodException` - VS debugger uses system runtime, not custom.

**Root Cause:** VS validates signatures of .NET debug libraries. Custom builds are unsigned → debugging blocked.

**Solution:**
1. Set `VSDebugger_ValidateDotnetDebugLibSignatures=0` in `vsdotnext.cmd` (BEFORE VS launches)
2. Create `launchSettings.json` with "CoreRun Debug" profile using `corerun.exe` as executable
3. Pass `$(TargetPath)` as command line argument to corerun

---

## Key Paths

| Item | Path |
|------|------|
| VS Launcher | `D:\Dev\DOTNExT\vsdotnext.cmd` |
| Test Solution | `D:\Dev\DOTNExT\test-isdonext\test-isdonext.sln` |
| Custom SDK | `D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet` |
| Core_Root | `...runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root` |
| corerun.exe | `Core_Root\corerun.exe` |
| Launch Settings | `test-isdonext\Properties\launchSettings.json` |

---

## How to Use (Complete Workflow)

### Launch VS with Custom Environment
```cmd
D:\Dev\DOTNExT\vsdotnext.cmd D:\Dev\DOTNExT\test-isdonext\test-isdonext.sln
```

### In VS2022
1. Select **"CoreRun Debug"** from debug profile dropdown
2. Press **F5**
3. See: `Environment.IsDotnext = True`

### CLI Testing
```powershell
D:\Dev\DOTNExT\test-isdonext\run-test.ps1
```

---

## vsdotnext.cmd (Updated)

```cmd
@echo off
SET DOTNET_ROOT=D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet
SET DOTNET_MULTILEVEL_LOOKUP=0
SET CORE_ROOT=D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root
SET PATH=%DOTNET_ROOT%;%CORE_ROOT%;%PATH%

REM Critical: Allow debugging unsigned .NET runtime builds
SET VSDebugger_ValidateDotnetDebugLibSignatures=0
SET DOTNET_ReadyToRun=0

start "" "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" /rootSuffix RoslynDev %*
```

---

## launchSettings.json

```json
{
  "profiles": {
    "CoreRun Debug": {
      "commandName": "Executable",
      "executablePath": "D:\\Dev\\DOTNExT\\src\\runtime\\...\\Core_Root\\corerun.exe",
      "commandLineArgs": "$(TargetPath)",
      "workingDirectory": "$(ProjectDir)",
      "environmentVariables": {
        "CORE_ROOT": "...",
        "DOTNET_ReadyToRun": "0",
        "VSDebugger_ValidateDotnetDebugLibSignatures": "0"
      }
    }
  }
}
```

---

## Git Status

**Branch:** `dotnext/smoke-test-markers`

**Committed:**
- Smoke-test markers (Environment.IsDotnext + Roslyn version)

**Uncommitted:**
- VS integration configuration files
- Build/test scripts

---

## Critical Survival Info

- **ALL WORKFLOWS WORKING** - corerun, IntelliSense, Build, F5 Debug
- **Key env var:** `VSDebugger_ValidateDotnetDebugLibSignatures=0` (must be set before VS launches)
- **Launch VS via:** `vsdotnext.cmd [solution.sln]`
- **Debug profile:** Select "CoreRun Debug" in VS
- **Branch:** `dotnext/smoke-test-markers`

---

*Full VS2022 integration achieved! Development workflow complete.*
