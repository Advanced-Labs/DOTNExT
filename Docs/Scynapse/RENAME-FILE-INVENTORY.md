# Scynapse Project - Orleans Reference Inventory

## STATUS: Rename Complete

**Date:** 2026-02-26
**Rename Phases Completed:**
1. NewOrleans -> Scynapse (custom code only) — 2026-02-26
2. Orleans -> Scynapse / Microsoft.Orleans -> Genesa.Scynapse (full codebase) — 2026-02-26
3. Non-UTF-8 encoding fix (GlobalSuppressions.cs) — 2026-02-26

---

## Post-Rename Audit Results

### Final Counts (after all scripts and manual fixes)

| Metric | Count |
|--------|-------|
| Directories named with "Orleans" | **0** |
| Files named with "Orleans" | **0** |
| Files containing "Orleans" in content (text) | **0** |
| Binary files with "Orleans" in metadata | **3** (logos — manual replacement pending) |
| Non-UTF-8 files with "Orleans" | **0** |
| Remaining `NewOrleans` references | **0** |

### Verification Command

```bash
cd Docs/Scynapse/
./scynapse-rename-audit.sh --summary
```

---

## Pre-Rename Inventory (Historical Record)

The following sections document what existed BEFORE the rename, preserved as a reference for understanding the scope and for handling future upstream merges.

### Grand Totals (Pre-Rename)

| Metric | Count |
|--------|-------|
| Directories named with "Orleans" | 139 |
| Files named with "Orleans" | 230 |
| Files containing "Orleans" in content | 2,891 |
| Line occurrences - `Orleans` (PascalCase) | ~16,768 |
| Line occurrences - `ORLEANS` (uppercase) | ~220 |
| Line occurrences - `orleans` (lowercase) | ~194 files |
| `NewOrleans` references | 2 (1 filename + 1 content) |
| Binary files with "Orleans" | 2 (.png) |

---

## What Was Renamed (Complete Record)

### Directories Renamed (139 total)

All directories under `src/Scynapse/` that contained "Orleans" were renamed to "Scynapse":

**Core Framework (35 dirs):** `Orleans.Analyzers/` -> `Scynapse.Analyzers/`, `Orleans.BroadcastChannel/` -> `Scynapse.BroadcastChannel/`, `Orleans.Client/` -> `Scynapse.Client/`, `Orleans.Core/` -> `Scynapse.Core/`, `Orleans.Core.Abstractions/` -> `Scynapse.Core.Abstractions/`, `Orleans.Runtime/` -> `Scynapse.Runtime/`, `Orleans.Serialization/` -> `Scynapse.Serialization/`, etc.

**Provider dirs (31 dirs):** `AWS/Orleans.Clustering.DynamoDB/` -> `AWS/Scynapse.Clustering.DynamoDB/`, `Azure/Orleans.Persistence.AzureStorage/` -> `Azure/Scynapse.Persistence.AzureStorage/`, `Redis/Orleans.Clustering.Redis/` -> `Redis/Scynapse.Clustering.Redis/`, etc.

**API reference dirs (48 dirs):** Mirror of above under `src/api/`.

**Test dirs (14 dirs):** `test/Orleans.CodeGenerator.Tests/` -> `test/Scynapse.CodeGenerator.Tests/`, `test/Orleans.Serialization.UnitTests/` -> `test/Scynapse.Serialization.UnitTests/`, etc.

**Identity dirs (4 dirs):** `Orleans.Identity/ManagedCode.Orleans.Identity.Client/` -> `Scynapse.Identity/ManagedCode.Scynapse.Identity.Client/`, etc.

### Files Renamed (230 total)

**By extension:**

