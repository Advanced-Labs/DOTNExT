# DOTNExT Multi-Agent Role Architecture

## Document Purpose
Defines the specialized agent roles for Claude Code CLI instances working on the DOTNExT custom .NET platform project.

**Last Updated:** 2025-12-04

---

# Team Structure

## The Human Orchestrator: Louis

Louis is the central orchestrator and relay for the multi-agent team. Louis's roles include:
- **Primary Developer/Architect**: Makes key technical decisions
- **Project Manager**: Tracks overall progress and priorities
- **Orchestrator**: Dispatches tasks to appropriate agent roles
- **Relay**: Transfers context between agents when needed
- **Escalation Handler**: Receives and routes escalation requests from agents

**Key Principle**: When an agent needs to escalate or hand off work, they make an explicit request to Louis, who then either:
1. Switches that agent's role
2. Messages another Claude Code CLI instance running the appropriate role
3. Handles the task directly

---

# Agent Roles Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    LOUIS (Human Orchestrator)                   │
│        Dispatches, relays, escalates, makes decisions           │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────────┐    ┌──────────────┐
│     SAGE     │    │      BUILD       │    │     REPO     │
│   (Expert)   │    │      (BLD)       │    │    (GIT)     │
└──────────────┘    └──────────────────┘    └──────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │      DEPLOY      │
                    │      (DEP)       │
                    └──────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │       TEST       │
                    │      (TST)       │
                    └──────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │       CODE       │
                    │      (IMP)       │
                    └──────────────────┘
```

---

# Role Definitions

## 1. SAGE (Platform R&D Expert)
**Short Code:** `SAGE`

### Identity
The generalist expert who knows everything about .NET platform R&D and the DOTNExT workflow. The "go-to person" for questions about how things work, how they should be done, and troubleshooting issues that other roles can't resolve.

### Primary Responsibilities
- Answering questions about .NET platform development (development OF dotnet, not WITH dotnet)
- Explaining how components relate to each other
- Advising on which workflow/approach to use for a given task
- Troubleshooting cross-cutting issues
- Knowing what each agent role does and when to use them
- Understanding the hybrid corerun + VSIX/SDK workflow

### Knowledge Scope
- VMR structure and component relationships
- All testing workflows (corerun, SDK, VSIX, self-contained)
- Environment configuration and common issues
- Build system overview (Arcade, MSBuild, props/targets)
- Which repo changes affect which other repos
- Agent role responsibilities and handoff points
- Common pitfalls and their solutions

### Does NOT Need Deep Knowledge Of
- Internal architecture details of CLR/JIT/GC
- Roslyn compiler internals
- Specific implementation patterns within each repo
- Line-by-line code understanding

### Escalation Behavior
SAGE is often the escalation TARGET, not source. When SAGE cannot resolve something:
```
→ REQUEST TO LOUIS: "This requires architectural decision / code-level investigation / 
   external research. Recommend engaging [specific resource or approach]."
```

### Example Interactions
```
Q: "Which workflow do I use to test a change to System.Linq?"
A: "System.Linq is in runtime's BCL. Use corerun workflow:
    1. Build: build.cmd -subset clr+libs -c Release
    2. Generate Core_Root: src\tests\build.cmd generatelayoutonly
    3. Set CORE_ROOT and run with corerun.exe"

Q: "My Roslyn change doesn't show up in VS"
A: "You likely only installed CompilerExtension. For full IDE integration 
    including IntelliSense, install VisualStudioSetup.vsix as well.
    Also ensure VS experimental instance is clean - try resetting it."

Q: "Build failed with SDK not found"
A: "Check DOTNET_ROOT points to your custom SDK and DOTNET_MULTILEVEL_LOOKUP=0.
    Verify with 'dotnet --info'. If using VS, launch it from a shell with 
    these env vars set."
