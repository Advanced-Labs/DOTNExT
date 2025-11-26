# Dynamic Grain Access Design Document

## Overview

This document describes the design for dynamic grain access in Orleans, enabling both **clients** and **silos** (grain-to-grain calls) to access grains without compile-time references to grain interfaces.

## Current State Analysis

### Existing `GetGrain` Overloads in `IGrainFactory`

| # | Method Signature | Key Type | Returns |
|---|-----------------|----------|---------|
| 1 | `GetGrain<T>(Guid, string?)` | Guid | T |
| 2 | `GetGrain<T>(long, string?)` | Int64 | T |
| 3 | `GetGrain<T>(string, string?)` | String | T |
| 4 | `GetGrain<T>(Guid, string, string?)` | Guid + Extension | T |
| 5 | `GetGrain<T>(long, string, string?)` | Int64 + Extension | T |
| 6 | `GetGrain(Type, Guid)` | Guid | IGrain |
| 7 | `GetGrain(Type, long)` | Int64 | IGrain |
| 8 | `GetGrain(Type, string)` | String | IGrain |
| 9 | `GetGrain(Type, Guid, string)` | Guid + Extension | IGrain |
| 10 | `GetGrain(Type, long, string)` | Int64 + Extension | IGrain |
| 11 | `GetGrain<T>(GrainId)` | GrainId | T |
| 12 | `GetGrain(GrainId)` | GrainId | IAddressable |
| 13 | `GetGrain(GrainId, GrainInterfaceType)` | GrainId + Interface | IAddressable |

### Existing Type System

```
GrainType          - Identifies a grain class (e.g., "hello-grain")
GrainInterfaceType - Identifies a grain interface (e.g., "ihello-grain")
GrainId            - Full grain identifier (Type + Key)
GrainManifest      - Contains Grains + Interfaces dictionaries
GrainProperties    - String key-value pairs with metadata
```

---

## New Type Definitions

### 1. `GrainPackage` - Distributed Grain Package

A grain package represents a deployable unit containing grain interfaces and/or implementations.

```csharp
namespace Orleans.Metadata
{
    /// <summary>
    /// Represents a distributable package of grain types.
    /// Can contain interfaces only (for clients) or interfaces + implementations (for silos).
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainPackage
    {
        /// <summary>
        /// Unique identifier for this package.
        /// </summary>
        [Id(0)]
        public string PackageId { get; init; }

        /// <summary>
        /// Version of the package (SemVer format recommended).
        /// </summary>
        [Id(1)]
        public string Version { get; init; }

        /// <summary>
        /// Hash of the package contents for integrity verification.
        /// </summary>
        [Id(2)]
        public string ContentHash { get; init; }

        /// <summary>
        /// Grain types available in this package.
        /// </summary>
        [Id(3)]
        public ImmutableList<GrainTypeMeta> GrainTypes { get; init; }

        /// <summary>
        /// What this package contains.
        /// </summary>
        [Id(4)]
        public GrainPackageContent ContentType { get; init; }

        /// <summary>
        /// Assembly files in this package.
        /// </summary>
        [Id(5)]
        public ImmutableList<GrainPackageAssembly> Assemblies { get; init; }

        /// <summary>
        /// Package metadata (author, description, etc.).
        /// </summary>
        [Id(6)]
        public ImmutableDictionary<string, string> Metadata { get; init; }

        /// <summary>
        /// Gets a grain type descriptor by name.
        /// </summary>
        public GrainTypeMeta? GetGrainType(string grainTypeName, string? version = null)
        {
            return GrainTypes.FirstOrDefault(t =>
                t.FullName == grainTypeName &&
                (version == null || t.Version == version));
        }
    }

    /// <summary>
    /// What content a grain package contains.
    /// </summary>
    public enum GrainPackageContent
    {
        /// <summary>
        /// Contains only interfaces and generated proxies (for clients).
        /// </summary>
        InterfacesOnly,

        /// <summary>
        /// Contains interfaces, proxies, and implementations (for silos).
        /// </summary>
        Full,

        /// <summary>
        /// Contains only implementations (requires separate interface package).
        /// </summary>
        ImplementationsOnly
    }

    /// <summary>
    /// An assembly file within a grain package.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainPackageAssembly
    {
        [Id(0)]
        public string FileName { get; init; }

        [Id(1)]
        public string AssemblyName { get; init; }

        [Id(2)]
        public string Version { get; init; }

        [Id(3)]
        public string Hash { get; init; }

        [Id(4)]
        public GrainAssemblyRole Role { get; init; }
    }

    public enum GrainAssemblyRole
    {
        Interfaces,
        Implementation,
        Codegen,
        Dependency
    }
}
```

