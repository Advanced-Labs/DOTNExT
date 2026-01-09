# Stateful Grain Auto-Persistence Feature

## Overview

This document proposes a new feature for NewOrleans that enables **automatic persistence of all grain instance members** without requiring explicit state class declaration. Developers can opt-in via a base class (`StatefulGrain`) or attribute (`[Stateful]`), and Orleans will automatically persist and restore all "persistable" members on activation/deactivation.

---

## Problem Statement

Current Orleans persistence requires explicit declaration:

```csharp
// Method A: Separate state class
public class MyState { public int Counter { get; set; } }

[StorageProvider(ProviderName = "Default")]
public class MyGrain : Grain<MyState>, IMyGrain
{
    public Task Increment() { State.Counter++; return WriteStateAsync(); }
}

// Method B: Constructor injection
public class MyGrain : Grain, IMyGrain
{
    private readonly IPersistentState<MyState> _state;
    public MyGrain([PersistentState("state")] IPersistentState<MyState> state) => _state = state;
}
```

**Pain points:**
1. Boilerplate: Must define separate state classes mirroring grain fields
2. Sync burden: Keeping state class in sync with grain members
3. Indirection: `State.X` instead of `this.X`
4. Not intuitive for developers from other actor frameworks (Akka.NET Persistence, Dapr actors)

---

## Proposed Solution

Allow grains to persist their own fields/properties directly:

```csharp
[Stateful]  // Or inherit from StatefulGrain
public class MyGrain : Grain, IMyGrain
{
    private int _counter;           // Auto-persisted
    public string Name { get; set; } // Auto-persisted

    [NonPersistent]
    private ILogger _logger;        // Excluded from persistence

    public async Task Increment()
    {
        _counter++;
        await SaveStateAsync();     // Explicit save
    }
}
```

---

## Design Options

### Option 1: Source Generator (Compile-Time) ⭐ Recommended for static grains

**Mechanism:**
- Source generator detects `[Stateful]` attribute or `StatefulGrain` base class
- Generates a "shadow state" class containing all persistable members
- Generates sync code: grain ↔ shadow state
- Hooks into `GrainLifecycleStage.SetupState` for load, manual `SaveStateAsync()` for save

**Generated code example:**
```csharp
// User writes:
[Stateful]
public partial class MyGrain : Grain, IMyGrain
{
    private int _counter;
    public string Name { get; set; }
}

// Generator produces:
[GenerateSerializer]
internal sealed class MyGrain__GeneratedState
{
    [Id(0)] public int _counter;
    [Id(1)] public string Name;
}

partial class MyGrain : ILifecycleParticipant<IGrainLifecycle>
{
    private IStorage<MyGrain__GeneratedState> __storage;

    void ILifecycleParticipant<IGrainLifecycle>.Participate(IGrainLifecycle lifecycle)
    {
        lifecycle.Subscribe<MyGrain>(
            GrainLifecycleStage.SetupState,
            OnSetupState,
            OnTeardownState);
    }

    private async Task OnSetupState(CancellationToken ct)
    {
        __storage = Runtime.GetStorage<MyGrain__GeneratedState>(GrainContext);
        await __storage.ReadStateAsync();
        // Sync: storage → grain
        this._counter = __storage.State._counter;
        this.Name = __storage.State.Name;
    }

    protected Task SaveStateAsync()
    {
        // Sync: grain → storage
        __storage.State._counter = this._counter;
        __storage.State.Name = this.Name;
        return __storage.WriteStateAsync();
    }
}
```

**Pros:**
- Zero runtime reflection overhead
- Type-safe, compile-time errors for non-serializable members
- Works with existing Orleans storage providers
- IDE support (generated code is visible)

**Cons:**
- Requires `partial` class declaration
- Doesn't work for dynamically loaded grains (no generator at runtime)
- Recompilation needed when grain changes

---

### Option 2: Runtime Reflection + Dynamic Serializer ⭐ Recommended for dynamic grains

**Mechanism:**
- At grain activation, reflect over the grain type to discover persistable members
- Build a runtime "member accessor" for each persistable member
- Serialize to `Dictionary<string, object>` or custom dynamic structure
- Cache the reflection metadata per grain type

