# Scynapse Project Rename Requirements

## STATUS: Orleans References Still Pervasive

**Previous Rename:** NewOrleans -> Scynapse (completed 2026-02-26)
**Current Issue:** The original Orleans framework naming is still present throughout the entire codebase
**Date Assessed:** 2026-02-26
**Scope:** `src/Scynapse/` directory (3,344 files, 772 directories)

### Summary of Remaining "Orleans" References

| Category | Count | Notes |
|----------|-------|-------|
| Directories with "Orleans" in name | 139 (direct match) | Core framework dirs like `Orleans.Core/`, `Orleans.Runtime/`, etc. |
| Files with "Orleans" in filename | 230 | `.csproj`, `.cs`, `.targets`, `.props`, `.slnx`, `.sln`, `.png`, `.fsproj` |
| Files containing "Orleans" in content | 2,891 | 86% of all files in the project |
| Total line occurrences (PascalCase) | ~16,768 | `Orleans` in namespaces, classes, comments, configs |
| Total line occurrences (UPPERCASE) | ~220 | `ORLEANS` in diagnostic IDs, env vars, constants |
| Remaining "NewOrleans" references | 1 filename + 1 content line | Should be cleaned up |

---

## 1. Case Variant Breakdown

### `Orleans` (PascalCase) - ~16,768 occurrences across 2,874 files
The dominant form. Appears in:
- Namespace declarations (`namespace Orleans.*`)
- Class/type names (`OrleansException`, `OrleansJsonSerializer`, etc.)
- Project names and assembly names (`Orleans.Core`, `Orleans.Runtime`, etc.)
- Package IDs (`Microsoft.Orleans.*`)
- Solution file references
- Using statements
- Comments, URLs, and documentation

### `ORLEANS` (Uppercase) - ~220 occurrences across 82 files
Appears in:
- Diagnostic analyzer IDs: `ORLEANS0001` through `ORLEANS0013`
- Experimental feature attributes: `ORLEANSEXP001` through `ORLEANSEXP004`
- Environment variables: `ORLEANS_CLUSTER_ID`, `ORLEANS_SERVICE_ID`
- Compiler define constants: `ORLEANS_CLUSTERING`
- Analyzer release documentation

### `orleans` (lowercase) - 194 files
Appears in:
- GitHub URLs: `github.com/dotnet/orleans/issues/*`
- Log file paths: `orleans-grain-storage-debug.log`
- RavenDB document paths: `orleans/{serviceId}/grains/...`
- Variable names: `_orleansSerializer`, `_orleansPayload`

### `NewOrleans` / `NEWORLEANS` - 2 instances total
- **Filename:** `src/Scynapse.AsyncPlus/Services/NewOrleansAsyncPersistenceService.cs`
- **Content:** `playground/PluginGrainScenarios/Grains/EventTestGrain.cs` line 6: `// NEWORLEANS EVENTS TEST GRAINS`

### Binary files containing "Orleans"
- `src/Dashboard/Orleans.Dashboard.App/src/assets/img/OrleansLogo.png` (logo image)
- `assets/logo_128.png` (binary content contains "orleans")

---

## 2. Directories with "Orleans" in Name (139 direct matches)

### Core Framework Directories (src/)

