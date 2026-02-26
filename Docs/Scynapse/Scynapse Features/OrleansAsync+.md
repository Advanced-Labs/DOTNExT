# OrleansAsync+: Orleans Driver for Async+ Persistence

**Version**: 1.0 (December 2025)
**Status**: Proof of Concept - 7 of 9 core scenarios verified
**Package**: `Scynapse.AsyncPlus`

---

## Overview

OrleansAsync+ is the Orleans driver for Async+ automatic workflow persistence. It bridges the `IAsyncPersistenceService` interface to Orleans grains, providing:

- **Distributed persistence** via Orleans grain storage
- **RavenDB storage** for durable checkpoint data
- **Multi-silo visibility** - checkpoints visible cluster-wide
- **Grain lifecycle integration** - state survives grain deactivation
- **Virtual actor model** - stateless service, stateful persistence grains

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      CLIENT CODE                                             │
│  using (AsyncPersistenceContext.SetCurrent(service, workflowId))            │
│  {                                                                           │
│      await MyPersistableWorkflow(input);                                    │
│  }                                                                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│              ScynapseAsyncPersistenceService                               │
│  (Implements IAsyncPersistenceService)                                      │
│                                                                             │
│  • Resolves grain ID from WorkflowId or methodId                           │
│  • Serializes state machine to JSON bytes                                  │
│  • Tracks pending checkpoint tasks                                         │
│  • Ensures checkpoints complete before restore                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Orleans RPC)
┌─────────────────────────────────────────────────────────────────────────────┐
│              IAsyncStatePersistenceGrain                                     │
│  (One grain per workflow, keyed by workflowId)                              │
│                                                                             │
│  • SaveCheckpointAsync(stateNumber, bytes, typeName)                       │
│  • TryGetCheckpointAsync() → AsyncStateCheckpoint?                         │
│  • CompleteAsync(serializedResult)                                         │
│  • FaultAsync(exceptionType, message, stackTrace)                          │
│  • ClearAsync()                                                            │
│  • HasPersistedStateAsync()                                                │
│  • RequestDeactivationAsync()                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Orleans Storage Provider)
┌─────────────────────────────────────────────────────────────────────────────┐
│              RavenDbGrainStorage                                             │
│  (Implements IGrainStorage, ILifecycleParticipant)                          │
│                                                                             │
│  • ReadStateAsync<T>(stateName, grainId, grainState)                       │
│  • WriteStateAsync<T>(stateName, grainId, grainState)                      │
│  • ClearStateAsync<T>(stateName, grainId, grainState)                      │
│  • Document ID: orleans/{serviceId}/grains/{stateName}/{grainIdKey}        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│              RavenDB Database                                                │
│                                                                             │
│  Document: GrainStateDocument                                               │
│  {                                                                          │
│      "Id": "orleans/myservice/grains/asyncState/...",                       │
│      "GrainType": "asyncState",                                             │
│      "GrainId": "MyNamespace.MyClass.MyMethod",                             │
│      "StateData": <base64 JSON bytes>,                                      │
│      "ServiceId": "myservice",                                              │
│      "LastModifiedUtc": "2025-12-03T..."                                    │
│  }                                                                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Components

### 1. `ScynapseAsyncPersistenceService`

The Orleans implementation of `IAsyncPersistenceService`.

```csharp
namespace Scynapse.AsyncPlus.Services;

public class ScynapseAsyncPersistenceService : IAsyncPersistenceService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ScynapseAsyncPersistenceService> _logger;

    // Tracked pending operations per workflow (keyed by resolved grain ID)
    private readonly Dictionary<string, Task> _pendingCheckpoints = new();
    private readonly object _pendingLock = new();

    /// <summary>
    /// Resolves the grain ID to use for persistence operations.
    /// Uses WorkflowId from context if set, otherwise falls back to methodId.
    /// </summary>
    private string ResolveGrainId(string methodId)
    {
        var workflowId = AsyncPersistenceContext.WorkflowId;
        return workflowId ?? methodId;
    }

    public void Checkpoint(object stateMachine, int stateNumber, string methodId)
    {
        var grainId = ResolveGrainId(methodId);

        // Fire async checkpoint, track the task
        var checkpointTask = CheckpointInternalAsync(stateMachine, stateNumber, grainId);

        lock (_pendingLock)
        {
            _pendingCheckpoints[grainId] = checkpointTask;
        }

        // Don't await - state machine will suspend anyway
        // The task is tracked so TryRestore can ensure completion
    }

    public int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId)
    {
        var grainId = ResolveGrainId(methodId);

        // Ensure any pending checkpoint for this workflow completed first
        EnsurePendingCheckpointComplete(grainId);

        return TryRestoreGenericInternal(ref stateMachine, grainId);
    }
}
```

