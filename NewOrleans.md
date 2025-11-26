# NewOrleans: Dynamic Grain Loading System

## Overview

This document describes the dynamic grain loading system implemented in Orleans, enabling runtime loading and unloading of grain assemblies without application restart. The implementation uses McMaster.NETCore.Plugins (MDCP) for assembly isolation and unloadability.

## Current Implementation Status

### Phase 1: MDCP-Based Plugin Grain Loading (Complete)

The core plugin grain loading infrastructure is now functional:

#### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `PluginAssemblyLoader` | `src/Orleans.Runtime/DynamicGrains/` | Loads grain assemblies using MDCP with `IsUnloadable=true`, `PreferSharedTypes=true` |
| `PluginGrainLoaderService` | `src/Orleans.Runtime/DynamicGrains/` | Orchestrates loading: manifest updates, serialization, cache invalidation, cluster propagation |
| `PluginGrainUnloaderService` | `src/Orleans.Runtime/DynamicGrains/` | Orchestrates unloading: grain deactivation, manifest removal, memory reclamation |
| `PluginSerializationManager` | `src/Orleans.Runtime/DynamicGrains/` | Registers serializers/copiers for dynamically loaded types |
| `GrainLifecycleManager` | `src/Orleans.Runtime/DynamicGrains/` | Manages grain activation lifecycle for type-based deactivation |
| `AssemblyValidator` | `src/Orleans.Runtime/DynamicGrains/` | Validates grain assemblies before loading |

#### Public Interfaces

```csharp
// Load grain assemblies at runtime
public interface IPluginGrainLoader
{
    Task<GrainLoadResult> LoadGrainAssemblyAsync(string assemblyPath, CancellationToken ct = default);
    IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents { get; }
}

// Unload grain assemblies at runtime
public interface IPluginGrainUnloader
{
    Task<GrainUnloadResult> UnloadGrainAssemblyAsync(string assemblyPath, TimeSpan? timeout = null, CancellationToken ct = default);
    IAsyncEnumerable<GrainAssemblyUnloadedEvent> UnloadEvents { get; }
}
```

#### DI Registration

Services are registered by default in `DefaultSiloServices.cs`:

```csharp
// Plugin grain loading (enabled by default)
services.TryAddSingleton<PluginAssemblyLoader>();
services.TryAddSingleton<PluginSerializationManager>();
services.TryAddSingleton<PluginGrainLoaderService>();
services.TryAddSingleton<IPluginGrainLoader>(...);

// Plugin grain unloading support
services.TryAddSingleton<GrainLifecycleManager>();
services.TryAddSingleton<IGrainLifecycleManager>(...);
services.TryAddSingleton<PluginGrainUnloaderService>();
services.TryAddSingleton<IPluginGrainUnloader>(...);
```

### Phase 2: Manifest Propagation (Complete)

When grains are loaded/unloaded, manifest changes propagate across the cluster:

#### Key Changes

1. **Fixed stale manifest snapshot** (`ClusterManifestSystemTarget.cs`):
   - `GetSiloManifest()` now returns `_clusterManifestProvider.LocalGrainManifest` instead of a cached snapshot
   - Ensures remote silos get the current manifest when they query

2. **Added manifest change notification** (`ISiloManifestSystemTarget.cs`):
   ```csharp
   ValueTask NotifyManifestChanged();
   ```

3. **Force refresh mechanism** (`ClusterManifestProvider.cs`):
   ```csharp
   internal async Task ForceRefreshAllManifestsAsync()
   ```

4. **Automatic notification** (`PluginGrainLoaderService.cs`):
   - After loading grains, notifies all other silos to refresh their cluster manifest
   - Uses `NotifyOtherSilosOfManifestChangeAsync()` to call each silo's system target

### Phase 3: Terminology Standardization (Complete)

Renamed "Dynamic" to "Plugin" throughout:
- `DynamicGrainLoader` → `PluginGrainLoader`
- `IDynamicGrainLoader` → `IPluginGrainLoader`
- All related classes follow this pattern

---

## Test Scenarios

Located in `playground/PluginGrainScenarios/`:

### Scenario 1: Single Silo Basic Load/Unload
- Starts single silo
- Loads grain assembly dynamically
- Invokes grain methods
- Unloads assembly
- **Status**: Working

