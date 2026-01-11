# Orleans State Property Access Enhancement

## Implementation Status: Phase 1 - In Progress

**Branch:** `claude/orleans-property-state-access-gCuYC`
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

---

## Completed Work

### 1. StateTask<T> Type (`Orleans.Core.Abstractions/State/StateTask.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateTask.cs`

Created the `StateTask<T>` struct that provides:
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

### 2. StateAttribute (`Orleans.Core.Abstractions/State/StateAttribute.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateAttribute.cs`

Configures code generation behavior for grain state properties:
- `Persisted` - Maps property to IPersistentState
- `StateProperty` - Name of the persistent state field
- `AutoSave` - Auto-call WriteStateAsync on set
- `CanSet` - Whether to generate setter method
- `MethodName` - Custom Get/Set method names

### 3. NotStateAttribute (`Orleans.Core.Abstractions/State/NotStateAttribute.cs`)

**Location:** `/src/NewOrleans/src/Orleans.Core.Abstractions/State/NotStateAttribute.cs`

Excludes public properties from state code generation (for dependencies, loggers, etc.)

### 4. LibraryTypes Extension (`Orleans.CodeGenerator/LibraryTypes.cs`)

**Modified:** Added StateTask_1, StateAttribute, NotStateAttribute type references
**Added:** `SupportsStateProperties` property to check availability

### 5. StatePropertyGenerator (`Orleans.CodeGenerator/StatePropertyGenerator.cs`)

**Location:** `/src/NewOrleans/src/Orleans.CodeGenerator/StatePropertyGenerator.cs`

Created generator that:
- Detects Get/Set method pairs on grain interfaces
- Generates StateTask<T> property declarations for proxies
- Handles Task<T> to ValueTask<T> conversion
- Creates read-only property support (throws if no setter)

### 6. ProxyGenerator Integration (`Orleans.CodeGenerator/ProxyGenerator.cs`)

**Modified:** Added StatePropertyGenerator integration to proxy generation pipeline:
- Creates StatePropertyGenerator instance in constructor
- Calls DetectStateProperties() to find Get/Set method pairs
- Calls GenerateStateTaskProperties() to create StateTask<T> properties
- Adds generated properties to proxy class declaration

---

## Remaining Work

### Phase 1 (Current - Core Infrastructure)
- [x] Integrate StatePropertyGenerator into ProxyGenerator
- [ ] Test compilation of modified Orleans.CodeGenerator
- [ ] Create unit tests for StateTask<T>

### Phase 2 (Interface/Method Generation)
- [ ] Create generator for interface method signatures from grain properties
- [ ] Create generator for grain method implementations

### Phase 3 (Partial Property Support)
- [ ] Add partial property detection
- [ ] Generate backing fields for partial properties
- [ ] Wire up property implementations

### Phase 4 (Persistence Integration)
- [ ] IPersistentState mapping support
- [ ] AutoSave implementation

---

## File Locations

| Component | Path |
|-----------|------|
| StateTask<T> | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateTask.cs` |
| StateAttribute | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/StateAttribute.cs` |
| NotStateAttribute | `/src/NewOrleans/src/Orleans.Core.Abstractions/State/NotStateAttribute.cs` |
| LibraryTypes | `/src/NewOrleans/src/Orleans.CodeGenerator/LibraryTypes.cs` |
| StatePropertyGenerator | `/src/NewOrleans/src/Orleans.CodeGenerator/StatePropertyGenerator.cs` |
| ProxyGenerator | `/src/NewOrleans/src/Orleans.CodeGenerator/ProxyGenerator.cs` |

---

## How to Continue

1. Run `git status` to see current changes
2. Read this file and the design spec for context
3. Continue with "Remaining Work" items above
4. The ProxyGenerator needs to be modified to call StatePropertyGenerator and add the generated properties to proxy classes

---

## Design Reference

The full design specification is in the task description provided to Claude. Key points:

1. **StateTask<T>** wraps remote property access
2. **<< operator** enables `await (grain.Name << "value")`
3. Properties on grain classes drive code generation
4. Get/Set methods are auto-generated on interfaces
5. Proxies get both method stubs AND StateTask properties
6. Full backward compatibility with existing Orleans code