**Key components:**
```csharp
internal class StatefulGrainMetadata
{
    public Type GrainType { get; }
    public IReadOnlyList<MemberAccessor> PersistableMembers { get; }

    // Cached per grain type
    private static ConcurrentDictionary<Type, StatefulGrainMetadata> _cache = new();
}

internal abstract class MemberAccessor
{
    public string Name { get; }
    public Type MemberType { get; }
    public abstract object GetValue(object grain);
    public abstract void SetValue(object grain, object value);
}
```

**State storage format:**
```csharp
[GenerateSerializer]
internal class DynamicGrainState
{
    [Id(0)] public Dictionary<string, byte[]> Members { get; set; } = new();
    [Id(1)] public string GrainTypeName { get; set; }
    [Id(2)] public int SchemaVersion { get; set; }
}
```

**Pros:**
- Works with dynamically loaded grains
- No source generator dependency
- Flexible schema evolution

**Cons:**
- Runtime reflection overhead (mitigated by caching)
- Boxing for value types
- Runtime errors instead of compile-time

---

### Option 3: IL Emit / Expression Trees (Performance Hybrid)

**Mechanism:**
- Same discovery as Option 2 (reflection)
- Instead of reflection for get/set, generate IL or compiled expressions
- Cache compiled delegates per grain type

```csharp
internal class CompiledMemberAccessor<TGrain, TMember> : MemberAccessor
{
    private readonly Func<TGrain, TMember> _getter;
    private readonly Action<TGrain, TMember> _setter;

    public override object GetValue(object grain) => _getter((TGrain)grain);
    public override void SetValue(object grain, object value) => _setter((TGrain)grain, (TMember)value);
}
```

**Pros:**
- Near-codegen performance after first access
- Works with dynamic grains
- No boxing for value types (with generic specialization)

**Cons:**
- Complex implementation
- Harder to debug
- JIT compilation overhead on first use

---

### Option 4: Interceptor-Based (Change Tracking)

**Mechanism:**
- Use Orleans grain call interceptors
- Intercept property setters to mark grain as "dirty"
- Auto-persist on deactivation or after method calls

**Pros:**
- Only saves changed state
- Transparent to grain code
- Could enable "event sourcing lite"

**Cons:**
- Only works for properties (not fields)
- Requires property interception infrastructure
- Complex lifecycle management

---

## Recommended Approach: Hybrid Strategy

Combine **Option 1** (source generator) and **Option 2** (runtime reflection):

```
┌─────────────────────────────────────────────────────────────────┐
│                    StatefulGrain Feature                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Static Grains (compiled with solution)                         │
│  ┌─────────────────────────────────────┐                        │
│  │  Source Generator                    │                       │
│  │  - Generates __GeneratedState class  │                       │
│  │  - Generates sync code               │                       │
│  │  - Zero runtime overhead             │                       │
│  └─────────────────────────────────────┘                        │
│                                                                 │
│  Dynamic Grains (loaded at runtime)                             │
│  ┌─────────────────────────────────────┐                        │
│  │  Runtime Reflection + IL Emit        │                       │
│  │  - Discovers members at activation   │                       │
│  │  - Compiles accessors, caches them   │                       │
│  │  - Dictionary<string,byte[]> storage │                       │
│  └─────────────────────────────────────┘                        │
│                                                                 │
│  Common Infrastructure                                          │
│  ┌─────────────────────────────────────┐                        │
│  │  - [Stateful] attribute              │                       │
│  │  - [NonPersistent] attribute         │                       │
│  │  - StatefulGrain base class          │                       │
│  │  - IStatefulGrainStorage interface   │                       │
│  │  - SaveStateAsync() / LoadStateAsync()                       │
│  └─────────────────────────────────────┘                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Member Filtering Rules

### What to INCLUDE:
1. Instance fields (including auto-property backing fields) declared in the grain class
2. Instance properties with setters declared in the grain class
3. Members explicitly marked with `[Persistent]` (for edge cases)

### What to EXCLUDE:
1. **All members from base classes** (`Grain`, `Grain<T>`, `StatefulGrain`, `object`)
2. Static members
3. Members marked with `[NonPersistent]`
4. Members marked with `[Inject]` or similar DI attributes
5. Read-only properties (get-only, no backing field)
6. Delegate types, event handlers
7. `ILogger`, `IGrainFactory`, and other known service types
8. Members with types that aren't serializable

### Detection of base class members:

```csharp
internal static class StatefulGrainMemberFilter
{
    // Types whose members should never be persisted
    private static readonly HashSet<Type> ExcludedBaseTypes = new()
    {
        typeof(object),
        typeof(Grain),
        typeof(Grain<>),
        typeof(StatefulGrain),
        // Add more as needed
    };