| Extension | Count | Examples |
|-----------|-------|---------|
| `.cs` | 138 | `OrleansException.cs` -> `ScynapseException.cs`, `OrleansSourceGenerator.cs` -> `ScynapseSourceGenerator.cs` |
| `.csproj` | 77 | `Orleans.Core.csproj` -> `Scynapse.Core.csproj`, `Orleans.Runtime.csproj` -> `Scynapse.Runtime.csproj` |
| `.targets` | 4 | `Microsoft.Orleans.Sdk.targets` -> `Genesa.Scynapse.Sdk.targets` |
| `.json` | 4 | `Orleans.*.xunit.runner.json` -> `Scynapse.*.xunit.runner.json` |
| `.props` | 3 | `Microsoft.Orleans.CodeGenerator.props` -> `Genesa.Scynapse.CodeGenerator.props` |
| `.slnx` | 1 | `Orleans.slnx` -> `Scynapse.slnx` |
| `.sln` | 1 | `ManagedCode.Orleans.Identity.sln` -> `ManagedCode.Scynapse.Identity.sln` |
| `.png` | 1 | `OrleansLogo.png` -> `ScynapseLogo.png` |
| `.fsproj` | 1 | `Orleans.Serialization.FSharp.Tests.fsproj` -> `Scynapse.Serialization.FSharp.Tests.fsproj` |

### Content Replaced (2,891 files)

**By file extension:**

| Extension | Files Changed |
|-----------|--------------|
| `.cs` | 2,625 |
| `.csproj` | 129 |
| `.md` | 59 |
| `.sql` | 29 |
| `.props` | 7 |
| `.tsx` | 5 |
| `.targets` | 5 |
| `.json` | 5 |
| `.yaml` | 4 |
| `.html` | 3 |
| `.fs` | 3 |
| `.css` | 3 |
| Other | ~14 |

### Content Categories That Were Renamed

| Category | Occurrences | Example Before -> After |
|----------|-------------|------------------------|
| **Namespace declarations** | 200+ distinct namespaces | `namespace Orleans.Runtime` -> `namespace Scynapse.Runtime` |
| **C# type names** | 50+ types | `OrleansException` -> `ScynapseException`, `OrleansJsonSerializer` -> `ScynapseJsonSerializer` |
| **NuGet PackageIds** | 40+ packages | `Microsoft.Orleans.Core` -> `Genesa.Scynapse.Core` |
| **Generated code namespaces** | 40+ | `OrleansCodeGen.Orleans.*` -> `ScynapseCodeGen.Scynapse.*` |
| **InternalsVisibleTo** | 30+ assemblies | `Orleans.Runtime` -> `Scynapse.Runtime` |
| **Diagnostic IDs** | `ORLEANS0001`-`0013` | `ORLEANS0001` -> `SCYNAPSE0001` |
| **Experimental flags** | `ORLEANSEXP001`-`004` | `ORLEANSEXP001` -> `SCYNAPSEEXP001` |
| **Environment variables** | `ORLEANS_CLUSTER_ID`, `ORLEANS_SERVICE_ID` | `SCYNAPSE_CLUSTER_ID`, `SCYNAPSE_SERVICE_ID` |
| **GitHub URLs** | All repo links | `github.com/dotnet/orleans` -> `github.com/dotnet/scynapse` |
| **SQL schemas** | 29 SQL files | Table/procedure references updated |
| **Comments & XML docs** | Thousands | All "Orleans" references in comments updated |
| **CI/CD configs** | 7 files | `.azure/`, `.github/` configs updated |
| **Build scripts** | Multiple | `build.ps1`, `Test.cmd`, etc. updated |

---

## What Was NOT Renamed (and Why)

### 3 Binary Logo Files

| File | Why Not Renamed | Future Action |
|------|----------------|---------------|
| `assets/logo_128.png` | Binary PNG — text tools corrupt images | Replace with Scynapse logo image |
| `src/Dashboard/Scynapse.Dashboard.App/src/assets/img/ScynapseLogo.png` | File was renamed, but binary metadata may still say "Orleans" | Re-export from design tool |
| `src/Scynapse.Identity/logo.png` | Binary PNG | Replace with Scynapse logo image |

**How to handle:** These need a designer to create/export new Scynapse-branded logos. The file references in the codebase already point to the renamed paths — only the image content itself needs replacement.

### The UTF-16 Edge Case (Fixed)

