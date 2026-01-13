# Orleans State Properties Enhancement
## Design Specification for DOTNExT Fork

**Version**: 1.0  
**Status**: Design Phase  
**Context**: This document captures the complete design for enhancing Microsoft Orleans with property-based state access, developed for the DOTNExT platform (a fork of .NET focused on solving semantic debt in distributed systems).

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem Statement](#2-problem-statement)
3. [Solution Overview](#3-solution-overview)
4. [What It Looks Like When Complete](#4-what-it-looks-like-when-complete)
5. [Core Components](#5-core-components)
6. [Code Generation Specifications](#6-code-generation-specifications)
7. [The StateTask Type](#7-the-statetask-type)
8. [Attribute System](#8-attribute-system)
9. [Compiler Hook Points](#9-compiler-hook-points)
10. [Implementation Phases](#10-implementation-phases)
11. [Open Questions & Future Work](#11-open-questions--future-work)

---

## 1. Executive Summary

This project enhances Orleans grain development by enabling **property-based syntax** for remote state access while maintaining full backward compatibility with Orleans' method-based RPC model.

**The core innovation**: Developers define properties on their grain implementations; code generation automatically creates the necessary interface methods, implementation wiring, and proxy enhancements—including a novel `StateTask<T>` type that enables intuitive property-like syntax on the client side.

**Key benefits**:
- Reduced boilerplate (no manual Get/Set method pairs)
- Single source of truth (property definition drives everything)
- Optional persistence mapping to Orleans' `IPersistentState<T>`
- Dual API on client: traditional methods AND property syntax
- Full backward compatibility with existing Orleans ecosystem

---

## 2. Problem Statement

### Current Orleans Pattern (Verbose)

To expose a simple string property from a grain, developers must currently write:

**Interface**:
```csharp
public interface IPlayerGrain : IGrainWithStringKey
{
    Task<string> GetName();
    Task SetName(string name);
    Task<int> GetScore();
    Task SetScore(int score);
    // ... repeated for every property
}
```

**Implementation**:
```csharp
public class PlayerGrain : Grain, IPlayerGrain
{
    private string _name;
    private int _score;
    
    public Task<string> GetName() => Task.FromResult(_name);
    public Task SetName(string name) { _name = name; return Task.CompletedTask; }
    public Task<int> GetScore() => Task.FromResult(_score);
    public Task SetScore(int score) { _score = score; return Task.CompletedTask; }
}
```

**Client**:
```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");
string name = await player.GetName();
await player.SetName("Louis");
```

### Problems

1. **Repetition**: Every property requires interface methods + implementation methods + backing field
2. **Sync risk**: Property name, interface, and implementation can diverge
3. **No property semantics**: Client code doesn't feel like working with properties
4. **Persistence boilerplate**: Mapping to `IPersistentState<T>` adds more manual wiring

---

## 3. Solution Overview

### Developer Writes (Minimal)

**Interface** (only custom methods):
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerSnapshot> GetSnapshotAsync();
    Task ApplyDamageAsync(int amount);
}
```

**Implementation** (properties only):
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }
    public partial int Score { get; set; }
    public partial int Health { get; set; }
    
    // Custom methods implemented by developer
    public Task<PlayerSnapshot> GetSnapshotAsync() => ...;
    public Task ApplyDamageAsync(int amount) => ...;
}
```

### Code Generation Produces

1. **Interface extension**: `GetName()`/`SetName()`, `GetScore()`/`SetScore()`, etc.
2. **Partial property implementations**: Backing fields or persistence mapping
3. **Interface method implementations**: Wire Get/Set methods to properties
4. **Enhanced proxy**: Standard method proxies PLUS `StateTask<T>` property wrappers

### Client Gets Both APIs

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Traditional Orleans style (still works)
string name = await player.GetName();
await player.SetName("Louis");

// NEW: Property-style syntax
string name = await player.Name;           // Remote get via awaiting
await (player.Name << "Louis");            // Remote set via << operator
```

---

## 4. What It Looks Like When Complete

This section shows the complete before/after to give full clarity on the developer experience.

### 4.1 Simple In-Memory State

**Developer writes**:
```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task RespawnAsync();
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }
    public partial int Health { get; set; }
    
    public Task RespawnAsync()
    {
        Health = 100;
        return Task.CompletedTask;
    }
}
```

**After codegen, the effective interface is**:
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    // Developer-written
    Task RespawnAsync();
    
    // Generated
    Task<string> GetName();
    Task SetName(string value);
    Task<int> GetHealth();
    Task SetHealth(int value);
}
```

**After codegen, the effective implementation is**:
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Generated backing fields
    private string _name_backing;
    private int _health_backing;
    
    // Generated partial property implementations
    public partial string Name
    {
        get => _name_backing;
        set => _name_backing = value;
    }
    
    public partial int Health
    {
        get => _health_backing;
        set => _health_backing = value;
    }
    
    // Generated interface method implementations
    Task<string> IPlayerGrain.GetName() => Task.FromResult(Name);
    Task IPlayerGrain.SetName(string value) { Name = value; return Task.CompletedTask; }
    Task<int> IPlayerGrain.GetHealth() => Task.FromResult(Health);
    Task IPlayerGrain.SetHealth(int value) { Health = value; return Task.CompletedTask; }
    
    // Developer-written (unchanged)
    public Task RespawnAsync()
    {
        Health = 100;
        return Task.CompletedTask;
    }
}
```

**Generated proxy includes StateTask properties**:
```csharp
internal sealed class Proxy_IPlayerGrain : GrainReference, IPlayerGrain
{
    // Standard method proxies
    public Task<string> GetName() { /* invoke */ }
    public Task SetName(string value) { /* invoke */ }
    public Task<int> GetHealth() { /* invoke */ }
    public Task SetHealth(int value) { /* invoke */ }
    public Task RespawnAsync() { /* invoke */ }
    
    // StateTask property wrappers
    public StateTask<string> Name => new StateTask<string>(
        this,
        getterFactory: () => GetInvokable<Invokable_GetName>(),
        setterFactory: v => { var r = GetInvokable<Invokable_SetName>(); r.Arg0 = v; return r; }
    );
    
    public StateTask<int> Health => new StateTask<int>(
        this,
        getterFactory: () => GetInvokable<Invokable_GetHealth>(),
        setterFactory: v => { var r = GetInvokable<Invokable_SetHealth>(); r.Arg0 = v; return r; }
    );
}
```

**Client usage**:
```csharp
IPlayerGrain player = client.GetGrain<IPlayerGrain>("player-1");

// Method style
await player.SetName("Louis");
string name = await player.GetName();

// Property style (same underlying RPC)
await (player.Name << "Louis");
string name = await player.Name;

// Mix and match freely
await player.RespawnAsync();
int health = await player.Health;  // Returns 100
```

### 4.2 With Orleans Persistent State

**Developer writes**:
```csharp
// PlayerData.cs - the persistent state class
[GenerateSerializer]
public class PlayerData
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public int Score { get; set; }
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;
    
    // These map directly to _state.State properties
    [State(Persisted = true, StateProperty = nameof(_state))]
    public partial string Name { get; set; }
    
    [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    public partial int Score { get; set; }
    
    // This one is in-memory only (no attribute or Persisted = false)
    public partial int Health { get; set; }
    
    public PlayerGrain(
        [PersistentState("player", "playerStore")] IPersistentState<PlayerData> state)
    {
        _state = state;
    }
}
```

**Generated partial property implementations**:
```csharp
public partial class PlayerGrain
{
    // Health: simple backing field (not persisted)
    private int _health_backing;
    public partial int Health
    {
        get => _health_backing;
        set => _health_backing = value;
    }
    
    // Name: maps to persistent state (no auto-save)
    public partial string Name
    {
        get => _state.State.Name;
        set => _state.State.Name = value;
    }
    
    // Score: maps to persistent state WITH auto-save
    public partial int Score
    {
        get => _state.State.Score;
        set
        {
            _state.State.Score = value;
            _ = _state.WriteStateAsync();  // Fire-and-forget save
        }
    }
}
```

### 4.3 Custom Property Logic

When the developer wants custom getter/setter behavior, they simply implement the property themselves (non-partial):

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }  // Codegen implements this
    
    // Developer implements this one manually - codegen skips property impl
    // but still generates GetHealth/SetHealth methods that call this property
    public int Health
    {
        get => _health;
        set => _health = Math.Clamp(value, 0, MaxHealth);
    }
    private int _health;
    private const int MaxHealth = 100;
}
```

### 4.4 Read-Only Properties

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [State(CanSet = false)]
    public partial DateTime CreatedAt { get; }  // No setter generated
    
    public partial string Name { get; set; }
}
```

Generated interface only includes `GetCreatedAt()`, no `SetCreatedAt()`.

---

## 5. Core Components

### 5.1 Component Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│                    COMPILE-TIME COMPONENTS                         │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌──────────────────────┐      ┌───────────────────────────────┐  │
│  │   Developer Source   │      │      Code Generator           │  │
│  │                      │      │                               │  │
│  │  - partial interface │ ───► │  1. Scan grain class for      │  │
│  │  - partial class     │      │     public properties         │  │
│  │  - partial properties│      │  2. Generate Get/Set methods  │  │
│  │  - [State] attrs     │      │     on partial interface      │  │
│  └──────────────────────┘      │  3. Generate Get/Set impls    │  │
│                                │     on partial class          │  │
│                                │  4. Generate partial property │  │
│                                │     implementations           │  │
│                                │  5. Generate proxy with:      │  │
│                                │     - method proxies          │  │
│                                │     - StateTask<T> properties │  │
│                                └───────────────────────────────┘  │
│                                              │                     │
│                                              ▼                     │
│                                ┌───────────────────────────────┐  │
│                                │     Generated Source Files    │  │
│                                │                               │  │
│                                │  - IFoo.g.cs (Get/Set sigs)   │  │
│                                │  - Foo.State.g.cs (prop impl) │  │
│                                │  - Foo.Methods.g.cs (methods) │  │
│                                │  - Proxy_IFoo.g.cs (proxy +   │  │
│                                │    StateTask properties)      │  │
│                                └───────────────────────────────┘  │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────┐
│                     RUNTIME COMPONENTS                             │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌────────────────────┐         ┌────────────────────────────┐    │
│  │   StateTask<T>     │         │   GrainReference (base)    │    │
│  │                    │         │                            │    │
│  │  - grainRef        │◄────────│   - InvokeAsync<T>()       │    │
│  │  - getterFactory   │         │   - GetInvokable<T>()      │    │
│  │  - setterFactory   │         │                            │    │
│  │                    │         └────────────────────────────┘    │
│  │  + GetAsync()      │                     ▲                     │
│  │  + SetAsync(T)     │                     │                     │
│  │  + GetAwaiter()    │         ┌────────────────────────────┐    │
│  │  + operator <<     │         │   Proxy_IPlayerGrain       │    │
│  └────────────────────┘         │   : GrainReference         │    │
│                                 │   , IPlayerGrain           │    │
│                                 │                            │    │
│                                 │  + GetName() : Task<string>│    │
│                                 │  + SetName(string) : Task  │    │
│                                 │  + Name : StateTask<string>│    │
│                                 └────────────────────────────┘    │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### 5.2 Data Flow

```
CLIENT SIDE                           NETWORK                    SILO SIDE
─────────────────────────────────────────────────────────────────────────────

await player.Name                         │
      │                                   │
      ▼                                   │
StateTask<string>.GetAwaiter()            │
      │                                   │
      ▼                                   │
StateTask<string>.GetAsync()              │
      │                                   │
      ▼                                   │
getterFactory() → Invokable_GetName       │
      │                                   │
      ▼                                   │
GrainReference.InvokeAsync<string>()  ────┼────►  Message dispatched
      │                                   │              │
      │                                   │              ▼
      │                                   │       PlayerGrain.GetName()
      │                                   │              │
      │                                   │              ▼
      │                                   │       return Task.FromResult(Name)
      │                                   │              │
      │                                   │              ▼
      │                                   │       Name property getter
      │                                   │              │
      │                                   │              ▼
result: "Louis"  ◄────────────────────────┼───── _state.State.Name (or backing field)
```

---

## 6. Code Generation Specifications

### 6.1 Inputs (What Drives Code Generation)

The code generator is **property-driven**. It scans grain implementation classes for properties and generates everything else.

**Primary input**: Public properties on grain classes

**Supporting inputs**:
1. **Partial interfaces** inheriting from `IGrainWithXXXKey` (needed to add Get/Set methods)
2. **Partial classes** inheriting from `Grain` (needed to add method implementations and property implementations)
3. **`[State]` attributes** for persistence and behavior configuration
4. **`[NotState]` attributes** for exclusion

**NOT an input**: The interface's existing methods. We don't scan for Get/Set pairs—we generate them from properties.

### 6.2 Detection Rules

**A property triggers code generation if ALL of**:
- Declared in a class that inherits from `Grain`
- The class implements at least one `IGrainWithXXXKey` interface
- Property is `public`
- Property does NOT have `[NotState]` attribute

**For each qualifying property, the generator produces**:
1. Interface method signatures (`GetX` / `SetX`) added to the grain interface
2. Interface method implementations added to the grain class
3. If property is `partial`: backing field + property implementation
4. Proxy StateTask<T> property wrapping the generated Get/Set method proxies

**Property requires implementation generation if**:
- It is declared with `partial` modifier
- (Non-partial properties already have implementations—only Get/Set methods are generated)

### 6.3 Output Files

For a grain `PlayerGrain : IPlayerGrain`:

| File | Contents |
|------|----------|
| `IPlayerGrain.g.cs` | Interface extension with Get/Set method signatures |
| `PlayerGrain.State.g.cs` | Partial property implementations (backing fields or persistence mapping) |
| `PlayerGrain.Methods.g.cs` | Interface method implementations calling properties |
| `Proxy_IPlayerGrain.g.cs` | Proxy class with method proxies and StateTask properties |
| `Invokable_*.g.cs` | Request types for each method (standard Orleans pattern) |

### 6.4 Naming Conventions

| Source | Generated |
|--------|-----------|
| Property `Name` | Methods `GetName()` / `SetName(string value)` |
| Property `IsActive` | Methods `GetIsActive()` / `SetIsActive(bool value)` |
| `[State(MethodName = "Title")]` on `Name` | Methods `GetTitle()` / `SetTitle(string value)` |

### 6.5 Type Mapping

| Property Type | Get Return Type | Set Parameter Type |
|---------------|-----------------|-------------------|
| `T` | `Task<T>` | `T` |
| `T?` (nullable) | `Task<T?>` | `T?` |
| `ImmutableArray<T>` | `Task<ImmutableArray<T>>` | `ImmutableArray<T>` |

---

## 7. The StateTask Type

### 7.1 Purpose

`StateTask<T>` wraps a grain property's getter and setter into a single type that:
- Is **awaitable** (for getting the value)
- Supports the **`<<` operator** (for setting the value)
- Creates **fresh invokables** per operation (thread-safe)

### 7.2 Implementation

```csharp
/// <summary>
/// Wraps remote grain property access with awaitable get and operator-based set.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public readonly struct StateTask<T>
{
    private readonly GrainReference _grainRef;
    private readonly Func<IInvokable> _getterFactory;
    private readonly Func<T, IInvokable> _setterFactory;

    /// <summary>
    /// Creates a new StateTask for a grain property.
    /// </summary>
    /// <param name="grainRef">The grain reference (proxy) owning this property.</param>
    /// <param name="getterFactory">Factory creating a fresh getter invokable per call.</param>
    /// <param name="setterFactory">Factory creating a fresh setter invokable with value set.</param>
    internal StateTask(
        GrainReference grainRef,
        Func<IInvokable> getterFactory,
        Func<T, IInvokable> setterFactory)
    {
        _grainRef = grainRef;
        _getterFactory = getterFactory;
        _setterFactory = setterFactory;
    }

    /// <summary>
    /// Asynchronously retrieves the property value from the grain.
    /// </summary>
    public ValueTask<T> GetAsync()
    {
        var request = _getterFactory();
        return _grainRef.InvokeAsync<T>(request);
    }

    /// <summary>
    /// Asynchronously sets the property value on the grain.
    /// </summary>
    /// <param name="value">The value to set.</param>
    public ValueTask SetAsync(T value)
    {
        var request = _setterFactory(value);
        return _grainRef.InvokeAsync(request);
    }

    // ═══════════════════════════════════════════════════════════════
    // AWAITABLE PATTERN
    // Enables: string name = await grain.Name;
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets an awaiter for the property value (enables await syntax).
    /// </summary>
    public ValueTaskAwaiter<T> GetAwaiter() => GetAsync().GetAwaiter();

    // ═══════════════════════════════════════════════════════════════
    // SHIFT OPERATOR FOR SET
    // Enables: await (grain.Name << "Louis");
    // C# 11+ allows any return type from shift operators
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Sets the property value using shift-left syntax.
    /// </summary>
    /// <param name="state">The StateTask representing the property.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A ValueTask that completes when the set operation completes.</returns>
    public static ValueTask operator <<(StateTask<T> state, T value)
        => state.SetAsync(value);
}
```

### 7.3 Why `<<` Operator?

C# property setters must return `void`, making them incompatible with async patterns:

```csharp
// IMPOSSIBLE - setters can't return Task
await grain.Name = "Louis";  // Syntax error

// IMPOSSIBLE - assignment expression evaluates to RHS
await (grain.Name = "Louis");  // This awaits "Louis", not the assignment
```

The `<<` operator provides a workaround:

```csharp
// WORKS - << can return ValueTask
await (grain.Name << "Louis");
```

**Why `<<` specifically?**
- Visually suggests "pushing" a value into something
- Available for overloading in C#
- C# 11+ removed return type restrictions on shift operators
- Short and doesn't conflict with common operators

**Alternative considered**: `>>=` (monadic bind feel), but `<<` is more intuitive for "set".

### 7.4 Thread Safety

Each access creates fresh invokables:

```csharp
// Safe - each << creates its own Invokable_SetName instance
var t1 = grain.Name << "Alice";
var t2 = grain.Name << "Bob";
await Task.WhenAll(t1, t2);  // No race condition
```

This is why we use factories (`Func<IInvokable>`) rather than storing invokable instances.

---

## 8. Attribute System

### 8.1 StateAttribute

```csharp
namespace Orleans;

/// <summary>
/// Configures code generation behavior for a grain state property.
/// Optional - all public properties on grains are processed by default.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class StateAttribute : Attribute
{
    /// <summary>
    /// If true, the property maps to an IPersistentState field.
    /// Requires StateProperty to be set.
    /// Default: false
    /// </summary>
    public bool Persisted { get; init; } = false;
    
    /// <summary>
    /// Name of the IPersistentState&lt;T&gt; field to map this property to.
    /// The T type must have a property with matching name and compatible type.
    /// Only used when Persisted = true.
    /// Example: nameof(_playerState)
    /// </summary>
    public string? StateProperty { get; init; }
    
    /// <summary>
    /// If true, WriteStateAsync() is called automatically after each set.
    /// For Persisted properties: calls the IPersistentState.WriteStateAsync()
    /// For non-persisted properties: calls the grain's WriteStateAsync() if available
    /// Default: false
    /// </summary>
    public bool AutoSave { get; init; } = false;
    
    /// <summary>
    /// If false, only a getter method is generated (read-only from client perspective).
    /// The property can still have a setter for internal grain use.
    /// Default: true
    /// </summary>
    public bool CanSet { get; init; } = true;
    
    /// <summary>
    /// Custom name for the generated Get/Set methods.
    /// Default: uses the property name (Name → GetName/SetName)
    /// Example: "DisplayName" → GetDisplayName/SetDisplayName
    /// </summary>
    public string? MethodName { get; init; }
}
```

### 8.2 NotStateAttribute

```csharp
namespace Orleans;

/// <summary>
/// Excludes a public property from state code generation.
/// Use for injected dependencies, loggers, or other non-state public properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class NotStateAttribute : Attribute
{
}
```

### 8.3 Usage Examples

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;
    private readonly ILogger<PlayerGrain> _logger;
    
    // Simple in-memory state
    public partial string Name { get; set; }
    
    // Persisted to Orleans state, manual save
    [State(Persisted = true, StateProperty = nameof(_state))]
    public partial int Score { get; set; }
    
    // Persisted with automatic save on every set
    [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    public partial int Level { get; set; }
    
    // Read-only from client (no SetRank method generated)
    [State(CanSet = false)]
    public partial string Rank { get; }
    
    // Custom method names
    [State(MethodName = "DisplayName")]
    public partial string Title { get; set; }  // → GetDisplayName/SetDisplayName
    
    // Excluded - not a state property
    [NotState]
    public ILogger<PlayerGrain> Logger => _logger;
    
    public PlayerGrain(
        [PersistentState("player", "store")] IPersistentState<PlayerData> state,
        ILogger<PlayerGrain> logger)
    {
        _state = state;
        _logger = logger;
    }
}
```

---

## 9. Compiler Hook Points

Since DOTNExT owns the full .NET/Roslyn stack, we can hook anywhere. Here are the options:

### 9.1 Compilation Pipeline Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ROSLYN COMPILATION PIPELINE                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Source Files (.cs)                                                         │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 1: PARSING                                            │            │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/Parser     │            │
│  │   - CSharpSyntaxTree.ParseText()                            │            │
│  │   - Creates SyntaxTree per file                             │            │
│  │   Output: Collection of SyntaxTrees                         │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 2: COMPILATION CREATION                               │  ◄── HOOK │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/           │            │
│  │             Compilation/CSharpCompilation.cs                │            │
│  │   - CSharpCompilation.Create()                              │            │
│  │   - Combines syntax trees + references                      │            │
│  │   - Can add/replace syntax trees here                       │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 3: SOURCE GENERATORS                                  │  ◄── HOOK │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/           │            │
│  │             SourceGeneration/GeneratorDriver.cs             │            │
│  │   - Runs ISourceGenerator / IIncrementalGenerator           │            │
│  │   - Standard API only allows adding files                   │            │
│  │   - WE CAN MODIFY TO ALLOW REPLACEMENT                      │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 4: SEMANTIC BINDING                                   │            │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/Binder     │            │
│  │   - Type resolution, overload resolution                    │            │
│  │   - Too late for adding members                             │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 5: LOWERING                                           │  ◄── HOOK │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/Lowering   │            │
│  │   - async/await transformation                              │            │
│  │   - Iterator rewriting                                      │            │
│  │   - DOTNExT already modifies this for TPL changes           │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │ PHASE 6: IL EMIT                                            │            │
│  │   Location: Roslyn/src/Compilers/CSharp/Portable/CodeGen    │            │
│  │   - Generates IL bytecode                                   │            │
│  └─────────────────────────────────────────────────────────────┘            │
│         │                                                                   │
│         ▼                                                                   │
│  Assembly Output (.dll)                                                     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 9.2 Recommended Hook: Enhanced Source Generator

**Location**: `Roslyn/src/Compilers/CSharp/Portable/SourceGeneration/`

**Modification**: Extend the source generator API to allow syntax tree replacement:

```csharp
// Current standard API
public readonly struct SourceProductionContext
{
    public void AddSource(string hintName, string source);
    public void AddSource(string hintName, SourceText source);
}

// Extended API for DOTNExT
public readonly struct SourceProductionContext
{
    public void AddSource(string hintName, string source);
    public void AddSource(string hintName, SourceText source);
    
    // NEW: Replace an existing syntax tree
    public void ReplaceSyntaxTree(SyntaxTree original, SyntaxTree replacement);
    
    // NEW: Transform a syntax tree (functional approach)
    public void TransformSyntaxTree(
        SyntaxTree original, 
        Func<SyntaxTree, SyntaxTree> transformer);
}
```

**Implementation approach**:

In `GeneratorDriver.cs`, after running generators:

```csharp
// Pseudocode for the modification
private Compilation ApplyGeneratorOutputs(
    Compilation compilation,
    ImmutableArray<GeneratorRunResult> results)
{
    var newCompilation = compilation;
    
    foreach (var result in results)
    {
        // Standard: add new syntax trees
        foreach (var addedSource in result.AddedSources)
        {
            newCompilation = newCompilation.AddSyntaxTrees(addedSource.Tree);
        }
        
        // NEW: apply replacements
        foreach (var replacement in result.ReplacedTrees)
        {
            newCompilation = newCompilation.ReplaceSyntaxTree(
                replacement.Original, 
                replacement.Replacement);
        }
    }
    
    return newCompilation;
}
```

### 9.3 Alternative Hook: MSBuild Pre-Compile Task

If you prefer not to modify Roslyn, use MSBuild:

**Location**: Custom MSBuild task running before `CoreCompile`

```xml
<!-- In Orleans.Sdk.targets or similar -->
<Target Name="OrleansStatePropertyPreprocess" BeforeTargets="CoreCompile">
  <OrleansPreprocessTask
    Sources="@(Compile)"
    OutputDirectory="$(IntermediateOutputPath)Generated\"
    ModifiedSources="@(ModifiedCompile)" />
  
  <!-- Replace original sources with modified ones -->
  <ItemGroup>
    <Compile Remove="@(Compile)" Condition="'%(ModifiedCompile.Original)' != ''" />
    <Compile Include="@(ModifiedCompile)" />
  </ItemGroup>
</Target>
```

This approach:
- Works without Roslyn modifications
- Runs before any compilation
- Can freely rewrite source files
- Output goes to intermediate directory (original untouched)

### 9.4 Chosen Approach for DOTNExT

**Primary**: Enhanced Source Generator (Hook 9.2)
- Cleaner integration with existing Orleans code generator
- Better IDE support (analyzers, code fixes work naturally)
- Incremental compilation support

**Fallback**: MSBuild task (Hook 9.3)
- For compatibility scenarios
- For users not on full DOTNExT stack

---

## 10. Implementation Phases

### Phase 1: Core Infrastructure

**Goal**: StateTask<T> type and foundational runtime support

**Deliverables**:
1. `StateTask<T>` struct implementation
2. Runtime support for StateTask in GrainReference base class (if needed)
3. Unit tests for StateTask behavior (standalone, not yet integrated)

**Validation**:
```csharp
// Test StateTask in isolation with mock grain reference
var stateTask = new StateTask<string>(
    mockGrainRef,
    () => mockGetterInvokable,
    v => { mockSetterInvokable.Arg0 = v; return mockSetterInvokable; }
);

// Verify awaitable pattern works
string value = await stateTask;

// Verify << operator works  
await (stateTask << "Test");
```

### Phase 2: Property-to-Interface Code Generation

**Goal**: Generate interface methods and grain method implementations from grain properties

**Deliverables**:
1. Property scanner for grain classes (detect public properties, respect [NotState])
2. Interface extension generator (adds Get/Set method signatures to partial interface)
3. Grain method generator (adds Get/Set method bodies that delegate to properties)
4. Integration tests

**Flow**:
```
Grain property (Name) 
    → Interface gets: Task<string> GetName(); Task SetName(string);
    → Grain gets: Task<string> IPlayerGrain.GetName() => Task.FromResult(Name);
```

**Validation**:
```csharp
// Developer writes
public partial interface ITestGrain : IGrainWithStringKey { }

public partial class TestGrain : Grain, ITestGrain
{
    public string Name { get; set; }  // Non-partial, already complete
}

// After codegen, interface has GetName/SetName
// After codegen, grain has method implementations
// Traditional client call works:
await grain.GetName();
await grain.SetName("Test");
```

### Phase 3: Partial Properties and Proxy Enhancement

**Goal**: Generate implementations for partial properties AND enhance proxy with StateTask

**Deliverables**:
1. Partial property detection
2. Backing field generation for partial properties
3. Property implementation generation
4. **Proxy enhancement**: Generate StateTask<T> properties wrapping the Get/Set method proxies
5. Handle mix of partial and complete properties

**Flow**:
```
Grain partial property (Name)
    → Backing field: private string _name_backing;
    → Property impl: public partial string Name { get => _name_backing; set => _name_backing = value; }
    
Interface methods (from Phase 2)
    → Proxy method: Task<string> GetName() { ... invoke ... }
    → Proxy method: Task SetName(string v) { ... invoke ... }
    → Proxy StateTask: StateTask<string> Name => new StateTask<string>(this, getterFactory, setterFactory);
```

**Validation**:
```csharp
// Developer writes
public partial class TestGrain : Grain, ITestGrain
{
    public partial string Name { get; set; }  // Codegen implements
    public int Health { get; set; }           // Already complete, codegen skips impl
}

// Client can use BOTH styles:
await grain.GetName();              // Method style
await grain.SetName("Test");        // Method style
string name = await grain.Name;     // StateTask style
await (grain.Name << "Test");       // StateTask style
```

### Phase 4: Persistence Integration

**Goal**: [State] attribute with Persisted mapping

**Deliverables**:
1. StateAttribute implementation
2. IPersistentState field detection
3. Property implementation that maps to state object
4. AutoSave support

**Validation**:
```csharp
public partial class TestGrain : Grain, ITestGrain
{
    private readonly IPersistentState<TestData> _state;
    
    [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    public partial int Score { get; set; }  // Maps to _state.State.Score
}
```

### Phase 5: Polish & Edge Cases

**Goal**: Production readiness

**Deliverables**:
1. NotStateAttribute support
2. CanSet = false (read-only properties)
3. MethodName customization
4. Comprehensive diagnostics/analyzers
5. Documentation
6. Performance optimization

---

## 10.5 CURRENT IMPLEMENTATION STATUS (January 2026)

> **This section documents the actual state of the implementation as of January 2026.**
> **STATUS: FEATURE COMPLETE** ✅

### Implementation Summary

**ALL COMPONENTS ARE COMPLETE AND WORKING!**

The StatePropertyAccess feature has been fully implemented and tested. All 19 tests pass, including direct property access via RPC.

### What Has Been Implemented

| Component | Status | Notes |
|-----------|--------|-------|
| `StateTask<T>` struct | ✅ COMPLETE | Full implementation with GetAsync, SetAsync, GetAwaiter, << operator |
| `[State]` / `[NotState]` attributes | ✅ COMPLETE | In Orleans.Core.Abstractions/State/ |
| Interface method generation (GetX/SetX signatures) | ✅ COMPLETE | Added to partial interface |
| Grain class method generation (GetX/SetX implementations) | ✅ COMPLETE | Explicit interface implementations |
| Partial property backing fields | ✅ COMPLETE | Generated for partial properties |
| Interface StateTask<T> properties | ✅ COMPLETE | `StateTask<T> Name { get; }` added to interface |
| Grain StateTask<T> property implementations | ✅ COMPLETE | Explicit interface impl with local access |
| Proxy GetX/SetX method invokables | ✅ COMPLETE | Custom invokable classes generated |
| Proxy GetX/SetX method implementations | ✅ COMPLETE | Explicit interface implementations using invokables |
| Proxy StateTask<T> properties | ✅ COMPLETE | Public properties that satisfy interface |
| ORLEANS0008 analyzer bypass | ✅ COMPLETE | StateTask properties allowed on interfaces |
| End-to-end runtime test | ✅ COMPLETE | Phase 5 tests all pass |
| VS2022 IDE integration | ✅ COMPLETE | Generator runs in IDE; full IntelliSense support |
| Generated file visibility | ✅ COMPLETE | `.norleans.g.cs` files visible in Solution Explorer |

### Test Results (19/19 PASSED)

```
Phase 1: StateTask<T> struct operations (5 tests)
✓ PASS StateTask<T>.GetAsync() returns value
✓ PASS StateTask<T>.SetAsync() updates value
✓ PASS StateTask<T> is awaitable (await stateTask)
✓ PASS StateTask<T> << operator works (await (st << value))
✓ PASS StateTask<int> works for value types

Phase 2: Method-style grain access (3 tests)
✓ PASS SetName/GetName works via Orleans RPC
✓ PASS SetScore/GetScore works via Orleans RPC
✓ PASS GetCreatedAt works (read-only)

Phase 3: Property-style access using StateTask<T> (2 tests)
✓ PASS Property-style: await (name << value) and await name
✓ PASS Property-style: works for value types (int)

Phase 4: Mixed style access (2 tests)
✓ PASS Mixed styles work: method set → property get, property set → method get
✓ PASS Custom method GetCombinedInfo() coexists with state properties

Phase 5: Direct grain.Name property access (7 tests)
✓ PASS grain.Name << value compiles and executes
✓ PASS await grain.Name returns correct value
✓ PASS Method set → property get and method get both work
✓ PASS Property set → method get works
✓ PASS grain.Score (int) property works
✓ PASS grain.CreatedAt (read-only) works
✓ PASS Custom method coexists with generated StateTask properties
```

### Files Modified in This Implementation

| File | Changes |
|------|---------|
| `src/Orleans.CodeGenerator/CodeGenerator.cs` | Deferred interface visiting; added StateTask props to interface/class |
| `src/Orleans.CodeGenerator/StatePropertyCodeGenerator.cs` | Added interface StateTask props, grain StateTask impls, proxy methods & invokables |
| `src/Orleans.CodeGenerator/ProxyGenerator.cs` | Integrated state property proxy methods and invokables |
| `src/Orleans.CodeGenerator/OrleansSourceGenerator.cs` | Changed output to `.norleans.g.cs`; enabled VS2022 IDE execution |
| `src/Orleans.Analyzers/GrainInterfacePropertyDiagnosticAnalyzer.cs` | Allow StateTask<T> properties on interfaces |
| `playground/PluginGrainScenarios/Grains/PartialPropertyTestGrain.cs` | Test grain with partial properties |
| `playground/PluginGrainScenarios/Scenarios/StatePropertyAccessScenario.cs` | Added Phase 5 tests |
| `playground/PluginGrainScenarios/Program.cs` | Added non-interactive mode support |
| `playground/PluginGrainScenarios/PluginGrainScenarios.csproj` | Added EmitCompilerGeneratedFiles, analyzer reference, generated file visibility |

### Generated Code Example

For `IPartialPropertyTestGrain`, the code generator produces:

```csharp
// In the partial interface
partial interface IPartialPropertyTestGrain
{
    // Generated Get/Set method signatures
    Task<string> GetName();
    Task SetName(string value);
    Task<int> GetScore();
    Task SetScore(int value);
    Task<DateTime> GetCreatedAt();

    // Generated StateTask<T> properties
    StateTask<string> Name { get; }
    StateTask<int> Score { get; }
    StateTask<DateTime> CreatedAt { get; }
}

// In the proxy class
internal sealed class Proxy_IPartialPropertyTestGrain : GrainReference, IPartialPropertyTestGrain
{
    // Get/Set methods call invokables for RPC
    Task<string> IPartialPropertyTestGrain.GetName() { /* RPC invokable */ }
    Task IPartialPropertyTestGrain.SetName(string arg0) { /* RPC invokable */ }

    // StateTask properties wrap Get/Set methods
    public StateTask<string> Name => new StateTask<string>(
        () => new ValueTask<string>(((IPartialPropertyTestGrain)this).GetName()),
        v => new ValueTask(((IPartialPropertyTestGrain)this).SetName(v)));
}
```

### How to Use the Feature

**Developer writes:**
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task RespawnAsync();  // Custom method
}

public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }  // Triggers code generation
    public partial int Health { get; set; }

    public Task RespawnAsync() { Health = 100; return Task.CompletedTask; }
}
```

**Client can use both styles:**
```csharp
var grain = grainFactory.GetGrain<IPlayerGrain>("player-1");

// Method style (traditional Orleans)
await grain.SetName("Louis");
string name = await grain.GetName();

// Property style (new StateTask syntax)
await (grain.Name << "Louis");
string name = await grain.Name;
```

### Build & Test Commands

```powershell
cd D:\dev\DOTNExT\src\NewOrleans

# Build the code generator
dotnet build src/Orleans.CodeGenerator/Orleans.CodeGenerator.csproj -c Debug

# Build test project
dotnet build playground/PluginGrainScenarios/PluginGrainScenarios.csproj -c Debug

# Run StatePropertyAccess tests (scenario 8)
dotnet run --project playground/PluginGrainScenarios/PluginGrainScenarios.csproj -- 8

# View generated code (now uses .norleans.g.cs extension)
# Files are at: obj/Debug/net8.0/generated/Orleans.CodeGenerator/
#               Orleans.CodeGenerator.OrleansSerializationSourceGenerator/
#               PluginGrainScenarios.norleans.g.cs
```

### IDE Integration (VS2022 IntelliSense)

**Problem Solved**: The original Orleans generator deliberately skipped execution in VS2022 for performance reasons. This caused IntelliSense errors because the IDE never saw the generated partial interface members.

**Solution Applied**:
1. Removed the VS2022 skip in `OrleansSourceGenerator.cs` (lines 21-28)
2. Changed generated file extension to `.norleans.g.cs` for DOTNExT identification
3. Added `EmitCompilerGeneratedFiles=true` to emit files to disk
4. Added explicit `<None>` include to make generated files visible in Solution Explorer

**Key Changes in `OrleansSourceGenerator.cs`**:
```csharp
// BEFORE: Generator skipped VS2022 entirely
var processName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
if (processName.Contains("devenv") || processName.Contains("servicehub"))
{
    return;  // IntelliSense never saw generated code!
}

// AFTER: Generator runs in VS2022 for proper IntelliSense
// (Skip code commented out in DOTNExT fork)
```

**Key Changes in `.csproj`**:
```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

<ItemGroup>
  <!-- Explicit analyzer reference for IDE visibility -->
  <ProjectReference Include="...\Orleans.CodeGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<ItemGroup>
  <!-- Make generated files visible in Solution Explorer -->
  <None Include="$(IntermediateOutputPath)generated\**\*.norleans.g.cs"
        Link="Grains\Generated\%(Filename)%(Extension)"
        Visible="true" />
</ItemGroup>
```

**Result**: Full IntelliSense support for generated members:
- `grain.Name` property visible
- `grain.GetName()` / `grain.SetName()` methods visible
- No more "does not contain a definition" errors in IDE

---

## 11. Open Questions & Future Work

### 11.1 Open Questions

**Q1: Batch persistence**
When multiple [State(AutoSave = true)] properties are set in sequence, should saves be batched?
```csharp
grain.Name = "Louis";   // Save?
grain.Score = 100;      // Save?
grain.Level = 5;        // Save?
// Current: 3 saves. Better: 1 batched save?
```

**Q2: Async property getters on grain side**
If a property getter needs to be async (e.g., lazy load), how to handle?
```csharp
// This can't work - property getters can't be async
public partial async Task<string> Name { get; set; }  // Invalid C#
```
Possible: Generate method instead of property for these cases.

**Q3: Nullable reference types**
How to handle:
```csharp
public partial string? Name { get; set; }  // Nullable
public partial string Name { get; set; }   // Non-nullable but might be default
```

**Q4: Collections**
Should `List<T>` properties have special handling?
```csharp
public partial List<string> Tags { get; set; }
// Client: await (grain.Tags << new List<string> { "a", "b" })
// Or should there be: await grain.Tags.AddAsync("a") ?
```

### 11.2 Future Work

1. **`>>` operator for get** (symmetry with `<<`)
   ```csharp
   string name;
   await (grain.Name >> name);  // Alternative to: name = await grain.Name
   ```

2. **Reactive state** (IObservable integration)
   ```csharp
   grain.Score.Subscribe(score => Console.WriteLine($"Score changed: {score}"));
   ```

3. **Computed properties**
   ```csharp
   [Computed]
   public int TotalPoints => Score * Level;  // Derived, read-only
   ```

4. **Property change events**
   ```csharp
   [State(NotifyOnChange = true)]
   public partial int Score { get; set; }
   // Generates: event Action<int> ScoreChanged;
   ```

5. **Bulk operations**
   ```csharp
   await grain.SetBulkAsync(new { Name = "Louis", Score = 100 });
   ```

---

## Appendix A: File Locations Reference

### Roslyn Source Files

| Purpose | Location |
|---------|----------|
| Syntax tree parsing | `Roslyn/src/Compilers/CSharp/Portable/Parser/` |
| Compilation creation | `Roslyn/src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs` |
| Source generators | `Roslyn/src/Compilers/CSharp/Portable/SourceGeneration/` |
| Generator driver | `Roslyn/src/Compilers/CSharp/Portable/SourceGeneration/GeneratorDriver.cs` |
| Lowering (transforms) | `Roslyn/src/Compilers/CSharp/Portable/Lowering/` |

### Orleans Source Files

| Purpose | Location |
|---------|----------|
| Code generator entry | `Orleans/src/Orleans.CodeGenerator/CodeGenerator.cs` |
| Proxy generator | `Orleans/src/Orleans.CodeGenerator/ProxyGenerator.cs` |
| Invokable generator | `Orleans/src/Orleans.CodeGenerator/InvokableGenerator.cs` |
| GrainReference base | `Orleans/src/Orleans.Core.Abstractions/Runtime/GrainReference.cs` |
| Grain reference activator | `Orleans/src/Orleans.Core/GrainReferences/GrainReferenceActivator.cs` |
| Persistent state | `Orleans/src/Orleans.Runtime/Storage/StateStorageBridge.cs` |

---

## Appendix B: Example Generated Files

For a grain defined as:

```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task RespawnAsync();
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }
    public partial int Score { get; set; }
    
    public Task RespawnAsync() { Score = 0; return Task.CompletedTask; }
}
```

### Generated: IPlayerGrain.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace MyGame.Grains
{
    partial interface IPlayerGrain
    {
        global::System.Threading.Tasks.Task<string> GetName();
        global::System.Threading.Tasks.Task SetName(string value);
        global::System.Threading.Tasks.Task<int> GetScore();
        global::System.Threading.Tasks.Task SetScore(int value);
    }
}
```

