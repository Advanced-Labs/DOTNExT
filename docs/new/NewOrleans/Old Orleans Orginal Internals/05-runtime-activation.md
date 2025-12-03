# Runtime and Activation System

## Overview

The Orleans Runtime is the core server-side component that manages grain activations throughout their lifecycle. Understanding the runtime and activation system is crucial for understanding how Orleans executes grain code efficiently and reliably.

**Location**: `src/Orleans.Runtime/`

## Key Components

### Silo

**File**: `src/Orleans.Runtime/Silo/Silo.cs`

The **Silo** is the top-level server process that hosts grain activations.

**Responsibilities**:
- Initialize and coordinate all runtime services
- Participate in cluster membership
- Host grain activations
- Process grain method calls
- Manage lifecycle stages

**Lifecycle Stages**:
```
Create
  → ServiceProviderStartup (DI container setup)
  → RuntimeInitialize (Initialize runtime services)
  → ClusterMembership (Join cluster)
  → GrainDirectoryStartup (Start directory)
  → BecomeActive (Start accepting work)
  → [Active State]
  → StopGrainDirectory
  → LeaveCluster
  → RuntimeStopGracefully
  → Dispose
```

### Catalog

**File**: `src/Orleans.Runtime/Catalog/Catalog.cs`

The **Catalog** is the central registry of all grain activations on the silo.

**Data Structure**:
```csharp
// Simplified
class Catalog
{
    // GrainId → ActivationData
    private readonly ConcurrentDictionary<GrainId, ActivationData> activations;

    // 32-way lock striping for concurrent activation creation
    private readonly object[] locks = new object[32];
}
```

**Key Operations**:

```csharp
// Get existing activation
bool TryGetGrainContext(GrainId grainId, out IGrainContext? context);

// Create new activation
Task<IGrainContext> GetOrCreateActivation(
    GrainId grainId,
    SiloAddress targetSilo,
    string? requestContextData);

// Remove activation
void UnregisterGrainActivation(ActivationData activation);

// Get all activations (for diagnostics)
List<IGrainContext> GetActivations();
```

**Concurrency Strategy**:
- **Read path**: Lock-free lookup in concurrent dictionary
- **Write path**: Lock striping (hash grain ID to one of 32 locks)
- Minimizes contention during activation creation

### ActivationData

**File**: `src/Orleans.Runtime/Catalog/ActivationData.cs`

**ActivationData** represents a single grain activation instance in memory.

**Key State**:
```csharp
class ActivationData : IGrainContext
{
    // Identity
    public GrainId GrainId { get; }
    public ActivationId ActivationId { get; }
    public GrainReference GrainReference { get; }

    // The actual grain instance
    public object GrainInstance { get; set; }

    // Activation-scoped DI container
    public IServiceProvider ActivationServices { get; }

    // Lifecycle management
    public IGrainLifecycle ObservableLifecycle { get; }

    // Message processing
    public WorkItemGroup WorkItemGroup { get; }

    // State management
    public ActivationState State { get; set; }

    // Scheduling
    public DateTime CollectionTicket { get; set; }
    public bool IsInactive { get; }

    // Reentrancy
    public bool IsReentrant { get; }
}
```

**Activation States**:
```csharp
enum ActivationState
{
    Create,        // Just created
    Activating,    // OnActivateAsync() running
    Valid,         // Ready to process messages
    Deactivating,  // OnDeactivateAsync() running
    Invalid        // Deactivated
}
```

**State Transitions**:
```
Create
  → Activating (OnActivateAsync called)
  → Valid (Ready for work)
  → Deactivating (DeactivateAsync called)
  → Invalid (Cleaned up)
```

## Activation Lifecycle

### Creation Flow

```
1. Client calls grain method
   ↓
2. GrainDirectory.Lookup(grainId)
   ↓ (not found)
3. PlacementService.GetPlacementDecision(grainId)
   ↓
4. Send ActivationRequest to selected silo
   ↓
5. Catalog.GetOrCreateActivation(grainId)
   ↓
6. Lock on grain ID (prevent duplicates)
   ↓
7. Check again if activation exists
   ↓ (still doesn't exist)
8. Create ActivationData
   ↓
9. Create activation-scoped DI container
   ↓
10. IGrainActivator.CreateInstance(context)
    ↓
11. Register with Catalog
    ↓
12. Start GrainLifecycle (OnActivateAsync)
    ↓
13. Load state (if stateful)
    ↓
14. Activation ready (state = Valid)
    ↓
15. Register with GrainDirectory
    ↓
16. Process queued message
```

