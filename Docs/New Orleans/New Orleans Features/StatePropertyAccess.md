# Orleans State Property Access Enhancement

## Implementation Status: Phase 2 Complete

**Branch:** `claude/orleans-property-state-access-Oq9Xb`
**Last Updated:** 2026-01-11

---

## Overview

This feature enables property-like syntax for accessing grain state remotely:

```csharp
// NEW: Property-style syntax
string name = await player.Name;           // Remote get via awaiting
await (player.Name << "Louis");            // Remote set via << operator

// Still works: Traditional Orleans style
string name = await player.GetName();
await player.SetName("Louis");
```

The implementation is **property-driven**: developers define properties on grain classes, and the code generator automatically creates interface methods, implementation wiring, and proxy enhancements.

---

## Completed Work

### Phase 1: Core Infrastructure

#### StateTask<T> Type (`Orleans.Core.Abstractions/State/StateTask.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateTask.cs`

The `StateTask<T>` struct provides:
- Awaitable pattern via `GetAwaiter()` - enables `await grain.Name`
- `<<` operator for setting - enables `await (grain.Name << "value")`
- `GetAsync()` and `SetAsync(T)` methods for explicit usage
- Thread-safe design with delegate-based invocation

```csharp
public readonly struct StateTask<T>
{
    private readonly Func<ValueTask<T>> _getter;
    private readonly Func<T, ValueTask> _setter;

    public StateTask(Func<ValueTask<T>> getter, Func<T, ValueTask> setter);
    public ValueTask<T> GetAsync();
    public ValueTask SetAsync(T value);
    public ValueTaskAwaiter<T> GetAwaiter();
    public static ValueTask operator <<(StateTask<T> state, T value);
}
```

#### StateAttribute (`Orleans.Core.Abstractions/State/StateAttribute.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateAttribute.cs`

Configures code generation behavior for grain state properties:
- `Persisted` - Maps property to IPersistentState (Phase 4)
- `StateProperty` - Name of the persistent state field (Phase 4)
- `AutoSave` - Auto-call WriteStateAsync on set (Phase 4)
- `CanSet` - Whether to generate setter method
- `MethodName` - Custom Get/Set method names

#### NotStateAttribute (`Orleans.Core.Abstractions/State/NotStateAttribute.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/NotStateAttribute.cs`

Excludes public properties from state code generation (for dependencies, loggers, etc.)

#### LibraryTypes Extension (`Orleans.CodeGenerator/LibraryTypes.cs`)

**Modified:** Added StateTask_1, StateAttribute, NotStateAttribute type references
**Added:** `SupportsStateProperties` property to check availability

---

### Phase 2: Property-to-Interface Code Generation

#### StatePropertyCodeGenerator (`Orleans.CodeGenerator/StatePropertyCodeGenerator.cs`)

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/StatePropertyCodeGenerator.cs`

The main generator that:
1. **Scans grain classes** for public properties (respects `[NotState]`)
2. **Generates interface method signatures** (Get/Set) on partial interface
3. **Generates grain method implementations** that delegate to properties
4. **Generates StateTask properties** on proxy classes

Key methods:
- `ScanGrainClass(INamedTypeSymbol)` - Detects state properties
- `GenerateInterfaceMethodSignatures(...)` - Creates Get/Set method declarations
- `GenerateGrainMethodImplementations(...)` - Creates Get/Set method bodies
- `GenerateProxyStateTaskProperties(...)` - Creates StateTask wrappers

#### CodeGenerator Integration (`Orleans.CodeGenerator/CodeGenerator.cs`)

**Modified:**
- Added `StatePropertyCodeGenerator` instance
- Added `_statePropertiesByInterface` dictionary to track properties
- Added grain class scanning in the type processing loop
- Added `GetStatePropertiesForInterface()` for proxy generator access
- Added `GeneratePartialInterfaceExtension()` and `GeneratePartialClassExtension()` helpers

#### ProxyGenerator Integration (`Orleans.CodeGenerator/ProxyGenerator.cs`)

**Modified:**
- Added StateTask property generation after proxy methods
- Uses `_codeGenerator.GetStatePropertiesForInterface()` to find properties
- Uses `StatePropertyCodeGenerator.GenerateProxyStateTaskProperties()` to create properties

---

## How It Works

### Developer Writes

**Interface (partial, only custom methods):**
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerSnapshot> GetSnapshotAsync();
    Task ApplyDamageAsync(int amount);
}
```

