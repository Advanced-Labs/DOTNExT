# NewOrleans Project - Detailed File Inventory for Renaming

## Purpose

This document provides a concrete, actionable inventory of **every file** that contains references to "NewOrleans", "New Orleans", or related naming patterns. Use this as your checklist when performing the rename.

---

## Files Organized by Category

### Category 1: Source Code Files with "NewOrleans" in Namespace or Class Names

These files **MUST** be updated to change namespace declarations and class names.

#### NewOrleans.AsyncPlus Component
Located: `src/NewOrleans/src/NewOrleans.AsyncPlus/`

| File Path | Current Name | Update Required | Type |
|---|---|---|---|
| `NewOrleans.AsyncPlus.csproj` | Assembly: NewOrleans.AsyncPlus | **RENAME FILE** | Project file |
| `Services/NewOrleansAsyncPersistenceService.cs` | Class: `NewOrleansAsyncPersistenceService` | Namespace + Class | C# source |
| `Storage/RavenDbGrainStorage.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace | C# source |
| `Storage/RavenDbStorageOptions.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace | C# source |
| `Grains/AsyncStatePersistenceGrain.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace | C# source |
| `Abstractions/IAsyncStatePersistenceGrain.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace | C# source |
| `Abstractions/AsyncStateCheckpoint.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace | C# source |
| `Extensions/AsyncPlusHostingExtensions.cs` | Namespace: `NewOrleans.AsyncPlus` | Namespace + Methods | C# source |

---

### Category 2: Playground Project Files with References

Located: `src/NewOrleans/playground/`

#### AsyncPersistenceScenarios

| File Path | References | Update Required |
|---|---|---|
| `AsyncPersistenceScenarios/AsyncPersistenceScenarios.csproj` | Project references to NewOrleans.AsyncPlus | `<ProjectReference>` paths |
| `AsyncPersistenceScenarios/Program.cs` | `using NewOrleans.AsyncPlus;` | Using statements + imports |
| `AsyncPersistenceScenarios/Services/IAsyncPersistenceService.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Helpers/SiloHelper.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/CrossSessionPersistence.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/ExceptionRecovery.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/GrainMobility.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/MultiSiloCheckpointVisibility.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/MultipleConcurrentWorkflows.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/NestedAsyncCalls.cs` | `using NewOrleans.AsyncPlus;` | Using statements |
| `AsyncPersistenceScenarios/Scenarios/RoslynPlusCrossSession.cs` | `using NewOrleans.AsyncPlus;` | Using statements |

#### PluginGrainScenarios

| File Path | References | Update Required |
|---|---|---|
| `PluginGrainScenarios/Program.cs` | Event scenario code with NewOrleans references | Code references |
| `PluginGrainScenarios/Grains/EventTestGrain.cs` | Grain event handling | Check for NewOrleans refs |
| `PluginGrainScenarios/Scenarios/EventScenario.cs` | Event scenario tests | Check for NewOrleans refs |

---

### Category 3: Orleans Core Framework Files

Located: `src/NewOrleans/src/Orleans.*/`

| Component | Has "NewOrleans" | Notes |
|---|---|---|
| `Orleans.Core.Abstractions/Events/NotEventAttribute.cs` | Yes | Check for comments/docs |
| `Orleans.CodeGenerator/ProxyGenerator.cs` | Yes | Check for comments/docs |
| `Orleans.CodeGenerator/EventCodeGenerator.cs` | Yes | Check for comments/docs |
| `Orleans.CodeGenerator/CodeGenerator.cs` | Yes | Check for comments/docs |

---

### Category 4: Root Solution & Configuration Files

Located: `src/NewOrleans/` (root directory)

| File | Type | Contains | Update |
|---|---|---|---|
| `Orleans.slnx` | Solution | Project paths, assembly references | Project references |
| `Directory.Build.props` | Build config | Shared properties | Check paths |
| `Directory.Build.targets` | Build config | Shared build targets | Check paths |
| `NuGet.Config` | Package config | Feed sources | May have NewOrleans refs |
| `Directory.Packages.props` | Package config | Package versions | Version refs |
| `global.json` | .NET config | SDK/version info | Check paths |

---

### Category 5: Build & Test Scripts

Located: `src/NewOrleans/` (root directory)

| File | Type | Contains | Update |
|---|---|---|---|
| `Build.cmd` | Script | Build instructions, paths | Hardcoded paths |
| `build.ps1` | PowerShell | Build logic, paths | Hardcoded paths |
| `Test.cmd` | Script | Test execution, paths | Hardcoded paths |
| `TestAll.cmd` | Script | Test suite runner | Hardcoded paths |
| `Parallel-Tests.ps1` | PowerShell | Parallel test execution | Hardcoded paths |
| `common.ps1` | PowerShell | Shared utilities | Utility references |

---

### Category 6: CI/CD Pipeline Files

Located: `src/NewOrleans/.azure/pipelines/`

| File | Type | Contains | Update |
|---|---|---|---|
| `build.yaml` | Azure Pipelines | Build pipeline | Project names, artifact names |
| `nightly-main.yaml` | Azure Pipelines | Nightly build config | Paths, project references |
| `github-mirror.yaml` | Azure Pipelines | GitHub sync | Project paths |
| `templates/build.yaml` | Build template | Shared build steps | Project references |
| `templates/vars.yaml` | Variables | Build variables | Variable values with project names |

---

### Category 7: Configuration & Metadata

Located: `src/NewOrleans/` and subdirectories

| File | Location | Type | Update |
|---|---|---|---|
| `.editorconfig` | Root | Editor config | File paths if present |
| `.gitignore` | Root | Git config | Path patterns |
| `.gitattributes` | Root | Git config | Path patterns |
| `LICENSE` | Root | License | Project name if referenced |
| `README.md` | Root | Documentation | Text references |
| `SUPPORT.md` | Root | Support info | Project name |
| `CONTRIBUTING.md` | Root | Contribution guide | Project name |
| `CODE-OF-CONDUCT.md` | Root | Code of conduct | Project name if present |

---

### Category 8: IDE Configuration

Located: `src/NewOrleans/`

| File | Type | Contains | Update |
|---|---|---|---|
| `.vscode/launch.json` | VS Code | Debug configurations | Project/assembly names |
| `.vscode/tasks.json` | VS Code | Build tasks | Commands with project names |
| `.devcontainer/devcontainer.json` | Dev Container | Container config | Paths, project names |
| `.github/copilot-instructions.md` | GitHub | Copilot config | Project references |

---

### Category 9: GitHub Configuration

Located: `src/NewOrleans/.github/`

| File | Type | Contains | Update |
|---|---|---|---|
| `dependabot.yml` | Dependencies | Package references | If NewOrleans packages listed |
| `policies/resourceManagement.yml` | Policy | Resource policies | Project references if present |

---

### Category 10: Documentation Files - Primary

Located: `Docs/New Orleans/`

| File | Type | Current Name | **RENAME FILE** |
|---|---|---|---|
| `New Orleans.md` | Markdown | Main documentation | **YES** |
| `New Orleans Features/` | Directory | Feature docs folder | **YES** |

---

### Category 11: Documentation Files - Features Subfolder

Located: `Docs/New Orleans/New Orleans Features/`

| File | Type | Change | Notes |
|---|---|---|---|
| `DynamicGrainAccess.md` | Markdown | Update internal references | To new project name |
| `OrleansAsync+.md` | Markdown | Update internal references | To new project name |
| `PluginGrainArchitecture.md` | Markdown | Update internal references | To new project name |
| `StatePropertyAccess.md` | Markdown | Update internal references | To new project name |

---

### Category 12: Research & Reference Documents

Located: `Research/NewOrleans/`

| File | Type | Name Format | Update |
|---|---|---|---|
| `orleans-state-properties-design.md` | Markdown | `orleans-*` (lowercase prefix) | Content references |
| `neworleans-events-v1.md` | Markdown | `neworleans-*` (lowercase) | File rename + content |
| `neworleans-client-principals.md` | Markdown | `neworleans-*` (lowercase) | File rename + content |

**Directory**: `Research/NewOrleans/` → **RENAME DIRECTORY**

---

### Category 13: Documentation References in Main Docs

Located: `Docs/For AI/` and `Docs/VAYRON/`

| File | Location | Contains | Update |
|---|---|---|---|
| `DOTNExT-Master-Reference.md` | `Docs/For AI/` | References to NewOrleans | Text references |
| `DOTNExT-Terminology.md` | `Docs/For AI/` | Project terminology | Definitions |
| `DOTNExT-Agent-Roles.md` | `Docs/For AI/` | Role references | If mentions NewOrleans |
| `DOTNExT-Index.md` | `Docs/For AI/` | Navigation links | Links to `/Docs/New Orleans/` |
| `VAYRON-R1-Platform-Vision.md` | `Docs/VAYRON/` | Integration vision | NewOrleans references |
| `VAYRON-R1-Roadmap-and-Codebase-Map.md` | `Docs/VAYRON/` | Roadmap | Component references |
| `Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md` | `Docs/VAYRON/` | Phase planning | NewOrleans integration |
| `README.md` | `Docs/VAYRON/` | Overview | Project references |

---

### Category 14: Project Root Files

Located: `/.claude/` and repository root

| File | Type | Contains | Update |
|---|---|---|---|
| `CLAUDE.md` | Markdown | Project docs | Multiple NewOrleans references |
| `CLAUDE.md` section: VMR Structure | Documentation | Directory listing | `/src/NewOrleans/` path |
| `CLAUDE.md` section: Documentation System | Documentation | Docs folder structure | `/Docs/New Orleans/` reference |

---

### Category 15: AI Context Files

Located: `AI-Contexts/Claude-Opus/`

| File | Type | Contains | Update |
|---|---|---|---|
| `NewOrleans-AsyncPlus-Integration.md` | **FILE RENAME** | Project integration notes | File name + content |
| `AsyncPlus-SiloPatterns.md` | Markdown | NewOrleans references | Text references |
| `AsyncDistributedComputing-Assessment.md` | Markdown | Project references | Content references |
| `DynamicGrainAccess.md` | Markdown | Project notes | Content references |
| `DOTNExT-Vision.md` | Markdown | Architecture vision | NewOrleans integration |
| `CURRENT-WORK.md` | Markdown | Active work | Project references |
| `CONTINUATION-PROTOCOL.md` | Markdown | Protocol docs | If mentions NewOrleans |
| `README.md` | Markdown | Context overview | Project references |
| `ROSLYN-BUILD-PROCEDURES.md` | Markdown | Build procedures | If mentions NewOrleans |
| `SESSION-LOG.md` | Markdown | Session history | Historical references |

---

### Category 16: Miscellaneous Documentation

Located: `Docs/to install/`

| File | Type | Contains | Update |
|---|---|---|---|
| `CLAUDE-revised.md` | Markdown | Copy of CLAUDE.md | NewOrleans references |
| `DOTNExT-Master-Reference-revised.md` | Markdown | Reference copy | NewOrleans references |
| `SAGE-role-prompt-revised.md` | Markdown | Role prompt | If mentions NewOrleans |
| `DOTNExT-Index-revised.md` | Markdown | Index copy | Navigation links |
| `DEPLOYMENT-GUIDE-revised.md` | Markdown | Deployment info | Project references |

---

## Renaming Tasks Checklist

### Phase 1: Structural Changes
- [ ] Create git branch: `claude/rename-neworleans-to-[NEWNAME]`
- [ ] Backup current state
- [ ] Rename directory: `src/NewOrleans/` → `src/[NEWNAME]/`
- [ ] Rename directory: `Docs/New Orleans/` → `Docs/[NEWNAME]/`
- [ ] Rename directory: `Research/NewOrleans/` → `Research/[NEWNAME]/`

### Phase 2: File Renames
- [ ] Rename in `src/[NEWNAME]/src/`: `NewOrleans.AsyncPlus.csproj` → `[NEWNAME].AsyncPlus.csproj`
- [ ] Rename in `Docs/[NEWNAME]/`: `New Orleans.md` → `[NEWNAME].md`
- [ ] Rename in `Docs/[NEWNAME]/`: `New Orleans Features/` → `[NEWNAME] Features/`
- [ ] Rename in `Research/[NEWNAME]/`: `neworleans-*.md` → `[newname]-*.md`
- [ ] Rename in `AI-Contexts/Claude-Opus/`: `NewOrleans-AsyncPlus-Integration.md` → `[NEWNAME]-AsyncPlus-Integration.md`
- [ ] Update solution file: `Orleans.slnx` (if referencing by path)

### Phase 3: Code Namespace Updates
- [ ] Update all `namespace NewOrleans.*;` → `namespace [NEWNAME].*;`
- [ ] Update all `using NewOrleans.*;` → `using [NEWNAME].*;`
- [ ] Update class names: `NewOrleansAsyncPersistenceService` → `[NEWNAME]AsyncPersistenceService`

### Phase 4: Project File Updates
- [ ] Update `<AssemblyName>` in .csproj files
- [ ] Update `<RootNamespace>` in .csproj files
- [ ] Update `<ProjectReference>` paths in .csproj files
- [ ] Update `<PackageId>` if publishing NuGet packages
- [ ] Update project references in solution file

### Phase 5: Configuration Updates
- [ ] Update `Directory.Build.props` with new assembly names
- [ ] Update `Directory.Packages.props` with new package versions if needed
- [ ] Update `NuGet.Config` if needed
- [ ] Check and update `.azure/pipelines/` variables and paths
- [ ] Update `.vscode/launch.json` with new assembly names
- [ ] Update `.vscode/tasks.json` with new build commands

### Phase 6: Script Updates
- [ ] Update `Build.cmd` with new project paths
- [ ] Update `build.ps1` with new paths and names
- [ ] Update `Test.cmd` with new project references
- [ ] Update `TestAll.cmd` with new project references
- [ ] Update `Parallel-Tests.ps1` with new project paths
- [ ] Update `common.ps1` utility references

### Phase 7: Documentation Updates
- [ ] Update content in renamed `.md` files (search for old project name references)
- [ ] Update `CLAUDE.md` with new directory path
- [ ] Update `Docs/For AI/DOTNExT-Index.md` with new links
- [ ] Update `Docs/VAYRON/` files with new references
- [ ] Update all cross-references between documentation files
- [ ] Update any README files with project name

### Phase 8: Verification
- [ ] Build solution: `Build.cmd`
- [ ] Run tests: `Test.cmd`
- [ ] Verify no compiler errors from namespace changes
- [ ] Check git status for all renamed files
- [ ] Grep for remaining old project name references
- [ ] Test playground projects
- [ ] Verify CI/CD pipelines still reference correct paths

### Phase 9: Final Commit
- [ ] Stage all changes: `git add .`
- [ ] Create commit with comprehensive message
- [ ] Push to feature branch

---

## Search Commands for Finding Remaining References

After completing the rename, use these commands to verify all references have been updated:

```bash
# Search for old project name in all files
grep -r "NewOrleans" /home/user/DOTNExT/ --include="*.cs" --include="*.csproj" --include="*.md" --include="*.json" --include="*.yaml" --include="*.ps1"