#### Key Design: Tracked Tasks Pattern

Checkpoints are fire-and-forget for performance, but tracked:

```csharp
public void Checkpoint(object stateMachine, int stateNumber, string methodId)
{
    // Fire async - don't await (MoveNext is sync)
    var checkpointTask = CheckpointInternalAsync(...);

    // Track the task
    _pendingCheckpoints[grainId] = checkpointTask;
}

public int TryRestore<T>(ref T stateMachine, string methodId)
{
    // Wait for any pending checkpoint before restore
    EnsurePendingCheckpointComplete(grainId);

    // Now safe to restore
    return TryRestoreInternal(...);
}
```

#### Serialization

```csharp
private static byte[] SerializeStateMachine(object stateMachine)
{
    var type = stateMachine.GetType();
    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    var fieldData = new Dictionary<string, object?>();
    foreach (var field in fields)
    {
        // Skip infrastructure fields
        if (fieldName.Contains("__awaiter") || typeName.Contains("Awaiter")) continue;
        if (fieldName.Contains("__builder") || typeName.Contains("MethodBuilder")) continue;
        if (fieldName.Contains("<>4__this")) continue;

        if (IsSerializableType(field.FieldType, value))
        {
            fieldData[field.Name] = field.GetValue(stateMachine);
        }
    }

    return JsonSerializer.SerializeToUtf8Bytes(fieldData);
}
```

### 2. `IAsyncStatePersistenceGrain`

Grain interface for persistence operations.

```csharp
namespace Scynapse.AsyncPlus;

public interface IAsyncStatePersistenceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Save a checkpoint at the given state number.
    /// </summary>
    Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName);

    /// <summary>
    /// Try to get the latest checkpoint for restoration.
    /// Returns null if no checkpoint exists or workflow is completed.
    /// </summary>
    Task<AsyncStateCheckpoint?> TryGetCheckpointAsync();

    /// <summary>
    /// Mark the workflow as completed successfully.
    /// Clears checkpoint data (no longer needed for recovery).
    /// </summary>
    Task CompleteAsync(byte[]? serializedResult);

    /// <summary>
    /// Mark the workflow as faulted.
    /// Preserves checkpoint for potential retry/investigation.
    /// </summary>
    Task FaultAsync(string exceptionType, string message, string? stackTrace);

    /// <summary>
    /// Clear all persisted state for this workflow.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Check if this workflow has any persisted state.
    /// </summary>
    Task<bool> HasPersistedStateAsync();

    /// <summary>
    /// Request the grain to deactivate when idle.
    /// Used for testing grain mobility scenarios.
    /// </summary>
    Task RequestDeactivationAsync();
}
```

### 3. `AsyncStatePersistenceGrain`

Grain implementation with Orleans persistent state.

```csharp
namespace Scynapse.AsyncPlus.Grains;

public class AsyncStatePersistenceGrain : Grain, IAsyncStatePersistenceGrain
{
    private readonly IPersistentState<AsyncStatePersistenceGrainState> _state;
    private readonly ILogger<AsyncStatePersistenceGrain> _logger;

    public AsyncStatePersistenceGrain(
        [PersistentState("asyncState", "AsyncPlusStorage")]
        IPersistentState<AsyncStatePersistenceGrainState> state,
        ILogger<AsyncStatePersistenceGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName)
    {
        _state.State.StateNumber = stateNumber;
        _state.State.SerializedStateMachine = serializedStateMachine;
        _state.State.StateMachineTypeName = stateMachineTypeName;
        _state.State.CheckpointTimeUtc = DateTime.UtcNow;
        _state.State.IsCompleted = false;
        _state.State.IsFaulted = false;

        await _state.WriteStateAsync();
    }

    public Task<AsyncStateCheckpoint?> TryGetCheckpointAsync()
    {
        if (_state.State.StateNumber < 0 ||
            _state.State.SerializedStateMachine == null ||
            _state.State.IsCompleted)
        {
            return Task.FromResult<AsyncStateCheckpoint?>(null);
        }

        return Task.FromResult<AsyncStateCheckpoint?>(new AsyncStateCheckpoint
        {
            StateNumber = _state.State.StateNumber,
            SerializedStateMachine = _state.State.SerializedStateMachine,
            StateMachineTypeName = _state.State.StateMachineTypeName!,
            CheckpointTimeUtc = _state.State.CheckpointTimeUtc ?? DateTime.UtcNow
        });
    }

    public Task RequestDeactivationAsync()
    {
        this.DeactivateOnIdle();
        return Task.CompletedTask;
    }
}
```

