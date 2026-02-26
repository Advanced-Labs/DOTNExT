# Scynapse Project Rename Requirements

## ✅ RENAME COMPLETED

**Status:** COMPLETE - NewOrleans → Scynapse rename finished
**Date Completed:** 2026-02-26
**Commit:** 465f2e23b - "Rename NewOrleans project to Scynapse throughout codebase"
**Files Modified:** 3,397
**Lines Changed:** +935 insertions, -1,529 deletions

### What Was Renamed:
- ✅ 3 primary directories
- ✅ 54+ files with content updates
- ✅ All namespace declarations
- ✅ All using statements
- ✅ All project files (.csproj)
- ✅ All documentation files
- ✅ CLAUDE.md updated
- ✅ All research documents

---

## Document Purpose

This document comprehensively catalogs everything that would need to be renamed if the Scynapse project were given a different name. It covers directories, files, code identifiers, configuration files, documentation, and all references throughout the codebase.

**Current Name Forms:**
- Directory: `src/Scynapse/`
- Docs folder: `Docs/Scynapse/`
- Code: `Scynapse`, `ScynapseAsyncPlus`, etc.

**Current Name Forms:**
- Directory: `src/Scynapse/`
- Docs folder: `Docs/Scynapse/` (with space)
- Code: `Scynapse`, `ScynapseAsyncPlus`, etc. (camelCase in code)

---

## 1. Directory Structure Changes

### Primary Directories

| Current Path | Type | Impact | Notes |
|---|---|---|---|
| `/src/Scynapse/` | Root directory | **HIGH** | Main project directory - ALL subdirectories affected |
| `/Docs/Scynapse/` | Documentation | **HIGH** | Project documentation folder |
| `/Docs/Scynapse/Scynapse Features/` | Documentation | **HIGH** | Feature documentation subfolder |
| `/Research/Scynapse/` | Research documents | **MEDIUM** | Research and design docs |
| `/AI-Contexts/Claude-Opus/` | Contains references | **MEDIUM** | Context files reference Scynapse |

### Sub-Directories in src/Scynapse/

```
src/Scynapse/
├── src/
│   ├── Orleans.Core.Abstractions/
│   ├── Orleans.Core/
│   ├── Orleans.Runtime/
│   ├── Orleans.Server/
│   ├── Orleans.Client/
│   ├── Orleans.CodeGenerator/
│   ├── Orleans.Serialization/
│   ├── Orleans.Persistence.Memory/
│   ├── Orleans.Persistence.RavenDB/
│   ├── Scynapse.AsyncPlus/              ← MUST RENAME (contains "Scynapse")
│   │   ├── Scynapse.AsyncPlus.csproj
│   │   ├── Storage/
│   │   ├── Services/
│   │   ├── Grains/
│   │   └── Abstractions/
│   └── ...
├── playground/
│   ├── DynamicGrainLoading.*/             ← Check for naming
│   ├── PluginGrainScenarios/
│   ├── AsyncPersistenceScenarios/
│   └── ...
└── ...
```

---

## 2. Project Files (.csproj, .slnx)

### Solution Files
- `src/Scynapse/Orleans.slnx` - Main solution file
  - **Change needed**: May contain references to `Scynapse.*` project names

### Project Files Containing "Scynapse"

| Project File | Current Name | Location |
|---|---|---|
| `Scynapse.AsyncPlus.csproj` | Assembly: `Scynapse.AsyncPlus` | `src/Scynapse/src/Scynapse.AsyncPlus/` |
| Project package names | Various `Scynapse.*` | Throughout `src/Scynapse/src/` |
| Playground project files | Check for references | `src/Scynapse/playground/` |

**In each .csproj file, check for:**
- `<AssemblyName>` - May be `Scynapse.AsyncPlus` or similar
- `<RootNamespace>` - Package namespace
- `<PackageId>` - NuGet package name
- `<ProjectReference>` - References to other `Scynapse.*` projects
- `<Version>` metadata