| Directory | Type |
|-----------|------|
| `src/Orleans.Analyzers/` | Roslyn analyzers |
| `src/Orleans.BroadcastChannel/` | Broadcast channel |
| `src/Orleans.Client/` | Client library |
| `src/Orleans.Clustering.Consul/` | Consul clustering |
| `src/Orleans.Clustering.ZooKeeper/` | ZooKeeper clustering |
| `src/Orleans.CodeGenerator/` | Source generator |
| `src/Orleans.Connections.Security/` | TLS/Security |
| `src/Orleans.Core/` | Core library |
| `src/Orleans.Core.Abstractions/` | Core abstractions |
| `src/Orleans.DurableJobs/` | Durable jobs |
| `src/Orleans.EventSourcing/` | Event sourcing |
| `src/Orleans.Hosting.Kubernetes/` | Kubernetes hosting |
| `src/Orleans.Identity/` | Identity (+ 4 subdirs: Client, Core, Server, Tests) |
| `src/Orleans.Journaling/` | Journaling |
| `src/Orleans.Persistence.Memory/` | In-memory persistence |
| `src/Orleans.Reminders/` | Reminders |
| `src/Orleans.Reminders.Abstractions/` | Reminders abstractions |
| `src/Orleans.Runtime/` | Runtime |
| `src/Orleans.Sdk/` | SDK metapackage |
| `src/Orleans.Serialization/` | Serialization |
| `src/Orleans.Serialization.Abstractions/` | Serialization abstractions |
| `src/Orleans.Serialization.FSharp/` | F# serialization |
| `src/Orleans.Serialization.MessagePack/` | MessagePack |
| `src/Orleans.Serialization.NewtonsoftJson/` | Newtonsoft.Json |
| `src/Orleans.Serialization.SystemTextJson/` | System.Text.Json |
| `src/Orleans.Serialization.TestKit/` | Serialization test kit |
| `src/Orleans.Server/` | Server library |
| `src/Orleans.Streaming/` | Streaming |
| `src/Orleans.Streaming.Abstractions/` | Streaming abstractions |
| `src/Orleans.Streaming.NATS/` | NATS streaming |
| `src/Orleans.TestingHost/` | Testing host |
| `src/Orleans.Transactions/` | Transactions |
| `src/Orleans.Transactions.TestKit.Base/` | Transactions test kit |
| `src/Orleans.Transactions.TestKit.xUnit/` | xUnit test kit |

### Provider Directories (src/[Provider]/)

| Directory | Provider |
|-----------|----------|
| `src/AWS/Orleans.Clustering.DynamoDB/` | AWS DynamoDB clustering |
| `src/AWS/Orleans.Persistence.DynamoDB/` | AWS DynamoDB persistence |
| `src/AWS/Orleans.Reminders.DynamoDB/` | AWS DynamoDB reminders |
| `src/AWS/Orleans.Streaming.SQS/` | AWS SQS streaming |
| `src/AdoNet/Orleans.Clustering.AdoNet/` | ADO.NET clustering |
| `src/AdoNet/Orleans.GrainDirectory.AdoNet/` | ADO.NET grain directory |
| `src/AdoNet/Orleans.Persistence.AdoNet/` | ADO.NET persistence |
| `src/AdoNet/Orleans.Reminders.AdoNet/` | ADO.NET reminders |
| `src/AdoNet/Orleans.Streaming.AdoNet/` | ADO.NET streaming |
| `src/Azure/Orleans.Clustering.AzureStorage/` | Azure clustering |
| `src/Azure/Orleans.Clustering.Cosmos/` | Cosmos DB clustering |
| `src/Azure/Orleans.DurableJobs.AzureStorage/` | Azure durable jobs |
| `src/Azure/Orleans.GrainDirectory.AzureStorage/` | Azure grain directory |
| `src/Azure/Orleans.Hosting.AzureCloudServices/` | Azure cloud services |
| `src/Azure/Orleans.Journaling.AzureStorage/` | Azure journaling |
| `src/Azure/Orleans.Persistence.AzureStorage/` | Azure persistence |
| `src/Azure/Orleans.Persistence.Cosmos/` | Cosmos DB persistence |
| `src/Azure/Orleans.Reminders.AzureStorage/` | Azure reminders |
| `src/Azure/Orleans.Reminders.Cosmos/` | Cosmos DB reminders |
| `src/Azure/Orleans.Streaming.AzureStorage/` | Azure streaming |
| `src/Azure/Orleans.Streaming.EventHubs/` | Event Hubs streaming |
| `src/Azure/Orleans.Transactions.AzureStorage/` | Azure transactions |
| `src/Cassandra/Orleans.Clustering.Cassandra/` | Cassandra clustering |
| `src/Dashboard/Orleans.Dashboard/` | Dashboard |
| `src/Dashboard/Orleans.Dashboard.Abstractions/` | Dashboard abstractions |
| `src/Dashboard/Orleans.Dashboard.App/` | Dashboard frontend |
| `src/Redis/Orleans.Clustering.Redis/` | Redis clustering |
| `src/Redis/Orleans.GrainDirectory.Redis/` | Redis grain directory |
| `src/Redis/Orleans.Persistence.Redis/` | Redis persistence |
| `src/Redis/Orleans.Reminders.Redis/` | Redis reminders |
| `src/Serializers/Orleans.Serialization.Protobuf/` | Protobuf serialization |