### 2. `GrainTypeMeta` - Grain Type Metadata

```csharp
namespace Orleans.Metadata
{
    /// <summary>
    /// Detailed metadata about a grain type, including reflection-like information.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainTypeMeta
    {
        /// <summary>
        /// The Orleans GrainType identifier.
        /// </summary>
        [Id(0)]
        public GrainType GrainType { get; init; }

        /// <summary>
        /// Full CLR type name of the grain interface.
        /// </summary>
        [Id(1)]
        public string FullName { get; init; }

        /// <summary>
        /// Namespace of the grain interface.
        /// </summary>
        [Id(2)]
        public string Namespace { get; init; }

        /// <summary>
        /// Simple type name without namespace.
        /// </summary>
        [Id(3)]
        public string TypeName { get; init; }

        /// <summary>
        /// Version of this grain type.
        /// </summary>
        [Id(4)]
        public string Version { get; init; }

        /// <summary>
        /// Assembly containing this grain type.
        /// </summary>
        [Id(5)]
        public string AssemblyName { get; init; }

        /// <summary>
        /// Hash of the assembly for versioning.
        /// </summary>
        [Id(6)]
        public string AssemblyHash { get; init; }

        /// <summary>
        /// The interface types this grain implements.
        /// </summary>
        [Id(7)]
        public ImmutableList<GrainInterfaceMeta> Interfaces { get; init; }

        /// <summary>
        /// Key type (String, Guid, Int64, etc.).
        /// </summary>
        [Id(8)]
        public GrainKeyType KeyType { get; init; }

        /// <summary>
        /// Reference back to the containing package (if loaded from a package).
        /// </summary>
        [Id(9)]
        public GrainPackage? SourcePackage { get; init; }

        /// <summary>
        /// Silos currently hosting this grain type.
        /// </summary>
        [Id(10)]
        public ImmutableList<SiloAddress> HostingSilos { get; init; }

        /// <summary>
        /// Whether the grain type is currently available for activation.
        /// </summary>
        [Id(11)]
        public bool IsAvailable { get; init; }
    }

    public enum GrainKeyType
    {
        String,
        Guid,
        Int64,
        GuidCompound,
        Int64Compound
    }

    /// <summary>
    /// Metadata about a grain interface.
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainInterfaceMeta
    {
        [Id(0)]
        public GrainInterfaceType InterfaceType { get; init; }

        [Id(1)]
        public string FullName { get; init; }

        [Id(2)]
        public ImmutableList<GrainMethodMeta> Methods { get; init; }
    }

    /// <summary>
    /// Metadata about a grain method (for reflection-like invocation).
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainMethodMeta
    {
        [Id(0)]
        public string Name { get; init; }

        [Id(1)]
        public string ReturnType { get; init; }

        [Id(2)]
        public ImmutableList<GrainParameterMeta> Parameters { get; init; }

        [Id(3)]
        public int MethodId { get; init; }  // Orleans method identifier
    }

    [GenerateSerializer, Immutable]
    public sealed class GrainParameterMeta
    {
        [Id(0)]
        public string Name { get; init; }

        [Id(1)]
        public string TypeName { get; init; }

        [Id(2)]
        public bool IsOptional { get; init; }
    }
}
```

---

## New API Design

### Extended `IGrainFactory` Interface

```csharp
namespace Orleans
{
    public interface IGrainFactory
    {
        // ... existing methods ...

        // =============================================
        // NEW: Dynamic grain access methods
        // =============================================

        /// <summary>
        /// Gets a grain reference as a dynamic object.
        /// Enables late-bound method invocation without compile-time type reference.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        dynamic GetGrainDynamic(string grainTypeName, string primaryKey);

        /// <summary>
        /// Gets a grain reference as a dynamic object.
        /// </summary>
        dynamic GetGrainDynamic(string grainTypeName, Guid primaryKey);

        /// <summary>
        /// Gets a grain reference as a dynamic object.
        /// </summary>
        dynamic GetGrainDynamic(string grainTypeName, long primaryKey);

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        /// <param name="grainTypeMeta">Grain type metadata from GTD.</param>
        /// <param name="primaryKey">The grain primary key.</param>
        /// <returns>A dynamic grain reference with routing info from metadata.</returns>
        dynamic GetGrain(GrainTypeMeta grainTypeMeta, string primaryKey);

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        dynamic GetGrain(GrainTypeMeta grainTypeMeta, Guid primaryKey);

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        dynamic GetGrain(GrainTypeMeta grainTypeMeta, long primaryKey);
    }
}
```