### Scenario 2: MDCP Isolation Verification
- Verifies assembly loads in isolated `AssemblyLoadContext`
- Confirms `IsCollectible = true`
- Validates shared types are properly resolved
- **Status**: Working

### Scenario 3: Multi-Silo Manifest Propagation
- Starts 3-silo cluster
- Loads grains on Silo1 only
- Verifies manifest propagates to Silo2 and Silo3
- **Status**: Working (minor timing variance in versions)

### Scenario 4: Assembly Unload & Memory Reclaim
- Loads assembly, uses grains
- Unloads assembly
- Forces GC and measures memory reclamation
- **Status**: Working (~51% memory recovered)

### Scenario 5: Split Grain Assemblies
- Tests loading Contracts (interfaces) and Implementation (grain classes) as separate DLLs
- Analyzes interface vs implementation separation
- Demonstrates Orleans codegen for both assemblies
- Invokes grains from split assemblies
- **Status**: Working with real split assemblies

#### Split Assembly Projects
Located in `playground/`:
- `DynamicGrainLoading.Contracts/` - Contains grain interfaces (IHelloGrain, ICounterGrain, IEchoGrain) and ComplexData
- `DynamicGrainLoading.Implementation/` - Contains grain classes (HelloGrain, CounterGrain, EchoGrain)

Both projects require `<OrleansBuildTimeCodeGen>true</OrleansBuildTimeCodeGen>` in their .csproj files:
- **Contracts**: Generates proxy/stub classes (GrainReference implementations) for clients
- **Implementation**: Generates grain activators and method invokers for silos

### Scenario 6: Grain Type Directory (GTD)
- Cluster-wide registry of all available grain types with metadata
- Query grain types without compile-time references
- Track which silos can host which grain types
- Expose method/property metadata for reflection-like access
- **Status**: NOT YET IMPLEMENTED - Placeholder scenario with planned API design

### Scenario 7: Dynamic Grain Client Loading
- Download interface/proxy DLLs from cluster on demand
- Load into isolated AssemblyLoadContext on client side
- Create grain references without static typing
- Support both strong-typed and fully dynamic access patterns
- **Status**: NOT YET IMPLEMENTED - Placeholder scenario with planned API design

---

## What's Left to Implement

The following features were planned in the original design but are not yet implemented:

### 1. Grain Type Directory (GTD)

A cluster-wide registry of all available grain types with metadata.

#### Purpose
- Central catalog of all grain types available in the cluster
- Tracks which silos can host which grain types
- Provides reflection-like metadata for grain interfaces
- Enables discovery of grain types without compile-time references

#### Planned Components
```
src/Orleans.Runtime/DynamicGrains/
├── GrainTypeDirectory.cs           # Core directory implementation
├── GrainTypeRegistryGrain.cs       # Singleton grain storing registry
├── IGrainTypeRegistryGrain.cs      # Interface for registry grain
├── GrainTypeMetadataGrain.cs       # Per-type metadata storage
├── IGrainTypeMetadataGrain.cs      # Interface for metadata grain
├── GrainTypeMetadataProvider.cs    # Service to query metadata
└── IGrainTypeDirectory.cs          # Public interface
```

#### Key Data Structures
```csharp
public class GrainTypeRegistration
{
    public string FullName { get; set; }
    public string Namespace { get; set; }
    public string AssemblyName { get; set; }
    public string AssemblyHash { get; set; }  // For versioning
    public GrainTypeKind Kind { get; set; }   // Interface or Class
    public bool IsGenericType { get; set; }
    public List<string> GenericTypeParameters { get; set; }
    public List<string> BaseTypes { get; set; }
    public List<string> Methods { get; set; }
    public List<string> Attributes { get; set; }
}

public class GrainInterfaceMetadata
{
    public string FullName { get; set; }
    public string AssemblyHash { get; set; }
    public List<MethodMetadata> Methods { get; set; }
    public List<PropertyMetadata> Properties { get; set; }
    // Enables reflection-like access without actual Type
}
```

### 2. Dynamic Grain Client Loading

Enable clients (and silos acting as clients) to access grains without compile-time references.

#### Features
- Download interface/proxy DLL from GTD on demand
- Load into isolated AssemblyLoadContext
- Create grain references without static typing
- Support both split (interface-only) and whole (full assembly) downloads

