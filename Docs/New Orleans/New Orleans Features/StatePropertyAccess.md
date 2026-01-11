# Orleans State Property Access Enhancement

## DOTNExT Modifications

This document tracks the implementation of property-based state access for Orleans grains in the DOTNExT fork.

| Modification | Status | Description |
|--------------|--------|-------------|
| StateTask<T> type | Complete | Awaitable struct for property-style remote access |
| StateAttribute | Complete | Configuration attribute for code generation |
| NotStateAttribute | Complete | Exclusion attribute for non-state properties |
| Property scanning | Complete | Detects properties on grain classes |
| Interface generation | Complete | Generates Get/Set method signatures |
| Class generation | Complete | Generates Get/Set method implementations |
| Proxy generation | Complete | Generates StateTask properties on proxies |
| Partial properties | Pending | Phase 3 - backing field generation |
| Persistence mapping | Pending | Phase 4 - IPersistentState integration |

---

## Implementation Status

| Phase | Status | Branch |
|-------|--------|--------|
| Phase 1: Core Infrastructure | **Complete** | `claude/orleans-property-state-access-Oq9Xb` |
| Phase 2: Code Generation | **Complete** | `claude/orleans-property-state-access-Oq9Xb` |
| Phase 3: Partial Properties | Pending | - |
| Phase 4: Persistence | Pending | - |

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

## Implementation History

### 2026-01-11: Phase 1 & 2 Complete

**Commit:** `919f76126` on `claude/orleans-property-state-access-Oq9Xb`

Implemented:
- StateTask<T> struct with awaitable pattern and << operator
- StateAttribute and NotStateAttribute
- StatePropertyCodeGenerator for property detection and code generation
- Integration with CodeGenerator and ProxyGenerator
- Partial interface/class extension generation

---

## Completed Work

### Phase 1: Core Infrastructure

#### 1.1 StateTask<T> Type

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

**Why `<<` operator?** C# property setters must return `void`, making them incompatible with async patterns. The `<<` operator visually suggests "pushing" a value and can return `ValueTask`.

#### 1.2 StateAttribute

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateAttribute.cs`

Configures code generation behavior for grain state properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Persisted` | bool | false | Maps property to IPersistentState (Phase 4) |
| `StateProperty` | string? | null | Name of persistent state field (Phase 4) |
| `AutoSave` | bool | false | Auto-call WriteStateAsync on set (Phase 4) |
| `CanSet` | bool | true | Whether to generate setter method |
| `MethodName` | string? | null | Custom Get/Set method names |

#### 1.3 NotStateAttribute

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/NotStateAttribute.cs`

Excludes public properties from state code generation (for dependencies, loggers, etc.)

#### 1.4 LibraryTypes Extension

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/LibraryTypes.cs`

Added:
- `StateTask_1` - Type reference for `Orleans.StateTask<T>`
- `StateAttribute` - Type reference for `Orleans.StateAttribute`
- `NotStateAttribute` - Type reference for `Orleans.NotStateAttribute`
- `SupportsStateProperties` - Property to check if all types are available

---

### Phase 2: Property-to-Interface Code Generation

#### 2.1 StatePropertyCodeGenerator

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/StatePropertyCodeGenerator.cs`

The main generator that orchestrates all state property code generation:

| Method | Purpose |
|--------|---------|
| `ScanGrainClass(INamedTypeSymbol)` | Detects public properties on grain classes |
| `GenerateInterfaceMethodSignatures(...)` | Creates Get/Set method declarations for interface |
| `GenerateGrainMethodImplementations(...)` | Creates Get/Set method bodies for class |
| `GenerateProxyStateTaskProperties(...)` | Creates StateTask property wrappers for proxy |

**Detection Rules:**
- Property must be `public`
- Property must NOT have `[NotState]` attribute
- Property must NOT be an indexer
- Grain class must implement a grain interface (inherits from `IGrainWithXXXKey`)

#### 2.2 CodeGenerator Integration

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/CodeGenerator.cs`