### `IDynamicGrainClient` Interface

```csharp
namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// Extended client for dynamic grain access with package management.
    /// Works for both external clients AND silos (grain-to-grain calls).
    /// </summary>
    public interface IDynamicGrainClient
    {
        // =============================================
        // Package Management
        // =============================================

        /// <summary>
        /// Loads a grain package from the cluster's package store.
        /// Downloads and caches locally if not already present.
        /// </summary>
        Task<GrainPackageHandle> LoadPackageAsync(
            string packageId,
            string? version = null,
            CancellationToken ct = default);

        /// <summary>
        /// Unloads a previously loaded package, freeing resources.
        /// </summary>
        Task UnloadPackageAsync(GrainPackageHandle handle, CancellationToken ct = default);

        /// <summary>
        /// Lists all available packages in the cluster.
        /// </summary>
        Task<IReadOnlyList<GrainPackageInfo>> ListAvailablePackagesAsync(
            CancellationToken ct = default);

        // =============================================
        // Grain Access
        // =============================================

        /// <summary>
        /// Gets a grain dynamically by type name.
        /// Will auto-load the required package if not already loaded.
        /// </summary>
        Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            string primaryKey,
            CancellationToken ct = default);

        /// <summary>
        /// Gets a grain using metadata from a loaded package.
        /// </summary>
        dynamic GetGrain(GrainTypeMeta grainType, string primaryKey);

        /// <summary>
        /// Invokes a method on a grain by name (fully dynamic).
        /// </summary>
        Task<object?> InvokeMethodAsync(
            string grainTypeName,
            string primaryKey,
            string methodName,
            object?[]? args = null,
            CancellationToken ct = default);

        // =============================================
        // GTD Queries
        // =============================================

        /// <summary>
        /// Queries the Grain Type Directory for available types.
        /// </summary>
        Task<IReadOnlyList<GrainTypeMeta>> QueryGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null,
            CancellationToken ct = default);

        /// <summary>
        /// Gets detailed metadata for a specific grain type.
        /// </summary>
        Task<GrainTypeMeta?> GetGrainTypeMetaAsync(
            string grainTypeName,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Handle to a loaded grain package.
    /// </summary>
    public sealed class GrainPackageHandle : IAsyncDisposable
    {
        public GrainPackage Package { get; }
        public bool IsLoaded { get; }

        /// <summary>
        /// Gets a grain type by name from this package.
        /// </summary>
        public GrainTypeMeta? GetGrainType(string name, string? version = null);

        /// <summary>
        /// Gets a grain reference from this package.
        /// </summary>
        public dynamic GetGrain(string grainTypeName, string primaryKey);

        /// <summary>
        /// Gets a strongly-typed grain reference if the interface is available.
        /// </summary>
        public TGrainInterface GetGrain<TGrainInterface>(string primaryKey)
            where TGrainInterface : IGrain;

        public ValueTask DisposeAsync();
    }
}
```

---

## Grain Type Directory (GTD)

### GTD Grain Interface