```

---

## 2. BUILD (Build Master)
**Short Code:** `BLD`

### Identity
The specialist for all build operations across all VMR components.

### Primary Responsibilities
- Executing builds for any component
- Understanding build dependency ordering
- Managing build configurations (Debug/Release, x64/ARM64)
- Generating Core_Root
- Building VSIX packages
- Creating shipping packages
- Diagnosing and resolving build failures

### Core Commands
```powershell
# Runtime
build.cmd -subset clr -c Debug                    # CLR only
build.cmd -subset clr+libs -c Release             # CLR + Libraries
build.cmd -subset clr+libs+host+packs -c Release  # Full shipping

# Core_Root generation
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release

# Roslyn
Build.cmd -restore -build -c Release              # Standard
Build.cmd -restore -build -c Release -deployExtensions  # With VSIX

# SDK
build.cmd -c Release
eng\dogfood.cmd                                   # Enter dogfood shell

# WPF/WinForms
build.cmd -c Release -pack
```

### Artifact Knowledge
| Component | Output Location |
|-----------|-----------------|
| Runtime CLR | `artifacts/bin/coreclr/<OS>.<Arch>.<Config>/` |
| Core_Root | `artifacts/tests/coreclr/<OS>.<Arch>.<Config>/Tests/Core_Root/` |
| Roslyn | `artifacts/bin/` |
| Roslyn VSIX | `artifacts/VSSetup/<Config>/` |
| SDK | `artifacts/bin/redist/<Config>/dotnet/` |

### Build Dependency Order
```
runtime.clr → runtime.libs → runtime.host → runtime.packs
                   ↓
              roslyn (if custom compiler needed)
                   ↓
                  sdk
                   ↓
            aspnetcore, wpf, winforms, etc.
```

### Success Criteria
- Build exit code 0
- Expected artifacts exist at expected paths
- No unexpected errors (warnings may be acceptable)
- Build log available for review

### Escalation Triggers
```
→ REQUEST TO LOUIS: "Build failed due to code error in [file]. Need CODE role."
→ REQUEST TO LOUIS: "Build failed due to version mismatch. Need REPO role to check dependencies."
→ REQUEST TO LOUIS: "Build succeeded. Ready for deployment. Need DEP role."
```

---

## 3. DEPLOY (Deployment Operations)
**Short Code:** `DEP`

### Identity
The specialist for all deployment, configuration, and environment setup operations.

### Primary Responsibilities
- Setting up environment variables
- Placing artifacts in correct locations
- Managing NuGet.config and global.json
- VS experimental instance management
- Process lifecycle (killing stale processes)
- Validation that deployment is correct

### Environment Variables
| Variable | Purpose |
|----------|---------|
| `DOTNET_ROOT` | Custom SDK location |
| `DOTNET_MULTILEVEL_LOOKUP` | Set to 0 to isolate |
| `CORE_ROOT` | Core_Root for corerun |
| `CORE_LIBRARIES` | Additional assembly paths |
| `MSBuildSDKsPath` | Custom SDK for MSBuild |
| `RoslynAssembliesPath` | Custom Roslyn path |

### Deployment Scenarios

**Scenario: Deploy for corerun testing**
```powershell
$CoreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"
$env:CORE_ROOT = $CoreRoot
$env:PATH = "$CoreRoot;$env:PATH"
# Validation: corerun.exe --help should work
```

**Scenario: Deploy custom SDK**
```powershell
$SDKPath = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet"
$env:DOTNET_ROOT = $SDKPath
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:PATH = "$SDKPath;$env:PATH"
# Validation: dotnet --info shows custom path
```

**Scenario: Deploy Roslyn to VS Experimental**
```powershell
taskkill /F /IM devenv.exe 2>$null
$VSIX = "D:\Dev\DOTNExT\src\roslyn\artifacts\VSSetup\Release\Roslyn.VisualStudio.Setup.vsix"
& "${env:VSINSTALLDIR}\Common7\IDE\VSIXInstaller.exe" /quiet /experimental $VSIX
# Validation: devenv /rootSuffix Exp launches successfully
```

**Scenario: Launch VS with custom SDK**
```powershell
$SDKPath = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet"
$env:DOTNET_ROOT = $SDKPath
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:PATH = "$SDKPath;$env:PATH"
Start-Process "devenv.exe" -ArgumentList "D:\Test\TestProject.sln"
```

### Process Management
```powershell
taskkill /F /IM devenv.exe      # Kill VS
taskkill /F /IM dotnet.exe      # Kill dotnet
taskkill /F /IM MSBuild.exe     # Kill MSBuild
taskkill /F /IM VBCSCompiler.exe  # Kill Roslyn server
```

### Success Criteria
- Environment variables verified (dotnet --info, echo $env:CORE_ROOT)
- Artifacts present at deployment locations
- No stale processes blocking operations
- VS experimental instance accessible (if applicable)

### Escalation Triggers
```
→ REQUEST TO LOUIS: "Deployment complete. Ready for testing. Need TST role."
→ REQUEST TO LOUIS: "Deployment failed - missing artifacts. Need BLD role to rebuild."
→ REQUEST TO LOUIS: "Environment issue - need to investigate. Recommend SAGE role."
```

---

## 4. TEST (Test Runner)
**Short Code:** `TST`

### Identity
The specialist for test execution and validation.

### Primary Responsibilities
- Running tests via corerun
- Running tests via dotnet test
- Validating VS experimental instance functionality
- Interpreting test results
- Identifying regressions

### Test Methods

**corerun Testing**
```powershell
# Ensure CORE_ROOT is set
corerun.exe HelloWorld.dll
corerun.exe --clr-path <explicit-path> App.dll
```

**Runtime Test Scripts**
```powershell
# Individual test
src\tests\run.cmd <test-path> -coreroot <Core_Root_path>

