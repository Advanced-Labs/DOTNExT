# DOTNExT Custom .NET Platform Development - Master Reference

## Document Purpose
This is the authoritative reference for the DOTNExT project - a custom fork/modification of the .NET platform. It captures all research, decisions, workflows, and role definitions for the multi-agent development system.

**Last Updated:** 2025-12-05  
**Project Location:** `D:\Dev\DOTNExT\` (VMR - Virtual Monolithic Repository)  
**GitHub Origin:** `Advanced-Labs/DOTNExT`

---

# Part 0: Documentation System

## Documentation Locations

| Purpose | Path |
|---------|------|
| **AI Agent Docs** | `/Docs/For AI/` |
| **Repo Structure Analysis** | `/Docs/Repo Map/` |
| **Async+ Feature** | `/Docs/Async+/` |
| **Orleans Modifications** | `/Docs/Scynapse/` |
| **Roslyn Modifications** | `/Docs/New Roslyn/` |
| **Runtime Modifications** | `/Docs/New dotnet-runtime/` |
| **Original Upstream Docs** | `/Docs/Pre Fork Docs - All projects/` |

## Key Files

### Always Read First
- `/CLAUDE.md` - Agent constitution, context system, core behaviors
- `/Docs/For AI/DOTNExT-Index.md` - Documentation navigation

### For Repo Understanding
- `/Docs/Repo Map/08-Feature-Location-Reference.md` - WHERE to find features
- `/Docs/Repo Map/13-Modification-Impact-Zones.md` - Change impact analysis
- `/Docs/Repo Map/14-Extension-Points-Catalog.md` - Where to extend

### For Our Modifications
- `/Docs/Async+/Async+.md` - Async+ enhancement
- `/Docs/Scynapse/Scynapse.md` - Orleans overview
- `/Docs/Scynapse/Scynapse Features/DynamicGrainAccess.md`
- `/Docs/Scynapse/Scynapse Features/OrleansAsync+.md`
- `/Docs/Scynapse/Scynapse Features/PluginGrainArchitecture.md`

## Documentation Rules

**NEVER MODIFY:**
- `/Docs/Pre Fork Docs - All projects/*` - Original upstream docs
- `/Docs/*/Original * Internals/*` - Reference copies

**Document modifications in:**
- `/Docs/New */` folders
- `/Docs/Async+/` for Async+ features
- Use modification header format (see CLAUDE.md)

---

# Part 1: VMR Structure & Repository Classification

## What is the VMR?
The Virtual Monolithic Repository (dotnet/dotnet) includes all source code and infrastructure needed to build the .NET SDK. It's:
- **Monolithic**: A join of multiple repositories (runtime, sdk, roslyn, etc.)
- **Virtual**: A mirror (not replacement) of product repos where sources are synchronized

## Repository List & Classification

### TIER 1: Full Core_Root/corerun Support
These produce runtime binaries directly testable with corerun:

| Repository | Path | What Core_Root Covers |
|------------|------|----------------------|
| **runtime** | `src/runtime` | CLR, JIT, GC, BCL, System.Private.CoreLib |
| **diagnostics** | `src/diagnostics` | Debugging tools, profilers |

Components within runtime testable via corerun:
- Microsoft.NETCore.Jit (JIT compiler)
- Microsoft.NETCore.ILAsm (IL assembler)
- Microsoft.NETCore.ILDAsm (IL disassembler)
- System.Private.CoreLib (core managed types)
- GC (garbage collector)
- All BCL libraries (System.Collections, System.Linq, etc.)

### TIER 2: Self-Contained App + Binary Replacement
Produce managed assemblies testable by publishing self-contained and replacing binaries:

| Repository | Path | Testing Method |
|------------|------|---------------|
| **wpf** | `src/wpf` | `dotnet publish -r <rid> --self-contained` + `copy-wpf.ps1` |
| **winforms** | `src/winforms` | Same as WPF |
| **windowsdesktop** | `src/windowsdesktop` | Meta-package for WPF/WinForms |
| **aspnetcore** | `src/aspnetcore` | Self-contained publish + overlay |
| **efcore** | `src/efcore` | NuGet package reference |

### TIER 3: Custom SDK/Tooling Testing (DOTNET_ROOT approach)
Require isolated SDK testing, not corerun:

| Repository | Path | Testing Method |
|------------|------|---------------|
| **sdk** | `src/sdk` | `eng\dogfood.cmd` - starts configured PowerShell |
| **msbuild** | `src/msbuild` | Isolated environment testing |
| **templating** | `src/templating` | SDK dogfood workflow |
| **nuget-client** | `src/nuget-client` | Local installation testing |
| **command-line-api** | `src/command-line-api` | Standard dotnet test |

### TIER 4: VSIX/VS Experimental Instance
Have their own VS integration workflow:

| Repository | Path | Testing Method |
|------------|------|---------------|
| **roslyn** | `src/roslyn` | VSIX + VS Experimental (F5 debugging) |
| **roslyn-analyzers** | `src/roslyn-analyzers` | Analyzer VSIX |
| **razor** | `src/razor` | VSIX + VS experimental |
| **fsharp** | `src/fsharp` | Follows Roslyn pattern |

### TIER 5: Build Infrastructure (No Direct Runtime Testing)

| Repository | Path | Purpose |
|------------|------|---------|
| **arcade** | `src/arcade` | Shared build infrastructure |
| **source-build-externals** | `src/source-build-externals` | External dependencies |
| **source-build-reference-packages** | `src/source-build-reference-packages` | Reference packages |
| **xliff-tasks** | `src/xliff-tasks` | Localization tasks |
| **xdt** | `src/xdt` | XML Document Transform |
| **sourcelink** | `src/sourcelink` | Source linking |
| **symreader** | `src/symreader` | Symbol reading |
| **cecil** | `src/cecil` | IL manipulation (Mono.Cecil) |
| **emsdk** | `src/emsdk` | Emscripten SDK (WebAssembly) |
| **scenario-tests** | `src/scenario-tests` | E2E scenario tests |
| **test-templates** | `src/test-templates` | Test project templates |

### TIER 6: Higher-Level Frameworks

| Repository | Path | Notes |
|------------|------|-------|
| **aspire** | `src/aspire` | Cloud-native stack |
| **vstest** | `src/vstest` | Test platform |
| **Scynapse** | `src/Scynapse` | Orleans fork (Louis's) |
| **deployment-tools** | `src/deployment-tools` | Deployment infrastructure |

---

# Part 2: Build Commands Reference

## Runtime Builds

```powershell
# CLR only
build.cmd -subset clr -c Debug