**Code Flow**:
```csharp
// Simplified activation creation
async Task<ActivationData> CreateActivation(GrainId grainId)
{
    // 1. Create activation data
    var activation = new ActivationData(
        grainId,
        ActivationId.NewId(),
        this.silo);

    // 2. Create DI scope
    activation.ActivationServices = CreateScope();

    // 3. Create grain instance
    var grainInstance = grainActivator.CreateInstance(activation);
    activation.GrainInstance = grainInstance;

    // 4. Register in catalog
    catalog.RegisterActivation(activation);

    // 5. Start lifecycle
    await activation.ObservableLifecycle.OnStart();

    // OnStart triggers:
    //   - IStorageGrain.ReadStateAsync() if stateful
    //   - IGrainBase.OnActivateAsync()

    // 6. Mark as valid
    activation.State = ActivationState.Valid;

    return activation;
}
```

### Activation

**Lifecycle Stages**:
```csharp
// In order of execution
public static class GrainLifecycleStage
{
    public const int First = int.MinValue;
    public const int SetupState = 1000;        // Load state
    public const int Activate = 2000;          // OnActivateAsync
    public const int Last = int.MaxValue;
}
```

**OnActivateAsync Hook**:
```csharp
public class UserGrain : Grain, IUserGrain
{
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // Custom initialization logic
        // Called automatically after state is loaded
        await base.OnActivateAsync(ct);
    }
}
```

**State Loading** (for stateful grains):
```csharp
// Happens in SetupState stage
class StorageStateFacet<TState>
{
    public async Task OnActivate()
    {
        await storage.ReadStateAsync();
        // State now available
    }
}
```

### Deactivation

**Triggers**:
1. **Idle timeout**: No messages for configured duration (default: 2 hours)
2. **Explicit**: Grain calls `DeactivateOnIdle()`
3. **Shutdown**: Silo is stopping
4. **Failure**: Grain throws unhandled exception

**Deactivation Flow**:
```
1. ActivationCollector marks activation for collection
   ↓
2. Check if activation is idle (no messages in queue)
   ↓
3. Set state to Deactivating
   ↓
4. Stop accepting new messages
   ↓
5. Process in-flight messages
   ↓
6. Call OnDeactivateAsync()
   ↓
7. Optionally save state
   ↓
8. Unregister from GrainDirectory
   ↓
9. Unregister from Catalog
   ↓
10. Dispose activation-scoped services
    ↓
11. Set state to Invalid
```

**OnDeactivateAsync Hook**:
```csharp
public class UserGrain : Grain, IUserGrain
{
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Cleanup logic
        // Called automatically before disposal

        if (reason.ReasonCode == DeactivationReasonCode.ApplicationRequested)
        {
            // Explicit deactivation
        }

        await base.OnDeactivateAsync(reason, ct);
    }
}
```

### Deactivation Reasons

```csharp
public enum DeactivationReasonCode
{
    None,
    ApplicationRequested,      // DeactivateOnIdle() called
    ActivationIdle,           // Idle timeout expired
    ActivationUnresponsive,   // Not processing messages
    ShuttingDown,             // Silo stopping
    ActivationFailed,         // Unhandled exception
    DuplicateActivation,      // Duplicate detected
    MigrationRequested        // Manual migration
}

public struct DeactivationReason
{
    public DeactivationReasonCode ReasonCode { get; }
    public string Description { get; }
}
```

### Activation Collection

**File**: `src/Orleans.Runtime/Catalog/ActivationCollector.cs`

**Purpose**: Garbage collection for idle activations.