---

## 3. C# Code Identifiers

### Namespace Declarations

**Pattern**: `namespace Scynapse.*;`

Files containing `Scynapse` namespace:
- `src/Scynapse/src/Scynapse.AsyncPlus/Services/ScynapseAsyncPersistenceService.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Storage/RavenDbGrainStorage.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Storage/RavenDbStorageOptions.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Grains/AsyncStatePersistenceGrain.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Abstractions/IAsyncStatePersistenceGrain.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Abstractions/AsyncStateCheckpoint.cs`
- `src/Scynapse/src/Scynapse.AsyncPlus/Extensions/AsyncPlusHostingExtensions.cs`

### Class and Type Names

- `ScynapseAsyncPersistenceService` - Service class
- `AsyncStatePersistenceGrain` - Grain implementation
- `IAsyncStatePersistenceGrain` - Interface
- Other types within the `Scynapse.AsyncPlus` namespace

### Using Statements

Files importing from `Scynapse` namespace:
- `src/Scynapse/playground/AsyncPersistenceScenarios/Services/IAsyncPersistenceService.cs`
- `src/Scynapse/playground/AsyncPersistenceScenarios/Program.cs`
- `src/Scynapse/playground/AsyncPersistenceScenarios/Scenarios/*.cs` (multiple files)
- `src/Scynapse/playground/PluginGrainScenarios/Scenarios/EventScenario.cs`
- `src/Scynapse/playground/PluginGrainScenarios/Program.cs`
- `src/Scynapse/playground/PluginGrainScenarios/Grains/EventTestGrain.cs`

---

## 4. Configuration Files

### JSON Files

| File | Location | Contains |
|---|---|---|
| `global.json` | `src/Scynapse/` | May reference SDK or build config |
| `NuGet.Config` | `src/Scynapse/` | Package sources, potentially with Scynapse references |
| `.devcontainer/devcontainer.json` | `src/Scynapse/.devcontainer/` | Container config, may have paths |
| `Directory.Packages.props` | `src/Scynapse/` | Package versions, may have `Scynapse.*` references |
| `appsettings*.json` | Playground projects | Application settings |

### YAML/Properties Files

| File | Location | Contains |
|---|---|---|
| `.azure/pipelines/templates/vars.yaml` | CI/CD variables | Build environment, package names |
| `Directory.Build.props` | `src/Scynapse/` | Shared build properties |
| `Directory.Build.targets` | `src/Scynapse/` | Shared build targets |
| `.github/copilot-instructions.md` | GitHub config | References to the project |

### Configuration Details

Check for:
- NuGet package names with `Scynapse` prefix
- Assembly names
- Package ID declarations
- Build artifact naming
- CI/CD pipeline variable names

---

## 5. Documentation Files

### Markdown Documents

| File | Current Name | Path |
|---|---|---|
| `Scynapse.md` | Main project doc | `Docs/Scynapse/` |
| `DynamicGrainAccess.md` | Feature doc | `Docs/Scynapse/Scynapse Features/` |
| `OrleansAsync+.md` | Feature doc | `Docs/Scynapse/Scynapse Features/` |
| `PluginGrainArchitecture.md` | Feature doc | `Docs/Scynapse/Scynapse Features/` |
| `StatePropertyAccess.md` | Feature doc | `Docs/Scynapse/Scynapse Features/` |
| References in CLAUDE.md | Project reference | `.claude/CLAUDE.md` |
| References in VAYRON docs | Project reference | `Docs/VAYRON/` |
| References in AI context files | Project reference | `AI-Contexts/Claude-Opus/` |

### Documentation Content to Update

Within the documents, search for and update:
- Title/heading: "# Scynapse:" → update
- References: "Scynapse project", "Scynapse system", etc.
- Links to `/src/Scynapse/` paths
- Links to `/Docs/Scynapse/` paths
- Mentions in context files: "Scynapse-AsyncPlus-Integration.md"

