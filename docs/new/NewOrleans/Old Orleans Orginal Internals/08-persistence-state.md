# Persistence and State Management

## Overview

Orleans provides a flexible abstraction for persisting grain state to various storage backends. State is loaded automatically on activation and saved explicitly by the grain.

**Location**: `src/Orleans.Runtime/Storage/`, `src/Orleans.Runtime/Facet/`

## Core Abstraction

### IStorage<TState>

```csharp
public interface IStorage<TState>
{
    TState State { get; set; }
    string Etag { get; }

    Task ReadStateAsync();
    Task WriteStateAsync();
    Task ClearStateAsync();
}
```

**Lifecycle**:
1. **Activation**: `ReadStateAsync()` called automatically
2. **Modification**: Application updates `State` property
3. **Persistence**: Explicit `WriteStateAsync()` call
4. **Deactivation**: Optional auto-save

## Using State in Grains

### IPersistentState<TState>

**Modern approach** (recommended):

```csharp
public class UserGrain : Grain, IUserGrain
{
    private readonly IPersistentState<UserProfile> _profile;

    public UserGrain(
        [PersistentState("profile", "Default")]
        IPersistentState<UserProfile> profile)
    {
        _profile = profile;
    }

    public Task<string> GetName() =>
        Task.FromResult(_profile.State.Name);

    public async Task SetName(string name)
    {
        _profile.State.Name = name;
        await _profile.WriteStateAsync();
    }
}
```

### Grain<TState>

**Legacy approach** (still supported):

```csharp
public class UserGrain : Grain<UserProfile>, IUserGrain
{
    public Task<string> GetName() =>
        Task.FromResult(State.Name);

    public async Task SetName(string name)
    {
        State.Name = name;
        await WriteStateAsync();
    }
}
```

## State Classes

```csharp
[GenerateSerializer]
public class UserProfile
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public string Email { get; set; }
    [Id(2)] public DateTime CreatedAt { get; set; }
}
```

**Requirements**:
- Serializable (marked with `[GenerateSerializer]`)
- Public properties with getters/setters
- Default constructor

## Storage Providers

### IGrainStorage

**Interface**:
```csharp
public interface IGrainStorage
{
    Task ReadStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState);

    Task WriteStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState);

    Task ClearStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState);
}
```

### Built-In Providers

**MemoryGrainStorage**:
- In-memory only (lost on restart)
- For development and testing

**AdoNetGrainStorage**:
- SQL databases (SQL Server, PostgreSQL, MySQL)
- Relational storage

**AzureBlobGrainStorage**:
- Azure Blob Storage
- Scalable, cost-effective

**AzureTableGrainStorage**:
- Azure Table Storage
- NoSQL, high throughput

**DynamoDBGrainStorage**:
- AWS DynamoDB
- Serverless, auto-scaling

### Configuration

```csharp
siloBuilder.AddAzureBlobGrainStorage("Default", options =>
{
    options.ConnectionString = "...";
    options.ContainerName = "grainstate";
});

siloBuilder.AddAdoNetGrainStorage("Default", options =>
{
    options.ConnectionString = "...";
    options.Invariant = "System.Data.SqlClient";
});
```

### Multiple Providers

```csharp
siloBuilder.AddAzureBlobGrainStorage("BlobStore", ...);
siloBuilder.AddAdoNetGrainStorage("SqlStore", ...);

// In grain
public UserGrain(
    [PersistentState("profile", "BlobStore")]
    IPersistentState<UserProfile> profile,
    [PersistentState("settings", "SqlStore")]
    IPersistentState<UserSettings> settings)
{
    // Use different providers for different state
}
```

## Optimistic Concurrency

### Etag Pattern

**Prevents lost updates**:

```csharp
// Provider returns etag on read
grainState.Etag = "abc123";

// Grain modifies state
grainState.State.Name = "New Name";

// On write, provider checks etag
WriteStateAsync(grainState);
// → UPDATE table SET ... WHERE id = ? AND etag = 'abc123'
// → If no rows updated, throw InconsistentStateException
```

