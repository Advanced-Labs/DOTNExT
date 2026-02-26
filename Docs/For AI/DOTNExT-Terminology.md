# DOTNExT Terminology & Conventions

## Document Purpose

Shared language and conventions for the DOTNExT multi-agent development team.

**Last Updated:** 2025-12-04

---

# Role Codes

| Code | Full Name | Purpose |
|------|-----------|---------|
| SAGE | Platform R&D Expert | Questions, troubleshooting, workflow guidance |
| BLD | Build | Build execution for all components |
| DEP | Deploy | Environment setup, artifact placement |
| TST | Test | Test execution and validation |
| GIT | Repo | Git operations, VMR management |
| IMP | Code | Code implementation and debugging |

---

# Component Shortcodes

| Code | Component | Repository Path |
|------|-----------|-----------------|
| RT | Runtime (CLR + BCL) | `src/runtime` |
| CLR | CoreCLR only | `src/runtime/src/coreclr` |
| BCL | Base Class Libraries | `src/runtime/src/libraries` |
| ROS | Roslyn compiler | `src/roslyn` |
| SDK | .NET SDK | `src/sdk` |
| MSB | MSBuild | `src/msbuild` |
| ASP | ASP.NET Core | `src/aspnetcore` |
| WPF | WPF | `src/wpf` |
| WFM | WinForms | `src/winforms` |
| EFC | Entity Framework Core | `src/efcore` |
| ORL | Orleans (Scynapse fork) | `src/Scynapse` |
| FSH | F# | `src/fsharp` |
| RAZ | Razor | `src/razor` |

---

# State Indicators

Use these to communicate component state:

| Indicator | Meaning |
|-----------|---------|
| `[BUILT]` | Component has been built successfully |
| `[DEPLOYED]` | Component is deployed to test environment |
| `[TESTED]` | Component has passed tests |
| `[DIRTY]` | Component has uncommitted changes |
| `[STALE]` | Component needs rebuild (dependency changed) |
| `[CLEAN]` | No local modifications |
| `[CONFLICT]` | Merge conflict exists |

**Example usage:**
```
RT [BUILT] [DEPLOYED]
ROS [DIRTY]
SDK [STALE]
```

---

# Command Shortcuts

Quick commands Louis can use to dispatch tasks:

## Build Commands
```
@bld runtime      → BUILD: build runtime (clr+libs)
@bld clr          → BUILD: build CLR only
@bld libs         → BUILD: build libraries only
@bld roslyn       → BUILD: build roslyn
@bld sdk          → BUILD: build sdk
@bld coreroot     → BUILD: generate Core_Root
@bld vsix         → BUILD: build Roslyn VSIX
@bld wpf          → BUILD: build WPF
@bld winforms     → BUILD: build WinForms
```

## Deploy Commands
```
@dep corerun      → DEPLOY: set up Core_Root environment
@dep sdk          → DEPLOY: set up custom SDK environment
@dep vsexp        → DEPLOY: deploy VSIX to VS experimental
@dep vslaunch     → DEPLOY: launch VS with custom SDK
```

## Test Commands
```
@tst smoke        → TEST: run smoke tests
@tst corerun <app> → TEST: run app with corerun
@tst vsexp        → TEST: validate VS experimental
@tst dotnet       → TEST: run dotnet CLI tests
```

## Repo Commands
```
@git status       → REPO: show git status
@git sync         → REPO: sync with upstream
@git branch <n>   → REPO: create/switch branch
@git commit <msg> → REPO: commit with message
```

## Expert Commands
```
@sage <question>  → SAGE: ask workflow/troubleshooting question
@sage impact <x>  → SAGE: analyze impact of changing X
@sage workflow <x> → SAGE: which workflow for X?
```

---

# Escalation Format

When an agent needs to hand off work:

```
REQUEST TO LOUIS: [Summary of what's needed]
Completed: [What was done]
Next Step: [What needs to happen]
Recommended Role: [Which role should handle it]
Context: [Any relevant details]
```

**Example:**
```
REQUEST TO LOUIS: Build complete, ready for testing.
Completed: Built runtime (clr+libs) Release. Core_Root generated.
Next Step: Deploy Core_Root and run tests.
Recommended Role: DEP then TST
Context: Testing JIT optimization for loop unrolling.
```

---

# Path Conventions

## VMR Root
```
D:\Dev\DOTNExT\
```

## Source Directories
```
D:\Dev\DOTNExT\src\{component}\
```

## Build Artifacts

| Type | Path Pattern |
|------|--------------|
| Runtime binaries | `src/runtime/artifacts/bin/coreclr/<OS>.<Arch>.<Config>/` |
| Core_Root | `src/runtime/artifacts/tests/coreclr/<OS>.<Arch>.<Config>/Tests/Core_Root/` |
| Roslyn binaries | `src/roslyn/artifacts/bin/` |
| Roslyn VSIX | `src/roslyn/artifacts/VSSetup/<Config>/` |
| SDK | `src/sdk/artifacts/bin/redist/<Config>/dotnet/` |
| Packages | `*/artifacts/packages/<Config>/Shipping/` |

## Configuration Strings
```
windows.x64.Debug
windows.x64.Release
windows.x64.Checked
windows.arm64.Release
linux.x64.Release
osx.x64.Release
osx.arm64.Release
```

---

# Configuration Values

## Build Configurations

| Config | Use |
|--------|-----|
| Debug | Full debugging, slow, symbols |
| Release | Optimized, testing, deployment |
| Checked | Debug CLR + Release libs (CLR debugging) |

## Architectures

| Arch | Platform |
|------|----------|
| x64 | 64-bit Intel/AMD |
| x86 | 32-bit Intel/AMD |
| arm64 | 64-bit ARM |
| arm | 32-bit ARM |

## Runtime Identifiers (RID)

```
win-x64
win-x86
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

---

# Environment Variables

| Variable | Purpose |
|----------|---------|
| `CORE_ROOT` | Core_Root path for corerun |
| `CORE_LIBRARIES` | Additional assembly paths |
| `DOTNET_ROOT` | Custom SDK location |
| `DOTNET_MULTILEVEL_LOOKUP` | Set to 0 to isolate |
| `MSBuildSDKsPath` | MSBuild SDK location |
| `RoslynAssembliesPath` | Custom Roslyn path |

---

# Testing Terminology

| Term | Meaning |
|------|---------|
| Smoke test | Basic functionality check |
| Unit test | Test of single unit in isolation |
| Integration test | Test of components working together |
| Regression test | Verify old functionality still works |
| Core_Root test | Test using corerun with custom runtime |
| VS Exp test | Test in VS experimental instance |

---

# Workflow Types

| Type | When to Use |
|------|-------------|
| corerun | Runtime/BCL changes |
| SDK dogfood | SDK/CLI changes |
| VSIX experimental | Roslyn/IDE changes |
| Self-contained | WPF/WinForms/ASP.NET changes |
| Full stack | Cross-component changes |

---

# Communication Patterns

## Status Report
```
STATUS: [component]
State: [state indicators]
Branch: [branch name]
Last action: [what was done]
```

## Completion Report
```
COMPLETE: [action]
Result: [success/failure]
Output: [artifacts/results]
Next: [recommended next step]
```

## Error Report
```
ERROR: [what failed]
Symptom: [what happened]
Likely cause: [hypothesis]
Tried: [what was attempted]
Recommend: [next step]
```

---

*This document is part of the DOTNExT project documentation suite.*