**Implementation (with properties):**
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public string Name { get; set; }
    public int Score { get; set; }

    // Custom methods implemented by developer
    public Task<PlayerSnapshot> GetSnapshotAsync() => ...;
    public Task ApplyDamageAsync(int amount) => ...;
}
```

### Generated Code Includes

1. **Interface extension** (Get/Set method signatures):
```csharp
partial interface IPlayerGrain
{
    Task<string> GetName();
    Task SetName(string value);
    Task<int> GetScore();
    Task SetScore(int value);
}
```

2. **Class extension** (method implementations):
```csharp
partial class PlayerGrain
{
    Task<string> IPlayerGrain.GetName() => Task.FromResult(Name);
    Task IPlayerGrain.SetName(string value) { Name = value; return Task.CompletedTask; }
    Task<int> IPlayerGrain.GetScore() => Task.FromResult(Score);
    Task IPlayerGrain.SetScore(int value) { Score = value; return Task.CompletedTask; }
}
```

3. **Proxy StateTask properties**:
```csharp
internal sealed class Proxy_IPlayerGrain : GrainReference, IPlayerGrain
{
    // Method proxies (standard)
    public Task<string> GetName() { ... }
    public Task SetName(string value) { ... }

    // StateTask properties (new)
    public StateTask<string> Name => new StateTask<string>(
        () => new ValueTask<string>(GetName()),
        v => new ValueTask(SetName(v)));
}
```

### Client Usage

```csharp
IPlayerGrain player = client.GetGrain<IPlayerGrain>("player-1");

// Method style (standard Orleans)
await player.SetName("Louis");
string name = await player.GetName();

// Property style (new)
await (player.Name << "Louis");
string name = await player.Name;
```

---

## Attribute Usage

### Excluding Properties

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Part of remote API
    public string Name { get; set; }

    // Excluded - not exposed remotely
    [NotState]
    public ILogger<PlayerGrain> Logger { get; }
}
```

### Read-Only Properties

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Only GetCreatedAt() is generated, no setter
    [State(CanSet = false)]
    public DateTime CreatedAt { get; }
}
```

### Custom Method Names

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Generates GetDisplayName/SetDisplayName instead of GetTitle/SetTitle
    [State(MethodName = "DisplayName")]
    public string Title { get; set; }
}
```

---

## File Locations

| Component | Path |
|-----------|------|
| StateTask<T> | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateTask.cs` |
| StateAttribute | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateAttribute.cs` |
| NotStateAttribute | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/NotStateAttribute.cs` |
| StatePropertyCodeGenerator | `/src/NewOrleans/src/Orleans.CodeGenerator/StatePropertyCodeGenerator.cs` |
| LibraryTypes | `/src/NewOrleans/src/Orleans.CodeGenerator/LibraryTypes.cs` |
| CodeGenerator | `/src/NewOrleans/src/Orleans.CodeGenerator/CodeGenerator.cs` |
| ProxyGenerator | `/src/NewOrleans/src/Orleans.CodeGenerator/ProxyGenerator.cs` |

---

## Remaining Work

### Phase 3: Partial Properties and Backing Fields
- [ ] Detect partial property declarations
- [ ] Generate backing fields for partial properties
- [ ] Wire up property implementations

### Phase 4: Persistence Integration
- [ ] `[State(Persisted = true)]` support
- [ ] IPersistentState mapping
- [ ] AutoSave implementation

---

## Design Reference

The full design specification is available in the original task description. Key design decisions:

1. **Property-driven generation**: Properties on grain classes drive all code generation
2. **StateTask<T> for remote access**: Enables `await grain.Name` and `await (grain.Name << value)`
3. **Backward compatible**: Traditional Get/Set methods still work
4. **Attribute-based configuration**: `[State]` and `[NotState]` control behavior
5. **Partial types required**: Both interface and class must be partial for extension
