# Dynamic Grain Loading in Orleans - Research & Implementation Guide

**Research Date**: 2025-11-13
**Orleans Version**: 9.1.0
**Difficulty**: MEDIUM-HARD (2-3 month implementation)

---

## Executive Summary

This document provides a comprehensive analysis of how to modify Orleans to support **dynamic grain loading at runtime**—loading grain assemblies after the silo has started without requiring application restart.

**Current State**: Orleans uses compile-time code generation and immutable type manifests, designed for static grain types known at startup.

**Goal**: Enable loading pre-compiled grain assemblies at runtime and propagating type information across the cluster.

**Verdict**: ✅ **Feasible** with moderate difficulty. Orleans architecture has good foundations—many components use concurrent collections and the cluster manifest system already supports updates.

---

## Table of Contents

1. [Current Orleans Architecture](#current-orleans-architecture)
2. [Three Implementation Approaches](#three-implementation-approaches)
3. [Recommended Approach Details](#recommended-approach-details)
4. [Required Modifications](#required-modifications)
5. [Implementation Roadmap](#implementation-roadmap)
6. [Proposed Public API](#proposed-public-api)
7. [Critical Risks & Mitigations](#critical-risks--mitigations)
8. [File Reference](#file-reference)

---

## Current Orleans Architecture

### Grain Discovery & Registration Flow

#### 1. **Compile-Time Code Generation**
Orleans uses a Roslyn source generator (`src/Orleans.CodeGenerator/`) that runs during compilation:

**Generated artifacts per assembly:**
- **Serializers** (`IFieldCodec<T>`) - Binary serialization for types
- **Copiers** (`IDeepCopier<T>`) - Deep cloning
- **Proxies** - Client-side grain reference implementations
- **Invokers** - Server-side method dispatch
- **Activators** - Grain instance creation
- **Metadata Provider** - Registers all generated types

**Example generated metadata:**
```csharp
[assembly: ApplicationPartAttribute("MyGrainAssembly")]
[assembly: TypeManifestProviderAttribute(typeof(Metadata_MyGrainAssembly))]

internal sealed class Metadata_MyGrainAssembly : TypeManifestProviderBase
{
    protected override void ConfigureInner(TypeManifestOptions config)
    {
        config.Serializers.Add(typeof(Codec_MyGrain));
        config.Copiers.Add(typeof(Copier_MyGrain));
        config.InterfaceProxies.Add(typeof(Proxy_IMyGrain));
        config.Interfaces.Add(typeof(IMyGrain));
        config.InterfaceImplementations.Add(typeof(MyGrain));
    }
}
```

#### 2. **Assembly Discovery (Startup)**
**Location**: `src/Orleans.Serialization/Hosting/ReferencedAssemblyProvider.cs:16-32`

At startup, Orleans scans for assemblies marked with `[ApplicationPart]`:
- DependencyContext (compile-time references)
- AssemblyLoadContext.Assemblies (loaded assemblies)
- AppDomain.CurrentDomain.GetAssemblies()

**Key limitation**: One-time scan—no mechanism to re-scan after startup.

#### 3. **Type Manifest Population**
**Location**: `src/Orleans.Core/Configuration/GrainTypeOptions.cs`

The `DefaultGrainTypeOptionsProvider` populates `GrainTypeOptions` with discovered types:
```csharp
public void Configure(GrainTypeOptions options)
{
    foreach (var type in _typeManifestOptions.Interfaces)
        if (typeof(IAddressable).IsAssignableFrom(type))
            options.Interfaces.Add(type);

    foreach (var type in _typeManifestOptions.InterfaceImplementations)
        if (IsImplementationType(type))
            options.Classes.Add(type);  // Grain classes registered here
}
```

#### 4. **GrainClassMap Creation**
**Location**: `src/Orleans.Runtime/Manifest/GrainClassMap.cs:14-26`

Maps `GrainType` (logical identifier) → CLR `Type` (implementation class):
```csharp
public class GrainClassMap
{
    private readonly ImmutableDictionary<GrainType, Type> _types;  // IMMUTABLE!

    public bool TryGetGrainClass(GrainType grainType, out Type grainClass)
    {
        return _types.TryGetValue(grainType, out grainClass);
    }
}
```

**Key limitation**: Immutable dictionary—cannot add types after construction.

#### 5. **Cluster Manifest Synchronization**
**Location**: `src/Orleans.Runtime/Manifest/ClusterManifestProvider.cs:97-195`

When silos join the cluster, their manifests are fetched and merged:
```csharp
private async Task UpdateManifest(ClusterMembershipSnapshot clusterMembership)
{
    var builder = _current.Silos.ToBuilder();

    // Add manifests from new silos
    foreach (var member in clusterMembership.Members)
    {
        if (member.Status == SiloStatus.Active && !_current.Silos.ContainsKey(member.SiloAddress))
        {
            var manifest = await remoteManifestProvider.GetSiloManifest();
            builder[member.SiloAddress] = manifest;
        }
    }
}
```

**Key insight**: ✅ Cluster manifest updates already supported—we can extend this!

#### 6. **Grain Activation Flow**
```
Message for GrainId arrives
    ↓
Catalog.GetOrCreateActivation(grainId)
    ↓
GrainContextActivator.CreateInstance(address)
    ↓
GrainClassMap.TryGetGrainClass(grainType) → Resolves CLR Type
    ↓
DefaultGrainActivator.CreateInstance(context) → Instantiates grain
```

**Key limitation**: Activators are cached forever in `ImmutableDictionary`.

---

## Three Implementation Approaches

### Option 1: Pre-Compiled Assembly Loading ⭐ **RECOMMENDED**

**Timeline**: 2-3 months | **Difficulty**: MEDIUM | **Risk**: LOW

Load assemblies that were compiled with Orleans code generation **before runtime**.

**Workflow:**
1. Developer compiles grain assembly with `Orleans.Sdk` separately
2. Silo calls `LoadGrainAssemblyAsync(path)` at runtime
3. Assembly with generated code is loaded
4. Manifests updated cluster-wide
5. Caches invalidated/refreshed

**Pros:**
- ✅ Full Orleans feature support (placement, reminders, transactions)
- ✅ Type-safe with compile-time checks
- ✅ Good performance (no reflection overhead)
- ✅ Uses existing code generation pipeline

**Cons:**
- ❌ Requires compilation step before loading
- ❌ Cannot generate code for truly dynamic types

**Use cases:**
- Plugin systems with pre-built DLLs
- Multi-tenant systems with customer-specific grains
- Microservice deployment with independent grain assemblies

---

### Option 2: Reflection-Based Dynamic Grains

**Timeline**: 1-2 months | **Difficulty**: MEDIUM | **Risk**: MEDIUM

Use `IGeneralizedCodec` interface for reflection-based serialization without generated code.

**Implementation:**
```csharp
public class ReflectionBasedGrainCodec : IGeneralizedCodec
{
    public bool IsSupportedType(Type type) =>
        typeof(IGrain).IsAssignableFrom(type) && !HasGeneratedCodec(type);

    public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, ...)
    {
        // Use reflection to serialize fields
    }

    public object ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        // Use reflection to deserialize
    }
}
```

**Pros:**
- ✅ No compilation required
- ✅ Works with truly dynamic types
- ✅ Simpler implementation

**Cons:**
- ❌ Performance overhead (reflection)
- ❌ Limited feature support (no invoker optimization)
- ❌ Less type-safe

**Use cases:**
- Prototype/development environments
- Simple CRUD grains without complex features
- Fallback for non-generated assemblies

---

### Option 3: Runtime Roslyn Compilation

**Timeline**: 4-6 months | **Difficulty**: VERY HARD | **Risk**: HIGH

Generate Orleans code at runtime using Roslyn compiler.

**Workflow:**
1. Load grain assembly at runtime
2. Run Orleans.CodeGenerator APIs in-memory
3. Compile generated C# code using Roslyn
4. Load compiled assembly
5. Register types

**Pros:**
- ✅ Complete feature parity
- ✅ Can handle any grain type

**Cons:**
- ❌ Very complex implementation
- ❌ Slow (compilation overhead)
- ❌ Large dependencies (Roslyn)
- ❌ Memory overhead
- ❌ Difficult to debug

**Use cases:**
- Advanced scenarios requiring full dynamic behavior
- Not recommended for most use cases

---

## Recommended Approach Details

**Option 1: Pre-Compiled Assembly Loading** is recommended because:
1. Leverages existing, battle-tested code generation
2. Maintains type safety and performance
3. Moderate implementation complexity
4. Minimal risk to Orleans stability

**Constraint**: Grain assemblies must be compiled with `Orleans.Sdk` before loading.

---

## Required Modifications

### A. Assembly Loading System

**Difficulty**: MEDIUM
**Timeline**: Weeks 1-2

**Files to modify:**
- `src/Orleans.Serialization/Hosting/ReferencedAssemblyProvider.cs`
- `src/Orleans.Serialization/Hosting/ServiceCollectionExtensions.cs`

**Current implementation:**
```csharp
// Called ONCE at startup
public static IEnumerable<Assembly> GetRelevantAssemblies()
{
    foreach (var loadedAsm in AppDomain.CurrentDomain.GetAssemblies())
    {
        if (loadedAsm.IsDefined(typeof(ApplicationPartAttribute)))
            AddAssembly(parts, loadedAsm);
    }
}
```

**Required changes:**
1. Add method to trigger re-scan after startup
2. Support `AssemblyLoadContext.LoadFromAssemblyPath()`
3. Track which assemblies have been processed
4. Thread-safe assembly registration

**New API:**
```csharp
public interface IDynamicAssemblyLoader
{
    Task<AssemblyLoadResult> LoadAssemblyAsync(string path, CancellationToken ct);
    Task<IReadOnlyList<Assembly>> RescanAssembliesAsync();
    event EventHandler<AssemblyLoadedEventArgs> AssemblyLoaded;
}

public class AssemblyLoadResult
{
    public Assembly Assembly { get; init; }
    public IReadOnlyList<Type> GrainClasses { get; init; }
    public IReadOnlyList<Type> GrainInterfaces { get; init; }
    public IReadOnlyList<Type> Serializers { get; init; }
}
```

**Risks:**
- Assembly unloading not supported (use `AssemblyLoadContext` for isolation)
- Must track processed assemblies to avoid duplicates
- Thread safety during concurrent loads

---

### B. Manifest System

**Difficulty**: HARD
**Timeline**: Weeks 3-4

**Files to modify:**
- `src/Orleans.Runtime/Manifest/GrainClassMap.cs:14-26`
- `src/Orleans.Runtime/Manifest/SiloManifestProvider.cs:16-28`
- `src/Orleans.Runtime/Manifest/ClusterManifestProvider.cs:97-195`

**Current implementation:**
```csharp
public class GrainClassMap
{
    private readonly ImmutableDictionary<GrainType, Type> _types;  // IMMUTABLE
}

public class SiloManifestProvider
{
    public GrainManifest SiloManifest { get; }  // Set once at construction
    public GrainClassMap GrainTypeMap { get; }  // Set once at construction
}
```

**Required changes:**

1. **Make GrainClassMap updateable:**
```csharp
public class GrainClassMap
{
    private volatile ImmutableDictionary<GrainType, Type> _types;

    public void UpdateTypes(ImmutableDictionary<GrainType, Type> updatedTypes)
    {
        _types = updatedTypes;  // Atomic replacement
    }
}
```

2. **Add incremental update to SiloManifestProvider:**
```csharp
public partial class SiloManifestProvider
{
    public (GrainManifest Manifest, GrainClassMap TypeMap) UpdateManifest(
        IEnumerable<Type> newClasses,
        IEnumerable<Type> newInterfaces)
    {
        // Create updated manifest by merging
        // Increment manifest version (minor bump)
        // Return new manifest and type map
    }
}
```

3. **Propagate to cluster via ClusterManifestProvider:**
```csharp
public async Task PublishManifestUpdateAsync(GrainManifest newLocalManifest)
{
    var builder = _current.Silos.ToBuilder();
    builder[_localSiloAddress] = newLocalManifest;

    var newVersion = _current.Version.Increment();
    _updates.TryPublish(new ClusterManifest(newVersion, builder.ToImmutable()));
}
```

**Key insight**: `ClusterManifestProvider` already has update infrastructure—just extend it!

**New API:**
```csharp
public interface IDynamicManifestProvider
{
    Task<ManifestUpdateResult> UpdateLocalManifestAsync(
        IEnumerable<Type> newGrainClasses,
        IEnumerable<Type> newGrainInterfaces,
        CancellationToken ct);

    Task PublishManifestUpdateAsync(ManifestUpdateResult update);
    IAsyncEnumerable<ClusterManifest> ManifestUpdates { get; }
}
```

**Risks:**
- **Cluster consistency**: All silos must receive manifest updates
- **Version conflicts**: Need careful version increment logic
- **Race conditions**: Multiple simultaneous updates
- **Rollback**: If update fails on some silos, need rollback

---

### C. Serialization System

**Difficulty**: MEDIUM-HARD
**Timeline**: Weeks 5-6

**Files to modify:**
- `src/Orleans.Serialization/Serializers/CodecProvider.cs:89-135`
- `src/Orleans.Serialization/Configuration/TypeManifestOptions.cs`

**Current implementation:**
```csharp
public class CodecProvider
{
    private readonly Dictionary<Type, Type> _fieldCodecs;  // Populated once
    private readonly ConcurrentDictionary<Type, IFieldCodec> _untypedCodecs;  // Runtime cache
    private bool _initialized;  // One-time initialization

    private void ConsumeMetadata(IOptions<TypeManifestOptions> codecConfiguration)
    {
        // Called ONCE at construction
        var metadata = codecConfiguration.Value;
        AddFromMetadata(_fieldCodecs, metadata.Serializers, typeof(IFieldCodec<>));
    }
}
```

**Key insight**: ✅ Already uses `ConcurrentDictionary` for runtime cache—just need registration API!

**Required changes:**

1. **Make metadata consumption repeatable:**
```csharp
public void RegisterSerializers(IEnumerable<Type> serializerTypes)
{
    var options = new TypeManifestOptions();
    options.Serializers.UnionWith(serializerTypes);
    ConsumeMetadata(Options.Create(options));  // Re-run metadata consumption
}
```

2. **Add cache invalidation:**
```csharp
public void InvalidateCodecCache(Type fieldType)
{
    _untypedCodecs.TryRemove(fieldType, out _);
    _typedCodecs.TryRemove(fieldType, out _);
}
```

**New API:**
```csharp
public interface IDynamicSerializationManager
{
    Task RegisterCodecsAsync(AssemblyLoadResult result);
    void InvalidateCachedCodecs(IEnumerable<Type> types);
}
```

**Risks:**
- Cache invalidation complexity (cascading dependencies)
- Performance during update window (cache misses)
- Thread safety during concurrent serialization
- Generalized codecs may need re-query

---

### D. Type Resolution & Activation

**Difficulty**: MEDIUM
**Timeline**: Week 7

**Files to modify:**
- `src/Orleans.Runtime/Activation/IGrainContextActivator.cs:167-210`
- `src/Orleans.Runtime/Catalog/GrainTypeSharedContext.cs`

**Current implementation:**
```csharp
public class GrainTypeSharedContextResolver
{
    private readonly ConcurrentDictionary<GrainType, GrainTypeSharedContext> _components;

    public GrainTypeSharedContext GetComponents(GrainType grainType)
        => _components.GetOrAdd(grainType, _createFunc);  // Cached forever
}

public class GrainContextActivator
{
    private ImmutableDictionary<GrainType, (IGrainContextActivator, ...)> _activators;
    // Cached forever
}
```

**Required changes:**

1. **Add cache invalidation:**
```csharp
public class GrainTypeSharedContextResolver
{
    public void InvalidateGrainType(GrainType grainType)
    {
        _components.TryRemove(grainType, out _);
    }

    public void RefreshAllGrainTypes()
    {
        _components.Clear();
    }
}
```

2. **Refresh activators:**
```csharp
public class GrainContextActivator
{
    public void InvalidateActivator(GrainType grainType)
    {
        lock (_lockObj)
        {
            _activators = _activators.Remove(grainType);
        }
    }
}
```

**Key insight**: `CachedTypeResolver` already scans `AppDomain.CurrentDomain.GetAssemblies()`—automatically picks up new assemblies!

**New API:**
```csharp
public interface IDynamicActivationManager
{
    Task RefreshActivatorsAsync(IEnumerable<GrainType> grainTypes);
}
```

**Risks:**
- In-flight activations during cache invalidation
- Consistency across multiple caches
- Performance impact of cold cache
- Version conflicts (old vs new implementations)

---

### E. Code Generation

**Difficulty**: EASY (no changes needed for Option 1!)

**For Option 1**: Assemblies must be pre-compiled with `Orleans.Sdk` before loading.

**Validation:**
```csharp
public interface IDynamicGrainValidator
{
    ValidationResult ValidateAssembly(Assembly assembly);
}

public class ValidationResult
{
    public bool HasGeneratedCode { get; init; }
    public IReadOnlyList<string> MissingGenerators { get; init; }
}
```

Check for:
- `[ApplicationPart]` attribute
- `[TypeManifestProvider]` attribute
- Generated codec types
- Generated proxy types

**Risks**: None—uses standard Orleans compilation path.

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)
**Difficulty**: MEDIUM

**Tasks:**
1. Create `IDynamicAssemblyLoader` interface and implementation
2. Add assembly loading via `AssemblyLoadContext`
3. Implement re-scan mechanism
4. Add validation for generated code
5. Unit tests for assembly loading

**Deliverable**: Can load assemblies with validation.

---

### Phase 2: Manifest Updates (Weeks 3-4)
**Difficulty**: HARD

**Tasks:**
1. Make `GrainClassMap` updateable (replace immutable dictionary)
2. Add `UpdateManifest()` to `SiloManifestProvider`
3. Implement manifest version bumping (minor version increment)
4. Add cluster propagation via `ClusterManifestProvider`
5. Test manifest sync across 3-silo cluster

**Deliverable**: Local manifest updates with cluster synchronization.

---

### Phase 3: Serialization (Weeks 5-6)
**Difficulty**: MEDIUM-HARD

**Tasks:**
1. Make `CodecProvider.ConsumeMetadata()` repeatable
2. Implement `RegisterSerializers()` API
3. Add codec cache invalidation
4. Test serialization with dynamic types
5. Performance benchmarks

**Deliverable**: New types can be serialized after loading.

---

### Phase 4: Activation (Week 7)
**Difficulty**: MEDIUM

**Tasks:**
1. Add cache invalidation to `GrainTypeSharedContextResolver`
2. Implement activator refresh in `GrainContextActivator`
3. Coordinate invalidation across all caches
4. Test grain activation for dynamically loaded types

**Deliverable**: Grains can be activated from dynamically loaded assemblies.

---

### Phase 5: Integration & Public API (Weeks 8-9)
**Difficulty**: MEDIUM

**Tasks:**
1. Create unified `IDynamicGrainLoader` facade
2. Add comprehensive safety checks
3. Implement transaction-like updates (all-or-nothing)
4. Error handling and recovery
5. Integration tests

**Deliverable**: Production-ready public API.

---

### Phase 6: Testing & Documentation (Week 10)
**Difficulty**: MEDIUM

**Tasks:**
1. Multi-silo cluster tests
2. Edge case testing (concurrent loads, failures, rollbacks)
3. Performance benchmarks
4. Documentation and samples
5. Migration guide

**Deliverable**: Tested and documented feature.

---

## Proposed Public API

```csharp
namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Service for loading grain assemblies at runtime.
/// </summary>
public interface IDynamicGrainLoader
{
    /// <summary>
    /// Loads a pre-compiled grain assembly with Orleans-generated code.
    /// Assembly must be compiled with Orleans.Sdk.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file</param>
    /// <param name="cancellation">Cancellation token</param>
    /// <returns>Result containing loaded grain types</returns>
    Task<GrainLoadResult> LoadGrainAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellation = default);

    /// <summary>
    /// Unloads grain types (requires assembly loaded in isolated AssemblyLoadContext).
    /// </summary>
    Task UnloadGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        CancellationToken cancellation = default);

    /// <summary>
    /// Gets a stream of grain assembly load events.
    /// </summary>
    IAsyncEnumerable<GrainAssemblyLoadedEvent> LoadEvents { get; }
}

public class GrainLoadResult
{
    public Assembly Assembly { get; init; }
    public IReadOnlyList<GrainType> GrainTypes { get; init; }
    public TimeSpan LoadDuration { get; init; }
    public ManifestVersion NewManifestVersion { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
}

public class GrainAssemblyLoadedEvent
{
    public Assembly Assembly { get; init; }
    public SiloAddress LoadedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<GrainType> NewGrainTypes { get; init; }
}
```

**Usage example:**
```csharp
// In Silo startup
builder.Services.AddDynamicGrainLoading();

// At runtime
var grainLoader = serviceProvider.GetRequiredService<IDynamicGrainLoader>();
var result = await grainLoader.LoadGrainAssemblyAsync("/path/to/MyGrains.dll");

if (result.Success)
{
    Console.WriteLine($"Loaded {result.GrainTypes.Count} grain types");
    Console.WriteLine($"New manifest version: {result.NewManifestVersion}");
}
```

---

## Critical Risks & Mitigations

### 1. Cluster Inconsistency
**Risk**: Silos have different grain type knowledge, routing failures.

**Mitigation:**
- Use versioned manifests with strict ordering
- Block grain activation until manifest synchronized across cluster
- Add health checks for manifest consistency
- Implement manifest version negotiation

### 2. In-Flight Requests During Update
**Risk**: Requests to grain type being updated fail or use old implementation.

**Mitigation:**
- Graceful activation migration (drain old activations)
- Maintain both old and new activators temporarily
- Version-based routing (route to correct implementation)
- Retry logic for transient failures

### 3. Serialization Compatibility
**Risk**: Old silos can't deserialize messages with new types.

**Mitigation:**
- Require generated codecs (Option 1 enforcement)
- Fallback to `IGeneralizedCodec` for unknown types
- Version negotiation in serialization protocol
- Compatibility testing in CI

### 4. Memory Leaks
**Risk**: Old types/activators never garbage collected.

**Mitigation:**
- Use `AssemblyLoadContext` with unload support
- Implement proper cleanup in unload path
- Weak references for cached activators
- Monitoring and diagnostics

### 5. Performance Degradation
**Risk**: Cache invalidation causes cold cache, high latency.

**Mitigation:**
- Incremental cache warming (pre-load activators)
- Keep old cache entries during transition
- Staged rollout across cluster
- Monitoring and alerts for performance regressions

### 6. Concurrent Updates
**Risk**: Multiple threads/silos updating simultaneously.

**Mitigation:**
- Distributed lock for manifest updates
- Optimistic concurrency with version checks
- Retry with backoff on conflicts
- Limit update frequency

---

## File Reference

### Key Files Requiring Modification

**Assembly Loading:**
- `src/Orleans.Serialization/Hosting/ReferencedAssemblyProvider.cs:16-32`
- `src/Orleans.Serialization/Hosting/ServiceCollectionExtensions.cs:40-43`

**Manifest System:**
- `src/Orleans.Runtime/Manifest/GrainClassMap.cs:14-26`
- `src/Orleans.Runtime/Manifest/SiloManifestProvider.cs:16-28`
- `src/Orleans.Runtime/Manifest/ClusterManifestProvider.cs:97-195`

**Serialization:**
- `src/Orleans.Serialization/Serializers/CodecProvider.cs:89-135`
- `src/Orleans.Serialization/Configuration/TypeManifestOptions.cs:10-94`

**Activation:**
- `src/Orleans.Runtime/Activation/IGrainContextActivator.cs:167-210`
- `src/Orleans.Runtime/Catalog/GrainTypeSharedContext.cs:28-69`
- `src/Orleans.Runtime/Activation/ConfigureDefaultGrainActivator.cs:17-28`

**Type Resolution:**
- `src/Orleans.Serialization/TypeSystem/CachedTypeResolver.cs:12-60`

---

## Conclusion

Dynamic grain loading is **feasible** with **moderate difficulty**. Orleans architecture provides good foundations:

✅ **Strengths:**
- Cluster manifest updates already supported
- Concurrent collections used throughout
- Lazy initialization of activators and contexts
- Type resolver already handles new assemblies

⚠️ **Challenges:**
- Immutable dictionaries need replacement
- Cache invalidation coordination
- Cluster-wide consistency
- Zero-downtime updates

**Recommended Approach**: Option 1 (Pre-compiled assemblies)
**Estimated Timeline**: 2-3 months for production-ready implementation
**Team Size**: 1-2 senior engineers familiar with Orleans internals
**Breaking Changes**: Minimal—mostly additive APIs

**Next Steps:**
1. Build proof-of-concept for Phase 1 (assembly loading)
2. Test manifest update mechanism in isolated environment
3. Validate serialization with dynamic types
4. Full integration testing
5. Production deployment with feature flags