**File:** `src/Scynapse.Core/GlobalSuppressions.cs`
**Problem:** Visual Studio created this file in UTF-16 encoding. The `sed`-based rename script couldn't match the Orleans pattern because UTF-16 stores characters with null bytes between them (`O\x00r\x00l\x00e\x00a\x00n\x00s\x00`).
**Resolution:** Fixed manually with a byte-level sed replacement. The `scynapse-rename-encoding-fix.sh` script was then created to handle this class of issue automatically in the future.
**Lesson for future AIs:** Always run `scynapse-rename-encoding-fix.sh` after the main rename script. Visual Studio commonly creates `GlobalSuppressions.cs`, `.designer.cs`, and some `.resx` files in UTF-16.

---

## Scripts Reference

Four scripts handle the complete rename lifecycle. All live in `Docs/Scynapse/`:

| Script | Purpose | When to Run |
|--------|---------|-------------|
| `scynapse-rename-audit.sh` | Count all Orleans references | Before & after rename |
| `scynapse-rename-execute.sh` | Perform the rename (dirs, files, content) | Main rename step |
| `scynapse-rename-encoding-fix.sh` | Fix non-UTF-8 files that sed missed | After execute script |
| `scynapse-rename-post-verify.sh` | Validate structural integrity | After all renames, before commit |

**Full workflow:** See `RENAME-REQUIREMENTS.md` section "Complete Rename Workflow (for future AI agents)".

### What Each Script Can and Cannot Handle

```
                    ┌─────────────────────────────────────┐
                    │        scynapse-rename-audit.sh      │
                    │  (BEFORE) Count all references       │
                    └─────────────┬───────────────────────┘
                                  │
                    ┌─────────────▼───────────────────────┐
                    │    scynapse-rename-execute.sh        │
                    │  Phase 1: Rename directories         │ ✅ Handles 99% of work
                    │  Phase 2: Rename files               │ ❌ Cannot: non-UTF-8,
                    │  Phase 3: Replace content (sed)      │    binary, SQL databases
                    └─────────────┬───────────────────────┘
                                  │
                    ┌─────────────▼───────────────────────┐
                    │  scynapse-rename-encoding-fix.sh     │
                    │  Find non-UTF-8 files               │ ✅ Handles: UTF-16, ISO-8859
                    │  Convert -> replace -> convert back  │ ❌ Cannot: binary files
                    └─────────────┬───────────────────────┘
                                  │
                    ┌─────────────▼───────────────────────┐
                    │   scynapse-rename-post-verify.sh     │
                    │  Check ProjectReference integrity    │ ✅ Catches broken refs
                    │  Check solution file references      │ ✅ Catches broken .sln
                    │  Check PackageId/AssemblyName        │ ✅ Catches naming issues
                    │  Check non-UTF-8 files               │ ✅ Catches encoding misses
                    │  Check binary files                  │ ⚠️  Lists but can't fix
                    │  Check directory/file names          │ ✅ Catches naming misses
                    └─────────────┬───────────────────────┘
                                  │
                    ┌─────────────▼───────────────────────┐
                    │        scynapse-rename-audit.sh      │
                    │  (AFTER) Verify all references gone  │
                    └─────────────────────────────────────┘
```

---

## Why the Main Script Missed the UTF-16 File

**Root cause:** `sed` is a byte-stream processor designed for ASCII/UTF-8. It reads files line-by-line using `\n` as a delimiter.

In UTF-16 files:
- Every ASCII character has a `\x00` (null byte) after it
- Line endings are `\x0D\x00\x0A\x00` instead of `\x0D\x0A`
- The pattern `Orleans` exists as `O\x00r\x00l\x00e\x00a\x00n\x00s\x00`

When sed reads this:
- It may see the null bytes as line terminators (depending on implementation)
- Even if it reads the whole "line", the pattern `Orleans` (7 bytes) doesn't match `O\x00r\x00l\x00e\x00a\x00n\x00s\x00` (14 bytes)
- The file appears to be "processed" (no error) but nothing changes

`grep -ril "orleans"` may partially work on UTF-16 because some grep implementations do binary-aware matching, but the match is unreliable. This is why the audit script could report the file as containing "orleans" while the execute script's sed silently failed.