# CLR + Libraries (needed for Core_Root)
build.cmd -subset clr+libs -c Release

# Full shipping (runtime + host + packs)
build.cmd -subset clr+libs+host+packs -c Release

# Generate Core_Root (requires libs built first)
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
```

## Roslyn Builds

```powershell
# Full build
Build.cmd -restore -build -c Release

# With NuGet packages
Build.cmd -restore -build -pack -c Release

# With VSIX for VS deployment
Build.cmd -restore -build -c Release -deployExtensions

# Incremental
Restore.cmd
Build.cmd -build -c Release
```

## SDK Builds

```powershell
# Full SDK build
build.cmd -c Release

# Enter dogfood shell (use custom SDK)
eng\dogfood.cmd
```

## WPF/WinForms Builds

```powershell
# With package output
build.cmd -c Release -pack
```

## Build Artifacts Locations

| Component | Location |
|-----------|----------|
| Runtime CLR | `artifacts/bin/coreclr/<OS>.<Arch>.<Config>/` |
| Core_Root | `artifacts/tests/coreclr/<OS>.<Arch>.<Config>/Tests/Core_Root/` |
| Roslyn | `artifacts/bin/` |
| Roslyn VSIX | `artifacts/VSSetup/<Config>/` |
| SDK | `artifacts/bin/redist/<Config>/dotnet/` |
| Shipping packages | `artifacts/packages/<Config>/Shipping/` |

## Build Dependency Order

```
runtime.clr → runtime.libs → runtime.host → runtime.packs
                   ↓
              roslyn (optional, for custom compiler)
                   ↓
                  sdk
                   ↓
            aspnetcore, wpf, winforms, etc.
