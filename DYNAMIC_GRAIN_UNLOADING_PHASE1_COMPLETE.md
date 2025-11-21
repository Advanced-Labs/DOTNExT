# Phase 1 Complete: DotNetCorePlugins Integration

**Completion Date**: 2025-11-21
**Status**: ✅ **COMPLETE** (Implementation)
**Commit**: `5aa92e6`

---

## Summary

Phase 1 successfully integrates **McMaster.NETCore.Plugins** into the dynamic grain loading system, replacing the default assembly loading mechanism with collectible `AssemblyLoadContext` support. This is the foundation for dynamic grain unloading.

---

## Changes Implemented

### 1. NuGet Package Addition
**File**: `src/Orleans.Runtime/Orleans.Runtime.csproj`

Added:
```xml
<PackageReference Include="McMaster.NETCore.Plugins" Version="2.0.0" />
```

### 2. DynamicAssemblyLoader Rewrite
**File**: `src/Orleans.Runtime/DynamicGrains/DynamicAssemblyLoader.cs`

**Key modifications**:

#### New Dependencies
```csharp
using McMaster.NETCore.Plugins;
private readonly ConcurrentDictionary<string, PluginLoader> _pluginLoaders = new();
private readonly ConcurrentDictionary<string, AssemblyLoadMetadata> _assemblyMetadata = new();
```

#### PluginLoader Integration (LoadAssemblyAsync)
**Before**:
```csharp
assembly = Assembly.LoadFrom(assemblyPath);  // Non-collectible
```

**After**:
```csharp
loader = PluginLoader.CreateFromAssemblyFile(
    assemblyFile: assemblyPath,
    sharedTypes: GetOrleansSharedTypes(),
    isUnloadable: true,
    configure: config =>
    {
        config.PreferSharedTypes = true;
        config.IsUnloadable = true;
        config.LoadInMemory = false;
    });

assembly = loader.LoadDefaultAssembly();
```

**Benefits**:
- Assemblies load into collectible context
- Shared Orleans types ensure type identity
- Ready for unloading

#### Shared Types Method (GetOrleansSharedTypes)
Comprehensive list of 60+ Orleans and .NET types that must be shared:

**Categories**:
- Core grain abstractions (IGrain, IAddressable, etc.)
- Base grain classes (Grain)
- Grain references (GrainReference, GrainId, GrainType)
- Grain context & runtime types
- Timers & reminders
- Common .NET types (Task, collections, value types)
- Exceptions and attributes

**Why**: Ensures plugin grains can cast to host's type definitions, preventing type identity issues.

#### Unload Method (UnloadAssemblyAsync) - NEW
```csharp
public async Task<bool> UnloadAssemblyAsync(string assemblyPath)
{
    // Remove from tracking
    if (!_pluginLoaders.TryRemove(assemblyPath, out var loader))
        return false;

    _loadedAssemblies.TryRemove(assemblyPath, out _);
    _assemblyMetadata.TryRemove(assemblyPath, out _);

    // Dispose triggers AssemblyLoadContext.Unload()
    loader.Dispose();

    // Force GC to reclaim memory (3 cycles)
    for (int i = 0; i < 3; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);
    }

    return true;
}
```

**Note**: This is the low-level unload mechanism. Full unloading orchestration (grain deactivation, cache cleanup) comes in later phases.

#### Helper Methods - NEW
```csharp
// Query loaded assembly info
public (Assembly Assembly, AssemblyLoadMetadata Metadata) GetLoadedAssemblyInfo(string assemblyPath)

// Check if assembly is loaded
public bool IsAssemblyLoaded(string assemblyPath)

// Get all loaded assembly paths
public IEnumerable<string> GetLoadedAssemblyPaths()
```

**Purpose**: Support future unloader service queries and diagnostics.

---

## Technical Details

### Collectible AssemblyLoadContext
- **isUnloadable: true** → Creates collectible context
- **PreferSharedTypes: true** → Plugin uses host's types where possible
- **LoadInMemory: false** → Better unloading behavior

### Shared Types Strategy
Orleans types are marked as "shared" so:
1. Plugin doesn't load its own copy of Orleans assemblies
2. Type identity maintained: `IGrain` in plugin = `IGrain` in host
3. Casting works: `var grain = (IMyGrain)obj` succeeds
4. Serialization recognizes types correctly

### Memory Management
Three-cycle GC collection:
```csharp
for (int i = 0; i < 3; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    await Task.Delay(100);
}
```

**Why**: Increases likelihood of assembly collection. AssemblyLoadContext is collected only when:
- No references remain to types from that context
- GC runs and detects no roots

---

## Backward Compatibility

✅ **Fully backward compatible** with existing dynamic loading:
- Same public API (`LoadAssemblyAsync`)
- Same return types
- Same validation logic
- Existing tests should pass unchanged

**New**: Additional methods for unloading (non-breaking additions)

---

## Testing Requirements

⚠️ **Cannot test in current environment** (no dotnet SDK installed)

**Required tests** (run in proper dev environment):

### 1. Compilation Test
```bash
dotnet build src/Orleans.Runtime/Orleans.Runtime.csproj
```

**Expected**: Clean build with no errors

### 2. Existing Loading Tests
```bash
dotnet test --filter "Category=DynamicLoading"
```