    // Types that indicate injected services
    private static readonly HashSet<Type> ServiceTypes = new()
    {
        typeof(ILogger),
        typeof(ILogger<>),
        typeof(IGrainFactory),
        typeof(IGrainContext),
        typeof(IServiceProvider),
        // Add more as needed
    };

    public static bool ShouldPersist(MemberInfo member, Type declaringGrainType)
    {
        // 1. Must be declared on the grain type itself, not inherited
        if (member.DeclaringType != declaringGrainType)
            return false;

        // 2. Check for [NonPersistent] attribute
        if (member.GetCustomAttribute<NonPersistentAttribute>() != null)
            return false;

        // 3. Check member type
        var memberType = GetMemberType(member);

        // 4. Exclude known service types
        if (IsServiceType(memberType))
            return false;

        // 5. Must be serializable
        if (!IsSerializable(memberType))
            return false;

        return true;
    }
}
```

---

## API Design

### Attributes

```csharp
namespace Orleans.Persistence
{
    /// <summary>
    /// Marks a grain for automatic state persistence of all its members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class StatefulAttribute : Attribute
    {
        /// <summary>
        /// Optional storage provider name. Uses default if not specified.
        /// </summary>
        public string StorageName { get; set; }

        /// <summary>
        /// Whether to auto-save state on deactivation. Default: true.
        /// </summary>
        public bool AutoSaveOnDeactivation { get; set; } = true;

        /// <summary>
        /// Schema version for migration support.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;
    }

    /// <summary>
    /// Excludes a member from automatic persistence.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NonPersistentAttribute : Attribute { }

    /// <summary>
    /// Explicitly includes a member in persistence (useful for inherited members).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PersistentAttribute : Attribute
    {
        /// <summary>
        /// Explicit ID for serialization stability across renames.
        /// </summary>
        public int Id { get; set; } = -1;
    }
}
```

### Base Class

```csharp
namespace Orleans
{
    /// <summary>
    /// Base class for grains with automatic state persistence.
    /// </summary>
    public abstract class StatefulGrain : Grain, ILifecycleParticipant<IGrainLifecycle>
    {
        private IStatefulGrainStorage _storage;
        private bool _isDirty;

        /// <summary>
        /// Marks the grain state as modified. Call after changing persisted members.
        /// Not required if using auto-save on deactivation.
        /// </summary>
        protected void MarkDirty() => _isDirty = true;

        /// <summary>
        /// Persists the current state of all persistable members.
        /// </summary>
        protected Task SaveStateAsync() => _storage.WriteStateAsync(this);

        /// <summary>
        /// Reloads state from storage, overwriting current member values.
        /// </summary>
        protected Task ReloadStateAsync() => _storage.ReadStateAsync(this);

        /// <summary>
        /// Clears persisted state from storage.
        /// </summary>
        protected Task ClearStateAsync() => _storage.ClearStateAsync(this);

        /// <summary>
        /// Gets whether there is persisted state for this grain.
        /// </summary>
        protected bool HasPersistedState => _storage.RecordExists;

        // Lifecycle participation
        void ILifecycleParticipant<IGrainLifecycle>.Participate(IGrainLifecycle lifecycle)
        {
            lifecycle.Subscribe<StatefulGrain>(
                GrainLifecycleStage.SetupState,
                OnSetupStateAsync,
                OnTeardownStateAsync);
        }

        private async Task OnSetupStateAsync(CancellationToken ct)
        {
            _storage = Runtime.GetStatefulGrainStorage(GrainContext, this.GetType());
            await _storage.ReadStateAsync(this);
        }

        private async Task OnTeardownStateAsync(CancellationToken ct)
        {
            // Check StatefulAttribute.AutoSaveOnDeactivation
            if (ShouldAutoSave() && _isDirty)
            {
                await _storage.WriteStateAsync(this);
            }
        }
    }
}
```

### Storage Interface

```csharp
namespace Orleans.Storage
{
    /// <summary>
    /// Storage abstraction for stateful grains that persists grain members directly.
    /// </summary>
    public interface IStatefulGrainStorage
    {
        /// <summary>
        /// Reads persisted state and populates grain members.
        /// </summary>
        Task ReadStateAsync(Grain grain);