```

---

# Part 3: Environment Variables Reference

## Critical Environment Variables

| Variable | Purpose | Example |
|----------|---------|---------|
| `DOTNET_ROOT` | Custom SDK location | `C:\MyCustomDotnet` |
| `DOTNET_MULTILEVEL_LOOKUP` | Set to 0 to disable searching other installs | `0` |
| `CORE_ROOT` | Core_Root directory for corerun | `D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root` |
| `CORE_LIBRARIES` | Additional assembly directories | Path to additional DLLs |
| `MSBuildSDKsPath` | Custom SDK path for MSBuild | `C:\Program Files\dotnet\sdk\VERSION\Sdks` |
| `RoslynAssembliesPath` | Custom Roslyn compiler path | Path to Roslyn DLLs |

## Setup Scripts

### Core_Root Setup (for corerun testing)
```powershell
$CoreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"
$env:CORE_ROOT = $CoreRoot
$env:PATH = "$CoreRoot;$env:PATH"
# Test: corerun.exe YourApp.dll
```

### Custom SDK Setup (for dotnet CLI testing)
```powershell
$SDKPath = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet"
$env:DOTNET_ROOT = $SDKPath
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:PATH = "$SDKPath;$env:PATH"
# Verify: dotnet --info
```

### VS Launch with Custom SDK
```powershell
$SDKPath = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet"
$env:DOTNET_ROOT = $SDKPath
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:PATH = "$SDKPath;$env:PATH"
Start-Process "devenv.exe" -ArgumentList "YourSolution.sln"
```

---

# Part 4: Testing Workflows

## Workflow A: corerun Testing (Runtime Changes)

1. Build runtime: `build.cmd -subset clr+libs -c Release`
2. Generate Core_Root: `src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release`
3. Set environment: `$env:CORE_ROOT = "<path>"`
4. Run: `corerun.exe YourApp.dll`

**Key Points:**
- corerun is a minimal host - no NuGet, no project system
- Good for testing CLR, JIT, GC, BCL changes
- App must be pre-compiled (dll), not project file

## Workflow B: Custom SDK Testing

1. Build SDK: `build.cmd -c Release`
2. Enter dogfood: `eng\dogfood.cmd`
3. Use `dotnet` commands normally
4. Or set DOTNET_ROOT manually for VS integration

**Key Points:**
- Full SDK experience
- Can test templates, CLI, MSBuild tasks
- Requires DOTNET_MULTILEVEL_LOOKUP=0 to avoid picking up system SDK

## Workflow C: VS Experimental Instance (Roslyn Changes)

1. Build Roslyn with VSIX: `Build.cmd -restore -build -c Release -deployExtensions`
2. Kill any VS instances: `taskkill /F /IM devenv.exe`
3. Install VSIX to experimental: `VSIXInstaller.exe /experimental <path.vsix>`
4. Launch experimental: `devenv.exe /rootSuffix Exp`

**Key Points:**
- Experimental instance is isolated from main VS
- VSIX includes CompilerExtension (command-line compiler) and VisualStudioSetup (IDE integration)
- For command-line builds with custom Roslyn, add Microsoft.Net.Compilers.Toolset reference

## Workflow D: WPF/WinForms Testing

1. Build WPF/WinForms: `build.cmd -c Release -pack`
2. Create test app: `dotnet new wpf -n TestApp`
3. Publish self-contained: `dotnet publish -r win-x64 --self-contained`
4. Copy built assemblies: `.\eng\copy-wpf.ps1 -destination <publish_folder>`
5. Run from publish folder

**Key Points:**
- WPF/WinForms are architecture-dependent
- Must use self-contained to avoid shared framework
- copy-wpf.ps1 script handles assembly copying

---

# Part 5: Core_Root/corerun Limitations

## What Core_Root CAN Test
- CLR/JIT changes (native code)
- GC modifications
- System.Private.CoreLib changes
- BCL (Base Class Library) changes
- Native runtime components

## What Core_Root CANNOT Test
- SDK commands (dotnet build, dotnet run, etc.)
- NuGet restore
- Project system / MSBuild
- apphost/hostfxr (host subset)
- Framework-dependent apps (must be self-contained)

## Depth Chart

| Layer | Core_Root Support |
|-------|-------------------|
| Native CLR (C++) | ✅ Full |
| JIT Compiler (C++) | ✅ Full |
| GC (C++) | ✅ Full |
| System.Private.CoreLib | ✅ Full |
| BCL (System.*) | ✅ Full |
| Host (hostfxr/hostpolicy) | ⚠️ Partial (separate build) |
| SDK Tasks | ❌ None |
| MSBuild | ❌ None |
| Roslyn Compiler | ❌ None (separate VSIX workflow) |
| NuGet Client | ❌ None |

---

# Part 6: Portability & Deployment to Other Machines

## What's Portable

| Artifact | Portability | Notes |
|----------|-------------|-------|
| Core_Root folder | ✅ xcopy-deployable | Contains everything needed for corerun |
| Self-contained publish | ✅ Fully portable | All runtime files included |
| Custom SDK (folder) | ✅ With env vars | Just set DOTNET_ROOT |

## Packaging Core_Root for Another Machine

```powershell
# On build machine
$CoreRoot = "artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"
Compress-Archive -Path $CoreRoot -DestinationPath "CustomRuntime.zip"