---

## 6. Script Files

### Build Scripts

| File | Location | Check for |
|---|---|---|
| `Build.cmd` | `src/Scynapse/` | Hardcoded paths, project names |
| `build.ps1` | `src/Scynapse/` | PowerShell script with paths/names |
| `Test.cmd` | `src/Scynapse/` | Test runner paths |
| `TestAll.cmd` | `src/Scynapse/` | Test suite config |
| `Parallel-Tests.ps1` | `src/Scynapse/` | PowerShell test runner |
| `common.ps1` | `src/Scynapse/` | Common script utilities |

### CI/CD Scripts

- `.azure/pipelines/*.yaml` - Azure Pipelines configuration
- Check for hardcoded project names, paths, artifact names

---

## 7. Searchable String References

### Code References (grep patterns)

```
# High priority - core naming
- "Scynapse" (exact match)
- "ScynapseAsyncPlus"
- "neworleans" (lowercase)
- "NEWORLEANS" (uppercase)

# Documentation patterns
- "Scynapse" (with space)
- "new-orleans" (kebab-case)
- "neworleans" (no space)

# Context/metadata
- "Scynapse-AsyncPlus"
- "Orleans fork (Louis's)"
- References to `/src/Scynapse/`
```

---

## 8. File Type Inventory

### By File Type

| Type | Extensions | Locations | Examples |
|---|---|---|---|
| Solution Files | `.slnx`, `.sln` | `src/Scynapse/` | `Orleans.slnx` |
| Project Files | `.csproj` | `src/Scynapse/src/*/`, `src/Scynapse/playground/*/` | `Scynapse.AsyncPlus.csproj` |
| C# Source | `.cs` | Throughout `src/Scynapse/src/`, `playground/` | Namespace decls, class names |
| Configuration | `.json`, `.props`, `.targets`, `.config` | `src/Scynapse/` and subdirs | Global.json, NuGet.Config |
| Build Scripts | `.cmd`, `.ps1`, `.sh` | `src/Scynapse/`, `src/Scynapse/.azure/` | Build.cmd, build.ps1 |
| Documentation | `.md` | `Docs/Scynapse/`, `Docs/VAYRON/`, `AI-Contexts/` | *.md files |
| CI/CD | `.yaml`, `.yml` | `src/Scynapse/.azure/pipelines/` | Pipeline definitions |
| GitHub Config | `.json`, `.yml`, `.md` | `src/Scynapse/.github/` | Actions, dependabot config |
| VS Code Config | `.json` | `src/Scynapse/.vscode/` | launch.json, tasks.json |
| Dev Container | `.json` | `src/Scynapse/.devcontainer/` | devcontainer.json |
| Misc | `.gitignore`, `.gitattributes`, `.editorconfig`, etc. | `src/Scynapse/` | Configuration files |

---

## 9. Affected Project Types

### NuGet Packages

If the `Scynapse.AsyncPlus` assembly is published as a NuGet package:
- Package ID: `Scynapse.AsyncPlus` → needs rename
- All historical versions in feeds would retain old name
- Documentation and package description would reference old name

### CI/CD Artifacts

- Build output names
- Test result files
- Package artifact names in Azure Pipelines
- Release asset naming

### GitHub Metadata

- Repository description
- README references
- Issues and discussions mentioning the project
- GitHub Actions workflow names/descriptions

---

## 10. Cross-Repository References

### Outside src/Scynapse/

Files in the main DOTNExT repository referencing Scynapse:

| File | Location | References |
|---|---|---|
| `CLAUDE.md` | `/.claude/` | "Orleans fork (Louis's)", directory reference |
| `Manage-Contexts.ps1` | Root (if exists) | Potential path references |
| Build coordination files | `/eng/` | May reference Scynapse for multi-repo builds |
| Documentation | `/Docs/For AI/`, `/Docs/VAYRON/` | References to the project |
| Context files | `/AI-Contexts/Claude-Opus/` | `Scynapse-AsyncPlus-Integration.md` |