### API Reference Directories (src/api/)
Mirror of the above under `src/api/` -- 48 additional directories with same `Orleans.*` naming pattern.

### Test Directories (test/)

| Directory |
|-----------|
| `test/Misc/TestInternalDtosRefOrleans/` |
| `test/NonSilo.Tests/OrleansRuntime/` |
| `test/Orleans.CodeGenerator.Tests/` |
| `test/Orleans.Connections.Security.Tests/` |
| `test/Orleans.Dashboard.Tests/` |
| `test/Orleans.Dashboard.Tests/Orleans.Dashboard.TestGrains/` |
| `test/Orleans.Dashboard.Tests/Orleans.Dashboard.UnitTests/` |
| `test/Orleans.Journaling.Tests/` |
| `test/Orleans.Serialization.FSharp.Tests/` |
| `test/Orleans.Serialization.UnitTests/` |
| `test/TestInfrastructure/Orleans.TestingHost.Tests/` |
| `test/TesterInternal/OrleansRuntime/` |
| `test/Transactions/Orleans.Transactions.Azure.Test/` |
| `test/Transactions/Orleans.Transactions.Tests/` |

---

## 3. Files with "Orleans" in Filename (230 files)

### By Extension

| Extension | Count | Examples |
|-----------|-------|---------|
| `.cs` | 138 | `OrleansException.cs`, `OrleansSourceGenerator.cs`, `OrleansSiloInstanceManager.cs` |
| `.csproj` | 77 | `Orleans.Core.csproj`, `Orleans.Runtime.csproj`, etc. |
| `.targets` | 4 | `Microsoft.Orleans.Sdk.targets`, `Orleans.Dashboard.Frontend.targets` |
| `.json` | 4 | `Orleans.*.xunit.runner.json` |
| `.props` | 3 | `Microsoft.Orleans.CodeGenerator.props` |
| `.slnx` | 1 | `Orleans.slnx` (main solution file) |
| `.sln` | 1 | `ManagedCode.Orleans.Identity.sln` |
| `.png` | 1 | `OrleansLogo.png` |
| `.fsproj` | 1 | `Orleans.Serialization.FSharp.Tests.fsproj` |

### By Location

| Location | Count |
|----------|-------|
| `src/` | 167 |
| `test/` | 62 |
| Root | 1 (`Orleans.slnx`) |

---

## 4. Files Containing "Orleans" in Content (2,891 files)

### By File Extension

| Extension | Count |
|-----------|-------|
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
| `.png` | 2 (binary) |
| `.gitignore` | 2 |
| Other (`.yml`, `.ts`, `.slnx`, `.sln`, `.resx`, `.ps1`, `.proto`, `.fsproj`, `.cmd`) | 1 each |

### By Top-Level Directory

| Directory | Files with Orleans content |
|-----------|---------------------------|
| `src/` | 2,016 |
| `test/` | 791 |
| `playground/` | 63 |
| `.azure/` | 4 |
| Root-level files | 16 |

### Root-Level Files with Orleans Content

- `Orleans.slnx` -- Solution file
- `README.md` -- Documentation
- `CONTRIBUTING.md` -- Documentation
- `SUPPORT.md` -- Documentation
- `Directory.Build.props` -- Build config
- `Directory.Build.targets` -- Build config
- `Directory.Packages.props` -- Package versions
- `Test.cmd` -- Build script
- `build.ps1` -- Build script
- `distributed-tests.yml` -- CI config
- `.gitignore` -- Git config
- `.config/tsaoptions.json` -- Tool config
- `.devcontainer/devcontainer.json` -- Dev container
- `.github/copilot-instructions.md` -- GitHub config

---

## 5. Namespace Declarations with "Orleans"

Over **200 distinct namespace declarations** beginning with `Orleans`:

