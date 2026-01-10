# NewOrleans Auto-Persistence & Auto-Interface Design v2

## Design Revisions Based on Analysis

This document refines the initial Varia design based on deeper analysis of:
- Field-to-property transformation patterns
- Transaction-based persistence vs per-method save
- Distributed transaction concerns
- Codegen patterns that minimize source modification
- Orleans interface/async conventions
- **Orleans property limitation in interfaces (CRITICAL)**
- **C# `new` and `virtual` keyword limitations**

---

## CRITICAL: Orleans Blocks Properties in Grain Interfaces

**Orleans has analyzer `ORLEANS0008` that BLOCKS properties in grain interfaces:**

```csharp
// This is a COMPILE ERROR in stock Orleans:
public interface IPlayerGrain : IGrain
{
    int Score { get; set; }  // ERROR: ORLEANS0008
}
```

**Rationale from Orleans:**
> "Grain interfaces cannot have properties because grain calls are RPC-based
> and properties would create ambiguity between local state and remote calls.
> All grain interaction must be through explicit method calls."

**File:** `src/Orleans.Analyzers/GrainInterfacePropertyDiagnosticAnalyzer.cs`

### NewOrleans Options

Since this is NewOrleans (our fork), we have three paths:

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A. Disable analyzer** | Remove `ORLEANS0008` for `[Memory]` grains | Enables property syntax | Need to modify proxy codegen too |
| **B. Generate methods** | Properties become `GetX()` / `SetX()` methods | Works today | Less elegant syntax |
| **C. Modify proxy codegen** | Properties in interface → async method calls under hood | Best of both worlds | Most work |

**Recommendation:** Option C long-term, Option B for MVP.

---

## C# Keyword Analysis: `new` and `virtual`

### The `new` Keyword

**Question:** Can `new` hide a member from another partial of the same class?

**Answer: NO.** `new` hides members from **base classes**, not within the same class.

```csharp
// This does NOT work:
public partial class PlayerGrain
{
    public void AddScore(int points) => Score += points;  // Original
}

public partial class PlayerGrain
{
    // ERROR: Can't use 'new' to hide member from same class
    public new void AddScore(int points) { ... }
}
```

**`new` only works for inheritance:**
```csharp
public class Base { public void Foo() { } }
public class Derived : Base
{
    public new void Foo() { }  // Hides Base.Foo - this WORKS
}
```

### The `virtual` Keyword

**Question:** Can we make dev's method `virtual` and override in generated partial?

**Answer: NO.** `override` requires **inheritance**, not partial classes.

```csharp
// This does NOT work:
public partial class PlayerGrain
{
    public virtual void AddScore(int points) => Score += points;
}

public partial class PlayerGrain
{
    // ERROR: Can't override in same class, even via partial
    public override void AddScore(int points) { ... }
}
```

**`virtual`/`override` only works for inheritance:**
```csharp
public class Base { public virtual void Foo() { } }
public class Derived : Base
{
    public override void Foo() { }  // Overrides Base.Foo - this WORKS
}
```

### Summary: Neither `new` nor `virtual` Help With Partials

Both keywords are for **inheritance hierarchies**, not **partial class composition**.

---

## The Explicit Interface Implementation Pattern (WORKS!)

Despite the above, **explicit interface implementation DOES work** because:
- The interface method and class method are **distinct members**
- No `new` or `virtual` needed
- Orleans calls go through interface → explicit impl is always used

```csharp
public partial class PlayerGrain : IPlayerGrain
{
    // Dev's method - untouched
    public void AddScore(int points) => Score += points;

    // Generated explicit interface impl - DIFFERENT member
    async Task IPlayerGrain.AddScore(int points)
    {
        __BeginTransaction();
        AddScore(points);  // Calls dev's method
        await __EndTransaction();
    }
}
```