### 4. `AsyncStatePersistenceGrainState`

The persisted state model.

```csharp
[GenerateSerializer]
public sealed class AsyncStatePersistenceGrainState
{
    /// <summary>
    /// Current state number (-1 if no checkpoint).
    /// JsonProperty with Include needed because Orleans uses DefaultValueHandling.Ignore
    /// which would skip StateNumber=0 (first await point).
    /// </summary>
    [Id(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public int StateNumber { get; set; } = -1;

    [Id(1)]
    public byte[]? SerializedStateMachine { get; set; }

    [Id(2)]
    public string? StateMachineTypeName { get; set; }

    [Id(3)]
    public DateTime? CheckpointTimeUtc { get; set; }

    [Id(4)]
    public bool IsCompleted { get; set; }

    [Id(5)]
    public bool IsFaulted { get; set; }

    [Id(6)]
    public byte[]? SerializedResult { get; set; }

    [Id(7)]
    public string? FaultExceptionType { get; set; }

    [Id(8)]
    public string? FaultMessage { get; set; }

    [Id(9)]
    public string? FaultStackTrace { get; set; }
}
```

### 5. `RavenDbGrainStorage`

Custom Orleans grain storage provider for RavenDB.

```csharp
namespace Scynapse.AsyncPlus.Storage;

public class RavenDbGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>, IDisposable
{
    private readonly string _name;
    private readonly string _serviceId;
    private readonly RavenDbStorageOptions _options;
    private IDocumentStore? _documentStore;

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(name, _options.InitStage, Init, Close);
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var documentId = GetDocumentId(stateName, grainId);

        using var session = _documentStore!.OpenAsyncSession();
        var doc = await session.LoadAsync<GrainStateDocument>(documentId);

        if (doc?.StateData != null)
        {
            grainState.State = _grainStorageSerializer.Deserialize<T>(new BinaryData(doc.StateData));
            grainState.ETag = session.Advanced.GetChangeVectorFor(doc);
            grainState.RecordExists = true;
        }
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var documentId = GetDocumentId(stateName, grainId);
        var stateBytes = _grainStorageSerializer.Serialize(grainState.State).ToArray();

        using var session = _documentStore!.OpenAsyncSession();
        var doc = new GrainStateDocument
        {
            Id = documentId,
            GrainType = stateName,
            GrainId = grainId.ToString(),
            StateData = stateBytes,
            ServiceId = _serviceId,
            LastModifiedUtc = DateTime.UtcNow
        };

        await session.StoreAsync(doc, documentId);
        await session.SaveChangesAsync();
    }

    private string GetDocumentId(string stateName, GrainId grainId)
    {
        // Format: orleans/{serviceId}/grains/{stateName}/{grainIdKey}
        var grainIdKey = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(grainId.ToString()))
            .Replace('/', '_').Replace('+', '-');

        return $"orleans/{_serviceId}/grains/{stateName}/{grainIdKey}";
    }
}
```

---

## Configuration

### Silo Builder Setup

```csharp
var builder = Host.CreateDefaultBuilder()
    .UseOrleans(siloBuilder =>
    {
        siloBuilder
            // Configure RavenDB storage for Async+
            .AddRavenDbGrainStorage("AsyncPlusStorage", options =>
            {
                options.Urls = new[] { "http://localhost:8080" };
                options.DatabaseName = "OrleansGrainState";
                options.CreateDatabaseIfNotExists = true;
            })
            // Enable Async+ persistence
            .UseAsyncPlusPersistence("AsyncPlusStorage");
    });
```

### Convenience Method

```csharp
siloBuilder.UseAsyncPlusPersistenceWithRavenDb(options =>
{
    options.Urls = new[] { "http://localhost:8080" };
    options.DatabaseName = "OrleansGrainState";
});
```