# On target machine
Expand-Archive -Path "CustomRuntime.zip" -DestinationPath "C:\CustomRuntime"
$env:CORE_ROOT = "C:\CustomRuntime"
corerun.exe App.dll
```

## Caveats
- Architecture must match (x64 → x64, ARM64 → ARM64)
- OS must match (Windows Core_Root won't work on Linux)
- May need VC++ redistributable on clean Windows
- Debug/Release configuration should be consistent

---

# Part 7: VSIX & VS Integration Details

## Roslyn VSIX Packages

| VSIX | Purpose |
|------|---------|
| Roslyn.Compilers.Extension.vsix | Command-line compilers for IDE builds |
| Roslyn.VisualStudio.Setup.vsix | Full IDE integration (IntelliSense, analyzers) |
| ExpressionEvaluatorPackage | Debugger expression evaluation |

## Installation Paths

- Main VS hive: Double-click .vsix
- Experimental instance: `VSIXInstaller.exe /experimental <path.vsix>`

## VS Experimental Instance

- Launch: `devenv.exe /rootSuffix Exp`
- With logging: `devenv.exe /rootSuffix Exp /log`
- Data location: `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_*Exp\`

## Process Management

```powershell
# Kill VS
taskkill /F /IM devenv.exe

# Kill dotnet processes
taskkill /F /IM dotnet.exe

# Kill MSBuild
taskkill /F /IM MSBuild.exe

# Kill Roslyn compiler server
taskkill /F /IM VBCSCompiler.exe
```

---

# Part 8: Key Files & Configurations

## Version Management
- `eng/Versions.props` - Package versions, assembly versions
- `eng/Version.Details.xml` - Dependency tracking
- `global.json` - SDK version pinning

## NuGet Configuration
Development feeds (add to NuGet.config):
```xml
<configuration>
  <packageSources>
    <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    <add key="dotnet9" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

## global.json Example
```json
{
  "sdk": {
    "version": "9.0.100",
    "rollForward": "latestFeature"
  }
}
```

---

# Part 9: Troubleshooting Quick Reference

## Build Fails

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| SDK not found | Wrong DOTNET_ROOT | Verify `dotnet --info` |
| Missing libraries | Libs not built | Build with `-subset clr+libs` |
| Core_Root empty | Generator not run | Run `generatelayoutonly` |
| VSIX won't install | VS running | Kill devenv.exe first |

## Runtime Issues

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| Wrong runtime used | CORE_ROOT not set | Set environment variable |
| Missing DLLs | CORE_LIBRARIES needed | Set additional paths |
| dotnet picks wrong SDK | MULTILEVEL_LOOKUP | Set to 0 |

## VS Issues

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| Old compiler used | VSIX not installed | Reinstall to Exp instance |
| IntelliSense wrong | Only CompilerExtension | Install VisualStudioSetup too |
| Build uses old | Need Toolset reference | Add Microsoft.Net.Compilers.Toolset |

---

*This document is part of the DOTNExT project documentation suite.*  
*See `/Docs/For AI/DOTNExT-Index.md` for full documentation structure.*