### Generated: PlayerGrain.State.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace MyGame.Grains
{
    partial class PlayerGrain
    {
        private string _name_backing = default!;
        private int _score_backing;
        
        public partial string Name
        {
            get => _name_backing;
            set => _name_backing = value;
        }
        
        public partial int Score
        {
            get => _score_backing;
            set => _score_backing = value;
        }
    }
}
```

### Generated: PlayerGrain.Methods.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace MyGame.Grains
{
    partial class PlayerGrain
    {
        global::System.Threading.Tasks.Task<string> global::MyGame.Grains.IPlayerGrain.GetName()
            => global::System.Threading.Tasks.Task.FromResult(this.Name);
        
        global::System.Threading.Tasks.Task global::MyGame.Grains.IPlayerGrain.SetName(string value)
        {
            this.Name = value;
            return global::System.Threading.Tasks.Task.CompletedTask;
        }
        
        global::System.Threading.Tasks.Task<int> global::MyGame.Grains.IPlayerGrain.GetScore()
            => global::System.Threading.Tasks.Task.FromResult(this.Score);
        
        global::System.Threading.Tasks.Task global::MyGame.Grains.IPlayerGrain.SetScore(int value)
        {
            this.Score = value;
            return global::System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
```

### Generated: Proxy_IPlayerGrain.g.cs (partial)