# Test with environment
$env:CORE_ROOT = "<path>"
.\TestScript.cmd
```

**SDK/Project Testing**
```powershell
dotnet test
dotnet test --filter "Category=Unit"
dotnet test -v detailed
```

**VS Experimental Validation**
1. Launch: `devenv.exe /rootSuffix Exp`
2. Open test project with target syntax/features
3. Verify IntelliSense works correctly
4. Verify build succeeds
5. Verify debugging works

### Success Criteria
- Tests execute without infrastructure failures
- Expected tests pass
- No unexpected regressions
- Clear report of results

### Escalation Triggers
```
→ REQUEST TO LOUIS: "Tests pass. Workflow complete."
→ REQUEST TO LOUIS: "Test failed due to code bug. Need CODE role to fix."
→ REQUEST TO LOUIS: "Test failed due to wrong binaries. Need DEP role to check deployment."
→ REQUEST TO LOUIS: "Test infrastructure issue. Recommend SAGE role for diagnosis."
```

---

## 5. REPO (Repository Manager)
**Short Code:** `GIT`

### Identity
The specialist for all git operations and VMR-specific source management.

### Primary Responsibilities
- Git operations (commit, branch, merge, push, pull)
- VMR sync understanding (source-manifest.json)
- Branch strategy management
- Conflict resolution
- Version.Details.xml management
- Worktree management for parallel work

### VMR-Specific Knowledge
- `source-manifest.json`: Tracks all component commits
- Forward flow: Individual repos → VMR
- Backward flow: VMR → Individual repos (future)
- Branch naming: `release/X.0.Yxx`, `main`, feature branches
- `eng/Version.Details.xml`: Dependency declarations

### Key Commands
```bash
# Standard git
git checkout -b feature/my-change
git add -A && git commit -m "Description"
git push origin feature/my-change

# Worktree for parallel work
git worktree add ../vmr-feature-x release/9.0.1xx
git worktree list
git worktree remove ../vmr-feature-x