**When `grain.AddScore(10)` is called:**
- Via `IPlayerGrain` reference → `IPlayerGrain.AddScore` (explicit impl) ✓
- Via `PlayerGrain` reference → `PlayerGrain.AddScore` (dev's method)

**Orleans always uses interface references**, so explicit impl is always invoked.

---

## Field + Explicit Interface Property Pattern (Your Proposal)

### The Pattern

**Your proposed approach:**
```csharp
// Dev's file (UNCHANGED):
public int Score = 20;  // Field stays as-is

// Generated interface:
public interface IPlayerGrain : IGrainWithStringKey
{
    int Score { get; set; }  // Property in interface (same name as field!)
}

// Generated explicit implementation:
partial class PlayerGrain : IPlayerGrain
{
    int IPlayerGrain.Score
    {
        get => Score;  // Reads the FIELD 'Score'
        set { Score = value; __OnPropertyChanged(); }  // Writes FIELD + triggers
    }
}
```

### C# Validity Analysis

**This IS valid C#.** The field `Score` and explicit interface property `IPlayerGrain.Score` are **distinct members**:

| Member | Type | Access | Name Resolution |
|--------|------|--------|-----------------|
| `Score` | Field | `this.Score` | Direct field access |
| `IPlayerGrain.Score` | Property (explicit) | `((IPlayerGrain)this).Score` | Interface property |

```csharp
// Both can coexist in same class:
public class PlayerGrain : IPlayerGrain
{
    public int Score = 20;  // Field

    int IPlayerGrain.Score  // Explicit interface property - DIFFERENT member
    {
        get => Score;       // Accesses the field
        set => Score = value;
    }
}
```

### What Blocks This in Orleans

1. **Analyzer `ORLEANS0008`** - Blocks properties in `IAddressable` interfaces
2. **Proxy codegen** - Orleans generates proxy classes that call methods; property support would need additions

### What NewOrleans Needs to Enable This

1. **Modify analyzer** - Allow properties in interfaces for `[Memory]` grains (or entirely)
2. **Modify proxy codegen** - Generate async method calls for property get/set:
   ```csharp
   // Generated proxy for IPlayerGrain.Score getter:
   int IPlayerGrain.Score
   {
       get => __GetPropertyAsync<int>("Score").Result;  // Or async pattern
   }
   ```
3. **Grain method invoker** - Handle property invocation in RPC layer

### Recommendation: Two-Phase Approach

**Phase 1 (MVP):** Generate methods instead of properties
```csharp
// Instead of: int Score { get; set; }
Task<int> GetScore();
Task SetScore(int value);
```

**Phase 2:** Enable property syntax
- Modify `ORLEANS0008` analyzer
- Add property support to Orleans proxy codegen
- Properties translate to method calls under the hood

---

## Key Design Decisions

### 1. Codegen Approach: Full Source Modification Allowed

We are **not limited to Roslyn source generators** (which are additive-only). Our codegen can:
- Modify developer source files directly
- Add `partial` keyword where missing
- Transform fields to properties
- Create nested codegen files visible in IDE

**Paradigm: "Code as Intent"**
- Developer code represents intent
- Codegen consults/transforms it at build time
- Developer-owned files are modified minimally and cleanly
- Codegen files can be overwritten freely (dev changes there are not preserved)

### 2. Attribute Design

**Separate concerns into distinct attributes:**

```csharp
// Interface generation (optional - can be auto-detected)
[Interface]                              // Defaults: auto-generate IPlayerGrain
[Interface(Name = "ICustomName")]        // Custom interface name
[Interface(Version = 2)]                 // Versioning support

// Memory/Persistence (required for auto-persistence)
[Memory]                                 // Defaults to MemoryPersistence.Auto (transactional)
[Memory(MemoryPersistence.Manual)]       // Dev calls SaveAsync()
[Memory(MemoryPersistence.Immediate)]    // Each setter persists immediately
[Memory(MemoryPersistence.OnDeactivation)] // Only on grain deactivation
[Memory(MemoryPersistence.Auto)]         // Transactional (default)
```

**MemoryPersistence enum:**
```csharp
public enum MemoryPersistence
{
    Auto,           // Transactional - batch persistence at method boundary
    Manual,         // Developer calls SaveAsync() explicitly
    Immediate,      // Each property setter triggers persistence
    OnDeactivation  // Only persist when grain deactivates
}
```

**Why separate attributes?**
- `[Interface]` could be optional (see section on auto-detection)
- `[Memory]` is the opt-in for persistence
- Clear separation of concerns
- Each can have its own parameters

### 3. Interface Auto-Detection

**Observation:** Orleans grains conventionally implement `I<GrainTypeName>`.

**Question:** If we can detect `class PlayerGrain : Grain` without a matching `IPlayerGrain`, should we auto-generate it without requiring `[Interface]`?

**Proposed behavior:**
```csharp
// Case 1: Interface exists - no generation needed
public class PlayerGrain : Grain, IPlayerGrain { }  // IPlayerGrain exists elsewhere

// Case 2: No interface - auto-generate if any [Memory] or other trigger attribute
[Memory]
public class PlayerGrain : Grain { }  // Auto-generates IPlayerGrain

// Case 3: Explicit control via [Interface]
[Interface(Name = "ICustomPlayer", Version = 2)]
public class PlayerGrain : Grain { }
```

**Detection logic:**
1. Find classes inheriting from `Grain` (or `Grain<T>`, etc.)
2. Check if corresponding `I<ClassName>` interface exists in solution
3. If not, and class has `[Memory]` or other trigger → generate interface
4. `[Interface]` attribute overrides defaults or provides explicit control

---

## Field-to-Property Transformation

### The Pattern

**Developer writes:**
```csharp
[Memory]
public partial class PlayerGrain : Grain
{
    public int Score = 20;
    private string name;

    public void AddScore(int points) => Score += points;
}
```

**Codegen transforms to:**
```csharp
[Memory]
public partial class PlayerGrain : Grain
{
    // Original field becomes backing field with __ prefix
    private int __Score = 20;

    // Property takes original name
    public int Score
    {
        get => __Score;
        set
        {
            if (__Score != value)
            {
                __Score = value;
                __OnPropertyChanged();
            }
        }
    }

    private string __name;
    private string name  // Private property (field was private)
    {
        get => __name;
        set
        {
            if (__name != value)
            {
                __name = value;
                __OnPropertyChanged();
            }
        }
    }

    // Method unchanged - "Score" now refers to property
    public void AddScore(int points) => Score += points;
}
```

**Why this works:**
- `Score += points` now uses property getter/setter
- All existing code referencing `Score` continues to work
- No need to rename usages throughout codebase
- Visibility preserved (public field → public property, private field → private property)

### Transformation Rules

| Original | Transformed |
|----------|-------------|
| `public int Score;` | `private int __Score;` + `public int Score { get; set; }` |
| `private int score;` | `private int __score;` + `private int score { get; set; }` |
| `protected string Name;` | `private string __Name;` + `protected string Name { get; set; }` |
| `public int Score = 20;` | `private int __Score = 20;` + property |
| `[NonPersistent] ILogger _log;` | Unchanged (excluded from persistence) |

---

## Transaction-Based Persistence Design

### Why Not Per-Method Auto-Save?

The original design proposed saving after each public method. Problems:
1. **Redundant saves**: If method changes 5 properties, and each property setter also saves, we get 6 saves
2. **Cascade inefficiency**: Method A calls Method B which changes state → both try to save
3. **No atomicity**: Partial failures leave inconsistent state

### The Transaction Pattern

**Core concept:** Property setters check for transaction context. If in transaction, defer persistence. If not, persist immediately (or based on `MemoryPersistence` mode).

```csharp
partial class PlayerGrain
{
    // Transaction context (not persisted)
    [NonPersistent]
    private GrainTransactionContext __transactionContext;

    public int Score
    {
        get => __Score;
        set
        {
            if (__Score != value)
            {
                __Score = value;
                __OnPropertyChanged();
            }
        }
    }

    private void __OnPropertyChanged()
    {
        if (__transactionContext != null)
        {
            // In transaction - mark for batch persist
            __transactionContext.MarkDirty(this);
        }
        else
        {
            // No transaction - immediate persist (if MemoryPersistence.Immediate)
            // Or queue for next transaction boundary
            __QueuePersistence();
        }
    }
}
```

### Method Transaction Wrapper Pattern

**Question from user:** Do we need `__AddScore_Impl`?

**Answer:** Possibly not. Let's explore alternatives.

#### Option A: Virtual + Override Pattern

```csharp
// Developer writes:
public void AddScore(int points) => Score += points;

// Codegen modifies to:
public virtual void AddScore(int points) => Score += points;

// Codegen adds in separate partial:
partial class PlayerGrain
{
    // Override that wraps with transaction
    public override void AddScore(int points)
    {
        __BeginTransactionIfNeeded();
        try
        {
            base.AddScore(points);  // Calls original
        }
        finally
        {
            __EndTransactionIfOwner();
        }
    }
}
```

**Problem:** Can't override in same class via partial.

#### Option B: Explicit Interface Implementation

**Key insight:** Grain method calls go through interfaces. If we implement interface explicitly, the explicit implementation is called when invoked via interface.

```csharp
// Developer writes:
[Memory]
public partial class PlayerGrain : Grain
{
    public int Score = 20;

    public void AddScore(int points) => Score += points;
}

// Codegen generates interface:
public interface IPlayerGrain : IGrainWithStringKey
{
    Task AddScore(int points);
    Task<int> GetScore();
    Task SetScore(int value);
}

// Codegen adds explicit implementation:
partial class PlayerGrain : IPlayerGrain
{
    // Explicit interface implementation - called when via interface
    async Task IPlayerGrain.AddScore(int points)
    {
        __BeginTransactionIfNeeded();
        try
        {
            AddScore(points);  // Calls the dev's method directly
        }
        finally
        {
            await __EndTransactionIfOwnerAsync();
        }
    }

    // Property accessors exposed via interface
    Task<int> IPlayerGrain.GetScore() => Task.FromResult(Score);

    async Task IPlayerGrain.SetScore(int value)
    {
        __BeginTransactionIfNeeded();
        try
        {
            Score = value;
        }
        finally
        {
            await __EndTransactionIfOwnerAsync();
        }
    }
}
```

**Why this works:**
1. Orleans grain calls go through interfaces: `grainFactory.GetGrain<IPlayerGrain>(id)`
2. When calling `grain.AddScore(10)`, the explicit `IPlayerGrain.AddScore` is invoked
3. Developer's `public void AddScore` remains untouched in their source
4. Explicit implementation wraps with transaction logic
5. Inside the wrapper, we call the dev's original method

**This is the cleanest pattern because:**
- Developer's method body stays exactly as written
- No `__Impl` suffix needed
- No `virtual` modification needed
- Clear separation: dev method = logic, interface method = infrastructure

#### Coexistence of Both Methods

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Developer's original - unchanged
    public void AddScore(int points) => Score += points;

    // Codegen explicit interface - handles transactions
    async Task IPlayerGrain.AddScore(int points)
    {
        // ... transaction wrapper ...
        AddScore(points);  // Calls dev's method
        // ... end transaction ...
    }
}
```

**When `playerGrain.AddScore(10)` is called:**
- If caller has `IPlayerGrain` reference → explicit interface impl is called ✓
- If caller has `PlayerGrain` reference directly → dev's method called (no transaction)

**Orleans always uses interface references**, so the explicit impl is always used for grain-to-grain calls.

---

## Transaction Context Design

### Local Transaction Context

```csharp
public class GrainTransactionContext
{
    public Guid TransactionId { get; }
    public bool IsOwner { get; set; }  // Did this method start the transaction?
    public HashSet<Grain> DirtyGrains { get; } = new();