### Research Documents

- `/Research/Scynapse/` directory
  - `orleans-state-properties-design.md`
  - `neworleans-events-v1.md`
  - `neworleans-client-principals.md`

---

## 11. String Replacement Strategy

### Phase 1: Preparation
- [ ] List all files containing "Scynapse" or "Scynapse"
- [ ] Back up entire codebase
- [ ] Create feature branch: `claude/rename-neworleans-to-[NEWNAME]`

### Phase 2: Directory Renames
- [ ] Rename `src/Scynapse/` → `src/[NEWNAME]/`
- [ ] Rename `Docs/Scynapse/` → `Docs/[NEWNAME]/` (or `Docs/[NewName]/` in camelCase)
- [ ] Rename `Research/Scynapse/` → `Research/[NEWNAME]/`
- [ ] Update git index with new paths

### Phase 3: File Renames
- [ ] Rename project files: `Scynapse.AsyncPlus.csproj` → `[NewName].AsyncPlus.csproj`
- [ ] Rename any source files starting with `Scynapse`
- [ ] Update solution file references

### Phase 4: Code Changes
- [ ] Update namespace declarations: `namespace Scynapse.*` → `namespace [NEWNAME].*`
- [ ] Update class names containing "Scynapse"
- [ ] Update assembly names in .csproj files
- [ ] Update package IDs in .csproj files

### Phase 5: Configuration Updates
- [ ] Update project references in .csproj files
- [ ] Update .slnx solution file with new project paths
- [ ] Update NuGet.Config if needed
- [ ] Update Directory.Packages.props assembly references

### Phase 6: Documentation Updates
- [ ] Rename markdown files in `/Docs/Scynapse/`
- [ ] Update content within markdown files
- [ ] Update references in `CLAUDE.md`
- [ ] Update references in `/Docs/VAYRON/` docs
- [ ] Update research document references

### Phase 7: Script/CI-CD Updates
- [ ] Update `Build.cmd`, `build.ps1` with new paths/project names
- [ ] Update `.azure/pipelines/` YAML files
- [ ] Update any hardcoded paths in scripts
- [ ] Update GitHub workflow references

### Phase 8: Verification
- [ ] Build solution (should find all unresolved references)
- [ ] Search codebase for any remaining old names
- [ ] Test all playground projects
- [ ] Verify CI/CD pipelines reference correct paths

---

## 12. Detailed File List

### Core Source Files Containing "Scynapse"

```
src/Scynapse/src/Scynapse.AsyncPlus/
├── Scynapse.AsyncPlus.csproj ← MUST RENAME FILE
├── Services/ScynapseAsyncPersistenceService.cs
├── Storage/RavenDbGrainStorage.cs
├── Storage/RavenDbStorageOptions.cs
├── Grains/AsyncStatePersistenceGrain.cs
├── Abstractions/IAsyncStatePersistenceGrain.cs
├── Abstractions/AsyncStateCheckpoint.cs
└── Extensions/AsyncPlusHostingExtensions.cs
```

All files in the above directory need namespace updates.

### Playground Files with References

```
src/Scynapse/playground/
├── AsyncPersistenceScenarios/
│   ├── Program.cs
│   ├── AsyncPersistenceScenarios.csproj
│   ├── Services/IAsyncPersistenceService.cs
│   └── Scenarios/
│       ├── CrossSessionPersistence.cs
│       ├── ExceptionRecovery.cs
│       ├── GrainMobility.cs
│       ├── MultiSiloCheckpointVisibility.cs
│       ├── MultipleConcurrentWorkflows.cs
│       ├── NestedAsyncCalls.cs
│       └── RoslynPlusCrossSession.cs
├── PluginGrainScenarios/
│   ├── Program.cs
│   ├── Scenarios/EventScenario.cs
│   └── Grains/EventTestGrain.cs
└── [other projects with potential references]
```