        /// <summary>
        /// Persists current grain member values.
        /// </summary>
        Task WriteStateAsync(Grain grain);

        /// <summary>
        /// Clears persisted state.
        /// </summary>
        Task ClearStateAsync(Grain grain);

        /// <summary>
        /// Whether a persisted record exists.
        /// </summary>
        bool RecordExists { get; }

        /// <summary>
        /// ETag for optimistic concurrency.
        /// </summary>
        string Etag { get; }
    }
}
```

---

## Implementation Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Grain Activation                             │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   StatefulGrainLifecycleObserver                    │
│  - Subscribes to GrainLifecycleStage.SetupState                     │
│  - Resolves IStatefulGrainStorage for grain type                    │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   IStatefulGrainStorageFactory                      │
│  - Creates storage instance per grain activation                    │
│  - Resolves underlying IGrainStorage provider                       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
        ┌───────────────────────┴───────────────────────┐
        │                                               │
        ▼                                               ▼
┌───────────────────────┐                 ┌───────────────────────────┐
│ SourceGen Path        │                 │ Runtime Reflection Path   │
│ (Static Grains)       │                 │ (Dynamic Grains)          │
├───────────────────────┤                 ├───────────────────────────┤
│ Generated:            │                 │ StatefulGrainMetadata     │
│ - __GeneratedState    │                 │ - Discovers members       │
│ - SyncToState()       │                 │ - Builds accessors        │
│ - SyncFromState()     │                 │ - Caches per type         │
└───────────┬───────────┘                 └─────────────┬─────────────┘
            │                                           │
            │                                           │
            ▼                                           ▼
┌───────────────────────┐                 ┌───────────────────────────┐
│ Standard IGrainStorage│                 │ DynamicStatefulStorage    │
│ - Uses generated state│                 │ - Dict<string,byte[]>     │
│ - Normal serialization│                 │ - Per-member serialization│
└───────────────────────┘                 └───────────────────────────┘
```

### Source Generator Implementation

```csharp
[Generator]
public class StatefulGrainSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [Stateful] attribute or inheriting StatefulGrain
        var grainDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Orleans.Persistence.StatefulAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => GetGrainToGenerate(ctx))
            .Where(m => m is not null);

        context.RegisterSourceOutput(grainDeclarations, GenerateStatefulGrainCode);
    }

    private void GenerateStatefulGrainCode(
        SourceProductionContext context,
        StatefulGrainInfo grainInfo)
    {
        // 1. Generate state class with [GenerateSerializer]
        var stateClass = GenerateStateClass(grainInfo);

        // 2. Generate partial class with lifecycle participation
        var partialClass = GeneratePartialGrain(grainInfo);

        context.AddSource($"{grainInfo.ClassName}__StatefulGrain.g.cs",
            SourceText.From(stateClass + partialClass, Encoding.UTF8));
    }
}
```

### Runtime Reflection Implementation

```csharp
internal sealed class StatefulGrainMetadataProvider
{
    private readonly ConcurrentDictionary<Type, StatefulGrainMetadata> _cache = new();
    private readonly Serializer _serializer;

    public StatefulGrainMetadata GetMetadata(Type grainType)
    {
        return _cache.GetOrAdd(grainType, CreateMetadata);
    }

    private StatefulGrainMetadata CreateMetadata(Type grainType)
    {
        var members = new List<MemberAccessor>();

        // Get fields declared on this type (not inherited)
        var fields = grainType.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        foreach (var field in fields)
        {
            if (StatefulGrainMemberFilter.ShouldPersist(field, grainType))
            {
                members.Add(CreateFieldAccessor(field));
            }
        }

        // Get properties declared on this type (not inherited)
        var properties = grainType.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        foreach (var property in properties)
        {
            // Skip if it's an auto-property (we already got the backing field)
            if (IsAutoProperty(property))
                continue;

            if (property.CanWrite && StatefulGrainMemberFilter.ShouldPersist(property, grainType))
            {
                members.Add(CreatePropertyAccessor(property));
            }
        }

        return new StatefulGrainMetadata(grainType, members);
    }

    private MemberAccessor CreateFieldAccessor(FieldInfo field)
    {
        // Use compiled expressions for performance
        var grainParam = Expression.Parameter(typeof(object), "grain");
        var typedGrain = Expression.Convert(grainParam, field.DeclaringType);
        var fieldAccess = Expression.Field(typedGrain, field);

        // Getter
        var getterLambda = Expression.Lambda<Func<object, object>>(
            Expression.Convert(fieldAccess, typeof(object)), grainParam);
        var getter = getterLambda.Compile();

        // Setter
        var valueParam = Expression.Parameter(typeof(object), "value");
        var assignment = Expression.Assign(fieldAccess,
            Expression.Convert(valueParam, field.FieldType));
        var setterLambda = Expression.Lambda<Action<object, object>>(
            assignment, grainParam, valueParam);
        var setter = setterLambda.Compile();

        return new CompiledMemberAccessor(field.Name, field.FieldType, getter, setter);
    }
}
```