    public void MarkDirty(Grain grain) => DirtyGrains.Add(grain);

    public async Task CommitAsync()
    {
        foreach (var grain in DirtyGrains)
        {
            await grain.__PersistStateAsync();
        }
        DirtyGrains.Clear();
    }
}
```

### Transaction Flow

```csharp
partial class PlayerGrain
{
    [ThreadStatic]  // Or AsyncLocal for async context
    private static GrainTransactionContext __currentTransaction;

    private void __BeginTransactionIfNeeded()
    {
        if (__currentTransaction == null)
        {
            __currentTransaction = new GrainTransactionContext
            {
                TransactionId = Guid.NewGuid(),
                IsOwner = true
            };
        }
        // Else: already in transaction, we're not the owner
    }

    private async Task __EndTransactionIfOwnerAsync()
    {
        if (__currentTransaction?.IsOwner == true)
        {
            await __currentTransaction.CommitAsync();
            __currentTransaction = null;
        }
    }
}
```

---

## Distributed Transaction Concerns

### Per-Node vs Cross-Node Transactions

**Current design:** Transaction context is per-node (thread/async-local).

**Problem:** Cascade across nodes:
1. Grain A on Node 1 calls Grain B on Node 2
2. Each has its own transaction context
3. If Node 1 commits but Node 2 fails → inconsistent state

### Analysis Needed

| Concern | Questions |
|---------|-----------|
| **Abort semantics** | Can we rollback across nodes? Orleans doesn't have built-in 2PC. |
| **Cascade tracking** | How to track which grains across nodes were modified? |
| **Failure modes** | What happens if node dies mid-transaction? |
| **Performance** | Cross-node coordination adds latency |

### Possible Approaches

#### Approach 1: Accept Per-Node Transactions (Simple)

- Each node manages its own transaction context
- Cross-node calls are "fire and forget" from transaction perspective
- Eventual consistency accepted
- No distributed abort capability

**Pros:** Simple, low overhead
**Cons:** No true ACID across nodes

#### Approach 2: Distributed Transaction Grain

Use Orleans grains themselves to coordinate:

```csharp
public interface ITransactionCoordinator : IGrainWithGuidKey
{
    Task<Guid> BeginTransactionAsync();
    Task RegisterParticipantAsync(GrainId grainId, SiloAddress silo);
    Task PrepareAsync();   // 2PC prepare phase
    Task CommitAsync();    // 2PC commit phase
    Task AbortAsync();     // Rollback all participants
}
```

**Flow:**
1. First grain creates `ITransactionCoordinator` grain
2. Each participating grain registers with coordinator
3. At end, coordinator runs 2PC protocol

**Pros:** True distributed transactions, uses Orleans naturally
**Cons:** Overhead of coordinator grain, complexity

#### Approach 3: Saga Pattern

- Each node commits locally
- On failure, emit compensating events
- Eventually consistent with compensation

**Pros:** Resilient, scalable
**Cons:** Eventual consistency only, complex compensation logic

### Recommendation

Start with **Approach 1** (per-node transactions) for simplicity. Design the infrastructure to allow upgrade to **Approach 2** later.

The transaction context should be designed to be replaceable:

```csharp
public interface IGrainTransactionContext
{
    Guid TransactionId { get; }
    bool IsOwner { get; }
    void MarkDirty(Grain grain);
    Task CommitAsync();
    Task AbortAsync();
}