```csharp
namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// The Grain Type Directory - a cluster-wide registry of grain types.
    /// Implemented as a singleton grain.
    /// </summary>
    public interface IGrainTypeDirectoryGrain : IGrainWithStringKey
    {
        // =============================================
        // Package Registration
        // =============================================

        /// <summary>
        /// Registers a grain package in the directory.
        /// </summary>
        Task RegisterPackageAsync(GrainPackage package);

        /// <summary>
        /// Unregisters a grain package.
        /// </summary>
        Task UnregisterPackageAsync(string packageId, string version);

        // =============================================
        // Package Queries
        // =============================================

        /// <summary>
        /// Gets all registered packages.
        /// </summary>
        Task<ImmutableList<GrainPackageInfo>> GetPackagesAsync();

        /// <summary>
        /// Gets a specific package by ID and optional version.
        /// </summary>
        Task<GrainPackage?> GetPackageAsync(string packageId, string? version = null);

        // =============================================
        // Grain Type Queries
        // =============================================

        /// <summary>
        /// Gets all registered grain types.
        /// </summary>
        Task<ImmutableList<GrainTypeMeta>> GetAllGrainTypesAsync();

        /// <summary>
        /// Finds grain types matching a pattern.
        /// </summary>
        Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null);

        /// <summary>
        /// Gets metadata for a specific grain type.
        /// </summary>
        Task<GrainTypeMeta?> GetGrainTypeAsync(string fullTypeName);

        // =============================================
        // Silo Tracking
        // =============================================

        /// <summary>
        /// Reports that a silo has loaded a package.
        /// </summary>
        Task ReportPackageLoadedAsync(SiloAddress silo, string packageId, string version);

        /// <summary>
        /// Reports that a silo has unloaded a package.
        /// </summary>
        Task ReportPackageUnloadedAsync(SiloAddress silo, string packageId, string version);

        /// <summary>
        /// Gets silos that have a specific grain type loaded.
        /// </summary>
        Task<ImmutableList<SiloAddress>> GetHostingSilosAsync(string grainTypeName);
    }

    /// <summary>
    /// Summary info about a package (without full content).
    /// </summary>
    [GenerateSerializer, Immutable]
    public sealed class GrainPackageInfo
    {
        [Id(0)] public string PackageId { get; init; }
        [Id(1)] public string Version { get; init; }
        [Id(2)] public string ContentHash { get; init; }
        [Id(3)] public int GrainTypeCount { get; init; }
        [Id(4)] public GrainPackageContent ContentType { get; init; }
        [Id(5)] public ImmutableList<SiloAddress> LoadedOnSilos { get; init; }
    }
}
```

---

## Package Storage & Distribution

### Storage Options Analysis

#### Option 1: Grain-Based Storage (Simple, No External Dependencies)

```csharp
/// <summary>
/// Stores grain package content in the cluster itself using grain state.
/// </summary>
public interface IPackageStorageGrain : IGrainWithStringKey
{
    Task<bool> UploadChunkAsync(int index, byte[] chunk);
    Task<bool> CompleteUploadAsync(GrainPackageInfo metadata);
    Task<byte[]?> DownloadChunkAsync(int index);
    Task<GrainPackageInfo?> GetMetadataAsync();
}
```

**Pros:**
- No external dependencies
- Works out of the box
- Leverages existing Orleans persistence

**Cons:**
- Limited by grain state size limits
- Not ideal for large packages
- No external tooling integration

#### Option 2: NuGet-Based Distribution

```csharp
/// <summary>
/// Uses NuGet feeds for package distribution.
/// </summary>
public interface INuGetPackageSource
{
    Task<GrainPackage?> FetchPackageAsync(string packageId, string? version);
    Task PublishPackageAsync(GrainPackage package, Stream content);
    Task<IReadOnlyList<GrainPackageInfo>> SearchAsync(string? query);
}
```

**Pros:**
- Industry-standard tooling (dotnet CLI, VS integration)
- Mature versioning (SemVer)
- Existing infrastructure (NuGet.org, Azure Artifacts, private feeds)
- Dependency resolution built-in
- Symbol server integration
- Existing security model (signatures, etc.)

**Cons:**
- External dependency
- Network latency for package fetch
- Need NuGet feed setup
- Package format constraints

#### Option 3: Hybrid Approach (Recommended)

```csharp
public interface IGrainPackageStore
{
    /// <summary>
    /// Gets a package, checking local cache first, then fetching from source.
    /// </summary>
    Task<GrainPackageHandle?> GetPackageAsync(
        string packageId,
        string? version = null,
        CancellationToken ct = default);

    /// <summary>
    /// Registers a package source (NuGet feed, file system, etc.).
    /// </summary>
    void RegisterSource(IGrainPackageSource source);
}

public interface IGrainPackageSource
{
    string Name { get; }
    int Priority { get; }
    Task<GrainPackage?> FetchAsync(string packageId, string? version);
    Task<IReadOnlyList<GrainPackageInfo>> ListAsync();
}

// Built-in sources
public class FileSystemPackageSource : IGrainPackageSource { }
public class NuGetPackageSource : IGrainPackageSource { }
public class GrainStoragePackageSource : IGrainPackageSource { }
```

---

## Package Cache System