### RavenDB Options

```csharp
public class RavenDbStorageOptions
{
    /// <summary>
    /// RavenDB server URLs.
    /// </summary>
    public string[] Urls { get; set; } = { "http://localhost:8080" };

    /// <summary>
    /// Database name for grain state.
    /// </summary>
    public string DatabaseName { get; set; } = "OrleansGrainState";

    /// <summary>
    /// Create database if it doesn't exist.
    /// </summary>
    public bool CreateDatabaseIfNotExists { get; set; } = true;

    /// <summary>
    /// Whether to delete documents on ClearState (vs setting StateData to null).
    /// </summary>
    public bool DeleteStateOnClear { get; set; } = false;

    /// <summary>
    /// Certificate path for secured RavenDB connections.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Lifecycle stage for initialization.
    /// </summary>
    public int InitStage { get; set; } = ServiceLifecycleStage.ApplicationServices;

    /// <summary>
    /// Custom serializer (defaults to Orleans JSON serializer).
    /// </summary>
    public IGrainStorageSerializer? GrainStorageSerializer { get; set; }
}
```

---

## Usage Examples

### Basic Usage

```csharp
// Get the persistence service from DI
var persistenceService = serviceProvider.GetRequiredService<IAsyncPersistenceService>();

// Generate unique workflow ID
var workflowId = $"order-{orderId}-{Guid.NewGuid():N}";

// Set up context and run workflow
using (AsyncPersistenceContext.SetCurrent(persistenceService, workflowId))
{
    var result = await ProcessOrderWorkflow(orderId);
}
```

### With Orleans Grain Client

```csharp
public class OrderController
{
    private readonly IGrainFactory _grainFactory;
    private readonly IAsyncPersistenceService _persistenceService;

    public async Task<OrderResult> ProcessOrder(int orderId)
    {
        var workflowId = $"order-{orderId}";

        using (AsyncPersistenceContext.SetCurrent(_persistenceService, workflowId))
        {
            return await ProcessOrderWorkflow(orderId);
        }
    }

    [Persistable]
    private async Task<OrderResult> ProcessOrderWorkflow(int orderId)
    {
        var order = await ValidateOrder(orderId);       // Checkpoint 0
        var payment = await ProcessPayment(order);      // Checkpoint 1
        var shipment = await ArrangeShipment(order);    // Checkpoint 2
        return new OrderResult(order, payment, shipment);
    }
}
```

### Concurrent Workflows

```csharp
// Each workflow gets isolated storage via unique workflowId
var tasks = orders.Select(async orderId =>
{
    var workflowId = $"order-{orderId}-{DateTime.UtcNow.Ticks}";

    using (AsyncPersistenceContext.SetCurrent(persistenceService, workflowId))
    {
        return await ProcessOrderWorkflow(orderId);
    }
});

await Task.WhenAll(tasks);
```

---

## Grain Lifecycle Integration

### Grain Activation

When a grain is activated (first call or after deactivation):
1. Orleans loads state from RavenDB via `ReadStateAsync`
2. `_state.State` contains persisted checkpoint data
3. Subsequent calls to `TryGetCheckpointAsync` return the loaded state

### Grain Deactivation

When a grain is deactivated (idle timeout, memory pressure, explicit request):
1. Any pending state writes are persisted
2. Grain is removed from memory
3. State remains in RavenDB
4. Next call reactivates grain and loads state

### Explicit Deactivation

```csharp
// From client code via the interface
await persistenceGrain.RequestDeactivationAsync();

// Grain implementation
public Task RequestDeactivationAsync()
{
    this.DeactivateOnIdle();
    return Task.CompletedTask;
}
```

---

## Multi-Silo Cluster Behavior

### Checkpoint Visibility

With RavenDB as shared storage, checkpoints are immediately visible across all silos:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         MULTI-SILO CLUSTER                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Silo 1                    Silo 2                    Silo 3            │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐ │
│  │ Grain A active  │      │                 │      │                 │ │
│  │ Checkpoint(0)   │──┐   │                 │      │                 │ │
│  └─────────────────┘  │   └─────────────────┘      └─────────────────┘ │
│                       │                                                 │
│                       ▼                                                 │
│               ┌────────────────┐                                       │
│               │    RavenDB     │                                       │
│               │  (Shared)      │                                       │
│               │  Checkpoint: 0 │ ◄──── All silos can read              │
│               └────────────────┘                                       │
│                       │                                                 │
│                       ▼                                                 │
│  If Silo 1 crashes, Silo 2 or 3 can resume:                           │
│  - Call grain → activates on healthy silo                              │
│  - ReadState loads checkpoint from RavenDB                            │
│  - TryRestore returns state 0                                         │
│  - Workflow continues                                                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Grain Mobility