**How It Works**:
```csharp
// Periodic scan (default: every 10 seconds)
async Task CollectActivations()
{
    var now = DateTime.UtcNow;
    var idleTimeout = TimeSpan.FromHours(2); // Configurable

    foreach (var activation in catalog.GetActivations())
    {
        if (activation.State != ActivationState.Valid)
            continue;

        // Check if idle
        var idleDuration = now - activation.CollectionTicket;
        if (idleDuration < idleTimeout)
            continue;

        // Check if any pending messages
        if (activation.WorkItemGroup.HasPendingWork)
        {
            // Reset idle timer
            activation.CollectionTicket = now;
            continue;
        }

        // Deactivate
        await DeactivateActivation(
            activation,
            new DeactivationReason(
                DeactivationReasonCode.ActivationIdle,
                $"Idle for {idleDuration}"));
    }
}
```

**Configuration**:
```csharp
siloBuilder.Configure<GrainCollectionOptions>(options =>
{
    options.CollectionAge = TimeSpan.FromHours(2);
    options.CollectionQuantum = TimeSpan.FromSeconds(10);
});
```

## Scheduler System

**Location**: `src/Orleans.Runtime/Scheduler/`

### OrleansTaskScheduler

**Purpose**: Global task scheduler for the silo.

**Architecture**:
```
OrleansTaskScheduler
    ├─ Thread Pool (N worker threads)
    ├─ Work Queue (global queue of WorkItemGroups)
    └─ WorkItemGroup per activation
            └─ Message Queue (per-activation)
```

**Key Responsibilities**:
- Manage worker threads
- Schedule work items across activations
- Fair scheduling (prevent starvation)
- Priority support

### WorkItemGroup

**File**: `src/Orleans.Runtime/Scheduler/WorkItemGroup.cs`

**Purpose**: Per-activation work queue.

**Structure**:
```csharp
class WorkItemGroup
{
    private readonly Queue<Message> workItems;
    private readonly ActivationData activation;

    // Execution state
    private WorkGroupStatus state;

    // Reentrancy
    private int runningRequests;
}

enum WorkGroupStatus
{
    Waiting,    // No work or waiting for scheduler
    Runnable,   // Has work, in scheduler queue
    Running     // Currently executing
}
```

**Scheduling Algorithm**:
```csharp
// Simplified
async Task ProcessWorkItems()
{
    while (true)
    {
        // Get next work item
        if (!workItems.TryDequeue(out var message))
            break;

        // Check reentrancy
        if (runningRequests > 0 && !CanInterleave(message))
        {
            // Put back in queue, reschedule later
            workItems.Enqueue(message);
            break;
        }

        runningRequests++;

        try
        {
            // Execute grain method
            await ProcessMessage(message);
        }
        finally
        {
            runningRequests--;
        }

        // Check if more work
        if (workItems.Count > 0 && runningRequests == 0)
        {
            // Reschedule
            scheduler.QueueWorkItem(this);
        }
    }
}
```

### Turn-Based Execution

**A Turn** = Execution of a grain method from entry to first `await` or completion.

**Example**:
```csharp
public async Task ProcessOrder(Order order)
{
    // === TURN 1 START ===
    ValidateOrder(order);
    _orders.Add(order);
    // === TURN 1 END (await) ===

    await _paymentGrain.ProcessPayment(order.Total);

    // === TURN 2 START ===
    SendConfirmationEmail(order);
    // === TURN 2 END (await) ===

    await WriteStateAsync();

    // === TURN 3 START ===
    return;
    // === TURN 3 END ===
}
```

**Turn Execution Guarantees**:
1. **Single-threaded**: One turn at a time per activation (unless reentrant)
2. **Non-preemptive**: Turn runs to completion or await
3. **FIFO**: Messages processed in order (unless reordered)

**Benefits**:
- No locks needed in grain code
- Simple reasoning about concurrency
- Natural backpressure (queue depth)

### Reentrancy

**Purpose**: Allow interleaved execution for specific scenarios.

**Grain-Level**:
```csharp
[Reentrant]
public class ChatRoomGrain : Grain, IChatRoomGrain
{
    // Can process multiple messages concurrently
}
```

**Method-Level**:
```csharp
public interface IMyGrain : IGrain
{
    [AlwaysInterleave]
    Task<int> GetCount(); // Read-only, can interleave

    Task UpdateState(); // Sequential
}
```

**Custom Interleaving**:
```csharp
[MayInterleave(nameof(MayInterleave))]
public class CustomGrain : Grain
{
    public static bool MayInterleave(IInvokable req)
    {
        // Custom logic
        return req is ReadOnlyRequest;
    }
}
```

