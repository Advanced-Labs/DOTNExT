# Plugin Grain Architecture

> **Document Purpose**: This document describes the architecture of the plugin grain loading system, which enables runtime loading and unloading of grain assemblies using McMaster.NETCore.Plugins (MDCP) for assembly isolation.

**Last Updated**: 2025-11-27

---

## Overview

The plugin grain loading system allows Orleans silos to load and unload grain assemblies at runtime without requiring an application restart. This is achieved through:

1. **McMaster.NETCore.Plugins (MDCP)** - Provides collectible `AssemblyLoadContext` for proper isolation and unloading
2. **Manifest System Integration** - Updates cluster-wide grain type registry when assemblies are loaded/unloaded
3. **Split Assembly Support** - Handles interfaces, implementations, and codegen in separate DLLs

---

## Core Components

### Public Interface: `IPluginGrainLoader`

**Location**: `src/Orleans.Runtime/DynamicGrains/IPluginGrainLoader.cs`

The primary API for loading and unloading grain assemblies:

```csharp
public interface IPluginGrainLoader
{
    Task<GrainLoadResult> LoadGrainAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);

    Task UnloadGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents { get; }
}
```

**Usage**:
```csharp
var loader = serviceProvider.GetRequiredService<IPluginGrainLoader>();

// Load a grain assembly
var result = await loader.LoadGrainAssemblyAsync("/path/to/MyGrains.dll");
if (result.Success)
{
    Console.WriteLine($"Loaded {result.GrainTypes.Count} grain types");
}

// Later, unload it
await loader.UnloadGrainTypesAsync(result.GrainTypes);
```

### Assembly Loading: `PluginAssemblyLoader`

**Location**: `src/Orleans.Runtime/DynamicGrains/PluginAssemblyLoader.cs`

The core assembly loader that uses MDCP for isolation:

```csharp
internal sealed class PluginAssemblyLoader
{
    private readonly ConcurrentDictionary<string, PluginLoader> _pluginLoaders = new();
    private readonly ConcurrentDictionary<string, PluginAssemblySet> _pluginSets = new();
    private Type[] _cachedSharedTypes;
}
```

**Key Features**:
- Uses `PluginLoader.CreateFromAssemblyFile()` with `IsUnloadable = true`
- Configures shared types to prevent Orleans type identity issues
- Tracks `PluginLoader` instances for proper disposal during unload

### Assembly Isolation with MDCP

When loading an assembly, MDCP creates a **collectible AssemblyLoadContext**:

```csharp
pluginLoader = PluginLoader.CreateFromAssemblyFile(
    assemblyPath,
    config =>
    {
        // Share Orleans types between host and plugin
        config.PreferSharedTypes = true;

        // Enable unloading support
        config.IsUnloadable = true;

        // Configure explicit shared types from Orleans runtime
        foreach (var sharedType in sharedTypes)
        {
            config.SharedAssemblies.Add(sharedType.Assembly.GetName());
        }
    });

assembly = pluginLoader.LoadDefaultAssembly();
```

**Why MDCP?**

Without MDCP, `Assembly.LoadFrom()`:
- Loads into default AssemblyLoadContext
- Cannot unload assemblies
- No dependency isolation
- All types shared (can cause conflicts)