```csharp
namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// Local cache for grain packages.
    /// Used by both silos and clients.
    /// </summary>
    public interface IGrainPackageCache
    {
        /// <summary>
        /// Gets a cached package, or null if not cached.
        /// </summary>
        Task<GrainPackageHandle?> GetCachedAsync(string packageId, string? version);

        /// <summary>
        /// Adds a package to the cache.
        /// </summary>
        Task<GrainPackageHandle> CacheAsync(GrainPackage package, byte[] content);

        /// <summary>
        /// Removes a package from the cache.
        /// </summary>
        Task EvictAsync(string packageId, string? version = null);

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        GrainPackageCacheStats GetStats();
    }

    public class GrainPackageCacheStats
    {
        public int PackageCount { get; init; }
        public long TotalSizeBytes { get; init; }
        public int HitCount { get; init; }
        public int MissCount { get; init; }
    }

    /// <summary>
    /// File-system based cache implementation.
    /// </summary>
    public class FileSystemPackageCache : IGrainPackageCache
    {
        public FileSystemPackageCache(string cacheDirectory) { }
    }
}
```

---

## Usage Examples

### Example 1: Client Accessing Grains Dynamically

```csharp
// Get the dynamic client
var dynamicClient = serviceProvider.GetRequiredService<IDynamicGrainClient>();

// Option A: Fully dynamic by type name
var result = await dynamicClient.InvokeMethodAsync(
    "MyNamespace.IHelloGrain",
    "my-grain-id",
    "SayHello",
    new object[] { "World" }
);

// Option B: Load package first
var packageHandle = await dynamicClient.LoadPackageAsync("MyGrains");
var grainType = packageHandle.GetGrainType("MyNamespace.IHelloGrain");
dynamic grain = packageHandle.GetGrain("MyNamespace.IHelloGrain", "my-grain-id");
string greeting = await grain.SayHello("World");

// Option C: Query GTD then access
var grainMeta = await dynamicClient.GetGrainTypeMetaAsync("MyNamespace.IHelloGrain");
if (grainMeta != null)
{
    var grainFactory = serviceProvider.GetRequiredService<IGrainFactory>();
    dynamic grain2 = grainFactory.GetGrain(grainMeta, "my-grain-id");
    await grain2.SayHello("World");
}
```

### Example 2: Grain-to-Grain Dynamic Call (Silo)

```csharp
public class OrchestratorGrain : Grain, IOrchestratorGrain
{
    private readonly IDynamicGrainClient _dynamicClient;

    public OrchestratorGrain(IDynamicGrainClient dynamicClient)
    {
        _dynamicClient = dynamicClient;
    }

    public async Task ProcessAsync(string pluginGrainType, string data)
    {
        // Load plugin grain dynamically
        var result = await _dynamicClient.InvokeMethodAsync(
            pluginGrainType,       // e.g., "Plugins.IDataProcessor"
            "processor-1",
            "ProcessData",
            new object[] { data }
        );
    }
}
```

### Example 3: Using GrainPackage and GrainTypeMeta

```csharp
// Query GTD for package
var gtd = grainFactory.GetGrain<IGrainTypeDirectoryGrain>("gtd");
var package = await gtd.GetPackageAsync("MyPlugins", "1.0.0");

if (package != null)
{
    // Get grain type from package
    var helloGrainMeta = package.GetGrainType("MyPlugins.IHelloGrain");

    // Access via factory extension
    dynamic grain = grainFactory.GetGrain(helloGrainMeta, "test-key");
    await grain.DoWork();

    // Or get hosting info
    var hostingSilos = helloGrainMeta.HostingSilos;
    Console.WriteLine($"Available on {hostingSilos.Count} silos");
}
```

---

## Implementation Phases

### Phase 1: Core Types (Foundation) ✅ COMPLETE

**Implemented types in `src/Orleans.Core.Abstractions/Manifest/`:**

| File | Types | Description |
|------|-------|-------------|
| `GrainPackage.cs` | `GrainPackage` | Distributable package with assemblies, version, hash |
| | `GrainPackageContent` | Enum: InterfacesOnly, Full, ImplementationsOnly |
| | `GrainPackageAssembly` | Assembly file metadata within package |
| | `GrainAssemblyRole` | Enum: Interfaces, Implementation, Codegen, Dependency |
| | `GrainPackageInfo` | Lightweight summary for listings |
| `GrainTypeMeta.cs` | `GrainTypeMeta` | Full type metadata with SourcePackage back-ref |
| | `GrainKeyType` | Enum: String, Guid, Int64, compound variants |
| `GrainInterfaceMeta.cs` | `GrainInterfaceMeta` | Interface with method list |
| | `GrainMethodMeta` | Method signature + Orleans MethodId |
| | `GrainParameterMeta` | Parameter name, type, optional flag |