**The encoding-fix script solves this** by:
1. Detecting the actual encoding via `file --mime-encoding`
2. Converting to UTF-8 with `iconv` (which properly handles null bytes)
3. Running sed on the clean UTF-8 version
4. Converting back to the original encoding

---

## Handling Future Upstream Merges

When merging upstream Orleans code:

1. **New upstream files will say "Orleans"** — this is expected
2. After the merge, run the full rename workflow (see RENAME-REQUIREMENTS.md)
3. The scripts are **idempotent** — running them on already-renamed files produces no changes
4. **Git will show the merge introducing "Orleans" references**, then the rename commit removing them

### Merge strategy

```bash
# 1. Merge upstream
git merge upstream/main

# 2. Resolve conflicts (keep Scynapse names where both sides changed the same area)

# 3. Run full rename workflow on the merged result
cd Docs/Scynapse/
./scynapse-rename-execute.sh --execute
./scynapse-rename-encoding-fix.sh --execute
./scynapse-rename-post-verify.sh
./scynapse-rename-audit.sh --summary

# 4. Commit the post-merge rename
git add -A && git commit -m "Re-apply Scynapse rename after upstream merge"
```

---

## Appendix: Pre-Rename Directory/File Lists

<details>
<summary>Click to expand full pre-rename directory list (139 entries)</summary>

### src/ Core (35 directories)
```
src/Orleans.Analyzers/
src/Orleans.BroadcastChannel/
src/Orleans.Client/
src/Orleans.Clustering.Consul/
src/Orleans.Clustering.ZooKeeper/
src/Orleans.CodeGenerator/
src/Orleans.Connections.Security/
src/Orleans.Core/
src/Orleans.Core.Abstractions/
src/Orleans.DurableJobs/
src/Orleans.EventSourcing/
src/Orleans.Hosting.Kubernetes/
src/Orleans.Identity/ (+ 4 subdirs)
src/Orleans.Journaling/
src/Orleans.Persistence.Memory/
src/Orleans.Reminders/
src/Orleans.Reminders.Abstractions/
src/Orleans.Runtime/
src/Orleans.Sdk/
src/Orleans.Serialization/
src/Orleans.Serialization.Abstractions/
src/Orleans.Serialization.FSharp/
src/Orleans.Serialization.MessagePack/
src/Orleans.Serialization.NewtonsoftJson/
src/Orleans.Serialization.SystemTextJson/
src/Orleans.Serialization.TestKit/
src/Orleans.Server/
src/Orleans.Streaming/
src/Orleans.Streaming.Abstractions/
src/Orleans.Streaming.NATS/
src/Orleans.TestingHost/
src/Orleans.Transactions/
src/Orleans.Transactions.TestKit.Base/
src/Orleans.Transactions.TestKit.xUnit/
```

### src/ Providers (31 directories)
```
src/AWS/Orleans.Clustering.DynamoDB/
src/AWS/Orleans.Persistence.DynamoDB/
src/AWS/Orleans.Reminders.DynamoDB/
src/AWS/Orleans.Streaming.SQS/
src/AdoNet/Orleans.Clustering.AdoNet/
src/AdoNet/Orleans.GrainDirectory.AdoNet/
src/AdoNet/Orleans.Persistence.AdoNet/
src/AdoNet/Orleans.Reminders.AdoNet/
src/AdoNet/Orleans.Streaming.AdoNet/
src/Azure/Orleans.Clustering.AzureStorage/
src/Azure/Orleans.Clustering.Cosmos/
src/Azure/Orleans.DurableJobs.AzureStorage/
src/Azure/Orleans.GrainDirectory.AzureStorage/
src/Azure/Orleans.Hosting.AzureCloudServices/
src/Azure/Orleans.Journaling.AzureStorage/
src/Azure/Orleans.Persistence.AzureStorage/
src/Azure/Orleans.Persistence.Cosmos/
src/Azure/Orleans.Reminders.AzureStorage/
src/Azure/Orleans.Reminders.Cosmos/
src/Azure/Orleans.Streaming.AzureStorage/
src/Azure/Orleans.Streaming.EventHubs/
src/Azure/Orleans.Transactions.AzureStorage/
src/Cassandra/Orleans.Clustering.Cassandra/
src/Dashboard/Orleans.Dashboard/
src/Dashboard/Orleans.Dashboard.Abstractions/
src/Dashboard/Orleans.Dashboard.App/
src/Redis/Orleans.Clustering.Redis/
src/Redis/Orleans.GrainDirectory.Redis/
src/Redis/Orleans.Persistence.Redis/
src/Redis/Orleans.Reminders.Redis/
src/Serializers/Orleans.Serialization.Protobuf/
```

