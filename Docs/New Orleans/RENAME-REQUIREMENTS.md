# NewOrleans Project Rename Requirements

## Document Purpose

This document comprehensively catalogs everything that would need to be renamed if the NewOrleans project were given a different name. It covers directories, files, code identifiers, configuration files, documentation, and all references throughout the codebase.

**Current Name Forms:**
- Directory: `src/NewOrleans/`
- Docs folder: `Docs/New Orleans/` (with space)
- Code: `NewOrleans`, `NewOrleansAsyncPlus`, etc. (camelCase in code)

---

## 1. Directory Structure Changes

### Primary Directories

| Current Path | Type | Impact | Notes |
|---|---|---|---|
| `/src/NewOrleans/` | Root directory | **HIGH** | Main project directory - ALL subdirectories affected |
| `/Docs/New Orleans/` | Documentation | **HIGH** | Project documentation folder |
| `/Docs/New Orleans/New Orleans Features/` | Documentation | **HIGH** | Feature documentation subfolder |
| `/Research/NewOrleans/` | Research documents | **MEDIUM** | Research and design docs |
| `/AI-Contexts/Claude-Opus/` | Contains references | **MEDIUM** | Context files reference NewOrleans |

### Sub-Directories in src/NewOrleans/

```
src/NewOrleans/
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
│   ├── NewOrleans.AsyncPlus/              ← MUST RENAME (contains "NewOrleans")
│   │   ├── NewOrleans.AsyncPlus.csproj
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
- `src/NewOrleans/Orleans.slnx` - Main solution file
  - **Change needed**: May contain references to `NewOrleans.*` project names

### Project Files Containing "NewOrleans"

| Project File | Current Name | Location |
|---|---|---|
| `NewOrleans.AsyncPlus.csproj` | Assembly: `NewOrleans.AsyncPlus` | `src/NewOrleans/src/NewOrleans.AsyncPlus/` |
| Project package names | Various `NewOrleans.*` | Throughout `src/NewOrleans/src/` |
| Playground project files | Check for references | `src/NewOrleans/playground/` |

**In each .csproj file, check for:**
- `<AssemblyName>` - May be `NewOrleans.AsyncPlus` or similar
- `<RootNamespace>` - Package namespace
- `<PackageId>` - NuGet package name
- `<ProjectReference>` - References to other `NewOrleans.*` projects
- `<Version>` metadata

---

## 3. C# Code Identifiers

### Namespace Declarations

**Pattern**: `namespace NewOrleans.*;`

Files containing `NewOrleans` namespace:
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Services/NewOrleansAsyncPersistenceService.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Storage/RavenDbGrainStorage.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Storage/RavenDbStorageOptions.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Grains/AsyncStatePersistenceGrain.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Abstractions/IAsyncStatePersistenceGrain.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Abstractions/AsyncStateCheckpoint.cs`
- `src/NewOrleans/src/NewOrleans.AsyncPlus/Extensions/AsyncPlusHostingExtensions.cs`

### Class and Type Names

- `NewOrleansAsyncPersistenceService` - Service class
- `AsyncStatePersistenceGrain` - Grain implementation
- `IAsyncStatePersistenceGrain` - Interface
- Other types within the `NewOrleans.AsyncPlus` namespace

### Using Statements

Files importing from `NewOrleans` namespace:
- `src/NewOrleans/playground/AsyncPersistenceScenarios/Services/IAsyncPersistenceService.cs`
- `src/NewOrleans/playground/AsyncPersistenceScenarios/Program.cs`
- `src/NewOrleans/playground/AsyncPersistenceScenarios/Scenarios/*.cs` (multiple files)
- `src/NewOrleans/playground/PluginGrainScenarios/Scenarios/EventScenario.cs`
- `src/NewOrleans/playground/PluginGrainScenarios/Program.cs`
- `src/NewOrleans/playground/PluginGrainScenarios/Grains/EventTestGrain.cs`

---

## 4. Configuration Files

### JSON Files

| File | Location | Contains |
|---|---|---|
| `global.json` | `src/NewOrleans/` | May reference SDK or build config |
| `NuGet.Config` | `src/NewOrleans/` | Package sources, potentially with NewOrleans references |
| `.devcontainer/devcontainer.json` | `src/NewOrleans/.devcontainer/` | Container config, may have paths |
| `Directory.Packages.props` | `src/NewOrleans/` | Package versions, may have `NewOrleans.*` references |
| `appsettings*.json` | Playground projects | Application settings |

### YAML/Properties Files

| File | Location | Contains |
|---|---|---|
| `.azure/pipelines/templates/vars.yaml` | CI/CD variables | Build environment, package names |
| `Directory.Build.props` | `src/NewOrleans/` | Shared build properties |
| `Directory.Build.targets` | `src/NewOrleans/` | Shared build targets |
| `.github/copilot-instructions.md` | GitHub config | References to the project |