# Search for old documentation folder name
grep -r "New Orleans" /home/user/DOTNExT/Docs/ --include="*.md"

# Search for old path in documentation
grep -r "src/NewOrleans" /home/user/DOTNExT/ --include="*.md"

# Search in just the primary directory
grep -r "NewOrleans" /home/user/DOTNExT/src/[NEWNAME]/

# Verify solution references
grep -r "NewOrleans\|New Orleans" /home/user/DOTNExT/.claude/
```

---

## Important Notes

1. **Case Sensitivity**: Be careful with case - "NewOrleans" vs "neworleans" are different
2. **Partial Matches**: Watch for files like `neworleans-*.md` that use lowercase
3. **Documentation Consistency**: Decide if docs use different naming (e.g., "New Project Name" with spaces vs "NewProjectName" in code)
4. **Test Thoroughly**: Build and test after each major phase
5. **Git Preservation**: Use `git mv` for renaming to preserve history
6. **Backup**: Always have a backup before bulk find-and-replace operations

---

## File Count Summary

- **Directories to rename**: 3
- **Files to rename**: 4+ (project files, doc files, context files)
- **C# source files to update**: 10+
- **Configuration files to update**: 15+
- **Build scripts to update**: 6
- **Documentation files to update**: 15+
- **CI/CD files to update**: 4
- **Total files affected**: 80+