**Handling Conflicts**:
```csharp
public async Task SetName(string name)
{
    while (true)
    {
        try
        {
            _profile.State.Name = name;
            await _profile.WriteStateAsync();
            break; // Success
        }
        catch (InconsistentStateException)
        {
            // Conflict - reload and retry
            await _profile.ReadStateAsync();
            // Merge or retry logic
        }
    }
}
```

## State Lifecycle

### Activation

```
Grain Activates
  → GrainLifecycle.OnStart()
  → SetupState stage
  → IStorage<T>.ReadStateAsync()
  → Deserialize from storage
  → State available
  → Activate stage
  → OnActivateAsync()
```

### Modification

```csharp
// Modify in-memory state
State.Counter++;

// State not persisted yet
// Lost if activation fails before WriteStateAsync()
```

### Persistence

```csharp
// Explicit write
await WriteStateAsync();
// → Serialize state
// → Call provider.WriteStateAsync()
// → Provider writes to storage
// → Update etag
```

### Deactivation

```csharp
// Optional: auto-save on deactivate
siloBuilder.Configure<PersistentStateOptions>(options =>
{
    options.WriteStateOnDeactivateAsync = true;
});
```

## Advanced Patterns

### Multiple State Objects

```csharp
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPersistentState<OrderData> _order;
    private readonly IPersistentState<PaymentData> _payment;

    public OrderGrain(
        [PersistentState("order")] IPersistentState<OrderData> order,
        [PersistentState("payment")] IPersistentState<PaymentData> payment)
    {
        _order = order;
        _payment = payment;
    }

    // Both states loaded independently
    // Can use different providers
}
```

### Custom State Name

```csharp
// State name defaults to parameter name
[PersistentState("custom-name", "StorageName")]

// Useful for versioning:
[PersistentState("user-profile-v2", "Default")]
```

### State Initialization

```csharp
public override async Task OnActivateAsync(CancellationToken ct)
{
    if (string.IsNullOrEmpty(_profile.State.Id))
    {
        // First activation - initialize
        _profile.State.Id = this.GetPrimaryKeyString();
        _profile.State.CreatedAt = DateTime.UtcNow;
        await _profile.WriteStateAsync();
    }

    await base.OnActivateAsync(ct);
}
```

## Transactional State

For ACID transactions across grains:

```csharp
public class AccountGrain : Grain, IAccountGrain
{
    private readonly ITransactionalState<AccountBalance> _balance;

    public AccountGrain(
        [TransactionalState("balance")]
        ITransactionalState<AccountBalance> balance)
    {
        _balance = balance;
    }

    [Transaction(TransactionOption.CreateOrJoin)]
    public async Task Transfer(IAccountGrain target, decimal amount)
    {
        // Reads and writes are transactional
        await _balance.PerformUpdate(state =>
        {
            if (state.Amount < amount)
                throw new InsufficientFundsException();
            state.Amount -= amount;
        });

        await target.Deposit(amount);
        // Commits atomically
    }
}
```

## Performance Considerations

### Read vs. Write Patterns

**Read-Heavy**: Consider caching
```csharp
private DateTime _lastRead;
private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

public async Task<UserProfile> GetProfile()
{
    if (DateTime.UtcNow - _lastRead > _cacheTimeout)
    {
        await _profile.ReadStateAsync();
        _lastRead = DateTime.UtcNow;
    }
    return _profile.State;
}
```

**Write-Heavy**: Consider batching
```csharp
private bool _isDirty;

public Task IncrementCounter()
{
    State.Counter++;
    _isDirty = true;
    return Task.CompletedTask;
}

// Background timer
private async Task FlushPeriodically()
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (_isDirty)
        {
            await WriteStateAsync();
            _isDirty = false;
        }
    }
}
```

### Serialization Cost

- State is serialized on every write
- Keep state objects reasonably sized (<1 MB)
- For large data, consider storing reference/URL

## Summary

Orleans' persistence system provides:

1. **Simple abstraction** for state management
2. **Automatic loading** on activation
3. **Explicit saving** for control
4. **Pluggable providers** for any backend
5. **Optimistic concurrency** via etags
6. **Transactional state** for ACID guarantees

---

**Next**: [Serialization System](09-serialization.md)