#### Planned API
```csharp
public interface IDynamicGrainClient
{
    // Get grain without compile-time type reference
    Task<dynamic> GetGrainDynamicAsync(string grainTypeName, string grainKey);

    // Load grain type client (interface + proxy)
    Task<GrainTypeClientHandle> LoadGrainTypeClientAsync(string grainTypeName);

    // Unload grain type client
    Task UnloadGrainTypeClientAsync(GrainTypeClientHandle handle);

    // Query available grain types
    Task<IReadOnlyList<GrainTypeInfo>> GetAvailableGrainTypesAsync();
}

public class GrainTypeClientHandle : IAsyncDisposable
{
    public string GrainTypeName { get; }
    public Type InterfaceType { get; }
    public Type ProxyType { get; }

    // Create strongly-typed reference (via reflection)
    public TGrainInterface GetGrain<TGrainInterface>(string key);

    // Create dynamic reference
    public dynamic GetGrainDynamic(string key);
}
```

### 3. Assembly Distribution System

Mechanism for distributing grain assemblies across the cluster.

#### Components Already Started (in other branch)
```csharp
// Storage grain for assembly bytes
public interface IAssemblyStorageGrain : IGrainWithStringKey
{
    Task<AssemblyMetadata> GetMetadataAsync();
    Task<bool> UploadChunkAsync(int chunkIndex, byte[] chunk);
    Task<bool> CompleteUploadAsync(AssemblyMetadata metadata);
    Task<byte[]> DownloadChunkAsync(int chunkIndex);
    Task RegisterDownloadAsync(SiloAddress siloAddress);
}
```

#### Planned Package System

**Option A: ZIP-based packages**
```
MyGrains.grainpkg (ZIP file)
├── manifest.json           # Metadata, dependencies, version
├── interfaces/
│   └── MyGrains.Contracts.dll
├── implementations/
│   └── MyGrains.dll
└── codegen/
    └── MyGrains.Orleans.dll
```

**Option B: NuGet-based system**
- Leverage existing NuGet infrastructure
- Use custom package type marker
- Store in private NuGet feed or cluster storage

### 4. Grain Type Cache System

Caching layer for grain type metadata and assemblies.

#### Purpose
- Reduce network traffic for repeated type lookups
- Cache downloaded assemblies locally
- Share cache between silos and clients in same process

#### Planned Implementation
```csharp
public interface IGrainTypeCache
{
    // Metadata cache
    Task<GrainTypeRegistration?> GetTypeRegistrationAsync(string typeName);
    Task CacheTypeRegistrationAsync(GrainTypeRegistration registration);

    // Assembly cache
    Task<byte[]?> GetAssemblyBytesAsync(string assemblyHash);
    Task CacheAssemblyBytesAsync(string assemblyHash, byte[] bytes);

    // Loaded type cache
    Type? GetLoadedType(string typeName, string assemblyHash);
    void CacheLoadedType(string typeName, string assemblyHash, Type type);

    // Cache management
    Task InvalidateAsync(string typeName);
    Task ClearAsync();
}
```

### 5. Versioning Support

Track and manage multiple versions of grain types.

#### Features
- Version identification via assembly hash
- Compatible version resolution
- Rolling upgrades without downtime
- Version-specific routing