### Dynamic Storage Implementation

```csharp
internal sealed class DynamicStatefulGrainStorage : IStatefulGrainStorage
{
    private readonly IGrainStorage _underlyingStorage;
    private readonly StatefulGrainMetadata _metadata;
    private readonly Serializer _serializer;
    private readonly IGrainContext _grainContext;
    private readonly string _stateName;

    private GrainState<DynamicGrainState> _state;

    public async Task ReadStateAsync(Grain grain)
    {
        _state ??= new GrainState<DynamicGrainState> { State = new DynamicGrainState() };

        await _underlyingStorage.ReadStateAsync(_stateName, _grainContext.GrainId, _state);

        if (_state.RecordExists)
        {
            // Deserialize each member
            foreach (var accessor in _metadata.PersistableMembers)
            {
                if (_state.State.Members.TryGetValue(accessor.Name, out var bytes))
                {
                    var value = _serializer.Deserialize(accessor.MemberType, bytes);
                    accessor.SetValue(grain, value);
                }
            }
        }
    }

    public async Task WriteStateAsync(Grain grain)
    {
        _state ??= new GrainState<DynamicGrainState> { State = new DynamicGrainState() };
        _state.State.Members.Clear();
        _state.State.GrainTypeName = grain.GetType().FullName;

        // Serialize each member
        foreach (var accessor in _metadata.PersistableMembers)
        {
            var value = accessor.GetValue(grain);
            if (value != null)
            {
                var bytes = _serializer.SerializeToArray(value);
                _state.State.Members[accessor.Name] = bytes;
            }
        }

        await _underlyingStorage.WriteStateAsync(_stateName, _grainContext.GrainId, _state);
    }

    public bool RecordExists => _state?.RecordExists ?? false;
    public string Etag => _state?.ETag;
}

[GenerateSerializer]
internal sealed class DynamicGrainState
{
    [Id(0)] public Dictionary<string, byte[]> Members { get; set; } = new();
    [Id(1)] public string GrainTypeName { get; set; }
    [Id(2)] public int SchemaVersion { get; set; } = 1;
}
```

---

## Dynamic Grain Loading Integration

For grains loaded via `PluginGrainLoaderService`:

1. **No source generator available** - must use runtime reflection path
2. **Register metadata at load time** - when `LoadGrainAssemblyAsync` completes, scan for `[Stateful]` grains
3. **Pre-compute accessors** - build and cache `StatefulGrainMetadata` during load, not first activation
4. **Serializer compatibility** - dynamic grains use `PluginSerializationManager`, ensure it works with per-member serialization

```csharp
// In PluginGrainLoaderService.LoadGrainAssemblyInternalAsync()
// Add after Phase 3 (serialization):

// Phase 3.6: Pre-compute stateful grain metadata
foreach (var grainClass in metadata.GrainClasses)
{
    if (IsStatefulGrain(grainClass))
    {
        _statefulMetadataProvider.PrecomputeMetadata(grainClass);
    }
}
```

---

## Schema Evolution

### Problem
Member names, types, or count may change between versions.

### Solution
1. **Member ID mapping** - Optional `[Persistent(Id = n)]` for stable IDs
2. **Schema version** - `[Stateful(SchemaVersion = 2)]` triggers migration
3. **Migration hooks** - Virtual methods for custom migration logic

