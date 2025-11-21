# Dynamic Grain Unloading in Orleans - Research & Design Document

**Research Date**: 2025-11-21
**Orleans Version**: 9.1.0
**Prerequisite**: Dynamic Grain Loading (Phases 1-4 complete)
**Difficulty**: VERY HARD (3-4 month implementation)

---

## Executive Summary

This document provides comprehensive research and design for **dynamic grain unloading at runtime**—the ability to unload grain assemblies from a running silo without restart, releasing memory and allowing true hot-swapping of grain implementations.

**Current State**: Dynamic loading (Phases 1-4) allows loading grain assemblies at runtime, but assemblies remain in memory forever. No mechanism exists to unload types or reclaim memory.

**Goal**: Enable safe unloading of grain assemblies from individual silos, including:
- Graceful deactivation of active grain instances
- Removal from all caches and registries
- Memory reclamation via collectible AssemblyLoadContext
- Cluster manifest updates (per-silo, not cluster-wide)

**Verdict**: ✅ **Feasible** with very high difficulty. Requires:
1. Integration with DotNetCorePlugins for collectible assembly loading
2. Grain lifecycle management (deactivation API)
3. Reference cleanup across 6+ subsystems
4. Careful orchestration to prevent dangling references

---

## Table of Contents

1. [Key Clarifications & Decisions](#key-clarifications--decisions)
2. [DotNetCorePlugins Integration](#dotnetcoreplugins-integration)
3. [Unloading Architecture](#unloading-architecture)
4. [Required Modifications](#required-modifications)
5. [Implementation Roadmap](#implementation-roadmap)
6. [Critical Risks & Mitigations](#critical-risks--mitigations)
7. [References](#references)

---

## Key Clarifications & Decisions

### 1. Silo-Level vs Cluster-Wide Unloading

**Decision**: Unloading operates at the **SILO LEVEL**, not cluster-wide.

**Rationale**:
- Orleans already supports silos having different grain type sets
- Current dynamic loading is silo-level with cluster manifest propagation
- Each silo independently loads assemblies; unloading follows same pattern

**Implementation**:
```
Silo A: Loads TenantX.Grains → Updates manifest → Cluster knows "Silo A has TenantX types"
Later: Silo A unloads TenantX.Grains → Updates manifest → Cluster knows "Silo A no longer has TenantX types"

Silos B, C, D: Unaffected, can still have TenantX.Grains loaded if they loaded independently
```

**Cluster Coordination**:
- Manifest update broadcasts type removal
- Routing layer stops sending requests for those types to that silo
- Other silos continue processing if they have the types loaded

### 2. Persistent State Handling

**Decision**: Persistent state remains in storage, accessible when/if type is reloaded.

**Rationale**:
- Grain state is orthogonal to type availability
- State stores don't care about CLR types
- No migration or cleanup needed

**Example**:
```
1. Silo A: TenantX.PaymentGrain active, state in database
2. Silo A: Unload TenantX.Grains assembly
3. State remains: { "grainId": "payment-123", "balance": 1000, ... }
4. Later: Silo B loads TenantX.Grains
5. Silo B: Activate PaymentGrain("payment-123") → Loads state from database → Works
```

### 3. Scope of Unloading

**What we're unloading**: Dynamically loaded grain assemblies (loaded via `IDynamicGrainLoader`)

**What we're NOT unloading**: Static grains compiled into the silo executable

**Reason**: Static grains are in the default AssemblyLoadContext (non-collectible). Only assemblies loaded into collectible contexts can be unloaded.

**Future consideration**: Could make ALL grains use collectible contexts, but not in scope for initial implementation.

### 4. Active Grain Deactivation

**Decision**: Active grain instances MUST be deactivated before type unloading.

**Why**: If even ONE grain instance remains active:
- It holds references to types in the assembly
- AssemblyLoadContext cannot be garbage collected
- Memory leak occurs (assembly stays in memory)

**Process**:
1. Enumerate all active grains of types being unloaded
2. Call `OnDeactivateAsync(DeactivationReason.TypeUnloading, cancellationToken)` on each
3. Wait for graceful completion (with timeout)
4. Force deactivation after timeout
5. Verify no references remain before unloading

### 5. Orleans Already Has Grain Shutdown Hooks

**Discovery**: `Grain.OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)` exists!

**Location**: `src/Orleans.Core.Abstractions/Core/Grain.cs:164`

**Existing reasons** (`DeactivationReasonCode` enum):
- `None`
- `ShuttingDown` ← Silo shutdown
- `ActivationFailed`
- `DirectoryFailure`
- `ActivationIdle`
- `ActivationUnresponsive`
- `DuplicateActivation`
- `IncompatibleRequest`
- `ApplicationError`
- `ApplicationRequested`
- `Migrating`
- `RuntimeRequested`
- `HighMemoryPressure`

**New reason needed**:
```csharp
/// <summary>
/// The grain type is being dynamically unloaded from this silo.
/// </summary>
TypeUnloading,
```

**Usage by grain developers**:
```csharp
public class MyGrain : Grain, IMyGrain
{
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
        {
            _logger.LogWarning("Type being unloaded! Saving critical state...");
            await SaveCriticalStateAsync();
        }

        await base.OnDeactivateAsync(reason, ct);
    }
}
```

---

## DotNetCorePlugins Integration

### Overview

**Library**: McMaster.NETCore.Plugins (aka DotNetCorePlugins)
**GitHub**: https://github.com/natemcmaster/DotNetCorePlugins
**NuGet**: `McMaster.NETCore.Plugins`
**Purpose**: Provides collectible AssemblyLoadContext with dependency resolution

### Key Features for Orleans

#### 1. Collectible AssemblyLoadContext

```csharp
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyFile: "/path/to/TenantA.Grains.dll",
    sharedTypes: GetOrleansSharedTypes(),
    isUnloadable: true  // ← Creates collectible context
);

var assembly = loader.LoadDefaultAssembly();

// Later: Unload
loader.Dispose();  // Triggers AssemblyLoadContext.Unload()
GC.Collect();      // Collects assembly if no references remain
GC.WaitForPendingFinalizers();
```

#### 2. Shared Types (Solves Type Identity Problem)

**Problem**: Grain assembly depends on Orleans types (IGrain, Grain, etc.). Without sharing:
- Plugin loads its own copy of Orleans.Core
- `IGrain` in plugin context ≠ `IGrain` in host context
- Type casting fails, serialization breaks

**Solution**: Declare Orleans types as "shared" → plugin uses host's Orleans assemblies:

```csharp
private static Type[] GetOrleansSharedTypes()
{
    return new[]
    {
        // Core abstractions
        typeof(IGrain),
        typeof(IGrainWithStringKey),
        typeof(IGrainWithGuidKey),
        typeof(IGrainWithIntegerKey),
        typeof(IAddressable),
        typeof(IGrainObserver),

        // Base classes
        typeof(Grain),
        typeof(GrainReference),

        // Serialization
        typeof(Orleans.Serialization.Serializer),

        // Common types grains might use
        typeof(Task),
        typeof(Task<>),
        typeof(CancellationToken),
        typeof(IServiceProvider),

        // Add more as needed based on compilation errors
    };
}
```

**Configuration**:
```csharp
configure: config => {
    config.PreferSharedTypes = true;  // Prefer host's types over plugin's
    config.IsUnloadable = true;       // Enable unloading
}
```

#### 3. Dependency Resolution

**Problem**: Grain assembly has dependencies (`Newtonsoft.Json`, `Dapper`, etc.)

**Solution**: DotNetCorePlugins reads `.deps.json` and resolves dependencies automatically.

**Directory structure**:
```
/app/grains/
  TenantA.Grains/
    TenantA.Grains.dll
    TenantA.Grains.deps.json    ← Dependency metadata
    TenantA.Grains.runtimeconfig.json
    Newtonsoft.Json.dll         ← Dependencies in same folder
    Dapper.dll
  TenantB.Grains/
    TenantB.Grains.dll
    TenantB.Grains.deps.json
    ...
```

**Each plugin in isolated directory** prevents dependency conflicts.

#### 4. Verified Solutions to Our Challenges

| Challenge | DotNetCorePlugins Solution |
|-----------|---------------------------|
| **Dependency resolution** | Reads `.deps.json`, resolves from plugin directory |
| **Type identity** | Shared types mechanism ensures host types used |
| **Unloadability** | Built-in collectible AssemblyLoadContext support |
| **Isolation** | Separate contexts per plugin, no interference |
| **Orleans dependencies** | Shared types array includes all Orleans types |

### Integration into DynamicAssemblyLoader

**Current implementation** (`src/Orleans.Runtime/DynamicGrains/DynamicAssemblyLoader.cs:80`):
```csharp
// PROBLEM: Default context, cannot unload
assembly = Assembly.LoadFrom(assemblyPath);
```

**New implementation with DotNetCorePlugins**:
```csharp
public class DynamicAssemblyLoader
{
    private readonly ConcurrentDictionary<string, PluginLoader> _pluginLoaders = new();
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new();

    public async Task<(Assembly, AssemblyLoadMetadata, List<string>)> LoadAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        // Normalize path
        assemblyPath = Path.GetFullPath(assemblyPath);

        // Check if already loaded
        if (_loadedAssemblies.TryGetValue(assemblyPath, out var existing))
        {
            return (existing, GetCachedMetadata(assemblyPath), new List<string> { "Already loaded" });
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check
            if (_loadedAssemblies.TryGetValue(assemblyPath, out existing))
                return (existing, GetCachedMetadata(assemblyPath), new List<string> { "Already loaded" });

            _logger.LogInformation("Loading grain assembly from {Path} using PluginLoader", assemblyPath);

            // Create plugin loader with Orleans shared types
            var loader = PluginLoader.CreateFromAssemblyFile(
                assemblyFile: assemblyPath,
                sharedTypes: GetOrleansSharedTypes(),
                isUnloadable: true,
                configure: config =>
                {
                    config.PreferSharedTypes = true;
                    config.IsUnloadable = true;
                    config.LoadInMemory = false;  // Load from disk for better unloading
                });

            // Load the assembly
            var assembly = loader.LoadDefaultAssembly();

            // Validate
            var validation = _validator.Validate(assembly);
            if (!validation.IsValid)
            {
                loader.Dispose();  // Clean up on failure
                return (null, null, validation.Errors.ToList());
            }

            // Track both loader and assembly
            _pluginLoaders[assemblyPath] = loader;
            _loadedAssemblies[assemblyPath] = assembly;

            return (assembly, validation.Metadata, new List<string>());
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<bool> UnloadAssemblyAsync(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);

        if (!_pluginLoaders.TryRemove(assemblyPath, out var loader))
            return false;

        _loadedAssemblies.TryRemove(assemblyPath, out _);

        _logger.LogInformation("Unloading assembly {Path}", assemblyPath);

        // Dispose triggers AssemblyLoadContext.Unload()
        loader.Dispose();

        // Force garbage collection
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);
        }

        _logger.LogInformation("Assembly {Path} unloaded", assemblyPath);
        return true;
    }

    private static Type[] GetOrleansSharedTypes()
    {
        // See above for full list
        return new[] { typeof(IGrain), typeof(Grain), /* ... */ };
    }
}
```

**Benefits**:
- ✅ Collectible context enables unloading
- ✅ Shared types solve type identity
- ✅ Dependency resolution automatic
- ✅ Battle-tested library (production-ready)
- ✅ Minimal code changes to existing loader

---

## Unloading Architecture

### High-Level Flow

```
User calls: await grainUnloader.UnloadGrainAssemblyAsync("/path/to/TenantA.Grains.dll")
    ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 1: Validate & Prepare                                 │
│ - Check assembly is loaded                                  │
│ - Identify grain types to unload                            │
│ - Check cluster state (optional safety checks)              │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: Deactivate Active Grains                           │
│ - Enumerate active grain instances via Catalog              │
│ - Call OnDeactivateAsync(TypeUnloading) on each             │
│ - Wait for completion (with timeout)                        │
│ - Force deactivate if timeout exceeded                      │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 3: Remove from Caches                                 │
│ - GrainContextActivator.RemoveActivator()                   │
│ - GrainTypeSharedContextResolver.RemoveContext()            │
│ - CodecProvider - remove cached codecs                      │
│ - GrainReferenceActivator - remove proxies                  │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 4: Update Silo Manifest                               │
│ - SiloManifestProvider.RemoveTypes()                        │
│ - GrainClassMap.RemoveTypes()                               │
│ - Update manifest version                                   │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 5: Propagate to Cluster                               │
│ - ClusterManifestProvider.UpdateLocalManifest()             │
│ - Other silos receive manifest update                       │
│ - Routing layer stops directing to this silo for types      │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 6: Unload Assembly                                    │
│ - DynamicAssemblyLoader.UnloadAssemblyAsync()               │
│ - PluginLoader.Dispose() → AssemblyLoadContext.Unload()     │
│ - Trigger GC collection                                     │
└──────────────────┬──────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────┐
│ Phase 7: Verify & Publish Event                             │
│ - Verify assembly unloaded (optional diagnostics)           │
│ - Publish GrainAssemblyUnloadedEvent                        │
│ - Return success result                                     │
└─────────────────────────────────────────────────────────────┘
```

### Reference Cleanup Requirements

Before unloading, **ALL** references to types from the assembly must be released:

```
Components holding references to grain types:
├── Active grain instances (in Catalog)              ← Phase 2: Deactivate
├── GrainClassMap (GrainType → Type mappings)        ← Phase 4: Remove
├── Cached activators (GrainContextActivator)        ← Phase 3: Invalidate
├── Cached contexts (GrainTypeSharedContextResolver) ← Phase 3: Invalidate
├── Codec caches (CodecProvider)                     ← Phase 3: Remove
├── Copier caches (CodecProvider)                    ← Phase 3: Remove
├── Proxy caches (GrainReferenceActivator)           ← Phase 3: Remove
├── Silo manifest (grain properties)                 ← Phase 4: Update
└── Cluster manifest (distributed state)             ← Phase 5: Propagate
```

**Critical**: If even ONE reference remains, `AssemblyLoadContext` won't unload → memory leak.

---

## Required Modifications

### A. New Enum Value - DeactivationReasonCode.TypeUnloading

**File**: `src/Orleans.Core.Abstractions/Core/IGrainBase.cs`
**Line**: ~311 (after HighMemoryPressure)

**Addition**:
```csharp
public enum DeactivationReasonCode : byte
{
    // ... existing codes ...
    HighMemoryPressure,

    /// <summary>
    /// The grain type is being dynamically unloaded from this silo.
    /// </summary>
    TypeUnloading,
}
```

**Purpose**: Allows grain developers to handle type unloading specially in `OnDeactivateAsync()`.

---

### B. Grain Lifecycle Manager (NEW)

**File**: `src/Orleans.Runtime/DynamicGrains/GrainLifecycleManager.cs` (NEW)

**Purpose**: Deactivate all active grains of specific types.

**Interface**:
```csharp
namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Manages lifecycle of grain activations for dynamic type management.
/// </summary>
public interface IGrainLifecycleManager
{
    /// <summary>
    /// Deactivates all active grain instances of the specified types.
    /// </summary>
    /// <param name="grainTypes">The grain types to deactivate.</param>
    /// <param name="timeout">Maximum time to wait for graceful deactivation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and deactivation details.</returns>
    Task<GrainDeactivationResult> DeactivateGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any grains of the specified types are currently active.
    /// </summary>
    bool HasActiveGrains(IEnumerable<GrainType> grainTypes);

    /// <summary>
    /// Gets count of active grains for each specified type.
    /// </summary>
    IReadOnlyDictionary<GrainType, int> GetActiveGrainCounts(IEnumerable<GrainType> grainTypes);
}

public sealed class GrainDeactivationResult
{
    public bool Success { get; init; }
    public int TotalGrainsDeactivated { get; init; }
    public IReadOnlyDictionary<GrainType, int> DeactivatedPerType { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public TimeSpan Duration { get; init; }
    public int ForcedDeactivations { get; init; }  // Timed out, force-deactivated
}
```

**Implementation**:
```csharp
internal sealed class GrainLifecycleManager : IGrainLifecycleManager
{
    private readonly Catalog _catalog;  // Need access to catalog!
    private readonly ILogger<GrainLifecycleManager> _logger;

    public GrainLifecycleManager(Catalog catalog, ILogger<GrainLifecycleManager> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<GrainDeactivationResult> DeactivateGrainTypesAsync(
        IEnumerable<GrainType> grainTypes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var grainTypeSet = grainTypes.ToHashSet();
        var deactivatedPerType = new Dictionary<GrainType, int>();
        var forcedCount = 0;

        _logger.LogInformation(
            "Starting deactivation of {TypeCount} grain types with {Timeout}ms timeout",
            grainTypeSet.Count,
            timeout.TotalMilliseconds);

        // Find all active grains of these types
        var activationsToDeactivate = new List<IGrainContext>();

        foreach (var activation in _catalog.GetAllActivations())  // Need this method!
        {
            if (grainTypeSet.Contains(activation.GrainId.Type))
            {
                activationsToDeactivate.Add(activation);

                if (!deactivatedPerType.ContainsKey(activation.GrainId.Type))
                    deactivatedPerType[activation.GrainId.Type] = 0;

                deactivatedPerType[activation.GrainId.Type]++;
            }
        }

        _logger.LogInformation(
            "Found {ActivationCount} active grain instances to deactivate",
            activationsToDeactivate.Count);

        // Deactivate with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var deactivationTasks = new List<Task>();
        var reason = new DeactivationReason(
            DeactivationReasonCode.TypeUnloading,
            "Grain type being dynamically unloaded");

        foreach (var activation in activationsToDeactivate)
        {
            var task = DeactivateGrainAsync(activation, reason, cts.Token);
            deactivationTasks.Add(task);
        }

        // Wait for all deactivations (or timeout)
        try
        {
            await Task.WhenAll(deactivationTasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Deactivation timeout reached, forcing remaining deactivations");

            // Count how many didn't complete gracefully
            forcedCount = deactivationTasks.Count(t => !t.IsCompletedSuccessfully);

            // Force deactivate remaining (implementation needed in Catalog)
            foreach (var activation in activationsToDeactivate)
            {
                _catalog.ForceDeactivate(activation.GrainId);
            }
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Deactivated {Count} grains in {Duration}ms ({Forced} forced)",
            activationsToDeactivate.Count,
            stopwatch.ElapsedMilliseconds,
            forcedCount);

        return new GrainDeactivationResult
        {
            Success = true,
            TotalGrainsDeactivated = activationsToDeactivate.Count,
            DeactivatedPerType = deactivatedPerType,
            Errors = new List<string>(),
            Duration = stopwatch.Elapsed,
            ForcedDeactivations = forcedCount
        };
    }

    private async Task DeactivateGrainAsync(
        IGrainContext activation,
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.DeactivateAsync(activation, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating grain {GrainId}", activation.GrainId);
        }
    }

    public bool HasActiveGrains(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = grainTypes.ToHashSet();
        return _catalog.GetAllActivations().Any(a => grainTypeSet.Contains(a.GrainId.Type));
    }

    public IReadOnlyDictionary<GrainType, int> GetActiveGrainCounts(IEnumerable<GrainType> grainTypes)
    {
        var grainTypeSet = grainTypes.ToHashSet();
        var counts = new Dictionary<GrainType, int>();

        foreach (var activation in _catalog.GetAllActivations())
        {
            if (grainTypeSet.Contains(activation.GrainId.Type))
            {
                if (!counts.ContainsKey(activation.GrainId.Type))
                    counts[activation.GrainId.Type] = 0;

                counts[activation.GrainId.Type]++;
            }
        }

        return counts;
    }
}
```

**Catalog modifications needed**:
```csharp
// In src/Orleans.Runtime/Catalog/Catalog.cs
public IEnumerable<IGrainContext> GetAllActivations()
{
    // Return all active grain contexts
    // Implementation depends on internal Catalog structure
}

public void ForceDeactivate(GrainId grainId)
{
    // Immediately deactivate without waiting for graceful shutdown
}
```

---

### C. Cache Removal APIs (Enhancement)

Current implementation has **InvalidateActivator()** (clears cache entry). Need **RemoveActivator()** (full removal).

**File**: `src/Orleans.Runtime/Activation/IGrainContextActivator.cs`

**Current** (~line 175):
```csharp
internal void InvalidateActivator(GrainType grainType)
{
    lock (_lockObj)
    {
        _activators = _activators.Remove(grainType);
    }
}
```

**Enhancement** (already sufficient for removal - just rename/clarify):
```csharp
/// <summary>
/// Removes the cached activator for the specified grain type.
/// Use this for type unloading; cache will be rebuilt on next access.
/// </summary>
internal void RemoveActivator(GrainType grainType)
{
    lock (_lockObj)
    {
        _activators = _activators.Remove(grainType);
    }
}

// Keep InvalidateActivator as alias for backward compatibility
internal void InvalidateActivator(GrainType grainType) => RemoveActivator(grainType);
```

Similar pattern for:
- `GrainTypeSharedContextResolver.RemoveContext()`
- `CodecProvider` - need removal methods (use reflection as fallback)

---

### D. Manifest Removal APIs

**File**: `src/Orleans.Runtime/Manifest/GrainClassMap.cs`

**Add**:
```csharp
/// <summary>
/// Removes the specified grain types from the map.
/// </summary>
internal void RemoveTypes(IEnumerable<GrainType> grainTypes)
{
    var current = _types;
    var builder = current.ToBuilder();

    foreach (var grainType in grainTypes)
    {
        builder.Remove(grainType);
    }

    _types = builder.ToImmutable();
}
```

**File**: `src/Orleans.Runtime/Manifest/SiloManifestProvider.cs`

**Add**:
```csharp
/// <summary>
/// Updates the manifest by removing the specified grain types.
/// </summary>
internal GrainManifest RemoveFromManifest(
    IEnumerable<Type> grainClassesToRemove,
    IEnumerable<Type> grainInterfacesToRemove)
{
    lock (_updateLock)
    {
        var currentManifest = _siloManifest;

        // Remove grain classes
        var updatedGrains = currentManifest.Grains
            .Where(kvp => !grainClassesToRemove.Any(t => kvp.Value.GrainClass == t.FullName))
            .ToImmutableDictionary();

        // Remove interfaces
        var updatedInterfaces = currentManifest.Interfaces
            .Where(kvp => !grainInterfacesToRemove.Any(t => kvp.Key == t.FullName))
            .ToImmutableDictionary();

        var newManifest = new GrainManifest(updatedGrains, updatedInterfaces);
        _siloManifest = newManifest;

        // Update GrainClassMap
        _grainClassMap.RemoveTypes(updatedGrains.Keys);

        return newManifest;
    }
}
```

---

### E. IDynamicGrainUnloader Interface (NEW)

**File**: `src/Orleans.Runtime/DynamicGrains/IDynamicGrainUnloader.cs` (NEW)

```csharp
namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Service for unloading grain assemblies at runtime.
/// </summary>
public interface IDynamicGrainUnloader
{
    /// <summary>
    /// Unloads a dynamically loaded grain assembly from this silo.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to unload.</param>
    /// <param name="timeout">Maximum time to wait for graceful grain deactivation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing unload status and details.</returns>
    Task<GrainUnloadResult> UnloadGrainAssemblyAsync(
        string assemblyPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a stream of grain assembly unload events.
    /// </summary>
    IAsyncEnumerable<GrainAssemblyUnloadedEvent> UnloadEvents { get; }
}

public sealed class GrainUnloadResult
{
    public Assembly Assembly { get; init; }
    public IReadOnlyList<GrainType> UnloadedGrainTypes { get; init; }
    public TimeSpan UnloadDuration { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public GrainDeactivationResult DeactivationResult { get; init; }
    public int ActiveGrainsDeactivated { get; init; }
    public bool MemoryReclaimed { get; init; }  // Diagnostic: Did GC collect?
}

public sealed class GrainAssemblyUnloadedEvent
{
    public Assembly Assembly { get; init; }
    public SiloAddress UnloadedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<GrainType> UnloadedGrainTypes { get; init; }
    public MajorMinorVersion ManifestVersion { get; init; }
    public int GrainsDeactivated { get; init; }
}
```

---

### F. DynamicGrainUnloaderService Implementation (NEW)

**File**: `src/Orleans.Runtime/DynamicGrains/DynamicGrainUnloaderService.cs` (NEW)

```csharp
internal sealed class DynamicGrainUnloaderService : IDynamicGrainUnloader, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly DynamicAssemblyLoader _assemblyLoader;
    private readonly IGrainLifecycleManager _lifecycleManager;
    private readonly SiloManifestProvider _manifestProvider;
    private readonly ClusterManifestProvider _clusterManifestProvider;
    private readonly GrainContextActivator _grainContextActivator;
    private readonly GrainTypeSharedContextResolver _sharedContextResolver;
    private readonly GrainReferenceActivator _grainReferenceActivator;
    private readonly ILogger<DynamicGrainUnloaderService> _logger;
    private readonly SiloAddress _siloAddress;
    private readonly Channel<GrainAssemblyUnloadedEvent> _unloadEventsChannel;
    private readonly SemaphoreSlim _unloadSemaphore = new(1, 1);

    private static readonly TimeSpan DefaultDeactivationTimeout = TimeSpan.FromSeconds(30);

    public DynamicGrainUnloaderService(
        DynamicAssemblyLoader assemblyLoader,
        IGrainLifecycleManager lifecycleManager,
        SiloManifestProvider manifestProvider,
        ClusterManifestProvider clusterManifestProvider,
        GrainContextActivator grainContextActivator,
        GrainTypeSharedContextResolver sharedContextResolver,
        GrainReferenceActivator grainReferenceActivator,
        ILocalSiloDetails siloDetails,
        ILogger<DynamicGrainUnloaderService> logger)
    {
        _assemblyLoader = assemblyLoader;
        _lifecycleManager = lifecycleManager;
        _manifestProvider = manifestProvider;
        _clusterManifestProvider = clusterManifestProvider;
        _grainContextActivator = grainContextActivator;
        _sharedContextResolver = sharedContextResolver;
        _grainReferenceActivator = grainReferenceActivator;
        _siloAddress = siloDetails.SiloAddress;
        _logger = logger;
        _unloadEventsChannel = Channel.CreateUnbounded<GrainAssemblyUnloadedEvent>();
    }

    public IAsyncEnumerable<GrainAssemblyUnloadedEvent> UnloadEvents =>
        _unloadEventsChannel.Reader.ReadAllAsync();

    public async Task<GrainUnloadResult> UnloadGrainAssemblyAsync(
        string assemblyPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= DefaultDeactivationTimeout;

        // Only one unload at a time
        await _unloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await UnloadGrainAssemblyInternalAsync(assemblyPath, timeout.Value, cancellationToken);
        }
        finally
        {
            _unloadSemaphore.Release();
        }
    }

    private async Task<GrainUnloadResult> UnloadGrainAssemblyInternalAsync(
        string assemblyPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting dynamic unload of grain assembly: {AssemblyPath}", assemblyPath);

            // Phase 1: Validate & Prepare
            _logger.LogDebug("Phase 1: Validating and preparing for unload");

            var (assembly, metadata) = _assemblyLoader.GetLoadedAssemblyInfo(assemblyPath);
            if (assembly == null)
            {
                return new GrainUnloadResult
                {
                    Success = false,
                    Errors = new[] { "Assembly not loaded or not found" },
                    UnloadDuration = stopwatch.Elapsed
                };
            }

            var grainTypes = metadata.GrainClasses
                .Select(t => GrainType.Create(t.FullName))
                .ToList();

            _logger.LogInformation(
                "Unloading assembly {AssemblyName} with {TypeCount} grain types",
                assembly.GetName().Name,
                grainTypes.Count);

            // Phase 2: Deactivate Active Grains
            _logger.LogDebug("Phase 2: Deactivating active grains");

            var activeCount = _lifecycleManager.GetActiveGrainCounts(grainTypes);
            var totalActive = activeCount.Values.Sum();

            if (totalActive > 0)
            {
                _logger.LogInformation(
                    "Deactivating {ActiveCount} active grain instances across {TypeCount} types",
                    totalActive,
                    activeCount.Count);

                var deactivationResult = await _lifecycleManager.DeactivateGrainTypesAsync(
                    grainTypes,
                    timeout,
                    cancellationToken);

                if (!deactivationResult.Success)
                {
                    return new GrainUnloadResult
                    {
                        Assembly = assembly,
                        Success = false,
                        Errors = deactivationResult.Errors,
                        DeactivationResult = deactivationResult,
                        UnloadDuration = stopwatch.Elapsed
                    };
                }

                _logger.LogInformation(
                    "Successfully deactivated {Count} grains ({Forced} forced)",
                    deactivationResult.TotalGrainsDeactivated,
                    deactivationResult.ForcedDeactivations);
            }
            else
            {
                _logger.LogInformation("No active grains to deactivate");
            }

            // Phase 3: Remove from Caches
            _logger.LogDebug("Phase 3: Removing from caches");

            foreach (var grainType in grainTypes)
            {
                _grainContextActivator.RemoveActivator(grainType);
                _sharedContextResolver.RemoveContext(grainType);
            }

            _grainReferenceActivator.InvalidateCache();

            _logger.LogInformation("Removed {TypeCount} grain types from caches", grainTypes.Count);

            // Phase 4: Update Silo Manifest
            _logger.LogDebug("Phase 4: Updating silo manifest");

            var updatedManifest = _manifestProvider.RemoveFromManifest(
                metadata.GrainClasses,
                metadata.GrainInterfaces);

            _logger.LogInformation("Updated silo manifest, removed {TypeCount} types", grainTypes.Count);

            // Phase 5: Propagate to Cluster
            _logger.LogDebug("Phase 5: Propagating manifest to cluster");

            var propagated = _clusterManifestProvider.UpdateLocalManifest(updatedManifest);
            var newVersion = _clusterManifestProvider.Current.Version;

            if (propagated)
            {
                _logger.LogInformation(
                    "Successfully propagated manifest removal to cluster. New version: {Version}",
                    newVersion);
            }
            else
            {
                _logger.LogWarning("Failed to propagate manifest update to cluster");
            }

            // Phase 6: Unload Assembly
            _logger.LogDebug("Phase 6: Unloading assembly");

            var unloaded = await _assemblyLoader.UnloadAssemblyAsync(assemblyPath);

            if (!unloaded)
            {
                return new GrainUnloadResult
                {
                    Assembly = assembly,
                    Success = false,
                    Errors = new[] { "Failed to unload assembly - may still have references" },
                    UnloadDuration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Assembly unloaded and memory reclaimed");

            // Phase 7: Publish Event
            _logger.LogDebug("Phase 7: Publishing unload event");

            var unloadEvent = new GrainAssemblyUnloadedEvent
            {
                Assembly = assembly,
                UnloadedBy = _siloAddress,
                Timestamp = DateTimeOffset.UtcNow,
                UnloadedGrainTypes = grainTypes,
                ManifestVersion = newVersion,
                GrainsDeactivated = totalActive
            };

            await _unloadEventsChannel.Writer.WriteAsync(unloadEvent, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully completed dynamic unload of assembly {AssemblyName} in {Duration}ms",
                assembly.GetName().Name,
                stopwatch.ElapsedMilliseconds);

            return new GrainUnloadResult
            {
                Assembly = assembly,
                UnloadedGrainTypes = grainTypes,
                UnloadDuration = stopwatch.Elapsed,
                Success = true,
                Errors = new List<string>(),
                ActiveGrainsDeactivated = totalActive,
                MemoryReclaimed = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during dynamic grain unloading from {AssemblyPath}", assemblyPath);

            return new GrainUnloadResult
            {
                Success = false,
                Errors = new[] { $"Unload failed: {ex.Message}" },
                UnloadDuration = stopwatch.Elapsed
            };
        }
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        // Register with silo lifecycle if needed
    }
}
```

---

### G. Service Registration Extensions

**File**: `src/Orleans.Runtime/DynamicGrains/DynamicGrainLoadingExtensions.cs`

**Update to include unloading**:
```csharp
public static class DynamicGrainLoadingExtensions
{
    public static ISiloBuilder AddDynamicGrainLoading(this ISiloBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Existing loading services
            services.AddSingleton<DynamicAssemblyLoader>();
            services.AddSingleton<AssemblyValidator>();
            services.AddSingleton<IDynamicGrainLoader, DynamicGrainLoaderService>();
            services.AddSingleton<DynamicSerializationManager>();

            // NEW: Unloading services
            services.AddSingleton<IGrainLifecycleManager, GrainLifecycleManager>();
            services.AddSingleton<IDynamicGrainUnloader, DynamicGrainUnloaderService>();
        });

        return builder;
    }
}
```

---

## Implementation Roadmap

### Phase 1: DotNetCorePlugins Integration (2 weeks)
**Difficulty**: MEDIUM
**Goal**: Replace `Assembly.LoadFrom()` with `PluginLoader`

**Tasks**:
1. Add `McMaster.NETCore.Plugins` NuGet package
2. Modify `DynamicAssemblyLoader` to use `PluginLoader.CreateFromAssemblyFile()`
3. Define `GetOrleansSharedTypes()` method (comprehensive list)
4. Track `PluginLoader` instances for later disposal
5. Test that existing loading functionality still works
6. Test that assemblies are in collectible contexts
7. Verify type identity (cast tests across boundaries)

**Success Criteria**:
- All existing dynamic loading tests pass
- Assemblies load into collectible `AssemblyLoadContext`
- Type casting works (grain interfaces recognized)
- Serialization works (shared Orleans types)

**Deliverable**: Modified `DynamicAssemblyLoader` using DotNetCorePlugins, fully backward-compatible.

---

### Phase 2: Grain Lifecycle Manager (2-3 weeks)
**Difficulty**: HARD
**Goal**: Deactivate all grains of specific types

**Tasks**:
1. Add `DeactivationReasonCode.TypeUnloading` enum value
2. Create `IGrainLifecycleManager` interface
3. Implement `GrainLifecycleManager` class
4. Add `Catalog.GetAllActivations()` method
5. Add `Catalog.ForceDeactivate()` method
6. Test deactivation with timeout
7. Test deactivation cancellation
8. Verify `OnDeactivateAsync()` is called with correct reason

**Success Criteria**:
- Can enumerate active grains by type
- Graceful deactivation completes successfully
- Timeout forces remaining deactivations
- `OnDeactivateAsync()` called on grain instances
- No dangling grain references after deactivation

**Deliverable**: `GrainLifecycleManager` service with full deactivation support.

---

### Phase 3: Cache Removal APIs (1 week)
**Difficulty**: MEDIUM
**Goal**: Remove grain types from all caches

**Tasks**:
1. Add `GrainClassMap.RemoveTypes()`
2. Clarify `GrainContextActivator.RemoveActivator()` (rename from Invalidate)
3. Add `GrainTypeSharedContextResolver.RemoveContext()`
4. Add cache removal to serialization system (or use invalidation)
5. Test cache removal (verify types not in caches after removal)
6. Test cache rebuild (if type re-loaded, caches rebuild correctly)

**Success Criteria**:
- Types removed from all caches
- No cache lookup succeeds for removed types
- Caches rebuild correctly if type re-added

**Deliverable**: Complete cache removal infrastructure.

---

### Phase 4: Manifest Removal (1 week)
**Difficulty**: MEDIUM
**Goal**: Remove types from silo and cluster manifests

**Tasks**:
1. Add `SiloManifestProvider.RemoveFromManifest()`
2. Update `GrainClassMap.RemoveTypes()` integration
3. Test manifest removal (verify types not in manifest)
4. Test cluster propagation (other silos see removal)
5. Verify routing respects manifest removal (no requests to this silo for removed types)

**Success Criteria**:
- Silo manifest updated (types removed)
- Cluster manifest propagated (other silos notified)
- Manifest version incremented
- Routing layer respects changes

**Deliverable**: Manifest removal with cluster propagation.

---

### Phase 5: Unloader Service Integration (2 weeks)
**Difficulty**: MEDIUM-HARD
**Goal**: Complete unloading orchestration

**Tasks**:
1. Create `IDynamicGrainUnloader` interface
2. Implement `DynamicGrainUnloaderService`
3. Implement 7-phase unload process
4. Add event publishing (UnloadEvents)
5. Integrate with all previous phases
6. Add comprehensive error handling
7. Add diagnostics (verify assembly actually unloaded)
8. Test single-silo unload
9. Test multi-silo cluster unload

**Success Criteria**:
- Complete unload succeeds (all phases)
- Memory reclaimed (verify via diagnostics)
- No errors or warnings
- Cluster manifest updated
- Events published correctly

**Deliverable**: Production-ready `IDynamicGrainUnloader` service.

---

### Phase 6: Testing & Documentation (2 weeks)
**Difficulty**: MEDIUM
**Goal**: Comprehensive testing and documentation

**Tasks**:
1. **Unit tests**: Each component (lifecycle manager, cache removal, etc.)
2. **Integration tests**: Full unload in single silo
3. **Multi-silo tests**: Load on silo A, unload on silo A, verify cluster
4. **Stress tests**: Repeated load/unload cycles
5. **Memory leak tests**: Verify no leaks over 1000+ cycles
6. **Concurrent tests**: Load and unload simultaneously
7. **Failure tests**: Partial failures, timeout scenarios
8. **Documentation**: Usage guide (add to `DYNAMIC_GRAIN_LOADING_USAGE.md`)
9. **Sample application**: Demo plugin system with load/unload

**Success Criteria**:
- All tests pass
- No memory leaks detected
- Documentation complete
- Sample app demonstrates feature

**Deliverable**: Tested, documented, production-ready unloading feature.

---

## Critical Risks & Mitigations

### 1. Dangling References (CRITICAL)

**Risk**: Even one reference to a type prevents assembly unloading → memory leak.

**Detection**:
- Use `WeakReference` diagnostics
- Check `AssemblyLoadContext.IsCollectible`
- Monitor GC generations after unload

**Mitigation**:
```csharp
// Diagnostic helper
public class UnloadDiagnostics
{
    public static async Task<bool> VerifyAssemblyUnloaded(WeakReference assemblyRef)
    {
        for (int i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);

            if (!assemblyRef.IsAlive)
                return true;  // Successfully collected
        }

        return false;  // Still in memory - leak!
    }
}

// Usage
var weakRef = new WeakReference(assembly);
await loader.Dispose();
var unloaded = await UnloadDiagnostics.VerifyAssemblyUnloaded(weakRef);

if (!unloaded)
    _logger.LogError("MEMORY LEAK: Assembly not collected!");
```

**Prevention**:
- Thorough cache cleanup (Phase 3)
- Complete grain deactivation (Phase 2)
- No static references to grain types
- Use shared types for Orleans dependencies

---

### 2. Active Grain Deactivation Failures (HIGH)

**Risk**: Grains don't deactivate within timeout, force-deactivation causes state loss.

**Mitigation**:
- Configurable timeout (per-tenant, per-type)
- Warning logs before timeout
- Grain developers handle `TypeUnloading` reason
- Retry mechanism for critical grains
- Postpone unload if critical grains active

**Best practice**:
```csharp
// In grain implementation
public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
{
    if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
    {
        _logger.LogWarning("Type unloading! Saving critical state quickly...");

        // Save only critical state (fast)
        await SaveCriticalStateAsync();

        // Don't do expensive cleanup - timeout is tight
    }

    await base.OnDeactivateAsync(reason, ct);
}
```

---

### 3. Type Identity Across Contexts (HIGH)

**Risk**: Without shared types, `IMyGrain` in plugin ≠ `IMyGrain` in host → cast failures.

**Mitigation**:
- Comprehensive shared types list (Phase 1)
- Test suite validates type identity
- Compilation errors guide missing shared types
- Documentation on adding shared types

**Validation test**:
```csharp
[Fact]
public void TypeIdentity_GrainInterfacesMatch()
{
    var loader = PluginLoader.CreateFromAssemblyFile(
        "TestGrains.dll",
        sharedTypes: GetOrleansSharedTypes(),
        isUnloadable: true);

    var assembly = loader.LoadDefaultAssembly();
    var pluginGrainType = assembly.GetType("TestGrains.MyGrain");

    // Should be assignable because IGrain is shared
    Assert.True(typeof(IGrain).IsAssignableFrom(pluginGrainType));
}
```

---

### 4. Cluster Inconsistency During Unload (MEDIUM)

**Risk**: Message arrives for unloaded type during unload window.

**Mitigation**:
- Manifest propagation happens BEFORE actual unload (Phase 5 before Phase 6)
- Routing layer checks manifest version
- Grace period: Wait N seconds after manifest propagation before unload
- Rejected messages get retried on other silos

**Implementation**:
```csharp
// After Phase 5 (manifest propagation)
_logger.LogInformation("Waiting {Delay}ms for manifest propagation...", propagationDelay);
await Task.Delay(propagationDelay, cancellationToken);

// Now Phase 6 (actual unload)
```

---

### 5. Concurrent Load/Unload (MEDIUM)

**Risk**: Load same assembly while unloading, or unload while loading.

**Mitigation**:
- Semaphore in loader (one load at a time)
- Semaphore in unloader (one unload at a time)
- Check state before operations
- Atomic state transitions

**Already implemented**:
```csharp
// In DynamicGrainLoaderService
private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

// In DynamicGrainUnloaderService
private readonly SemaphoreSlim _unloadSemaphore = new(1, 1);
```

**Additional check**:
```csharp
// Before unload
if (_assemblyLoader.IsCurrentlyLoading(assemblyPath))
{
    return new GrainUnloadResult
    {
        Success = false,
        Errors = new[] { "Assembly is currently being loaded, cannot unload" }
    };
}
```

---

### 6. Dependency Chains (MEDIUM)

**Risk**: Plugin A depends on Plugin B. Unload B while A still loaded → crash.

**Mitigation**:
- Track dependencies between plugins
- Prevent unload if dependents exist
- Cascade unload (unload A first, then B)
- Documentation: Keep plugins independent

**Future enhancement**:
```csharp
public interface IDynamicGrainUnloader
{
    // Check if safe to unload
    Task<UnloadFeasibilityResult> CanUnloadAsync(string assemblyPath);

    // Cascade unload
    Task<GrainUnloadResult> UnloadWithDependentsAsync(string assemblyPath);
}
```

---

### 7. Performance During Unload (LOW)

**Risk**: Unload causes latency spike (deactivation, cache cleanup, GC).

**Mitigation**:
- Unload during low-traffic windows
- Spread deactivation over time (batch deactivation)
- Background GC (don't block on collection)
- Monitoring/alerting on unload duration

**Best practice**:
```csharp
// Unload during maintenance window
if (DateTime.UtcNow.Hour >= 2 && DateTime.UtcNow.Hour <= 4)
{
    await grainUnloader.UnloadGrainAssemblyAsync(path);
}
```

---

## References

### External Resources

1. **DotNetCorePlugins**
   - GitHub: https://github.com/natemcmaster/DotNetCorePlugins
   - NuGet: `McMaster.NETCore.Plugins`
   - Blog: https://natemcmaster.com/blog/2018/07/25/netcore-plugins/

2. **AssemblyLoadContext Documentation**
   - Microsoft Docs: https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext
   - Collectible contexts: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support

### Orleans Components Referenced

1. **Lifecycle & Deactivation**
   - `src/Orleans.Core.Abstractions/Core/Grain.cs:164` - `OnDeactivateAsync()`
   - `src/Orleans.Core.Abstractions/Core/IGrainBase.cs:242` - `DeactivationReasonCode` enum
   - `src/Orleans.Core.Abstractions/Lifecycle/IGrainLifecycle.cs`

2. **Catalog (Activation Management)**
   - `src/Orleans.Runtime/Catalog/Catalog.cs` - Active grain tracking
   - Need: `GetAllActivations()`, `ForceDeactivate()` methods

3. **Manifest System**
   - `src/Orleans.Runtime/Manifest/GrainClassMap.cs` - Type mappings
   - `src/Orleans.Runtime/Manifest/SiloManifestProvider.cs` - Local manifest
   - `src/Orleans.Runtime/Manifest/ClusterManifestProvider.cs` - Cluster manifest

4. **Caches**
   - `src/Orleans.Runtime/Activation/IGrainContextActivator.cs` - Activator cache
   - `src/Orleans.Runtime/Catalog/GrainTypeSharedContext.cs` - Context cache
   - `src/Orleans.Serialization/Serializers/CodecProvider.cs` - Codec cache

5. **Existing Dynamic Loading**
   - `src/Orleans.Runtime/DynamicGrains/DynamicAssemblyLoader.cs` - Current loader
   - `src/Orleans.Runtime/DynamicGrains/DynamicGrainLoaderService.cs` - Loading orchestration
   - `DYNAMIC_GRAIN_LOADING_RESEARCH.md` - Loading research doc
   - `DYNAMIC_GRAIN_LOADING_IMPLEMENTATION.md` - Loading implementation doc
   - `DYNAMIC_GRAIN_LOADING_USAGE.md` - Loading usage guide

---

## Appendix A: Complete Code Samples

### A.1 Grain Handling Type Unloading

```csharp
public class PaymentGrain : Grain, IPaymentGrain
{
    private readonly ILogger<PaymentGrain> _logger;
    private decimal _balance;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        _balance = await LoadStateAsync();
        await base.OnActivateAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Check if type is being unloaded
        if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
        {
            _logger.LogWarning(
                "Payment grain type being unloaded! Saving state for {GrainId}",
                this.GetPrimaryKeyString());

            // Save critical state quickly (timeout is tight!)
            await SaveStateAsync(_balance);

            // Don't do expensive cleanup - will timeout
        }
        else if (reason.ReasonCode == DeactivationReasonCode.ShuttingDown)
        {
            _logger.LogInformation("Silo shutting down, saving state");
            await SaveStateAsync(_balance);
        }
        else
        {
            // Normal deactivation (idle, etc.)
            await SaveStateAsync(_balance);
        }

        await base.OnDeactivateAsync(reason, ct);
    }

    public Task<decimal> GetBalance() => Task.FromResult(_balance);

    public Task Deposit(decimal amount)
    {
        _balance += amount;
        return Task.CompletedTask;
    }

    private Task<decimal> LoadStateAsync()
    {
        // Load from storage
        return Task.FromResult(0m);
    }

    private Task SaveStateAsync(decimal balance)
    {
        // Save to storage
        return Task.CompletedTask;
    }
}
```

### A.2 Admin Controller for Unloading

```csharp
[ApiController]
[Route("api/admin/grains")]
[Authorize(Roles = "Administrator")]
public class GrainManagementController : ControllerBase
{
    private readonly IDynamicGrainLoader _loader;
    private readonly IDynamicGrainUnloader _unloader;
    private readonly ILogger<GrainManagementController> _logger;

    public GrainManagementController(
        IDynamicGrainLoader loader,
        IDynamicGrainUnloader unloader,
        ILogger<GrainManagementController> logger)
    {
        _loader = loader;
        _unloader = unloader;
        _logger = logger;
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadGrainAssembly([FromBody] LoadRequest request)
    {
        _logger.LogWarning(
            "User {User} loading assembly {Path}",
            User.Identity?.Name,
            request.AssemblyPath);

        var result = await _loader.LoadGrainAssemblyAsync(request.AssemblyPath);

        return result.Success
            ? Ok(new { result.Success, result.GrainTypes, result.LoadDuration })
            : BadRequest(new { result.Success, result.Errors });
    }

    [HttpPost("unload")]
    public async Task<IActionResult> UnloadGrainAssembly([FromBody] UnloadRequest request)
    {
        _logger.LogWarning(
            "User {User} unloading assembly {Path}",
            User.Identity?.Name,
            request.AssemblyPath);

        var timeout = request.TimeoutSeconds.HasValue
            ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
            : TimeSpan.FromSeconds(30);

        var result = await _unloader.UnloadGrainAssemblyAsync(
            request.AssemblyPath,
            timeout);

        if (result.Success)
        {
            return Ok(new
            {
                result.Success,
                result.UnloadedGrainTypes,
                result.ActiveGrainsDeactivated,
                result.UnloadDuration,
                result.MemoryReclaimed
            });
        }
        else
        {
            return BadRequest(new { result.Success, result.Errors });
        }
    }

    [HttpGet("loaded")]
    public IActionResult GetLoadedAssemblies()
    {
        // Return list of currently loaded dynamic assemblies
        // Implementation depends on tracking in DynamicAssemblyLoader
        return Ok(new { assemblies = new[] { "TenantA.Grains", "TenantB.Grains" } });
    }
}

public class LoadRequest
{
    public string AssemblyPath { get; set; }
}

public class UnloadRequest
{
    public string AssemblyPath { get; set; }
    public int? TimeoutSeconds { get; set; }
}
```

### A.3 Background Service Monitoring Unload Events

```csharp
public class GrainUnloadMonitor : BackgroundService
{
    private readonly IDynamicGrainUnloader _unloader;
    private readonly ILogger<GrainUnloadMonitor> _logger;

    public GrainUnloadMonitor(
        IDynamicGrainUnloader unloader,
        ILogger<GrainUnloadMonitor> logger)
    {
        _unloader = unloader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Grain unload monitor started");

        await foreach (var unloadEvent in _unloader.UnloadEvents.WithCancellation(stoppingToken))
        {
            _logger.LogInformation(
                "UNLOAD EVENT: Assembly {AssemblyName} unloaded by silo {SiloAddress} " +
                "at {Timestamp}. {TypeCount} types unloaded, {GrainCount} grains deactivated. " +
                "Manifest version: {Version}",
                unloadEvent.Assembly.GetName().Name,
                unloadEvent.UnloadedBy,
                unloadEvent.Timestamp,
                unloadEvent.UnloadedGrainTypes.Count,
                unloadEvent.GrainsDeactivated,
                unloadEvent.ManifestVersion);

            // Could trigger:
            // - Metrics update
            // - Notification to monitoring system
            // - Audit log entry
            // - Cleanup of related resources
        }
    }
}

// Register in Startup.cs:
builder.Services.AddHostedService<GrainUnloadMonitor>();
```

---

## Appendix B: Testing Strategy

### B.1 Unit Test Structure

```csharp
public class GrainLifecycleManagerTests
{
    [Fact]
    public async Task DeactivateGrainTypes_WithActiveGrains_DeactivatesAll()
    {
        // Arrange
        var catalog = CreateMockCatalog(activeGrainCount: 10);
        var manager = new GrainLifecycleManager(catalog, logger);
        var grainTypes = new[] { GrainType.Create("MyGrain") };

        // Act
        var result = await manager.DeactivateGrainTypesAsync(
            grainTypes,
            TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(10, result.TotalGrainsDeactivated);
        Assert.Equal(0, result.ForcedDeactivations);
    }

    [Fact]
    public async Task DeactivateGrainTypes_WithTimeout_ForcesDeactivation()
    {
        // Arrange: Grains that take 10 seconds to deactivate
        var catalog = CreateSlowMockCatalog(deactivationDelay: TimeSpan.FromSeconds(10));
        var manager = new GrainLifecycleManager(catalog, logger);
        var grainTypes = new[] { GrainType.Create("SlowGrain") };

        // Act: Timeout after 1 second
        var result = await manager.DeactivateGrainTypesAsync(
            grainTypes,
            TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ForcedDeactivations > 0);
    }
}
```

### B.2 Integration Test

```csharp
public class DynamicGrainUnloadingIntegrationTests
{
    [Fact]
    public async Task LoadAndUnload_CompleteCycle_Success()
    {
        // Arrange
        using var host = await StartTestSilo();
        var loader = host.Services.GetRequiredService<IDynamicGrainLoader>();
        var unloader = host.Services.GetRequiredService<IDynamicGrainUnloader>();
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        // Act: Load
        var loadResult = await loader.LoadGrainAssemblyAsync("TestGrains.dll");
        Assert.True(loadResult.Success);

        // Act: Use grain
        var grain = grainFactory.GetGrain<ITestGrain>("test-1");
        var response = await grain.SayHello("World");
        Assert.Equal("Hello, World!", response);

        // Act: Unload
        var unloadResult = await unloader.UnloadGrainAssemblyAsync("TestGrains.dll");

        // Assert
        Assert.True(unloadResult.Success);
        Assert.Equal(1, unloadResult.ActiveGrainsDeactivated);
        Assert.True(unloadResult.MemoryReclaimed);

        // Act: Try to use grain after unload (should fail)
        await Assert.ThrowsAsync<OrleansException>(async () =>
        {
            await grain.SayHello("Again");
        });
    }
}
```

---

## Conclusion

Dynamic grain unloading is **feasible but complex**. Success requires:

1. ✅ **DotNetCorePlugins** for collectible assembly loading
2. ✅ **Grain lifecycle management** for deactivation
3. ✅ **Comprehensive reference cleanup** across 6+ subsystems
4. ✅ **Careful orchestration** with 7-phase unload process
5. ✅ **Extensive testing** for memory leaks and edge cases

**Recommended Timeline**: 3-4 months for production-ready implementation

**Team Size**: 1-2 senior engineers familiar with Orleans internals and .NET AssemblyLoadContext

**Risk Level**: HIGH - requires careful attention to reference management and testing

**Reward**: True hot-swapping, memory reclamation, and production flexibility for Orleans applications

---

## Document Metadata

**Created**: 2025-11-21
**Author**: Claude (Anthropic)
**Orleans Version**: 9.1.0
**Total Length**: ~1,200 lines / ~50 pages
**Related Documents**:
- `DYNAMIC_GRAIN_LOADING_RESEARCH.md`
- `DYNAMIC_GRAIN_LOADING_IMPLEMENTATION.md`
- `DYNAMIC_GRAIN_LOADING_USAGE.md`

**Next Step**: Begin Phase 1 (DotNetCorePlugins Integration)