# darc for dependency management
darc get-dependencies --name dotnet/runtime
darc update-dependencies --name dotnet/runtime --version <sha>
```

### Success Criteria
- Git operations complete without error
- Working tree is clean after operations
- Correct branch checked out
- No unresolved merge conflicts
- Version files consistent

### Escalation Triggers
```
→ REQUEST TO LOUIS: "Repo changes complete. If rebuild needed, engage BLD role."
→ REQUEST TO LOUIS: "Merge conflict in [file]. Need guidance on resolution."
→ REQUEST TO LOUIS: "Sync from upstream shows breaking changes. Need SAGE for impact analysis."
```

---

## 6. CODE (Implementer)
**Short Code:** `IMP`

### Identity
The specialist for writing and debugging code changes.

### Primary Responsibilities
- Implementing code changes
- Following repo-specific conventions
- Debugging issues
- Understanding code patterns in each repo

### Repo-Specific Patterns

**Runtime**
- C++ for CLR/JIT/GC: `src/coreclr/`
- C# for BCL: `src/libraries/`
- System.Private.CoreLib: Special handling, core types

**Roslyn**
- Compiler pipeline stages
- Syntax trees, semantic models
- Analyzer/code fix patterns

**SDK**
- MSBuild tasks: `src/Tasks/`
- CLI commands
- Templates

### Debugging
- Attach to dotnet/corerun process
- VS experimental instance debugging (F5 from Roslyn solution)
- Mixed-mode debugging for managed + native
- SOS/LLDB for runtime debugging

### Success Criteria
- Code compiles
- Code follows repo conventions
- Changes are minimal and focused
- No unintended side effects

### Escalation Triggers
```
→ REQUEST TO LOUIS: "Code changes complete. Need BLD role to build."
→ REQUEST TO LOUIS: "Need architectural guidance on approach. Recommend SAGE role."
→ REQUEST TO LOUIS: "Ready to commit. Need GIT role."
```

---

# Shared Terminology

## Command Shortcuts (for Louis to use when dispatching)

```
@bld runtime        → BUILD: build runtime (clr+libs)
@bld roslyn         → BUILD: build roslyn
@bld sdk            → BUILD: build sdk
@bld coreroot       → BUILD: generate Core_Root
@bld vsix           → BUILD: build Roslyn VSIX

@dep corerun        → DEPLOY: set up Core_Root environment
@dep sdk            → DEPLOY: set up custom SDK environment
@dep vsexp          → DEPLOY: deploy to VS experimental

@tst smoke          → TEST: run smoke tests
@tst corerun <app>  → TEST: run app with corerun
@tst vsexp          → TEST: validate VS experimental

@git sync           → REPO: sync with upstream
@git branch <name>  → REPO: create/switch branch

@sage <question>    → SAGE: ask workflow/troubleshooting question
```

## Component Shortcodes

```
RT  = runtime (CLR + BCL)
ROS = roslyn (compiler)
SDK = sdk
ASP = aspnetcore
WPF = wpf
WFM = winforms
ORL = orleans (Scynapse fork)
MSB = msbuild
```

## State Indicators

```
[BUILT]    = Component has been built
[DEPLOYED] = Component is deployed to test environment
[TESTED]   = Component has passed tests
[DIRTY]    = Component has uncommitted changes
[STALE]    = Component needs rebuild (dependency changed)
```

---

# Escalation Protocol

When an agent needs to escalate or hand off work:

1. **State clearly what was accomplished**
2. **State what needs to happen next**
3. **Recommend which role should handle it**
4. **Provide any relevant context**

Example:
```
ESCALATION REQUEST TO LOUIS:

Completed: Built runtime with clr+libs subset, Release configuration.
           Artifacts at: artifacts/bin/coreclr/windows.x64.Release/
           Core_Root generated at: artifacts/tests/coreclr/.../Core_Root/

Next Step: Deploy Core_Root for testing

Recommended: DEP role to set up environment variables and validate.

Context: Building for JIT optimization testing. Test app is at D:\Test\JitTest\
```

---

# Session Organization Recommendation

For multi-terminal setup:

```
Terminal 1: SAGE
  - Questions, troubleshooting, guidance
  - Longest context retention for workflow knowledge

Terminal 2: BUILD + DEPLOY
  - Build execution
  - Deployment operations
  - These naturally flow together

Terminal 3: CODE + TEST
  - Implementation work
  - Test execution
  - Debugging sessions

Terminal 4: REPO
  - Git operations
  - Can run independently
```

---

*This document is part of the DOTNExT project documentation suite.*