```csharp
[Stateful(SchemaVersion = 2)]
public class MyGrain : StatefulGrain, IMyGrain
{
    [Persistent(Id = 0)]
    private int _counter;  // Renamed from _count

    [Persistent(Id = 1)]
    public string DisplayName { get; set; }  // New in v2

    protected override void OnMigrate(int fromVersion, IDictionary<string, object> oldState)
    {
        if (fromVersion == 1)
        {
            // Migrate _count to _counter (same ID, just renamed)
            // Add default for new DisplayName
            DisplayName ??= "Unnamed";
        }
    }
}
```

---

## Performance Considerations

| Aspect | Source Generator | Runtime Reflection |
|--------|-----------------|-------------------|
| First activation | No overhead | Metadata lookup (cached) |
| Read/Write | Direct field access | Compiled delegate call |
| Memory | One state object | Dictionary + byte arrays |
| Serialization | Single object | Per-member (more allocations) |

**Recommendation:** Use source generator for performance-critical grains. Runtime reflection is acceptable for dynamic grains or rapid prototyping.

---

## Configuration

```csharp
siloBuilder.AddStatefulGrainPersistence(options =>
{
    // Default storage provider for stateful grains
    options.DefaultStorageName = "Default";

    // Whether to use source-generated storage when available
    options.PreferSourceGenerated = true;

    // Whether to auto-save on deactivation by default
    options.DefaultAutoSaveOnDeactivation = true;

    // Member types to always exclude
    options.ExcludedTypes.Add(typeof(CancellationToken));

    // Custom member filter
    options.MemberFilter = (member, grainType) =>
        !member.Name.StartsWith("_temp");
});
```

---

## Usage Examples

### Basic Usage

```csharp
[Stateful]
public partial class CounterGrain : Grain, ICounterGrain
{
    private int _count;
    private DateTime _lastUpdated;

    [NonPersistent]
    private ILogger<CounterGrain> _logger;

    public CounterGrain(ILogger<CounterGrain> logger) => _logger = logger;

    public async Task<int> Increment()
    {
        _count++;
        _lastUpdated = DateTime.UtcNow;
        await SaveStateAsync();
        return _count;
    }

    public Task<int> GetCount() => Task.FromResult(_count);
}
```

### With Base Class

```csharp
public class PlayerGrain : StatefulGrain, IPlayerGrain
{
    private string _name;
    private int _score;
    private List<string> _achievements = new();

    public async Task AddScore(int points)
    {
        _score += points;
        MarkDirty();  // Will auto-save on deactivation
    }

    public async Task UnlockAchievement(string achievement)
    {
        _achievements.Add(achievement);
        await SaveStateAsync();  // Save immediately
    }
}
```

### With Custom Storage Provider

```csharp
[Stateful(StorageName = "PlayerStorage")]
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Uses "PlayerStorage" provider instead of default
}
```

---

## Open Questions

1. **Auto-save frequency** - Should there be a timer-based auto-save option?
2. **Dirty tracking** - Should we implement automatic dirty tracking via IL weaving or source generators?
3. **Transactions** - How does this interact with Orleans transactions?
4. **Migration tooling** - Should we provide a CLI tool for schema migration?
5. **Debugging** - How to inspect persisted state for stateful grains?

---

## Implementation Phases

### Phase 1: Foundation
- [ ] `[Stateful]` and `[NonPersistent]` attributes
- [ ] `StatefulGrain` base class with basic API
- [ ] Runtime reflection-based metadata discovery
- [ ] `DynamicStatefulGrainStorage` implementation
- [ ] Integration with existing `IGrainStorage`

### Phase 2: Source Generator
- [ ] Incremental source generator for `[Stateful]` grains
- [ ] Generated state class with `[GenerateSerializer]`
- [ ] Generated sync code
- [ ] Generated lifecycle participation

### Phase 3: Dynamic Grain Integration
- [ ] Hook into `PluginGrainLoaderService`
- [ ] Pre-compute metadata on assembly load
- [ ] Verify serialization compatibility

### Phase 4: Polish
- [ ] Schema evolution support
- [ ] Migration hooks
- [ ] Configuration options
- [ ] Documentation and samples

---

## References

- Orleans `Grain<T>` implementation: `src/Orleans.Core.Abstractions/Core/Grain.cs`
- Orleans `IPersistentState<T>`: `src/Orleans.Runtime/Facet/Persistent/`
- Orleans `IGrainStorage`: `src/Orleans.Core/Providers/IGrainStorage.cs`
- Orleans serialization: `src/Orleans.Serialization/`
- Dynamic grain loading: `src/Orleans.Runtime/DynamicGrains/`