### src/api/ (48 directories - mirrors of src/)
```
[Same pattern as above with src/api/ prefix]
```

### test/ (14 directories)
```
test/Misc/TestInternalDtosRefOrleans/
test/NonSilo.Tests/OrleansRuntime/
test/Orleans.CodeGenerator.Tests/
test/Orleans.Connections.Security.Tests/
test/Orleans.Dashboard.Tests/ (+ 2 subdirs)
test/Orleans.Journaling.Tests/
test/Orleans.Serialization.FSharp.Tests/
test/Orleans.Serialization.UnitTests/
test/TestInfrastructure/Orleans.TestingHost.Tests/
test/TesterInternal/OrleansRuntime/
test/Transactions/Orleans.Transactions.Azure.Test/
test/Transactions/Orleans.Transactions.Tests/
```

</details>

<details>
<summary>Click to expand full pre-rename file list (230 entries, by category)</summary>

### Root Level (1 file)
```
Orleans.slnx
```

### src/ - Core Framework .csproj Files (38 files)
```
src/Orleans.Analyzers/Orleans.Analyzers.csproj
src/Orleans.BroadcastChannel/Orleans.BroadcastChannel.csproj
src/Orleans.Client/Orleans.Client.csproj
[... all 38 listed in original inventory ...]
```

### src/ - Provider .csproj Files (22 files)
```
src/AWS/Orleans.Clustering.DynamoDB/Orleans.Clustering.DynamoDB.csproj
[... all 22 listed in original inventory ...]
```

### src/ - C# Source Files with Orleans in Name (33 files)
```
src/Orleans.Analyzers/AtMostOneOrleansConstructorAnalyzer.cs
src/Orleans.CodeGenerator/OrleansGeneratorDiagnosticAnalysisException.cs
src/Orleans.CodeGenerator/OrleansSourceGenerator.cs
[... all 33 listed in original inventory ...]
```

### test/ files (62 total: 15 .csproj, 4 config, 10 .cs, 33 snapshot)
```
[All listed in original inventory]
```

</details>

---

## Commit History

| Commit | Message | What Changed |
|--------|---------|--------------|
| `74e4decf4` | Fix last remaining Orleans reference in GlobalSuppressions.cs | UTF-16 encoded file manually fixed |
| `37632551d` | Rename Orleans -> Scynapse / Microsoft.Orleans -> Genesa.Scynapse | Main bulk rename via execute script |
| `465f2e23b` | Rename NewOrleans project to Scynapse throughout codebase | Initial NewOrleans -> Scynapse rename |
| `b4be794da` | Add rename scripts for Orleans -> Scynapse / Microsoft -> Genesa | Script creation |
| `742199150` | Comprehensive audit: find all Orleans references in src/Scynapse | Pre-rename audit + inventory docs |

---

## Notes

- This inventory was originally generated by exhaustive filesystem search on 2026-02-26
- All paths are relative to `src/Scynapse/` unless otherwise noted
- The `src/api/` directory is an auto-generated API reference mirror that follows src/ changes
- The `.verified.cs` snapshot files in test/ are auto-generated and need test regeneration after rename
- SQL files contain database schema references that need migration scripts for deployed databases
- Binary files (`.png`) need image replacement, not text editing
- For full rename workflow and script documentation, see `RENAME-REQUIREMENTS.md`