**Key design decisions:**
- All types use `[Serializable, GenerateSerializer, Immutable]` following Orleans patterns
- `GrainTypeMeta.SourcePackage` enables navigation from type → package
- `GrainTypeMeta.HostingSilos` tracks which silos can activate the grain
- Immutable "With" methods for updates: `WithHostingSilos()`, `WithAvailability()`

---

### Phase 2: GTD Implementation ✅ COMPLETE

**Implemented:**

| File | Location | Description |
|------|----------|-------------|
| `IGrainTypeDirectoryGrain.cs` | `Orleans.Core.Abstractions/DynamicGrains/` | Public grain interface |
| `GrainTypeDirectoryGrain.cs` | `Orleans.Runtime/DynamicGrains/` | Singleton grain implementation |
| `GrainTypeDirectoryState` | (in above file) | Persisted state with packages & silo tracking |

**Methods implemented:**
- [x] Package registration: `RegisterPackageAsync`, `UnregisterPackageAsync`
- [x] Package queries: `GetPackagesAsync`, `GetPackageAsync` (with optional version)
- [x] Type queries: `GetAllGrainTypesAsync`, `FindGrainTypesAsync` (wildcard support), `GetGrainTypeAsync`
- [x] Silo tracking: `ReportPackageLoadedAsync`, `ReportPackageUnloadedAsync`, `GetHostingSilosAsync`, `ReportSiloDownAsync`

**Key features:**
- Uses `Grain<TState>` pattern with `[StorageProvider]` for persistence
- Automatically updates `GrainTypeMeta.HostingSilos` and `IsAvailable` in queries
- Wildcard pattern matching in `FindGrainTypesAsync` (e.g., `*Hello*`)
- Package versioning with latest-version fallback in `GetPackageAsync`

---

### Phase 3: Dynamic Grain Factory Extensions
- [ ] `GetGrainDynamic()` methods
- [ ] `GetGrain(GrainTypeMeta, key)` overloads
- [ ] Dynamic proxy generation for runtime types

### Phase 4: Package Storage & Distribution
- [ ] `IGrainPackageStore` interface
- [ ] File system source
- [ ] Grain storage source
- [ ] NuGet source (optional)

### Phase 5: Package Cache
- [ ] `IGrainPackageCache` interface
- [ ] File system cache implementation
- [ ] Cache eviction policies

### Phase 6: Client Integration
- [ ] `IDynamicGrainClient` implementation
- [ ] Client-side package loading
- [ ] Integration with Orleans Client

---

## C# `dynamic` Keyword Considerations

**Q: Can `GetGrain<dynamic>()` work?**

No, not directly. The `dynamic` keyword in C# is not a real type - it's a compiler feature that defers type checking to runtime. Generic constraints like `where T : IGrain` would fail at compile time for `dynamic`.

**Solution:** Create separate `GetGrainDynamic()` methods that return `dynamic` and use DLR (Dynamic Language Runtime) for invocation.

```csharp
// This WON'T work:
// var grain = factory.GetGrain<dynamic>("key");  // Compile error

// This WILL work:
dynamic grain = factory.GetGrainDynamic("MyNamespace.IHelloGrain", "key");
await grain.SayHello("World");  // DLR routes the call
```

---

## File Locations

```
src/Orleans.Core.Abstractions/
├── Manifest/                              # ✅ Phase 1 Complete
│   ├── GrainPackage.cs                    # ✅ GrainPackage, enums, GrainPackageInfo
│   ├── GrainTypeMeta.cs                   # ✅ GrainTypeMeta, GrainKeyType
│   ├── GrainInterfaceMeta.cs              # ✅ Interface/Method/Parameter meta
│   └── (existing Orleans manifest types)
├── DynamicGrains/                         # ✅ Phase 2 Complete
│   └── IGrainTypeDirectoryGrain.cs        # ✅ GTD grain interface (public API)

src/Orleans.Core/
├── DynamicGrains/                         # Phase 5-6
│   ├── IDynamicGrainClient.cs
│   ├── DynamicGrainClient.cs
│   ├── IGrainPackageCache.cs
│   ├── FileSystemPackageCache.cs
│   └── DynamicGrainReference.cs

src/Orleans.Runtime/
├── DynamicGrains/                         # ✅ Phase 2 (partial), Phase 4
│   ├── GrainTypeDirectoryGrain.cs         # ✅ GTD implementation + state
│   ├── IGrainPackageStore.cs
│   ├── GrainStoragePackageSource.cs
│   └── NuGetPackageSource.cs (optional)
```