Grains can move between silos:
1. Grain deactivates on Silo 1 (idle, shutdown, explicit)
2. Next call activates grain on any available silo
3. State loaded from RavenDB (same data regardless of silo)
4. Workflow continues from checkpoint

---

## StateNumber = 0 Issue

A critical bug to be aware of: Orleans JSON serialization uses `DefaultValueHandling.Ignore` which skips default values. Since `int` defaults to 0, `StateNumber = 0` (first await point) would not be serialized.

**Solution:**
```csharp
[Id(0)]
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
public int StateNumber { get; set; } = -1;
```

The `JsonProperty` attribute forces inclusion of the value even when 0.

---

## ClearStateAsync Behavior

Orleans `ClearStateAsync`:
- Sets `RecordExists = false`
- **Does NOT reset in-memory `State` object**

The grain must manually reset state:
```csharp
public async Task ClearAsync()
{
    await _state.ClearStateAsync();

    // CRITICAL: Also reset in-memory state!
    _state.State.StateNumber = -1;
    _state.State.SerializedStateMachine = null;
    _state.State.IsCompleted = false;
    // ... reset all fields
}
```

---

## Debugging

### Log Files

- **Roslyn codegen**: `%TEMP%/dotnext-roslyn-codegen.log`
- **Grain storage**: `%TEMP%/orleans-grain-storage-debug.log`

### RavenDB Studio

Query persisted checkpoints:
```
from 'orleans/myservice/grains/asyncState'
where GrainId = 'MyNamespace.MyClass.MyMethod'
```

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| No checkpoint saved | WorkflowId collision | Use unique workflowId per workflow instance |
| StateNumber=0 not persisted | JSON serialization | Add `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]` |
| Stale state after Clear | In-memory state not reset | Reset all `_state.State` fields after `ClearStateAsync` |
| Grain not activating | Port conflict | Check silo ports are available |
| RavenDB connection failed | URL or database issue | Verify RavenDB is running, database exists |

---

## Verified Scenarios

| Scenario | Cluster | Description | Status |
|----------|---------|-------------|--------|
| **C2** | 1 silo | Multiple concurrent workflows | PASS |
| **C3** | 1 silo | Nested async calls | PASS |
| **C4** | 1 silo | Exception recovery | PASS |
| **C8** | 3 silos | Multi-silo checkpoint visibility | PASS |
| **C9** | 2 silos | Grain deactivation/reactivation | PASS |
| **C5** | 1 silo | Large state serialization | Pending |
| **C6** | 2 silos | Silo failover mid-checkpoint | Pending |
| **C7** | 1 silo | Checkpoint version migration | Pending |

---

## Future Considerations

### Active Recovery

Current: **Passive recovery** - grain reactivates when called.

Future: **Active recovery** - detect incomplete workflows and resume:
- Query RavenDB for incomplete checkpoints
- Trigger grain reactivation
- Resume workflows automatically

Options:
1. Orleans Reminders (self-healing)
2. External orchestrator service
3. Silo lifecycle hooks on shutdown

### Graceful Silo Shutdown

On `ISiloLifecycleObserver.OnStopping`:
1. Query active persistence grains on this silo
2. Checkpoint current state
3. Optionally trigger reactivation on another silo

### Version Migration

When state machine changes (new fields, removed fields):
- Store schema version with checkpoint
- Migration logic in deserialization
- Forward compatibility considerations

---

## File Reference

| File | Purpose |
|------|---------|
| `ScynapseAsyncPersistenceService.cs` | `IAsyncPersistenceService` implementation |
| `IAsyncStatePersistenceGrain.cs` | Grain interface |
| `AsyncStatePersistenceGrain.cs` | Grain implementation |
| `AsyncStateCheckpoint.cs` | DTOs and grain state |
| `RavenDbGrainStorage.cs` | RavenDB storage provider |
| `RavenDbStorageOptions.cs` | Configuration options |
| `AsyncPlusHostingExtensions.cs` | Builder extensions |