### Primary namespaces
- `namespace Orleans` / `namespace Orleans;`
- `namespace Orleans.Core` / `Orleans.Core.Internal`
- `namespace Orleans.Runtime` / `Orleans.Runtime.*` (20+ sub-namespaces)
- `namespace Orleans.Serialization` / `Orleans.Serialization.*` (15+ sub-namespaces)
- `namespace Orleans.Hosting` / `Orleans.Hosting.*`
- `namespace Orleans.Configuration` / `Orleans.Configuration.*`
- `namespace Orleans.Streaming` / `Orleans.Streaming.*` (10+ sub-namespaces)
- `namespace Orleans.Transactions` / `Orleans.Transactions.*`
- `namespace Orleans.Placement` / `Orleans.Placement.*`
- `namespace Orleans.Persistence` / `Orleans.Persistence.*`
- `namespace Orleans.Clustering.*`
- `namespace Orleans.GrainDirectory` / `Orleans.GrainDirectory.*`
- `namespace Orleans.Reminders` / `Orleans.Reminders.*`
- `namespace Orleans.EventSourcing` / `Orleans.EventSourcing.*`
- `namespace Orleans.DurableJobs` / `Orleans.DurableJobs.*`
- `namespace Orleans.Journaling` / `Orleans.Journaling.*`
- `namespace Orleans.Dashboard` / `Orleans.Dashboard.*`
- `namespace Orleans.BroadcastChannel`
- `namespace Orleans.TestingHost` / `Orleans.TestingHost.*`
- `namespace Orleans.Analyzers`
- `namespace Orleans.CodeGenerator` / `Orleans.CodeGenerator.*`
- `namespace Orleans.DynamicGrains`
- `namespace Orleans.Identity.*`

### Generated code namespaces
- `namespace OrleansCodeGen.Orleans.*` (40+ sub-namespaces)
- `namespace OrleansAWSUtils.*`

---

## 6. C# Type Names Containing "Orleans" (50+)

### Exception Types
- `OrleansException`, `OrleansConfigurationException`
- `OrleansLifecycleCanceledException`, `OrleansMessageRejectionException`
- `OrleansClusterConnectivityCheckFailedException`, `OrleansMissingMembershipEntryException`
- `OrleansTransactionException` (+ 10 transaction-related exception types)
- `OrleansBrokenTransactionLockException`, `OrleansCascadingAbortException`
- `OrleansOrphanCallException`, `OrleansReadOnlyViolatedException`

### Generated Codec/Copier Types (40 types)
- `Codec_Orleans*Exception` (20 types)
- `Copier_Orleans*Exception` (20 types)

### Service/Utility Types
- `OrleansJsonSerializer`, `OrleansJsonSerializerSettings`, `OrleansJsonSerializerOptions`
- `OrleansJsonSerializationBinder`, `OrleansGrainStateSerializer`
- `OrleansClientGenericHostExtensions`, `OrleansSiloGenericHostExtensions`
- `OrleansSourceGenerator`, `OrleansGeneratorDiagnosticAnalysisException`
- `OrleansApplicationProtocol`, `OrleansDebuggerHelper`
- `OrleansTestingBase`, `OrleansTaskScheduler*` (3 test class variants)
- `OrleansSiloInstanceManager`, `OrleansDefaultHasher`
- `Orleans3CompatibleHasher`, `Orleans3CompatibleStorageHashPicker`, `Orleans3CompatibleStringKeyHasher`
- `OrleansIdentityConstants`, `OrleansIdentityExtensions`, `OrleansAuthorizationActionFilter`
- `OrleansBuilderMarker`, `OrleansCallBackDataEvent`
- `OrleansGeneratedCodeHelper`, `OrleansRelationalDownloadStream`
- `RelationalOrleansQueries`, `OrleansQueries`
- `OrleansServiceBusErrorCode`