// Simple per-node implementation
public class LocalGrainTransactionContext : IGrainTransactionContext { }

// Future: distributed implementation
public class DistributedGrainTransactionContext : IGrainTransactionContext { }
```

---

## Exception Handling

### Orleans Exception Model

Orleans has specific exception handling:
1. Exceptions in grains are serialized and sent back to caller
2. Grain remains active after exception (unless deactivated explicitly)
3. `OrleansException` subtypes for specific scenarios

### Exception + Transaction Interaction

```csharp
async Task IPlayerGrain.AddScore(int points)
{
    __BeginTransactionIfNeeded();
    try
    {
        AddScore(points);
    }
    catch (Exception ex)
    {
        if (__currentTransaction?.IsOwner == true)
        {
            // Owner: abort transaction, don't persist dirty state
            __currentTransaction.Abort();
            __currentTransaction = null;
        }
        throw;  // Rethrow for Orleans to handle
    }
    finally
    {
        // Only commit if no exception and we're owner
        if (__currentTransaction?.IsOwner == true)
        {
            await __currentTransaction.CommitAsync();
            __currentTransaction = null;
        }
    }
}
```

### Distributed Exception Concerns

| Scenario | Concern |
|----------|---------|
| Grain A calls Grain B, B throws | A's transaction should abort, but B may have already modified state |
| Network partition mid-transaction | Some nodes may have committed |
| Timeout during cross-node call | Partial state? |

**Needs deep analysis of:**
- Orleans' internal exception propagation
- Grain lifecycle on exception
- Network failure handling

---

## Orleans Async/Interface Conventions Analysis

### Async Methods

Orleans requires grain interface methods to return `Task` or `Task<T>`.

**User's method:** `public void AddScore(int points)`
**Interface method:** `Task AddScore(int points)`

The explicit interface implementation handles the async wrapper:

```csharp
// Dev writes sync
public void AddScore(int points) => Score += points;