```csharp
// <auto-generated />
#nullable enable

namespace OrleansCodeGen.MyGame.Grains
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("OrleansCodeGen", "...")]
    internal sealed class Proxy_IPlayerGrain : global::Orleans.Runtime.GrainReference, global::MyGame.Grains.IPlayerGrain
    {
        public Proxy_IPlayerGrain(
            global::Orleans.Runtime.GrainReferenceShared shared, 
            global::Orleans.Runtime.IdSpan key) 
            : base(shared, key) 
        { }
        
        // Method proxies
        public global::System.Threading.Tasks.Task<string> GetName()
        {
            var request = base.GetInvokable<Invokable_IPlayerGrain_GetName>();
            return base.InvokeAsync<string>(request).AsTask();
        }
        
        public global::System.Threading.Tasks.Task SetName(string value)
        {
            var request = base.GetInvokable<Invokable_IPlayerGrain_SetName>();
            request.Arg0 = value;
            return base.InvokeAsync(request).AsTask();
        }
        
        public global::System.Threading.Tasks.Task<int> GetScore()
        {
            var request = base.GetInvokable<Invokable_IPlayerGrain_GetScore>();
            return base.InvokeAsync<int>(request).AsTask();
        }
        
        public global::System.Threading.Tasks.Task SetScore(int value)
        {
            var request = base.GetInvokable<Invokable_IPlayerGrain_SetScore>();
            request.Arg0 = value;
            return base.InvokeAsync(request).AsTask();
        }
        
        public global::System.Threading.Tasks.Task RespawnAsync()
        {
            var request = base.GetInvokable<Invokable_IPlayerGrain_RespawnAsync>();
            return base.InvokeAsync(request).AsTask();
        }
        
        // StateTask properties
        public global::Orleans.StateTask<string> Name => new global::Orleans.StateTask<string>(
            this,
            () => base.GetInvokable<Invokable_IPlayerGrain_GetName>(),
            v => { var r = base.GetInvokable<Invokable_IPlayerGrain_SetName>(); r.Arg0 = v; return r; }
        );
        
        public global::Orleans.StateTask<int> Score => new global::Orleans.StateTask<int>(
            this,
            () => base.GetInvokable<Invokable_IPlayerGrain_GetScore>(),
            v => { var r = base.GetInvokable<Invokable_IPlayerGrain_SetScore>(); r.Arg0 = v; return r; }
        );
    }
}
```

---

*End of Document*