### ActivationTaskScheduler

**Purpose**: Custom `TaskScheduler` for grain activations.

**Integration**:
```csharp
// When executing grain code
Task.Factory.StartNew(
    () => grainMethod(),
    CancellationToken.None,
    TaskCreationOptions.None,
    activation.TaskScheduler); // Custom scheduler
```

**Ensures**:
- Grain code runs on Orleans threads
- Proper request context propagation
- Turn-based execution semantics

## GrainActivator

**File**: `src/Orleans.Runtime/Activation/IGrainActivator.cs`

**Purpose**: Factory for creating grain instances.

**Interface**:
```csharp
public interface IGrainActivator
{
    object CreateInstance(IGrainContext context);
    void DisposeInstance(IGrainContext context, object instance);
}
```

**Default Implementation** (DI-based):
```csharp
class DefaultGrainActivator : IGrainActivator
{
    public object CreateInstance(IGrainContext context)
    {
        var grainType = GetGrainType(context.GrainId);

        // Resolve from activation-scoped DI container
        return ActivatorUtilities.CreateInstance(
            context.ActivationServices,
            grainType);
    }

    public void DisposeInstance(IGrainContext context, object instance)
    {
        (instance as IDisposable)?.Dispose();
    }
}
```

**Custom Activators**:
```csharp
// Register custom activator
services.AddSingleton<IGrainActivator, MyCustomActivator>();
```

## Dependency Injection

### Service Scopes

Orleans uses **three levels** of DI scopes:

1. **Silo-Scoped**: Services shared across entire silo
   ```csharp
   services.AddSingleton<IMyService, MyService>();
   ```

2. **Grain-Type-Scoped**: Services per grain type (rare)

3. **Activation-Scoped**: Services per grain activation
   ```csharp
   services.AddScoped<IMyService, MyService>();
   ```

### Grain Constructor Injection

```csharp
public class UserGrain : Grain, IUserGrain
{
    private readonly ILogger<UserGrain> _logger;
    private readonly IEmailService _emailService;

    public UserGrain(
        ILogger<UserGrain> logger,
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }
}
```

### Persistent State Injection

```csharp
public class UserGrain : Grain, IUserGrain
{
    private readonly IPersistentState<UserState> _state;

    public UserGrain(
        [PersistentState("user", "Default")]
        IPersistentState<UserState> state)
    {
        _state = state;
    }
}
```

## Performance Optimizations

### Catalog Lookup

- **Lock-free reads**: `ConcurrentDictionary` for fast lookup
- **Lock striping**: 32 locks minimize write contention
- **O(1) lookup**: Direct dictionary access

### Message Processing

- **Zero-copy paths**: Messages not copied unnecessarily
- **Pooling**: Message objects pooled and reused
- **Batching**: Multiple messages can be batched

### Scheduler

- **Work stealing**: Idle threads steal work from others
- **Fair scheduling**: Prevents activation starvation
- **Adaptive**: Adjusts based on load

## Configuration

```csharp
siloBuilder.Configure<SiloOptions>(options =>
{
    options.SiloName = "Silo1";
});

siloBuilder.Configure<GrainCollectionOptions>(options =>
{
    options.CollectionAge = TimeSpan.FromHours(2);
    options.CollectionQuantum = TimeSpan.FromSeconds(10);
});

siloBuilder.Configure<SchedulingOptions>(options =>
{
    options.MaxActiveThreads = Environment.ProcessorCount;
});
```

## Summary

The Runtime and Activation System:

1. **Manages** grain lifecycle from creation to destruction
2. **Schedules** grain execution efficiently
3. **Enforces** turn-based concurrency
4. **Provides** activation collection (garbage collection)
5. **Integrates** with dependency injection
6. **Optimizes** for high throughput and low latency

Key components:
- **Silo**: Top-level host
- **Catalog**: Activation registry
- **ActivationData**: Grain instance metadata
- **Scheduler**: Work scheduling
- **WorkItemGroup**: Per-activation message queue
- **GrainActivator**: Instance factory

---

**Next**: [Clustering and Membership](06-clustering-membership.md)