Modifications:
- Added `StatePropertyCodeGenerator` instance in constructor
- Added `_statePropertiesByInterface` dictionary to track properties by interface
- Added grain class scanning in the type processing loop (line 273-311)
- Added `GetStatePropertiesForInterface()` method for proxy generator access
- Added `GeneratePartialInterfaceExtension()` helper
- Added `GeneratePartialClassExtension()` helper

#### 2.3 ProxyGenerator Integration

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/ProxyGenerator.cs`

Modifications:
- Added StateTask property generation after proxy methods (line 52-70)
- Queries `_codeGenerator.GetStatePropertiesForInterface()` for properties
- Calls `StatePropertyCodeGenerator.GenerateProxyStateTaskProperties()` to create StateTask properties

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

**1. Interface extension** (Get/Set method signatures):
```csharp
partial interface IPlayerGrain
{
    Task<string> GetName();
    Task SetName(string value);
    Task<int> GetScore();
    Task SetScore(int value);
}
```

**2. Class extension** (method implementations):
```csharp
partial class PlayerGrain
{
    Task<string> IPlayerGrain.GetName() => Task.FromResult(Name);
    Task IPlayerGrain.SetName(string value) { Name = value; return Task.CompletedTask; }
    Task<int> IPlayerGrain.GetScore() => Task.FromResult(Score);
    Task IPlayerGrain.SetScore(int value) { Score = value; return Task.CompletedTask; }
}
```

**3. Proxy StateTask properties**:
```csharp
internal sealed class Proxy_IPlayerGrain : GrainReference, IPlayerGrain
{
    // Method proxies (standard Orleans)
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

// Both can be mixed freely
await player.ApplyDamageAsync(10);
int score = await player.Score;
```

---

## Attribute Usage Examples

### Excluding Properties

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Part of remote API
    public string Name { get; set; }

    // Excluded - not exposed remotely
    [NotState]
    public ILogger<PlayerGrain> Logger { get; }

    // Also excluded - internal tracking
    [NotState]
    public DateTime LastAccessedInternal { get; set; }
}
```

### Read-Only Properties

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Only GetCreatedAt() is generated, no setter
    [State(CanSet = false)]
    public DateTime CreatedAt { get; }

    // Full read-write
    public string Name { get; set; }
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

**Goal:** Support C# partial properties with generated backing fields

| Task | Status | Description |
|------|--------|-------------|
| Detect partial property declarations | Pending | Check for `partial` modifier on properties |
| Generate backing fields | Pending | `private T _propertyName_backing;` |
| Wire up property implementations | Pending | Connect getter/setter to backing field |

**Expected usage:**
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Codegen generates backing field and implementation
    public partial string Name { get; set; }
}
```

### Phase 4: Persistence Integration

**Goal:** Map properties to Orleans `IPersistentState<T>`

| Task | Status | Description |
|------|--------|-------------|
| `[State(Persisted = true)]` support | Pending | Detect persistence attribute |
| IPersistentState field detection | Pending | Find matching state fields |
| Property-to-state mapping | Pending | Generate `_state.State.Property` access |
| AutoSave implementation | Pending | Fire-and-forget `WriteStateAsync()` |

**Expected usage:**
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;

    [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    public partial int Score { get; set; }
}
```

---

## Design Reference

The full design specification is in `/Docs/New Orleans/New Orleans Features/` (original task description).

**Key Design Decisions:**

1. **Property-driven generation**: Properties on grain classes drive all code generation (not interface methods)
2. **StateTask<T> for remote access**: Enables `await grain.Name` and `await (grain.Name << value)`
3. **Backward compatible**: Traditional Get/Set methods still work alongside property syntax
4. **Attribute-based configuration**: `[State]` and `[NotState]` control behavior
5. **Partial types required**: Both interface and class must be `partial` for extension
6. **Thread-safe**: Each StateTask access creates fresh delegates (no shared state)

---

## How to Continue Development

1. Read this file and the design spec for context
2. Check the "Remaining Work" section above
3. For Phase 3: Modify `StatePropertyCodeGenerator.ScanGrainClass()` to detect `partial` properties, add backing field generation
4. For Phase 4: Add `IPersistentState` field detection, modify property implementation generation to use state mapping
5. Update this document as work progresses