// Interface is async
public interface IPlayerGrain
{
    Task AddScore(int points);
}

// Explicit impl bridges them
async Task IPlayerGrain.AddScore(int points)
{
    __BeginTransactionIfNeeded();
    try
    {
        AddScore(points);  // Sync call
    }
    finally
    {
        await __EndTransactionIfOwnerAsync();
    }
}
```

### User's Async Methods

If user writes async:
```csharp
public async Task AddScoreAsync(int points)
{
    await SomeAsyncOperation();
    Score += points;
}
```

The wrapper awaits it:
```csharp
async Task IPlayerGrain.AddScoreAsync(int points)
{
    __BeginTransactionIfNeeded();
    try
    {
        await AddScoreAsync(points);
    }
    finally
    {
        await __EndTransactionIfOwnerAsync();
    }
}
```

---

## Codegen File Visibility

### Nested File Pattern

Files should appear nested in IDE:

```
PlayerGrain.cs                    (dev's file)
├── PlayerGrain.g.cs              (generated - interface impl, transaction wrappers)
├── PlayerGrain.State.g.cs        (generated - state class)
└── IPlayerGrain.g.cs             (generated - interface)
```

**In .csproj:**
```xml
<ItemGroup>
  <Compile Update="PlayerGrain.g.cs">
    <DependentUpon>PlayerGrain.cs</DependentUpon>
  </Compile>
  <Compile Update="PlayerGrain.State.g.cs">
    <DependentUpon>PlayerGrain.cs</DependentUpon>
  </Compile>
</ItemGroup>
```

### Non-Parented Generated Files

Files without obvious parent go to dedicated folder:

```
/Generated/
├── GrainTransactionContext.g.cs
├── MemoryPersistenceExtensions.g.cs
└── ...
```

---

## Complete Example

### Developer Writes

```csharp
// PlayerGrain.cs
[Memory]
public partial class PlayerGrain : Grain
{
    public int Score = 20;
    private string name;
    private List<string> achievements = new();

    [NonPersistent]
    private ILogger<PlayerGrain> _logger;

    public PlayerGrain(ILogger<PlayerGrain> logger) => _logger = logger;

    public void AddScore(int points)
    {
        Score += points;
        _logger.LogInformation("Score is now {Score}", Score);
    }

    public int GetScore() => Score;

    public void SetName(string value) => name = value;

    public void UnlockAchievement(string achievement)
    {
        if (!achievements.Contains(achievement))
            achievements.Add(achievement);
    }
}
```

### Codegen Transforms Developer's File To

```csharp
// PlayerGrain.cs (modified by codegen)
[Memory]
public partial class PlayerGrain : Grain
{
    // Field → backing field with __ prefix
    private int __Score = 20;
    public int Score
    {
        get => __Score;
        set { if (__Score != value) { __Score = value; __OnPropertyChanged(); } }
    }

    private string __name;
    private string name
    {
        get => __name;
        set { if (__name != value) { __name = value; __OnPropertyChanged(); } }
    }

    private List<string> __achievements = new();
    private List<string> achievements
    {
        get => __achievements;
        set { if (__achievements != value) { __achievements = value; __OnPropertyChanged(); } }
    }

    [NonPersistent]
    private ILogger<PlayerGrain> _logger;  // Unchanged

    public PlayerGrain(ILogger<PlayerGrain> logger) => _logger = logger;  // Unchanged

    // Methods unchanged - they now use properties
    public void AddScore(int points)
    {
        Score += points;
        _logger.LogInformation("Score is now {Score}", Score);
    }

    public int GetScore() => Score;

    public void SetName(string value) => name = value;

    public void UnlockAchievement(string achievement)
    {
        if (!achievements.Contains(achievement))
            achievements.Add(achievement);
    }
}
```

### Codegen Generates

```csharp
// IPlayerGrain.g.cs
public interface IPlayerGrain : IGrainWithStringKey
{
    Task AddScore(int points);
    Task<int> GetScore();
    Task SetName(string value);
    Task UnlockAchievement(string achievement);

    // Property accessors
    Task<int> GetScoreValue();
    Task SetScoreValue(int value);
}
```

```csharp
// PlayerGrain.g.cs
partial class PlayerGrain : IPlayerGrain, ILifecycleParticipant<IGrainLifecycle>
{
    private GrainTransactionContext __transactionContext;
    private IStorage<PlayerGrain__State> __storage;

    // Transaction infrastructure
    private void __BeginTransactionIfNeeded()
    {
        if (__transactionContext == null)
        {
            __transactionContext = new GrainTransactionContext { IsOwner = true };
        }
    }

    private async Task __EndTransactionIfOwnerAsync()
    {
        if (__transactionContext?.IsOwner == true)
        {
            await __PersistStateAsync();
            __transactionContext = null;
        }
    }

    private void __OnPropertyChanged()
    {
        __transactionContext?.MarkDirty(this);
        // If no transaction and MemoryPersistence.Immediate, persist now
    }

    private async Task __PersistStateAsync()
    {
        __storage.State.Score = this.__Score;
        __storage.State.name = this.__name;
        __storage.State.achievements = this.__achievements;
        await __storage.WriteStateAsync();
    }

    // Lifecycle
    void ILifecycleParticipant<IGrainLifecycle>.Participate(IGrainLifecycle lifecycle)
    {
        lifecycle.Subscribe<PlayerGrain>(
            GrainLifecycleStage.SetupState,
            __OnActivateAsync,
            __OnDeactivateAsync);
    }

    private async Task __OnActivateAsync(CancellationToken ct)
    {
        __storage = Runtime.GetStorage<PlayerGrain__State>(GrainContext);
        await __storage.ReadStateAsync();
        __SyncFromStorage();
    }

    private async Task __OnDeactivateAsync(CancellationToken ct)
    {
        await __PersistStateAsync();
    }

    private void __SyncFromStorage()
    {
        __Score = __storage.State.Score;
        __name = __storage.State.name;
        __achievements = __storage.State.achievements ?? new();
    }

    // Explicit interface implementations with transaction wrapping
    async Task IPlayerGrain.AddScore(int points)
    {
        __BeginTransactionIfNeeded();
        try
        {
            AddScore(points);  // Calls dev's method
        }
        finally
        {
            await __EndTransactionIfOwnerAsync();
        }
    }

    Task<int> IPlayerGrain.GetScore()
    {
        return Task.FromResult(GetScore());
    }

    async Task IPlayerGrain.SetName(string value)
    {
        __BeginTransactionIfNeeded();
        try
        {
            SetName(value);
        }
        finally
        {
            await __EndTransactionIfOwnerAsync();
        }
    }

    async Task IPlayerGrain.UnlockAchievement(string achievement)
    {
        __BeginTransactionIfNeeded();
        try
        {
            UnlockAchievement(achievement);
        }
        finally
        {
            await __EndTransactionIfOwnerAsync();
        }
    }

    // Property accessor interface methods
    Task<int> IPlayerGrain.GetScoreValue() => Task.FromResult(Score);

    async Task IPlayerGrain.SetScoreValue(int value)
    {
        __BeginTransactionIfNeeded();
        try { Score = value; }
        finally { await __EndTransactionIfOwnerAsync(); }
    }
}
```

```csharp
// PlayerGrain.State.g.cs
[GenerateSerializer]
internal sealed class PlayerGrain__State
{
    [Id(0)] public int Score;
    [Id(1)] public string name;
    [Id(2)] public List<string> achievements;
}
```

---

## Summary of Key Patterns

| Pattern | Description |
|---------|-------------|
| **Field → Property** | Fields become `__field` backing + property with original name |
| **Explicit Interface Impl** | Dev's methods untouched; interface impl wraps with transaction |
| **Transaction Context** | Per-method boundary; first caller owns, nested calls participate |
| **Interface Auto-Detect** | If no `I<ClassName>` exists, auto-generate |
| **Separate Attributes** | `[Interface]` for interface, `[Memory]` for persistence |
| **Nested Codegen Files** | `.g.cs` files nested under source in IDE |

---

## Open Items for Analysis

1. **Distributed transactions** - Per-node vs coordinator grain vs saga
2. **Exception handling** - Orleans exception model + transaction abort
3. **Orleans async conventions** - Verify explicit interface impl works correctly
4. **Collection change tracking** - `List<T>.Add()` doesn't trigger property setter
5. **Reference type comparison** - Need deep equality for dirty checking?
6. **Thread safety** - `AsyncLocal<T>` vs `[ThreadStatic]` for transaction context