### NuGet Package IDs (Microsoft.Orleans.*)
All use `Microsoft.Orleans.*` prefix:
- `Microsoft.Orleans.Core`, `Microsoft.Orleans.Runtime`, `Microsoft.Orleans.Sdk`
- `Microsoft.Orleans.Client`, `Microsoft.Orleans.Server`
- `Microsoft.Orleans.Serialization.*` (6 variants)
- `Microsoft.Orleans.Clustering.*` (6 variants)
- `Microsoft.Orleans.Persistence.*` (5 variants)
- `Microsoft.Orleans.Streaming.*` (5 variants)
- `Microsoft.Orleans.Reminders.*` (5 variants)
- `Microsoft.Orleans.Transactions.*` (3 variants)
- `Microsoft.Orleans.CodeGenerator`, `Microsoft.Orleans.Analyzers`
- `Microsoft.Orleans.Dashboard`, `Microsoft.Orleans.Dashboard.Abstractions`
- And many more

---

## 7. Diagnostic/Constant Identifiers

| Identifier | Usage |
|------------|-------|
| `ORLEANS0001` - `ORLEANS0013` | Roslyn analyzer diagnostic IDs |
| `ORLEANSEXP001` - `ORLEANSEXP004` | Experimental feature flags |
| `ORLEANS_CLUSTER_ID` | Kubernetes environment variable |
| `ORLEANS_SERVICE_ID` | Kubernetes environment variable |
| `ORLEANS_CLUSTERING` | Compiler define constant |

---

## 8. SQL Database References

29 SQL files in `src/AdoNet/` contain Orleans references in:
- Table names and stored procedure names
- Comments referencing Orleans
- Schema definitions
- Files: PostgreSQL, MySQL, SQLServer, Oracle persistence/reminders/clustering/streaming SQL

---

## 9. CI/CD and Infrastructure

| File | Content |
|------|---------|
| `.azure/pipelines/templates/vars.yaml` | Build variables with Orleans names |
| `.azure/pipelines/templates/build.yaml` | Build steps referencing Orleans |
| `.azure/pipelines/github-mirror.yaml` | Mirror config |
| `.azure/pipelines/nightly-main.yaml` | Nightly build config |
| `.github/copilot-instructions.md` | References Orleans |
| `.config/tsaoptions.json` | References Orleans |
| `.devcontainer/devcontainer.json` | References Orleans |

---

## 10. Scale Assessment

This is NOT a simple find-and-replace rename. The "Orleans" naming is deeply embedded as the **core identity** of the framework:

- **2,891 out of 3,344 files** (86%) contain "Orleans" in their content
- **139 directories** are named with "Orleans"
- **230 files** have "Orleans" in their filename
- **~16,768+ line occurrences** of the PascalCase form alone
- **200+ distinct namespaces** starting with `Orleans`
- **50+ type names** containing "Orleans"
- **77 .csproj files** named `Orleans.*.csproj`
- SQL schemas, binary assets, CI/CD configs all affected

### Rename Strategy Considerations

1. **Full Rename**: Would touch 2,891+ files, rename 139+ directories, rename 230 files. Extremely high risk of breakage.
2. **Namespace-Only Rename**: Keep directory/file names, change `namespace Orleans` -> new namespace. Still massive.
3. **Wrapper/Alias Approach**: Keep internal Orleans naming, add Scynapse as a public-facing alias layer.
4. **Accept Orleans Naming Internally**: The project directory is already `Scynapse/`. Keep internal framework code using `Orleans.*` namespaces (as this is a fork), rename only our custom additions.

---

## 11. Immediate Cleanup Items (2 remaining NewOrleans references)

These should be cleaned up regardless of any broader rename decision:

1. **File rename needed:** `src/Scynapse.AsyncPlus/Services/NewOrleansAsyncPersistenceService.cs`
2. **Content update needed:** `playground/PluginGrainScenarios/Grains/EventTestGrain.cs` line 6: `// NEWORLEANS EVENTS TEST GRAINS`

---

## Notes

- The previous NewOrleans -> Scynapse rename only touched the **project's own custom code** (Scynapse.AsyncPlus and related docs)
- The original Orleans framework naming (`Orleans.*`) was intentionally left intact as it represents the upstream fork identity
- This document serves as a comprehensive reference for any future deeper rename effort
- Generated by exhaustive `find` and `grep` search on 2026-02-26