**Expected**: All existing dynamic loading tests pass

### 3. Collectible Context Test
```csharp
[Fact]
public async Task LoadAssembly_UsesCollectibleContext()
{
    var loader = new DynamicAssemblyLoader(...);
    var (assembly, _, _) = await loader.LoadAssemblyAsync("TestGrains.dll");

    // Check assembly is in collectible context
    var alc = AssemblyLoadContext.GetLoadContext(assembly);
    Assert.NotNull(alc);
    Assert.NotEqual(AssemblyLoadContext.Default, alc);
    Assert.True(alc.IsCollectible);
}
```

### 4. Type Identity Test
```csharp
[Fact]
public async Task LoadAssembly_TypeIdentityPreserved()
{
    var loader = new DynamicAssemblyLoader(...);
    var (assembly, _, _) = await loader.LoadAssemblyAsync("TestGrains.dll");

    var grainType = assembly.GetType("TestGrains.MyGrain");

    // Should be assignable because IGrain is shared
    Assert.True(typeof(IGrain).IsAssignableFrom(grainType));
    Assert.True(typeof(Grain).IsAssignableFrom(grainType));
}
```

### 5. Unload Test (Basic)
```csharp
[Fact]
public async Task UnloadAssembly_ReleasesMemory()
{
    var loader = new DynamicAssemblyLoader(...);
    var (assembly, _, _) = await loader.LoadAssemblyAsync("TestGrains.dll");

    var weakRef = new WeakReference(assembly);

    // Unload
    var result = await loader.UnloadAssemblyAsync("TestGrains.dll");

    Assert.True(result);

    // Give GC time to collect
    for (int i = 0; i < 10; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);
    }

    // Assembly should be collected
    Assert.False(weakRef.IsAlive);
}
```

---

## Known Limitations (Current Phase)

1. ❌ **Active grains not deactivated** - Unloading doesn't deactivate active grain instances (Phase 2)
2. ❌ **Caches not cleaned** - Activators, contexts, codecs still reference types (Phase 3)
3. ❌ **Manifest not updated** - Silo/cluster manifests not updated on unload (Phase 4)
4. ❌ **No orchestration** - `UnloadAssemblyAsync` is low-level only (Phase 5)

**Result**: Calling `UnloadAssemblyAsync` directly will **fail** if:
- Any grain instances are active
- Any caches still reference types
- Assembly will NOT unload, memory will leak

**Solution**: Wait for Phase 5 (DynamicGrainUnloaderService) which orchestrates all cleanup steps.

---

## Next Phase

**Phase 2: Grain Lifecycle Manager**
- Add `DeactivationReasonCode.TypeUnloading` enum value
- Implement `IGrainLifecycleManager` for bulk grain deactivation
- Access Catalog to enumerate and deactivate grains
- Test graceful deactivation with timeout

**Estimated Duration**: 2-3 weeks

---

## Files Modified

1. `src/Orleans.Runtime/Orleans.Runtime.csproj` - Added NuGet package
2. `src/Orleans.Runtime/DynamicGrains/DynamicAssemblyLoader.cs` - Complete rewrite

**Total changes**: +205 lines, -12 lines

---

## Validation Checklist

- [x] NuGet package added
- [x] PluginLoader integration implemented
- [x] GetOrleansSharedTypes() comprehensive list
- [x] UnloadAssemblyAsync() implemented
- [x] Helper methods added
- [x] Code committed and pushed
- [ ] **Compilation verified** (pending proper environment)
- [ ] **Tests pass** (pending proper environment)
- [ ] **Type identity verified** (pending proper environment)

---

## How to Verify (For Dev Team)

1. **Pull branch**: `git pull origin claude/orleans-dynamic-grain-loading-017dZi9NJjvsQRCaoeo28M3z`
2. **Restore packages**: `dotnet restore`
3. **Build**: `dotnet build src/Orleans.Runtime/Orleans.Runtime.csproj`
4. **Run tests**: `dotnet test --filter "Category=DynamicLoading"`
5. **Check for errors**: Compilation should succeed, tests should pass

If any compilation errors occur, likely missing shared types in `GetOrleansSharedTypes()` - add the missing type.

---

## Notes

### Shared Types Expansion

If you encounter `InvalidCastException` or type identity issues during testing:

1. **Identify missing type** from exception message
2. **Add to GetOrleansSharedTypes()** in DynamicAssemblyLoader.cs:
   ```csharp
   typeof(YourMissingType),  // Add with comment explaining why
   ```
3. **Rebuild and retest**

### PluginLoader Configuration

Current config is optimal for Orleans:
- `PreferSharedTypes = true` → Use host types
- `IsUnloadable = true` → Enable unloading
- `LoadInMemory = false` → Better for unloading

**Don't change** unless you have a specific reason.

---

## References

- **DotNetCorePlugins**: https://github.com/natemcmaster/DotNetCorePlugins
- **Research Doc**: `DYNAMIC_GRAIN_UNLOADING_RESEARCH.md`
- **Commit**: `5aa92e6`

---

**Status**: Phase 1 implementation complete, pending compilation and runtime testing in proper environment.

**Next**: Proceed to Phase 2 when testing confirms Phase 1 works correctly.
