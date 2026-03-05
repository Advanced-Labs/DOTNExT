# Scynapse: Dynamic Grain Loading System

## Overview

This document describes the dynamic grain loading system implemented in Scynapse (our Orleans fork), enabling runtime loading and unloading of grain assemblies without application restart. The implementation uses McMaster.NETCore.Plugins (MDCP) for assembly isolation and unloadability.

---

## Table of Contents

1. [Current Implementation Status](#current-implementation-status)
2. [System Architecture](#system-architecture)
3. [Component Locations & Responsibilities](#component-locations--responsibilities)
4. [Grain Type Directory (GTD)](#grain-type-directory-gtd)
5. [Package Cache System](#package-cache-system)
6. [Dynamic Grain Client](#dynamic-grain-client)
7. [Versioning & Compatibility](#versioning--compatibility)
8. [Integration Points](#integration-points)
9. [What's Missing / Incomplete](#whats-missing--incomplete)
10. [Future Vision: Distributed Package System](#future-vision-distributed-package-system)
11. [Development Guidelines](#development-guidelines)
12. [State Property Access](#state-property-access)
13. [Test Scenarios](#test-scenarios)

---

## Current Implementation Status

### Implemented ✅

| Feature | Status | Location |
|---------|--------|----------|
| MDCP-based assembly loading | Complete | `Scynapse.Runtime/DynamicGrains/PluginAssemblyLoader.cs` |
| Assembly unloading with GC | Complete | `Scynapse.Runtime/DynamicGrains/PluginGrainUnloaderService.cs` |
| Cluster manifest propagation | Complete | `Scynapse.Runtime/Manifest/ClusterManifestProvider.cs` |
| Grain Type Directory (GTD) | Complete | `Scynapse.Runtime/DynamicGrains/GrainTypeDirectoryGrain.cs` |
| Dynamic Grain Client | Complete | `Scynapse.Runtime/DynamicGrains/DynamicGrainClient.cs` |
| Package Cache (File System) | Complete | `Scynapse.Core/DynamicGrains/FileSystemPackageCache.cs` |
| Package Store (Multi-source) | Complete | `Scynapse.Runtime/DynamicGrains/GrainPackageStore.cs` |
| GrainPackage metadata model | Complete | `Scynapse.Core.Abstractions/Manifest/GrainPackage.cs` |
| GrainTypeMeta hierarchy | Complete | `Scynapse.Core.Abstractions/Manifest/GrainTypeMeta.cs` |
| DLR dynamic invocation | Complete | `Scynapse.Core/DynamicGrains/DynamicGrainReference.cs` |
| GetGrainDynamic extensions | Complete | `Scynapse.Core/DynamicGrains/GrainFactoryExtensions.cs` |
| StateTask\<T\> awaitable type | Complete | `Orleans.Core.Abstractions/State/StateTask.cs` |
| State/NotState attributes | Complete | `Orleans.Core.Abstractions/State/StateAttribute.cs` |
| Property→Interface codegen | Complete | `Orleans.CodeGenerator/StatePropertyCodeGenerator.cs` |
| Partial property backing fields | Complete | `Orleans.CodeGenerator/StatePropertyCodeGenerator.cs` |
| Proxy StateTask generation | Complete | `Orleans.CodeGenerator/ProxyGenerator.cs` |
| IPersistentState mapping | Complete | `Orleans.CodeGenerator/StatePropertyCodeGenerator.cs` |

### Partially Implemented ⚠️

| Feature | Status | Notes |
|---------|--------|-------|
| Package versioning | Basic | Version strings exist, no semantic versioning enforcement |
| Interface compatibility hashing | Planned | Fields exist but not fully utilized |
| Cross-silo package distribution | Basic | GrainStoragePackageSource works but no smart routing |

### Not Yet Implemented ❌

| Feature | Priority | Description |
|---------|----------|-------------|
| NuGet-based package system | High | Leverage NuGet for distributed packages |
| Interface hash compatibility | Medium | Automatic compatibility detection via hashing |
| Package dependency resolution | Medium | Handle inter-package dependencies |
| Rolling upgrades | Medium | Version-aware routing during upgrades |
| Package signing/verification | Low | Security for package distribution |

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                             Scynapse Cluster                                     │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                 Grain Type Directory (GTD)                               │   │
│  │                 IGrainTypeDirectoryGrain (Singleton: "gtd")              │   │
│  │  ┌──────────────────────┐  ┌───────────────────────────────────────┐    │   │
│  │  │ State:               │  │ Capabilities:                         │    │   │
│  │  │ - Packages{}         │  │ - RegisterPackageAsync()              │    │   │
│  │  │ - PackageSilos{}     │  │ - GetAllGrainTypesAsync()             │    │   │
│  │  │ (Persisted via       │  │ - FindGrainTypesAsync()               │    │   │
│  │  │  Default Storage)    │  │ - GetHostingSilosAsync()              │    │   │
│  │  └──────────────────────┘  └───────────────────────────────────────┘    │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                                        ▲                                        │
│                                        │ Uses metadata from                     │
│                                        │                                        │
│  ┌─────────────┐     ┌─────────────┐  │  ┌─────────────┐                       │
│  │   Silo 1    │     │   Silo 2    │  │  │   Silo 3    │                       │
│  │             │     │             │  │  │             │                       │
│  │ ┌─────────┐ │     │ ┌─────────┐ │  │  │ ┌─────────┐ │                       │
│  │ │ Plugin  │─┼─────┼─│ Plugin  │─┼──┴──┼─│ Plugin  │ │                       │
│  │ │ Loader  │ │     │ │ Loader  │ │     │ │ Loader  │ │                       │
│  │ └────┬────┘ │     │ └────┬────┘ │     │ └────┬────┘ │                       │
│  │      │      │     │      │      │     │      │      │                       │
│  │ ┌────▼────┐ │     │ ┌────▼────┐ │     │ ┌────▼────┐ │                       │
│  │ │Manifest │◄┼─────┼─┤Manifest │◄┼─────┼─┤Manifest │ │                       │
│  │ │Provider │ │     │ │Provider │ │     │ │Provider │ │                       │
│  │ └─────────┘ │     │ └─────────┘ │     │ └─────────┘ │                       │
│  │             │     │             │     │             │                       │
│  │ ┌─────────┐ │     │ ┌─────────┐ │     │ ┌─────────┐ │                       │
│  │ │Package  │ │     │ │Package  │ │     │ │Package  │ │                       │
│  │ │Cache    │ │     │ │Cache    │ │     │ │Cache    │ │                       │
│  │ │(Local)  │ │     │ │(Local)  │ │     │ │(Local)  │ │                       │
│  │ └─────────┘ │     │ └─────────┘ │     │ └─────────┘ │                       │
│  └─────────────┘     └─────────────┘     └─────────────┘                       │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                      Package Store (Multi-Source)                        │   │
│  │  ┌────────────────────────┐  ┌────────────────────────────────────────┐ │   │
│  │  │ FileSystemPackageSource│  │ GrainStoragePackageSource              │ │   │
│  │  │ Priority: 100          │  │ Priority: 200                          │ │   │
│  │  │ Writable: Yes          │  │ Writable: Yes (chunked storage)        │ │   │
│  │  │ Path: configurable     │  │ Uses: IPackageIndexGrain,              │ │   │
│  │  │                        │  │       IPackageStorageGrain             │ │   │
│  │  └────────────────────────┘  └────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        │ DynamicGrainClient
                                        ▼
                    ┌───────────────────────────────────────┐
                    │        Scynapse Client / Silo          │
                    │                                       │
                    │  ┌─────────────────────────────────┐  │
                    │  │ IDynamicGrainClient             │  │
                    │  │ - LoadPackageAsync()            │  │
                    │  │ - GetGrainDynamicAsync()        │  │
                    │  │ - InvokeMethodAsync()           │  │
                    │  │ - QueryGrainTypesAsync()        │  │
                    │  └─────────────────────────────────┘  │
                    │                                       │
                    │  ┌─────────────────────────────────┐  │
                    │  │ DynamicGrainReference (DLR)     │  │
                    │  │ - TryInvokeMember()             │  │
                    │  │ - TryGetMember()                │  │
                    │  │ - TryConvert()                  │  │
                    │  └─────────────────────────────────┘  │
                    └───────────────────────────────────────┘
```

---

## Component Locations & Responsibilities

### Scynapse.Core.Abstractions (Interfaces & Metadata)

| File | Type | Purpose |
|------|------|---------|
| `Manifest/GrainPackage.cs` | `GrainPackage`, `GrainPackageInfo`, `GrainPackageAssembly` | Package metadata model with assemblies and grain types |
| `Manifest/GrainTypeMeta.cs` | `GrainTypeMeta` | Per-grain-type metadata with interfaces and availability |
| `Manifest/GrainInterfaceMeta.cs` | `GrainInterfaceMeta`, `GrainMethodMeta`, `GrainParameterMeta` | Interface reflection metadata for dynamic invocation |
| `DynamicGrains/IGrainTypeDirectoryGrain.cs` | `IGrainTypeDirectoryGrain` | GTD interface - cluster-wide grain registry |
| `DynamicGrains/IGrainPackageStore.cs` | `IGrainPackageStore`, `IGrainPackageSource` | Package storage abstraction |

### Scynapse.Core (Client-Side Implementations)

| File | Type | Purpose |
|------|------|---------|
| `DynamicGrains/IDynamicGrainClient.cs` | `IDynamicGrainClient` | Client interface for dynamic grain access |
| `DynamicGrains/GrainPackageHandle.cs` | `GrainPackageHandle` | In-memory handle to loaded package |
| `DynamicGrains/DynamicGrainReference.cs` | `DynamicGrainReference` | DLR wrapper enabling `dynamic` keyword access |
| `DynamicGrains/GrainFactoryExtensions.cs` | Extension methods | `GetGrainDynamic()` methods on `IGrainFactory` |
| `DynamicGrains/IGrainPackageCache.cs` | `IGrainPackageCache` | Cache interface for packages |
| `DynamicGrains/FileSystemPackageCache.cs` | `FileSystemPackageCache` | Disk-based cache with LRU/LFU eviction |

### Scynapse.Runtime (Server-Side Implementations)

| File | Type | Purpose |
|------|------|---------|
| `DynamicGrains/GrainTypeDirectoryGrain.cs` | `GrainTypeDirectoryGrain` | GTD implementation - singleton grain with persistent state |
| `DynamicGrains/DynamicGrainClient.cs` | `DynamicGrainClient` | Client implementation with caching |
| `DynamicGrains/GrainPackageStore.cs` | `GrainPackageStore` | Orchestrates multiple package sources |
| `DynamicGrains/FileSystemPackageSource.cs` | `FileSystemPackageSource` | File-based package source |
| `DynamicGrains/GrainStoragePackageSource.cs` | `GrainStoragePackageSource` | Scynapse storage-based package source |
| `DynamicGrains/PluginGrainLoaderService.cs` | `PluginGrainLoaderService` | Assembly loading with manifest updates |
| `DynamicGrains/PluginGrainUnloaderService.cs` | `PluginGrainUnloaderService` | Assembly unloading with GC |
| `DynamicGrains/PluginAssemblyLoader.cs` | `PluginAssemblyLoader` | MDCP-based isolated assembly loading |
| `DynamicGrains/GrainLifecycleManager.cs` | `GrainLifecycleManager` | Tracks activations for type-based deactivation |

---

## Grain Type Directory (GTD)

### What It Is

The GTD is a **cluster-wide singleton grain** that serves as a registry of all available grain types, their metadata, and which silos have them loaded.

### Current Implementation

```csharp
// Interface: Scynapse.Core.Abstractions/DynamicGrains/IGrainTypeDirectoryGrain.cs
public interface IGrainTypeDirectoryGrain : IGrainWithStringKey
{
    // Package Registration
    Task RegisterPackageAsync(GrainPackage package);
    Task<bool> UnregisterPackageAsync(string packageId, string version);

    // Package Queries
    Task<ImmutableList<GrainPackageInfo>> GetPackagesAsync();
    Task<GrainPackage?> GetPackageAsync(string packageId, string? version = null);

    // Grain Type Queries
    Task<ImmutableList<GrainTypeMeta>> GetAllGrainTypesAsync();
    Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(
        string? namespaceFilter = null, string? namePattern = null);
    Task<GrainTypeMeta?> GetGrainTypeAsync(string fullTypeName);

    // Silo Tracking
    Task ReportPackageLoadedAsync(SiloAddress silo, string packageId, string version);
    Task ReportPackageUnloadedAsync(SiloAddress silo, string packageId, string version);
    Task<ImmutableList<SiloAddress>> GetHostingSilosAsync(string grainTypeName);
}
```

### Persistence

**Storage**: Uses Scynapse default grain storage (`[StorageProvider(ProviderName = ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME)]`)

**State Structure**:
```csharp
[GenerateSerializer]
public class GrainTypeDirectoryState
{
    [Id(0)] public Dictionary<string, GrainPackage> Packages { get; set; } = new();
    [Id(1)] public Dictionary<string, HashSet<SiloAddress>> PackageSilos { get; set; } = new();
}
```

**Key**: Singleton grain with key `"gtd"`

### What GTD Does NOT Do (Currently)

- ❌ Does not store actual assembly bytes (that's the Package Store's job)
- ❌ Does not perform interface compatibility checking
- ❌ Does not handle package dependencies
- ❌ Does not provide package download capability (uses Package Store)

### Relationship with Package Cache

The GTD and Package Cache are **separate systems**:

| System | Purpose | Storage |
|--------|---------|---------|
| **GTD** | Cluster-wide registry of grain types and their metadata | Scynapse Grain Storage |
| **Package Cache** | Local cache of downloaded package binaries | Local file system |

The GTD **does not use** the Package Cache. They serve different purposes:
- GTD: "What grain types exist and where can I find them?"
- Cache: "I have these package binaries stored locally to avoid re-downloading"

---

## Package Cache System

### Current Implementation: FileSystemPackageCache

**Location**: `Scynapse.Core/DynamicGrains/FileSystemPackageCache.cs`

```csharp
public interface IGrainPackageCache
{
    Task<LoadedGrainPackage?> GetAsync(string packageId, string? version = null);
    Task PutAsync(LoadedGrainPackage package);
    Task<bool> RemoveAsync(string packageId, string? version = null);
    Task ClearAsync();
    GrainPackageCacheStatistics GetStatistics();
}
```

### Storage Details

| Property | Default Value | Configuration |
|----------|---------------|---------------|
| Cache Directory | `{TempPath}/orleans-package-cache` | `GrainPackageCacheOptions.CacheDirectory` |
| Max Packages | 100 | `GrainPackageCacheOptions.MaxPackageCount` |
| Max Size | 500 MB | `GrainPackageCacheOptions.MaxTotalSizeBytes` |
| Expiration | 24 hours | `GrainPackageCacheOptions.ExpirationTime` |
| Eviction Policy | LRU | `GrainPackageCacheOptions.EvictionPolicy` |

### Cache Entry Structure

```
{cache-dir}/
├── {packageId}/
│   └── {version}/
│       ├── package.json          # Serialized GrainPackage metadata
│       ├── MyGrains.dll          # Assembly file
│       ├── MyGrains.Contracts.dll
│       └── ...
```

### Does the Cache Store GrainPackages?

**Yes**, but as `LoadedGrainPackage` which includes:
- `GrainPackage` metadata
- `Dictionary<string, byte[]>` assembly binaries

---

## Dynamic Grain Client

### Purpose

Enables **both clients AND silos** to access grains dynamically without compile-time references.

### Current Implementation

```csharp
// Interface: Scynapse.Core/DynamicGrains/IDynamicGrainClient.cs
public interface IDynamicGrainClient
{
    // Package Management
    Task<GrainPackageHandle> LoadPackageAsync(string packageId, string? version = null);
    Task UnloadPackageAsync(GrainPackageHandle handle);
    Task<IReadOnlyList<GrainPackageInfo>> ListAvailablePackagesAsync();

    // Grain Access
    Task<dynamic> GetGrainDynamicAsync(string grainTypeName, string primaryKey);
    dynamic GetGrain(GrainTypeMeta grainType, string primaryKey);

    // Reflection-style invocation
    Task<object?> InvokeMethodAsync(string grainTypeName, string primaryKey,
        string methodName, object?[]? args = null);

    // GTD Queries
    Task<IReadOnlyList<GrainTypeMeta>> QueryGrainTypesAsync(
        string? namespaceFilter = null, string? namePattern = null);
    Task<GrainTypeMeta?> GetGrainTypeMetaAsync(string grainTypeName);
}
```

### Data Flow

```
IDynamicGrainClient.LoadPackageAsync("MyGrains", "1.0.0")
    │
    ├── Check _loadedPackages (in-memory cache)
    │
    ├── Try IGrainPackageCache.GetAsync() (local disk cache)
    │
    └── Fall back to IGrainPackageStore.GetPackageAsync()
            │
            ├── FileSystemPackageSource (Priority: 100)
            │
            └── GrainStoragePackageSource (Priority: 200)
```

### DynamicGrainReference (DLR Support)

Wraps a grain reference to enable C# `dynamic` keyword usage:

```csharp
// Usage
dynamic grain = await client.GetGrainDynamicAsync("MyApp.IHelloGrain", "key-1");
string result = await grain.SayHello("World");  // DLR dispatches this

// Implementation: Scynapse.Core/DynamicGrains/DynamicGrainReference.cs
public class DynamicGrainReference : DynamicObject
{
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        // Find method via reflection, invoke on underlying grain reference
    }
}
```

---

## Versioning & Compatibility

### Current Versioning Support

**Package Level**:
```csharp
public sealed class GrainPackage
{
    public string PackageId { get; }      // e.g., "MyApp.Grains"
    public string Version { get; }        // e.g., "1.0.0" (SemVer recommended)
    public string ContentHash { get; }    // SHA-256 of package content
}
```

**Assembly Level**:
```csharp
public sealed class GrainPackageAssembly
{
    public string FileName { get; }       // e.g., "MyGrains.dll"
    public string Version { get; }        // Assembly version
    public string Hash { get; }           // File hash for integrity
    public GrainAssemblyRole Role { get; } // Interfaces|Implementation|Codegen|Dependency
}
```

**Type Level**:
```csharp
public sealed class GrainTypeMeta
{
    public string Version { get; }        // Type-specific version
    public string AssemblyHash { get; }   // Hash of containing assembly
}
```

### What's NOT Implemented

1. **Interface Compatibility Hashing**: No automatic detection of compatible interface versions
2. **Semantic Version Enforcement**: Version strings exist but no SemVer logic
3. **Version-Aware Routing**: No automatic routing to compatible versions
4. **Dependency Versioning**: Inter-package dependencies not resolved

### Recommended Future Approach

```csharp
// Interface hash for compatibility checking
public class InterfaceCompatibilityHash
{
    // Hash only the parts that affect wire compatibility:
    // - Method names
    // - Parameter types (by name, not by assembly)
    // - Return types
    // - Method IDs

    // Exclude:
    // - Attributes (unless they affect serialization)
    // - XML docs
    // - Parameter names
}
```

---

## Integration Points

### How Systems Connect

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                           Integration Flow                                    │
└──────────────────────────────────────────────────────────────────────────────┘

1. PACKAGE LOADING (Silo-side)
   IPluginGrainLoader.LoadGrainAssemblyAsync()
        │
        ├── PluginAssemblyLoader: Load DLL into isolated ALC
        │
        ├── PluginSerializationManager: Register serializers
        │
        ├── SiloManifestProvider.UpdateManifest(): Add to local manifest
        │
        ├── ClusterManifestProvider: Propagate to cluster
        │
        └── IGrainTypeDirectoryGrain.RegisterPackageAsync(): Register in GTD
                                     .ReportPackageLoadedAsync(): Track silo

2. PACKAGE DISCOVERY (Client-side)
   IDynamicGrainClient.LoadPackageAsync()
        │
        ├── Check in-memory _loadedPackages
        │
        ├── IGrainPackageCache: Check local disk cache
        │
        └── IGrainPackageStore: Download from sources
                │
                ├── FileSystemPackageSource
                └── GrainStoragePackageSource

3. DYNAMIC GRAIN ACCESS
   IDynamicGrainClient.GetGrainDynamicAsync("MyApp.IHelloGrain", "key")
        │
        ├── Resolve type from loaded GrainPackageHandle
        │
        ├── IGrainFactory.GetGrain(resolvedType, key)
        │
        └── Wrap in DynamicGrainReference for DLR support

4. GTD QUERIES
   IGrainTypeDirectoryGrain.FindGrainTypesAsync("MyApp.*", "*Grain")
        │
        └── Returns ImmutableList<GrainTypeMeta> with:
            - Grain types matching pattern
            - Interface metadata with methods
            - Hosting silo information
            - Package reference
```

---

## What's Missing / Incomplete

### 1. Package Dependencies

**Current State**: Each package is independent
**Needed**:
- Dependency declaration in `GrainPackage.Dependencies`
- Transitive dependency resolution
- Version conflict resolution

### 2. Interface Compatibility Detection

**Current State**: Version strings only, no automatic compatibility checking
**Needed**:
- Hash-based interface compatibility (hash method signatures, not implementation)
- Automatic detection of breaking vs non-breaking changes
- Upgrade path tracking

### 3. Smart Package Distribution

**Current State**: Manual loading, basic storage sources
**Needed**:
- Automatic download when grain is first accessed
- Preferred silo selection based on package availability
- Background pre-loading

### 4. Package Security

**Current State**: No verification
**Needed**:
- Package signing
- Trust chain verification
- Sandboxing for untrusted packages

### 5. GTD ↔ Cache Integration

**Current State**: Separate, unconnected systems
**Potential Integration**:
- GTD could provide "where to download" information
- Cache could register with GTD on cache hits
- Unified query interface

---

## Future Vision: Distributed Package System

### The Case for Building on NuGet

The user's question about leveraging NuGet is excellent. Here's an analysis:

#### Advantages of NuGet-Based System

| Advantage | Description |
|-----------|-------------|
| **Mature tooling** | `dotnet`, Visual Studio, CLI all work |
| **Versioning** | SemVer baked in, version ranges, floating versions |
| **Dependencies** | Transitive resolution is solved |
| **Caching** | Global and local package caches exist |
| **Signing** | Package signing infrastructure exists |
| **Feeds** | Private/public feed concept well understood |
| **IDE Integration** | PackageReference, restore, binding redirects |
| **Metadata** | .nuspec format is extensible |

#### Challenges to Research

| Challenge | Notes |
|-----------|-------|
| **Custom package types** | Can mark packages as "grain packages" |
| **Distributed feed** | Would need Scynapse-backed NuGet feed or gateway |
| **Runtime resolution** | NuGet is build-time focused, need runtime equivalent |
| **Hot updates** | NuGet assumes rebuild, we want hot-swap |
| **Cluster awareness** | Which silos have which packages? |

#### Proposed Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    Scynapse Distributed Package System                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      Package Directory Service                       │   │
│  │  (Could be based on NuGet V3 protocol / BaGet / custom)              │   │
│  │                                                                       │   │
│  │  - Metadata queries (versions, dependencies)                          │   │
│  │  - Package search                                                     │   │
│  │  - Version resolution                                                 │   │
│  │  - Scynapse storage backend                                            │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                              │                                              │
│                              ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Grain Type Directory (GTD)                        │   │
│  │                                                                       │   │
│  │  - Package ↔ Grain Type mapping                                       │   │
│  │  - Silo ↔ Package availability                                        │   │
│  │  - Interface metadata for dynamic access                              │   │
│  │  - Version compatibility tracking                                     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                              │                                              │
│                              ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      Package Cache (per-node)                        │   │
│  │                                                                       │   │
│  │  - Local NuGet cache integration (~/.nuget/packages)                 │   │
│  │  - LRU eviction for dynamic packages                                  │   │
│  │  - Integrity verification                                             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                              │                                              │
│                              ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Assembly Loader (MDCP)                            │   │
│  │                                                                       │   │
│  │  - Load from cache                                                    │   │
│  │  - Isolated AssemblyLoadContext                                       │   │
│  │  - Unloadable                                                         │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Research Topics

1. **NuGet Protocol Study**
   - V3 API for metadata/search/download
   - Can we implement Scynapse-backed NuGet feed?
   - [NuGet Server API](https://docs.microsoft.com/en-us/nuget/api/overview)

2. **BaGet Analysis**
   - Open-source NuGet server
   - Could be adapted for Scynapse storage backend
   - [BaGet GitHub](https://github.com/loic-sharma/BaGet)

3. **NuGet Client Libraries**
   - `NuGet.Protocol` for programmatic access
   - `NuGet.Packaging` for reading .nupkg files
   - Version resolution logic we could reuse

4. **Custom Package Types**
   - `.nupkg` is just a ZIP with conventions
   - Could add Scynapse-specific metadata
   - Mark as `<PackageType>ScynapseGrainPackage</PackageType>`

---

## Development Guidelines

### Important: Project References vs NuGet Packages

When developing Scynapse itself (using project references to `src/Scynapse.*` instead of NuGet packages), **host/application projects must include `<ScynapseBuildTimeCodeGen>true</ScynapseBuildTimeCodeGen>`** in their .csproj file.

#### Why This Is Required

Scynapse discovers grain implementations at runtime using `ReferencedAssemblyProvider`. This provider filters assemblies based on their dependency chain to `Scynapse.Serialization`:

```csharp
// From ReferencedAssemblyProvider.cs - only processes assemblies with direct Scynapse.Serialization dependency
if (!lib.Name.Contains("Scynapse.Serialization") &&
    !lib.Dependencies.Any(dep => dep.Name.Contains("Scynapse.Serialization")))
{
    continue;  // Skip assembly!
}
```

**The issue**: `Scynapse.Persistence.Memory` depends on `Scynapse.Serialization` *transitively* (via `Scynapse.Runtime` → `Scynapse.Core`), not directly. When using project references, this can cause the `MemoryStorageGrain` (and other framework grains) to not be discovered.

#### The Solution

Adding `ScynapseBuildTimeCodeGen=true` to your application project causes the Scynapse source generator to:
1. Run at **compile-time** on your application
2. Scan **all** transitively referenced assemblies (including `Scynapse.Persistence.Memory`)
3. Generate a **comprehensive** `TypeManifestProvider` that includes ALL grain types
4. No runtime discovery needed - everything is baked into your generated code

#### NuGet Package Users

When using Scynapse via NuGet packages (the normal case), this is handled automatically:
- The `Genesa.Scynapse.Sdk` package sets up code generation
- Each Scynapse package has pre-compiled generated code baked in
- Assembly discovery works because packages have proper dependency metadata

---

## State Property Access

### What It Is

A code-generation feature that enables **property-like syntax** for accessing grain state remotely, replacing verbose `GetX()`/`SetX()` boilerplate with natural property access:

```csharp
// Property-style syntax (new)
string name = await player.Name;           // Remote get via awaiting
await (player.Name << "Louis");            // Remote set via << operator

// Traditional Orleans style (still works)
string name = await player.GetName();
await player.SetName("Louis");
```

### How It Works

The system is **property-driven**: developers define properties on grain classes, and the Scynapse code generator automatically produces interface methods, implementation wiring, and proxy enhancements.

**Developer writes:**

```csharp
// Interface (partial, only custom methods)
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerSnapshot> GetSnapshotAsync();
    Task ApplyDamageAsync(int amount);
}

// Implementation (with properties)
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }    // Codegen implements this
    public partial int Score { get; set; }      // Codegen implements this

    public Task<PlayerSnapshot> GetSnapshotAsync() => ...;
    public Task ApplyDamageAsync(int amount) => ...;
}
```

**Code generator produces:**

1. **Interface extension** — `GetName()`/`SetName()`, `GetScore()`/`SetScore()` method signatures
2. **Class extension** — backing fields (`_name_backing`), partial property implementations, and explicit interface method implementations
3. **Proxy StateTask properties** — `StateTask<string> Name` wrappers that bridge property syntax to RPC calls

### Core Types

#### StateTask\<T\>

**Location**: `src/Scynapse/src/Orleans.Core.Abstractions/State/StateTask.cs`

A readonly struct that enables both `await` (get) and `<<` (set) on grain properties:

```csharp
public readonly struct StateTask<T>
{
    private readonly Func<ValueTask<T>> _getter;
    private readonly Func<T, ValueTask> _setter;

    public ValueTask<T> GetAsync();
    public ValueTask SetAsync(T value);
    public ValueTaskAwaiter<T> GetAwaiter();                     // enables: await grain.Name
    public static ValueTask operator <<(StateTask<T> state, T value);  // enables: await (grain.Name << "x")
}
```

**Why `<<`?** C# property setters must return `void`, making them incompatible with async. The `<<` operator visually suggests "pushing" a value and can return `ValueTask`.

#### StateAttribute / NotStateAttribute

**Location**: `src/Scynapse/src/Orleans.Core.Abstractions/State/`

| Attribute | Purpose |
|-----------|---------|
| `[State]` | Configures code generation: `Persisted`, `StateProperty`, `AutoSave`, `CanSet`, `MethodName` |
| `[NotState]` | Excludes a public property from state code generation (for loggers, DI dependencies, etc.) |

### Property Detection Rules

The `StatePropertyCodeGenerator` scans grain classes and includes a property if:
- It is `public`
- It does NOT have `[NotState]`
- It is NOT an indexer
- The grain class implements a grain interface (inherits from `IGrainWithXXXKey`)

### Persistence Integration

Properties can map directly to Orleans `IPersistentState<T>` fields:

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;

    [State(Persisted = true, StateProperty = nameof(_state))]
    public partial int Score { get; set; }
    // Generated: get => _state.State.Score; set => _state.State.Score = value;

    [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    public partial int Level { get; set; }
    // Generated: get => _state.State.Level; set { _state.State.Level = value; _ = _state.WriteStateAsync(); }
}
```

### Generated Code Structure

For a grain class, the generator outputs a single partial class extension:

```csharp
partial class PlayerGrain
{
    // 1. Backing fields (for partial properties without persistence)
    private string _name_backing = default!;

    // 2. Partial property implementations
    public partial string Name { get => _name_backing; set => _name_backing = value; }

    // 3. Explicit interface method implementations
    Task<string> IPlayerGrain.GetName() => Task.FromResult(Name);
    Task IPlayerGrain.SetName(string value) { Name = value; return Task.CompletedTask; }
}
```

And on the proxy side:

```csharp
internal sealed class Proxy_IPlayerGrain : GrainReference, IPlayerGrain
{
    public StateTask<string> Name => new StateTask<string>(
        () => new ValueTask<string>(GetName()),
        v => new ValueTask(SetName(v)));
}
```

### Implementation Status

| Phase | Status | Key Components |
|-------|--------|----------------|
| Phase 1: Core Infrastructure | ✅ Complete | `StateTask<T>`, `StateAttribute`, `NotStateAttribute`, `LibraryTypes` |
| Phase 2: Code Generation | ✅ Complete | `StatePropertyCodeGenerator`, `CodeGenerator` integration, `ProxyGenerator` integration |
| Phase 3: Partial Properties | ✅ Complete | Backing field generation, partial property implementations |
| Phase 4: Persistence | ✅ Complete | `IPersistentState<T>` detection, persisted property generation, AutoSave |

**Detailed design document**: `Docs/Scynapse/Scynapse Features/StatePropertyAccess.md`

---

## Test Scenarios

Located in `playground/PluginGrainScenarios/`:

### Scenario 1: Single Silo Basic Load/Unload
- Starts single silo
- Loads grain assembly dynamically
- Invokes grain methods
- Unloads assembly
- **Status**: ✅ Working

### Scenario 2: MDCP Isolation Verification
- Verifies assembly loads in isolated `AssemblyLoadContext`
- Confirms `IsCollectible = true`
- Validates shared types are properly resolved
- **Status**: ✅ Working

### Scenario 3: Multi-Silo Manifest Propagation
- Starts 3-silo cluster
- Loads grains on Silo1 only
- Verifies manifest propagates to Silo2 and Silo3
- **Status**: ✅ Working

### Scenario 4: Assembly Unload & Memory Reclaim
- Loads assembly, uses grains
- Unloads assembly
- Forces GC and measures memory reclamation
- **Status**: ✅ Working (~51% memory recovered)

### Scenario 5: Split Grain Assemblies
- Tests loading Contracts (interfaces) and Implementation (grain classes) as separate DLLs
- **Status**: ✅ Working

### Scenario 6: Grain Type Directory (GTD)
- Register packages with GTD
- Query grain types by pattern
- Track which silos have packages loaded
- **Status**: ✅ Working

### Scenario 7: Dynamic Grain Client
- Create GrainTypeMeta and GrainPackage programmatically
- Test GetGrainDynamic extension methods
- Verify DynamicGrainReference DLR support
- **Status**: ✅ Working

---

## Files Summary

### Core Abstractions (Scynapse.Core.Abstractions)
```
src/Scynapse.Core.Abstractions/
├── Manifest/
│   ├── GrainPackage.cs              # GrainPackage, GrainPackageInfo, GrainPackageAssembly
│   ├── GrainTypeMeta.cs             # GrainTypeMeta
│   └── GrainInterfaceMeta.cs        # GrainInterfaceMeta, GrainMethodMeta, GrainParameterMeta
└── DynamicGrains/
    ├── IGrainTypeDirectoryGrain.cs  # GTD interface
    └── IGrainPackageStore.cs        # Package store interface
```

### Client-Side (Scynapse.Core)
```
src/Scynapse.Core/DynamicGrains/
├── IDynamicGrainClient.cs           # Dynamic client interface
├── GrainPackageHandle.cs            # Loaded package handle
├── DynamicGrainReference.cs         # DLR wrapper
├── GrainFactoryExtensions.cs        # GetGrainDynamic() extensions
├── IGrainPackageCache.cs            # Cache interface
└── FileSystemPackageCache.cs        # Disk cache implementation
```

### Server-Side (Scynapse.Runtime)
```
src/Scynapse.Runtime/DynamicGrains/
├── GrainTypeDirectoryGrain.cs       # GTD implementation
├── DynamicGrainClient.cs            # Dynamic client implementation
├── GrainPackageStore.cs             # Multi-source package store
├── FileSystemPackageSource.cs       # File-based source
├── GrainStoragePackageSource.cs     # Scynapse storage source
├── PluginGrainLoaderService.cs      # Assembly loading
├── PluginGrainUnloaderService.cs    # Assembly unloading
├── PluginAssemblyLoader.cs          # MDCP wrapper
├── PluginSerializationManager.cs    # Serializer registration
└── GrainLifecycleManager.cs         # Activation tracking
```

### State Property Access (Orleans.Core.Abstractions + Orleans.CodeGenerator)
```
src/Scynapse/src/Orleans.Core.Abstractions/State/
├── StateTask.cs                    # StateTask<T> awaitable struct with << operator
├── StateAttribute.cs               # [State] configuration attribute
└── NotStateAttribute.cs            # [NotState] exclusion attribute

src/Scynapse/src/Orleans.CodeGenerator/
├── StatePropertyCodeGenerator.cs   # Property scanning, interface/class/proxy generation
├── CodeGenerator.cs                # Integration point (state property processing)
├── ProxyGenerator.cs               # StateTask property generation on proxies
└── LibraryTypes.cs                 # Type references (StateTask_1, StateAttribute, etc.)
```

### Playground Projects
```
playground/
├── PluginGrainScenarios/            # Test scenarios
├── DynamicGrainLoading.Contracts/   # Split assemblies: interfaces
├── DynamicGrainLoading.Implementation/ # Split assemblies: implementations
├── DynamicGrainLoading.SingleSilo/  # Single silo test host
├── DynamicGrainLoading.MultiSilo/   # Multi-silo test host
└── DynamicGrainLoading.TestGrains/  # Test grain implementations
```

---

## References

- [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) - Plugin loading library
- [AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) - .NET assembly isolation
- [NuGet Server API](https://docs.microsoft.com/en-us/nuget/api/overview) - For distributed package system research
- [BaGet](https://github.com/loic-sharma/BaGet) - Open-source NuGet server to study
- Scynapse Manifest System - `src/Scynapse.Runtime/Manifest/`
