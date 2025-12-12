# Current Plan

**Context:** 001 - 2025-12-04
**Last Updated:** 2025-12-05 21:30

---

## Status: VS2022 FULL INTEGRATION COMPLETE

---

## Completed

### Build Infrastructure
- [x] Create do-build.cmd (runtime build script)
- [x] Fix vswhere.exe PATH issue
- [x] Fix cdac-build-tool "Baseline empty not known" error
- [x] Create test-isdonext/ verification folder

### Runtime
- [x] Add Environment.IsDotnext to implementation (Environment.cs)
- [x] Add Environment.IsDotnext to reference assembly (System.Runtime.cs)
- [x] Build runtime clr+libs Release
- [x] Verify IsDotnext in System.Private.CoreLib.dll
- [x] Verify IsDotnext in System.Runtime.dll (ref)
- [x] Set up Core_Root
- [x] Test corerun workflow - `Environment.IsDotnext = True`

### Roslyn
- [x] Modify PreReleaseVersionLabel to `3-DOTNExT`
- [x] Build Roslyn Release
- [x] Deploy VSIX to RoslynDev hive

### SDK
- [x] Build SDK (partial - redist-installer fails but SDK usable)
- [x] Verify dotnet.exe version: 9.0.112-dev

### VS2022 Integration (SOLVED!)
- [x] Get VS2022 IntelliSense to recognize `Environment.IsDotnext`
- [x] Get VS2022 F5 debugging to use custom runtime
- [x] Full development workflow operational

---

## VS2022 Integration Solution (Key Discovery)

### Problem 1: IntelliSense Not Recognizing Custom APIs

**Root Cause:** VS SDK resolution happens BEFORE project evaluation. The VMR's `global.json` requests SDK version that doesn't exist in custom location, causing fallback to system SDK.

**Solution:** Multi-part fix for test projects:

1. **`test-isdonext\Directory.Build.props`** - Override targeting pack root + skip Arcade:
   ```xml
   <Project>
     <PropertyGroup>
       <SkipArcadeSdkImport>true</SkipArcadeSdkImport>
       <RestorePackagesPath></RestorePackagesPath>
       <NetCoreTargetingPackRoot>D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet\packs</NetCoreTargetingPackRoot>
     </PropertyGroup>
   </Project>
   ```

2. **`test-isdonext\Directory.Build.targets`** - Empty file to prevent VMR inheritance

3. **`test-isdonext\global.json`** - Request our custom SDK version:
   ```json
   { "sdk": { "version": "9.0.112-dev" } }
   ```

4. **`test-isdonext\test-isdonext.csproj`** - Force targeting pack version:
   ```xml
   <FrameworkReference Update="Microsoft.NETCore.App" TargetingPackVersion="9.0.10" />
   ```

5. **Create 9.0.11 ref pack folder** (copy of 9.0.10) in custom SDK packs

### Problem 2: F5 Debugging Using System Runtime

**Root Cause:** VS debugger validates signatures of .NET debug libraries. Custom builds are unsigned.

**Solution:**

1. **`vsdotnext.cmd`** - Set critical environment variable BEFORE VS launches:
   ```cmd
   SET VSDebugger_ValidateDotnetDebugLibSignatures=0
   ```

2. **`Properties\launchSettings.json`** - Use corerun.exe as debug host:
   ```json
   {
     "profiles": {
       "CoreRun Debug": {
         "commandName": "Executable",
         "executablePath": "D:\\Dev\\DOTNExT\\src\\runtime\\...\\Core_Root\\corerun.exe",
         "commandLineArgs": "$(TargetPath)",
         "environmentVariables": {
           "CORE_ROOT": "...",
           "VSDebugger_ValidateDotnetDebugLibSignatures": "0"
         }
       }
     }
   }
   ```

---

## Test Commands

**Full VS2022 workflow:**
```cmd
D:\Dev\DOTNExT\vsdotnext.cmd D:\Dev\DOTNExT\test-isdonext\test-isdonext.sln
REM Select "CoreRun Debug" profile, press F5
REM Result: Environment.IsDotnext = True
```

**Command-line corerun:**
```powershell
D:\Dev\DOTNExT\test-isdonext\run-test.ps1
```

---

## Files Created/Modified

| File | Purpose |
|------|---------|
| `do-build.cmd` | Runtime build script |
| `vsdotnext.cmd` | VS launcher with all env vars (including VSDebugger_ValidateDotnetDebugLibSignatures) |
| `fix-nuget-cache.ps1` | Copy custom refs to NuGet cache |
| `test-isdonext/Directory.Build.props` | Override targeting pack root |
| `test-isdonext/Directory.Build.targets` | Block VMR target inheritance |
| `test-isdonext/global.json` | Request custom SDK version |
| `test-isdonext/test-isdonext.sln` | Solution file for VS |
| `test-isdonext/Properties/launchSettings.json` | CoreRun Debug profile |
| `src/sdk/.../packs/.../9.0.11/` | Copy of 9.0.10 with custom refs |

---

## Architecture Understanding (UPDATED)

```
┌─────────────────────────────────────────────────────────────┐
│                    Workflow Layers                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. corerun (contributor workflow)     ✅ WORKING          │
│     - Compile with custom refs                              │
│     - Run with corerun.exe + Core_Root                      │
│                                                             │
│  2. SDK isolation (DOTNET_ROOT)        ✅ WORKING          │
│     - SDK built, dotnet.exe works                           │
│     - Custom refs via targeting pack override               │
│                                                             │
│  3. VS2022 IntelliSense                ✅ WORKING          │
│     - Directory.Build.props overrides                       │
│     - global.json forces custom SDK                         │
│     - TargetingPackVersion matches our packs                │
│                                                             │
│  4. VS2022 F5 Debugging                ✅ WORKING          │
│     - VSDebugger_ValidateDotnetDebugLibSignatures=0         │
│     - launchSettings.json with corerun.exe                  │
│     - Full breakpoint/stepping support                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Critical Environment Variables

| Variable | Value | Purpose |
|----------|-------|---------|
| `VSDebugger_ValidateDotnetDebugLibSignatures` | `0` | **CRITICAL** - Allow debugging unsigned runtime |
| `DOTNET_ROOT` | Custom SDK path | Point to our SDK |
| `DOTNET_MULTILEVEL_LOOKUP` | `0` | Prevent fallback to system |
| `CORE_ROOT` | Core_Root path | Runtime for corerun.exe |
| `DOTNET_ReadyToRun` | `0` | Better debugging experience |

---

## Git Status

**Branch:** `dotnext/smoke-test-markers`

**Committed:**
- Smoke-test markers (Environment.IsDotnext + Roslyn version)

**Uncommitted:**
- Build scripts, test folder, VS integration files

---

*VS2022 full integration achieved. All workflows operational.*