#### Planned Data Model
```csharp
public class GrainTypeVersion
{
    public string TypeName { get; set; }
    public string AssemblyHash { get; set; }
    public Version AssemblyVersion { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public IReadOnlyList<SiloAddress> AvailableOn { get; set; }
    public bool IsDeprecated { get; set; }
    public string CompatibleWithHash { get; set; }  // For upgrade paths
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Orleans Cluster                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐               │
│  │   Silo 1    │     │   Silo 2    │     │   Silo 3    │               │
│  │             │     │             │     │             │               │
│  │ ┌─────────┐ │     │ ┌─────────┐ │     │ ┌─────────┐ │               │
│  │ │ Plugin  │ │     │ │ Plugin  │ │     │ │ Plugin  │ │               │
│  │ │ Loader  │ │     │ │ Loader  │ │     │ │ Loader  │ │               │
│  │ └────┬────┘ │     │ └────┬────┘ │     │ └────┬────┘ │               │
│  │      │      │     │      │      │     │      │      │               │
│  │ ┌────▼────┐ │     │ ┌────▼────┐ │     │ ┌────▼────┐ │               │
│  │ │Manifest │◄┼─────┼─┤Manifest │◄┼─────┼─┤Manifest │ │               │
│  │ │Provider │ │     │ │Provider │ │     │ │Provider │ │               │
│  │ └─────────┘ │     │ └─────────┘ │     │ └─────────┘ │               │
│  │             │     │             │     │             │               │
│  │ ┌─────────┐ │     │ ┌─────────┐ │     │ ┌─────────┐ │               │
│  │ │  MDCP   │ │     │ │  MDCP   │ │     │ │  MDCP   │ │               │
│  │ │ ALC     │ │     │ │ ALC     │ │     │ │ ALC     │ │               │
│  │ │(Plugin1)│ │     │ │(Plugin1)│ │     │ │(Plugin1)│ │               │
│  │ └─────────┘ │     │ └─────────┘ │     │ └─────────┘ │               │
│  └─────────────┘     └─────────────┘     └─────────────┘               │
│                                                                         │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │              Grain Type Directory (Future)                │          │
│  │  ┌─────────────────┐  ┌─────────────────────────────┐    │          │
│  │  │ Type Registry   │  │ Assembly Storage            │    │          │
│  │  │ - Type metadata │  │ - Interface DLLs            │    │          │
│  │  │ - Version info  │  │ - Implementation DLLs       │    │          │
│  │  │ - Silo mapping  │  │ - Codegen DLLs              │    │          │
│  │  └─────────────────┘  └─────────────────────────────┘    │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ (Future: Dynamic Client Loading)
                                    ▼
                    ┌───────────────────────────────┐
                    │         Orleans Client         │
                    │                               │
                    │  ┌─────────────────────────┐  │
                    │  │ Dynamic Grain Client    │  │
                    │  │ - Load types on demand  │  │
                    │  │ - GetGrainDynamic()     │  │
                    │  │ - Type cache            │  │
                    │  └─────────────────────────┘  │
                    └───────────────────────────────┘
```

---

## Usage Example (Current Implementation)

```csharp
// In silo code
var loader = serviceProvider.GetRequiredService<IPluginGrainLoader>();
var unloader = serviceProvider.GetRequiredService<IPluginGrainUnloader>();

// Load grain assembly at runtime
var loadResult = await loader.LoadGrainAssemblyAsync("/plugins/MyGrains.dll");
if (loadResult.Success)
{
    Console.WriteLine($"Loaded {loadResult.GrainTypes.Count} grain types");

    // Grains are now available cluster-wide
    var grain = grainFactory.GetGrain<IMyGrain>("key");
    await grain.DoSomething();
}

// Later, unload the assembly
var unloadResult = await unloader.UnloadGrainAssemblyAsync("/plugins/MyGrains.dll");
if (unloadResult.Success)
{
    Console.WriteLine($"Unloaded, memory reclaimed: {unloadResult.MemoryReclaimed}");
}
```

---

## Files Modified/Added

### New Files
- `src/Orleans.Runtime/DynamicGrains/GrainLifecycleManager.cs`
- `playground/PluginGrainScenarios/` (entire project)
- `playground/DynamicGrainLoading.Contracts/` - Split assembly: grain interfaces
- `playground/DynamicGrainLoading.Implementation/` - Split assembly: grain implementations

### Modified Files
- `src/Orleans.Runtime/Hosting/DefaultSiloServices.cs` - Added unloader registration
- `src/Orleans.Runtime/GrainTypeManager/ClusterManifestSystemTarget.cs` - Fixed stale manifest, added notification
- `src/Orleans.Runtime/GrainTypeManager/ISiloManifestSystemTarget.cs` - Added `NotifyManifestChanged()`
- `src/Orleans.Runtime/Manifest/ClusterManifestProvider.cs` - Added `ForceRefreshAllManifestsAsync()`
- `src/Orleans.Runtime/DynamicGrains/PluginGrainLoaderService.cs` - Added cluster notification

---

## References

- [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) - Plugin loading library
- [AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) - .NET assembly isolation
- Orleans Manifest System - `src/Orleans.Runtime/Manifest/`