### Configuration Details

Check for:
- NuGet package names with `NewOrleans` prefix
- Assembly names
- Package ID declarations
- Build artifact naming
- CI/CD pipeline variable names

---

## 5. Documentation Files

### Markdown Documents

| File | Current Name | Path |
|---|---|---|
| `New Orleans.md` | Main project doc | `Docs/New Orleans/` |
| `DynamicGrainAccess.md` | Feature doc | `Docs/New Orleans/New Orleans Features/` |
| `OrleansAsync+.md` | Feature doc | `Docs/New Orleans/New Orleans Features/` |
| `PluginGrainArchitecture.md` | Feature doc | `Docs/New Orleans/New Orleans Features/` |
| `StatePropertyAccess.md` | Feature doc | `Docs/New Orleans/New Orleans Features/` |
| References in CLAUDE.md | Project reference | `.claude/CLAUDE.md` |
| References in VAYRON docs | Project reference | `Docs/VAYRON/` |
| References in AI context files | Project reference | `AI-Contexts/Claude-Opus/` |

### Documentation Content to Update

Within the documents, search for and update:
- Title/heading: "# NewOrleans:" → update
- References: "NewOrleans project", "New Orleans system", etc.
- Links to `/src/NewOrleans/` paths
- Links to `/Docs/New Orleans/` paths
- Mentions in context files: "NewOrleans-AsyncPlus-Integration.md"

---

## 6. Script Files

### Build Scripts

| File | Location | Check for |
|---|---|---|
| `Build.cmd` | `src/NewOrleans/` | Hardcoded paths, project names |
| `build.ps1` | `src/NewOrleans/` | PowerShell script with paths/names |
| `Test.cmd` | `src/NewOrleans/` | Test runner paths |
| `TestAll.cmd` | `src/NewOrleans/` | Test suite config |
| `Parallel-Tests.ps1` | `src/NewOrleans/` | PowerShell test runner |
| `common.ps1` | `src/NewOrleans/` | Common script utilities |

### CI/CD Scripts

- `.azure/pipelines/*.yaml` - Azure Pipelines configuration
- Check for hardcoded project names, paths, artifact names

---

## 7. Searchable String References

### Code References (grep patterns)

```
# High priority - core naming
- "NewOrleans" (exact match)
- "NewOrleansAsyncPlus"
- "neworleans" (lowercase)
- "NEWORLEANS" (uppercase)

# Documentation patterns
- "New Orleans" (with space)
- "new-orleans" (kebab-case)
- "neworleans" (no space)

# Context/metadata
- "NewOrleans-AsyncPlus"
- "Orleans fork (Louis's)"
- References to `/src/NewOrleans/`
```

---

## 8. File Type Inventory

### By File Type

| Type | Extensions | Locations | Examples |
|---|---|---|---|
| Solution Files | `.slnx`, `.sln` | `src/NewOrleans/` | `Orleans.slnx` |
| Project Files | `.csproj` | `src/NewOrleans/src/*/`, `src/NewOrleans/playground/*/` | `NewOrleans.AsyncPlus.csproj` |
| C# Source | `.cs` | Throughout `src/NewOrleans/src/`, `playground/` | Namespace decls, class names |
| Configuration | `.json`, `.props`, `.targets`, `.config` | `src/NewOrleans/` and subdirs | Global.json, NuGet.Config |
| Build Scripts | `.cmd`, `.ps1`, `.sh` | `src/NewOrleans/`, `src/NewOrleans/.azure/` | Build.cmd, build.ps1 |
| Documentation | `.md` | `Docs/New Orleans/`, `Docs/VAYRON/`, `AI-Contexts/` | *.md files |
| CI/CD | `.yaml`, `.yml` | `src/NewOrleans/.azure/pipelines/` | Pipeline definitions |
| GitHub Config | `.json`, `.yml`, `.md` | `src/NewOrleans/.github/` | Actions, dependabot config |
| VS Code Config | `.json` | `src/NewOrleans/.vscode/` | launch.json, tasks.json |
| Dev Container | `.json` | `src/NewOrleans/.devcontainer/` | devcontainer.json |
| Misc | `.gitignore`, `.gitattributes`, `.editorconfig`, etc. | `src/NewOrleans/` | Configuration files |

---

## 9. Affected Project Types

### NuGet Packages

If the `NewOrleans.AsyncPlus` assembly is published as a NuGet package:
- Package ID: `NewOrleans.AsyncPlus` → needs rename
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

### Outside src/NewOrleans/

Files in the main DOTNExT repository referencing NewOrleans:

| File | Location | References |
|---|---|---|
| `CLAUDE.md` | `/.claude/` | "Orleans fork (Louis's)", directory reference |
| `Manage-Contexts.ps1` | Root (if exists) | Potential path references |
| Build coordination files | `/eng/` | May reference NewOrleans for multi-repo builds |
| Documentation | `/Docs/For AI/`, `/Docs/VAYRON/` | References to the project |
| Context files | `/AI-Contexts/Claude-Opus/` | `NewOrleans-AsyncPlus-Integration.md` |

### Research Documents

- `/Research/NewOrleans/` directory
  - `orleans-state-properties-design.md`
  - `neworleans-events-v1.md`
  - `neworleans-client-principals.md`

---

## 11. String Replacement Strategy

### Phase 1: Preparation
- [ ] List all files containing "NewOrleans" or "New Orleans"
- [ ] Back up entire codebase
- [ ] Create feature branch: `claude/rename-neworleans-to-[NEWNAME]`

### Phase 2: Directory Renames
- [ ] Rename `src/NewOrleans/` → `src/[NEWNAME]/`
- [ ] Rename `Docs/New Orleans/` → `Docs/[NEWNAME]/` (or `Docs/[NewName]/` in camelCase)
- [ ] Rename `Research/NewOrleans/` → `Research/[NEWNAME]/`
- [ ] Update git index with new paths

### Phase 3: File Renames
- [ ] Rename project files: `NewOrleans.AsyncPlus.csproj` → `[NewName].AsyncPlus.csproj`
- [ ] Rename any source files starting with `NewOrleans`
- [ ] Update solution file references

### Phase 4: Code Changes
- [ ] Update namespace declarations: `namespace NewOrleans.*` → `namespace [NEWNAME].*`
- [ ] Update class names containing "NewOrleans"
- [ ] Update assembly names in .csproj files
- [ ] Update package IDs in .csproj files

### Phase 5: Configuration Updates
- [ ] Update project references in .csproj files
- [ ] Update .slnx solution file with new project paths
- [ ] Update NuGet.Config if needed
- [ ] Update Directory.Packages.props assembly references

### Phase 6: Documentation Updates
- [ ] Rename markdown files in `/Docs/New Orleans/`
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

### Core Source Files Containing "NewOrleans"

```
src/NewOrleans/src/NewOrleans.AsyncPlus/
├── NewOrleans.AsyncPlus.csproj ← MUST RENAME FILE
├── Services/NewOrleansAsyncPersistenceService.cs
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
src/NewOrleans/playground/
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
Docs/New Orleans/ ← DIRECTORY RENAME
├── New Orleans.md ← FILE RENAME
├── New Orleans Features/ ← SUBDIRECTORY RENAME
│   ├── DynamicGrainAccess.md
│   ├── OrleansAsync+.md
│   ├── PluginGrainArchitecture.md
│   └── StatePropertyAccess.md
└── [Original Orleans Internals/] ← Reference docs
```

### Research Documents

```
Research/NewOrleans/ ← DIRECTORY RENAME
├── orleans-state-properties-design.md
├── neworleans-events-v1.md
└── neworleans-client-principals.md
```

---

## 13. Potential Naming Inconsistencies to Resolve

### Current Naming Inconsistency

The project currently uses **two different naming conventions**:
- **Directory**: `NewOrleans` (camelCase, no space)
- **Documentation**: `New Orleans` (with space)

**Recommendation**: Decide on a single convention for the new name:
- Option A: `MyProjectName` (camelCase) - Use consistently everywhere
- Option B: `My Project Name` (with spaces) - Use only in docs/display names
- Option C: `my-project-name` (kebab-case) - Use for URLs/identifiers only

### Files with Mixed Conventions

- `neworleans-*.md` files in Research folder (lowercase, hyphenated)
- Documentation refers to it as "New Orleans" (with space)
- Code uses "NewOrleans" (camelCase)

---

## 14. Git Considerations

### Tracking the Rename

When renaming directories and files:
```bash
# Git will track as delete + add unless using:
git mv src/NewOrleans src/[NEWNAME]
git mv Docs/"New Orleans" Docs/"[NewName]"

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
# Search for remaining "NewOrleans" references
grep -r "NewOrleans" src/ Docs/ --include="*.cs" --include="*.md" --include="*.csproj" --include="*.json"

# Search for old doc folder name
grep -r "New Orleans" Docs/ --include="*.md" | grep -v "New Orleans Features"

# Search for old directory path references
grep -r "src/NewOrleans" . --include="*.md" --include="*.ps1" --include="*.cmd"

# In code - check for namespace declarations
grep -r "namespace NewOrleans" src/
```

---

## Appendix: Summary Statistics

- **Directories to rename**: 3 (src/NewOrleans, Docs/New Orleans, Research/NewOrleans)
- **Project files to update**: 20+ (.csproj files)
- **Source files with "NewOrleans" namespace**: 8+ files
- **Files importing from "NewOrleans"**: 10+ files
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