### Documentation Files to Rename

```
Docs/Scynapse/ ← DIRECTORY RENAME
├── Scynapse.md ← FILE RENAME
├── Scynapse Features/ ← SUBDIRECTORY RENAME
│   ├── DynamicGrainAccess.md
│   ├── OrleansAsync+.md
│   ├── PluginGrainArchitecture.md
│   └── StatePropertyAccess.md
└── [Original Orleans Internals/] ← Reference docs
```

### Research Documents

```
Research/Scynapse/ ← DIRECTORY RENAME
├── orleans-state-properties-design.md
├── neworleans-events-v1.md
└── neworleans-client-principals.md
```

---

## 13. Potential Naming Inconsistencies to Resolve

### Current Naming Inconsistency

The project currently uses **two different naming conventions**:
- **Directory**: `Scynapse` (camelCase, no space)
- **Documentation**: `Scynapse` (with space)

**Recommendation**: Decide on a single convention for the new name:
- Option A: `MyProjectName` (camelCase) - Use consistently everywhere
- Option B: `My Project Name` (with spaces) - Use only in docs/display names
- Option C: `my-project-name` (kebab-case) - Use for URLs/identifiers only

### Files with Mixed Conventions

- `neworleans-*.md` files in Research folder (lowercase, hyphenated)
- Documentation refers to it as "Scynapse" (with space)
- Code uses "Scynapse" (camelCase)

---

## 14. Git Considerations

### Tracking the Rename

When renaming directories and files:
```bash
# Git will track as delete + add unless using:
git mv src/Scynapse src/[NEWNAME]
git mv Docs/"Scynapse" Docs/"[NewName]"

# This preserves file history
```

### Commit Strategy

Suggest separate commits:
1. Directory/file renames (structural changes)
2. Namespace changes in code
3. Configuration updates
4. Documentation updates

This makes the diff cleaner and easier to review.

---

## 15. Testing Checklist

After renaming, verify:

- [ ] Solution loads in Visual Studio
- [ ] All project references resolve
- [ ] Build completes successfully (`Build.cmd`)
- [ ] Unit tests pass (`Test.cmd`)
- [ ] Playground projects run
- [ ] No broken documentation links
- [ ] NuGet package builds (if applicable)
- [ ] CI/CD pipelines pass
- [ ] Git history is preserved
- [ ] No remaining hardcoded old names in code/configs

---

## 16. Search Commands for Verification

After renaming, use these to verify completion:

```bash
# Search for remaining "Scynapse" references
grep -r "Scynapse" src/ Docs/ --include="*.cs" --include="*.md" --include="*.csproj" --include="*.json"

# Search for old doc folder name
grep -r "Scynapse" Docs/ --include="*.md" | grep -v "Scynapse Features"

# Search for old directory path references
grep -r "src/Scynapse" . --include="*.md" --include="*.ps1" --include="*.cmd"

# In code - check for namespace declarations
grep -r "namespace Scynapse" src/
```

---

## Appendix: Summary Statistics

- **Directories to rename**: 3 (src/Scynapse, Docs/Scynapse, Research/Scynapse)
- **Project files to update**: 20+ (.csproj files)
- **Source files with "Scynapse" namespace**: 8+ files
- **Files importing from "Scynapse"**: 10+ files
- **Documentation files**: 5+ markdown files
- **Configuration files to check**: 15+ (JSON, YAML, Props, Targets)
- **External references in CLAUDE.md**: Multiple
- **Total files affected**: 80+ files across directories

---

## Notes

- This document was generated by comprehensive analysis
- The actual list may grow as the codebase evolves
- Always verify with grep/search in your specific version
- Test thoroughly before committing rename changes
- Consider the impact on downstream users of any published packages