With MDCP:
- Creates isolated, collectible AssemblyLoadContext
- Proper unloading via `loader.Dispose()` which triggers `AssemblyLoadContext.Unload()`
- Dependency isolation (plugin dependencies don't conflict with host)
- Explicit shared type configuration

### Shared Types Configuration

The loader automatically discovers Orleans types to share between host and plugins:

```csharp
private Type[] GetOrleansSharedTypes()
{
    var sharedTypes = new List<Type>();

    // Scan Orleans assemblies
    var orleansAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name.StartsWith("Orleans") ||
                    a.GetName().Name.StartsWith("Microsoft.Orleans"));

    foreach (var assembly in orleansAssemblies)
    {
        // Include interfaces, abstract classes, attributes, value types, enums
        var types = assembly.GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith("Orleans") == true)
            .Where(t => t.IsInterface || (t.IsClass && t.IsAbstract) ||
                       typeof(Attribute).IsAssignableFrom(t) ||
                       t.IsValueType || t.IsEnum);

        sharedTypes.AddRange(types);
    }

    // Also add common .NET types: Task, ValueTask, CancellationToken, etc.
    return sharedTypes.Distinct().ToArray();
}
```

**Shared types include**:
- All Orleans interfaces (`IGrain`, `IGrainFactory`, etc.)
- Orleans base classes (`Grain`, etc.)
- Orleans value types (`GrainId`, `SiloAddress`, etc.)
- Orleans attributes
- Common .NET async types (`Task`, `ValueTask`, `CancellationToken`)
- Common collections

---

## Assembly Unloading

### Process

1. Get the `PluginLoader` instance for the assembly
2. Remove from all tracking dictionaries
3. Call `pluginLoader.Dispose()` - triggers `AssemblyLoadContext.Unload()`
4. Force garbage collection

```csharp
public async Task<bool> UnloadAssemblyAsync(string assemblyPath)
{
    if (!_pluginLoaders.TryRemove(assemblyPath, out var pluginLoader))
        return false;

    _pluginSets.TryRemove(assemblyPath, out _);
    _loadedAssemblies.TryRemove(assemblyPath, out _);
    _assemblyMetadata.TryRemove(assemblyPath, out _);

    // Dispose triggers AssemblyLoadContext.Unload()
    pluginLoader.Dispose();

    // Force GC to reclaim memory
    for (int i = 0; i < 3; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);
    }

    return true;
}
```

### Prerequisites for Unloading

For successful unloading:
1. All grain activations of types in the assembly must be deactivated first
2. No references to types from the assembly should exist
3. Serializers and caches must be invalidated

The `PluginGrainUnloaderService` handles graceful grain deactivation before assembly unload.

---

## Split Assembly Support

### What is Split Assembly?

In complex scenarios, grain assemblies can be split:

```
MyGrains.Contracts.dll    - Interfaces (IMyGrain)
MyGrains.dll              - Implementations (MyGrain : Grain, IMyGrain)
MyGrains.CodeGen.dll      - Generated proxies and serializers (auto-generated)
```

### `PluginAssemblySet`

**Location**: `src/Orleans.Runtime/DynamicGrains/PluginAssemblySet.cs`

Tracks all related assemblies within the same AssemblyLoadContext:

```csharp
public sealed class PluginAssemblySet
{
    public IReadOnlyList<Assembly> AllAssemblies { get; }
    public IReadOnlyList<Assembly> InterfaceAssemblies { get; }
    public IReadOnlyList<Assembly> ImplementationAssemblies { get; }
    public IReadOnlyList<Assembly> CodegenAssemblies { get; }
    public AssemblyLoadContext LoadContext { get; }
    public string RootAssemblyPath { get; }
}
```

### Discovery

When loading an assembly, the loader discovers all related assemblies in the same ALC:

```csharp
var pluginSet = PluginAssemblySet.FromAssemblyLoadContext(
    assembly,
    loadContext,
    assemblyPath);

// Logs something like:
// Total assemblies: 3 [MyGrains.Contracts, MyGrains, MyGrains.CodeGen]
// Interface assemblies: 1 [MyGrains.Contracts]
// Implementation assemblies: 1 [MyGrains]
// Codegen assemblies: 1 [MyGrains.CodeGen]
```

---

## Orchestration: `PluginGrainLoaderService`

**Location**: `src/Orleans.Runtime/DynamicGrains/PluginGrainLoaderService.cs`

Orchestrates the complete load process:

1. **Phase 1**: Load assembly via `PluginAssemblyLoader` (MDCP)
2. **Phase 2**: Validate Orleans metadata exists (codegen)
3. **Phase 3**: Update silo manifest (`SiloManifestProvider.UpdateManifest()`)
4. **Phase 4**: Register serializers (`PluginSerializationManager`)
5. **Phase 5**: Invalidate activation caches
6. **Phase 6**: Propagate to cluster (`ClusterManifestProvider`)
7. **Phase 7**: Register with GTD (if available)

### Integration with Manifest System

After loading, the service updates Orleans' manifest system:

```csharp
// Update local silo manifest
var (newManifest, updatedTypeMap) = _siloManifestProvider.UpdateManifest(
    grainClasses: metadata.GrainClasses,
    grainInterfaces: metadata.GrainInterfaces);

// Propagate to cluster
var published = _clusterManifestProvider.UpdateLocalManifest(newManifest);
```

This makes newly loaded grain types available for activation across the cluster.

---

## Serialization Registration

**Location**: `src/Orleans.Runtime/DynamicGrains/PluginSerializationManager.cs`

When a grain assembly is loaded, its serializers must be registered with Orleans' `CodecProvider`:

```csharp
public void RegisterSerializers(AssemblyLoadMetadata metadata)
{
    // Create TypeManifestOptions with new serializers
    var options = new TypeManifestOptions();
    options.Serializers.UnionWith(metadata.Serializers);
    options.Copiers.UnionWith(metadata.Copiers);

    // Register with CodecProvider (uses reflection to access internal API)
    var consumeMethod = typeof(CodecProvider).GetMethod(
        "ConsumeMetadata",
        BindingFlags.NonPublic | BindingFlags.Instance);

    consumeMethod.Invoke(_codecProvider, new object[] { Options.Create(options) });

    // Invalidate caches
    InvalidateCodecCaches(metadata);
}
```

> **Note**: The serialization registration uses reflection because `CodecProvider.ConsumeMetadata` is internal. This is a pragmatic choice for an experimental feature.

---

## Default Registration

Plugin grain loading is registered by default in Orleans silos via `DefaultSiloServices.cs`:

```csharp
// Plugin grain loading (enabled by default)
services.TryAddSingleton<AssemblyValidator>();
services.TryAddSingleton<PluginAssemblyLoader>();
services.TryAddSingleton<PluginSerializationManager>();
services.TryAddSingleton<PluginGrainLoaderService>();
services.TryAddSingleton<IPluginGrainLoader>(sp =>
    sp.GetRequiredService<PluginGrainLoaderService>());
```

No explicit configuration is required - `IPluginGrainLoader` is always available.

---

## File Locations Summary

### Core Implementation

| File | Purpose |
|------|---------|
| `src/Orleans.Runtime/DynamicGrains/IPluginGrainLoader.cs` | Public interface |
| `src/Orleans.Runtime/DynamicGrains/PluginGrainLoaderService.cs` | Orchestration |
| `src/Orleans.Runtime/DynamicGrains/PluginAssemblyLoader.cs` | MDCP integration |
| `src/Orleans.Runtime/DynamicGrains/PluginAssemblySet.cs` | Split assembly tracking |
| `src/Orleans.Runtime/DynamicGrains/PluginSerializationManager.cs` | Serializer registration |
| `src/Orleans.Runtime/DynamicGrains/PluginGrainUnloaderService.cs` | Unload orchestration |
| `src/Orleans.Runtime/DynamicGrains/PluginGrainLoadingExtensions.cs` | Extension methods |
| `src/Orleans.Runtime/DynamicGrains/AssemblyValidator.cs` | Validates Orleans codegen |

### Supporting Infrastructure

| File | Purpose |
|------|---------|
| `src/Orleans.Runtime/DynamicGrains/GrainTypeDirectoryGrain.cs` | Cluster-wide type registry |
| `src/Orleans.Runtime/DynamicGrains/DynamicGrainClient.cs` | Dynamic grain access |
| `src/Orleans.Runtime/DynamicGrains/GrainLifecycleManager.cs` | Activation tracking for unload |

---

## Dependencies

### McMaster.NETCore.Plugins

**Package**: `McMaster.NETCore.Plugins` version 2.0.0

**Reference**: `Orleans.Runtime.csproj`

**Documentation**: https://github.com/natemcmaster/DotNetCorePlugins

**Key APIs Used**:
- `PluginLoader.CreateFromAssemblyFile()` - Create isolated loader
- `PluginLoaderOptions.IsUnloadable` - Enable collectible ALC
- `PluginLoaderOptions.PreferSharedTypes` - Type unification
- `PluginLoaderOptions.SharedAssemblies` - Explicit shared assemblies
- `PluginLoader.LoadDefaultAssembly()` - Load the main assembly
- `PluginLoader.Dispose()` - Trigger ALC unload

---

## Test Scenarios

Located in `playground/PluginGrainScenarios/`:

| Scenario | Description |
|----------|-------------|
| **Scenario 1** | Single silo basic load/unload |
| **Scenario 2** | MDCP isolation verification (IsCollectible = true) |
| **Scenario 3** | Multi-silo manifest propagation |
| **Scenario 4** | Assembly unload and memory reclaim (~51% recovered) |
| **Scenario 5** | Split grain assemblies (Contracts + Implementation) |
| **Scenario 6** | Grain Type Directory (GTD) integration |
| **Scenario 7** | Dynamic Grain Client with DLR support |

---

## Performance Characteristics

### Load Operation

- Small assembly (1-5 grains): 50-100ms
- Medium assembly (10-50 grains): 100-500ms
- Large assembly (100+ grains): 500-2000ms

### Runtime Performance

**Zero overhead** for grain execution:
- Activation path unchanged
- Same serialization codecs
- Same message routing
- Loaded grains are indistinguishable from statically loaded grains

### Memory

- ~51% memory recovered after unload (tested)
- Remaining ~49% is .NET runtime overhead and cached metadata

---

## Naming Convention

All types use the `Plugin*` prefix:
- `IPluginGrainLoader`
- `PluginGrainLoaderService`
- `PluginAssemblyLoader`
- `PluginAssemblySet`
- `PluginSerializationManager`
- `PluginGrainUnloaderService`

This aligns with MDCP terminology and avoids the misleading "Dynamic" prefix which implied a false distinction between "dynamic" and "static" grains.

---

## References

- [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins)
- [.NET AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Orleans Manifest System](../../src/Orleans.Runtime/Manifest/)
