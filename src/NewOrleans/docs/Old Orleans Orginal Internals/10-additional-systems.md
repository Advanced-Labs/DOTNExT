# Additional Systems

## Overview

Beyond the core runtime systems, Orleans includes several specialized subsystems for advanced scenarios.

## Grain Directory System

**Location**: `src/Orleans.Runtime/GrainDirectory/`

### Purpose

Distributed hash table (DHT) that tracks which silo hosts each grain activation.

### Key Components

**LocalGrainDirectory**:
- Owns a partition of the grain ID space
- Maps `GrainId` → `ActivationAddress`
- Handles registration/lookup/unregistration

**GrainLocator**:
- Finds or creates grain activations
- Caching layer for performance
- Coordinates with directory and placement

### Directory Operations

**Register**:
```csharp
await directory.Register(grainAddress);
// Stores GrainId → (SiloAddress, ActivationId) mapping
```

**Lookup**:
```csharp
var address = await directory.Lookup(grainId);
// Returns activation address or null
```

**Unregister**:
```csharp
await directory.Unregister(grainAddress);
// Removes mapping on deactivation
```

### Partitioning

Uses consistent hashing:
- Each silo owns portion of grain ID space
- Minimizes remapping on membership changes
- Successor silos take over on failure

## Placement System

**Location**: `src/Orleans.Runtime/Placement/`

### Purpose

Decides which silo should host a new grain activation.

### Placement Strategies

**RandomPlacement**:
```csharp
[RandomPlacement]
public class MyGrain : Grain, IMyGrain { }
```
- Picks random silo
- Simple, good for uniform load

**PreferLocalPlacement**:
```csharp
[PreferLocalPlacement]
public class MyGrain : Grain, IMyGrain { }
```
- Prefers local silo (caller's silo)
- Reduces network hops

**HashBasedPlacement**:
```csharp
[HashBasedPlacement]
public class MyGrain : Grain, IMyGrain { }
```
- Consistent hashing of grain ID
- Same grain always on same silo (unless silo fails)
- Good for caching

**ActivationCountPlacement**:
```csharp
[ActivationCountBasedPlacement]
public class MyGrain : Grain, IMyGrain { }
```
- Load balancing based on activation count
- Distributes grains evenly

**StatelessWorkerPlacement**:
```csharp
[StatelessWorker(maxLocalWorkers: 10)]
public class MyGrain : Grain, IMyGrain { }
```
- Multiple activations per grain ID
- Allows concurrent calls
- Good for stateless, CPU-bound work

### Custom Placement

```csharp
public class MyPlacementStrategy : PlacementStrategy { }

public class MyPlacementDirector : IPlacementDirector
{
    public Task<SiloAddress> OnAddActivation(
        PlacementStrategy strategy,
        PlacementTarget target,
        IPlacementContext context)
    {
        // Custom logic
        return Task.FromResult(selectedSilo);
    }
}
```

## Timers and Reminders

**Location**: `src/Orleans.Runtime/Timers/`, `src/Orleans.Reminders/`

### Timers

**Non-durable**, in-memory timers:

```csharp
public class MyGrain : Grain, IMyGrain
{
    private IDisposable _timer;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        _timer = RegisterTimer(
            OnTimerTick,
            null,
            dueTime: TimeSpan.FromSeconds(10),
            period: TimeSpan.FromMinutes(1));

        return base.OnActivateAsync(ct);
    }

    private Task OnTimerTick(object state)
    {
        // Timer callback
        return Task.CompletedTask;
    }
}
```

**Characteristics**:
- Lost if grain deactivates
- Cheap (in-memory only)
- Good for transient tasks

### Reminders

**Durable**, persistent timers:

```csharp
public class MyGrain : Grain, IMyGrain, IRemindable
{
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await RegisterOrUpdateReminder(
            "daily-task",
            dueTime: TimeSpan.FromHours(1),
            period: TimeSpan.FromHours(24));

        await base.OnActivateAsync(ct);
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == "daily-task")
        {
            // Handle reminder
        }
    }
}
```

**Characteristics**:
- Survive grain deactivation and silo restart
- Stored in external storage (provider required)
- More expensive than timers

### Reminder Providers

```csharp
siloBuilder.UseAdoNetReminderService(options =>
{
    options.ConnectionString = "...";
});
```

## Streams

**Location**: `src/Orleans.Streaming/`

### Purpose

Managed, distributed stream processing for pub/sub patterns.

### Stream Providers

**EventHub Streams**:
```csharp
siloBuilder.AddEventHubStreams("StreamProvider", options =>
{
    options.ConnectionString = "...";
    options.EventHubName = "events";
});
```

**Simple Message Streams** (memory):
```csharp
siloBuilder.AddMemoryStreams("StreamProvider");
```

### Producer

```csharp
public class ProducerGrain : Grain, IProducerGrain
{
    public async Task PublishEvent(string streamId, string message)
    {
        var streamProvider = this.GetStreamProvider("StreamProvider");
        var stream = streamProvider.GetStream<string>("events", streamId);

        await stream.OnNextAsync(message);
    }
}
```

### Consumer

```csharp
public class ConsumerGrain : Grain, IConsumerGrain
{
    private StreamSubscriptionHandle<string> _subscription;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("StreamProvider");
        var stream = streamProvider.GetStream<string>("events", "mystream");

        _subscription = await stream.SubscribeAsync(OnNextAsync);

        await base.OnActivateAsync(ct);
    }

    private Task OnNextAsync(string item, StreamSequenceToken token)
    {
        // Handle message
        return Task.CompletedTask;
    }
}
```

### Features

- **Guaranteed delivery**: At-least-once semantics
- **Batch processing**: Receive multiple items
- **Filtering**: Subscribe to subset of events
- **Replay**: Resume from checkpoint

## Transactions

**Location**: `src/Orleans.Transactions/`

### Purpose

ACID transactions across multiple grains.

### Transactional State

```csharp
public class BankAccountGrain : Grain, IBankAccountGrain
{
    private readonly ITransactionalState<AccountBalance> _balance;

    public BankAccountGrain(
        [TransactionalState("balance")]
        ITransactionalState<AccountBalance> balance)
    {
        _balance = balance;
    }

    [Transaction(TransactionOption.CreateOrJoin)]
    public async Task Transfer(IBankAccountGrain target, decimal amount)
    {
        await _balance.PerformUpdate(state =>
        {
            if (state.Balance < amount)
                throw new InsufficientFundsException();

            state.Balance -= amount;
        });

        await target.Deposit(amount);
        // Both updates commit atomically
    }
}
```

### Transaction Semantics

- **ACID**: Atomicity, Consistency, Isolation, Durability
- **Serializable isolation**: Transactions appear sequential
- **Distributed**: No central coordinator
- **Lock-free**: Uses optimistic concurrency

## Event Sourcing

**Location**: `src/Orleans.EventSourcing/`

### Purpose

Event-driven state management with full history.

### JournaledGrain

```csharp
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ChatRoomGrain : JournaledGrain<ChatRoomState, IChatRoomEvent>, IChatRoomGrain
{
    public Task<string> GetHistory()
    {
        // State is current state
        return Task.FromResult(State.GetHistory());
    }

    public Task PostMessage(string user, string message)
    {
        // Raise event
        RaiseEvent(new MessagePostedEvent
        {
            User = user,
            Message = message,
            Timestamp = DateTime.UtcNow
        });

        return ConfirmEvents(); // Persist
    }

    protected override void OnStateChanged()
    {
        // Called after each event is applied
    }
}
```

### Events

```csharp
[GenerateSerializer]
public abstract class IChatRoomEvent { }

[GenerateSerializer]
public class MessagePostedEvent : IChatRoomEvent
{
    [Id(0)] public string User { get; set; }
    [Id(1)] public string Message { get; set; }
    [Id(2)] public DateTime Timestamp { get; set; }
}
```

### State

```csharp
[GenerateSerializer]
public class ChatRoomState
{
    [Id(0)] public List<string> Messages { get; set; } = new();

    public void Apply(MessagePostedEvent evt)
    {
        Messages.Add($"[{evt.Timestamp}] {evt.User}: {evt.Message}");
    }
}
```

## Broadcast Channel

**Location**: `src/Orleans.BroadcastChannel/`

### Purpose

Efficient pub/sub within a cluster.

```csharp
public class NotificationGrain : Grain, INotificationGrain
{
    private IBroadcastChannelWriter<string> _writer;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetBroadcastChannelProvider("notifications");
        var channelId = ChannelId.Create("global", "notifications");
        _writer = provider.GetChannelWriter<string>(channelId);

        return base.OnActivateAsync(ct);
    }

    public Task SendNotification(string message)
    {
        return _writer.Publish(message);
    }
}

public class SubscriberGrain : Grain, ISubscriberGrain, IOnBroadcastChannelSubscribed
{
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetBroadcastChannelProvider("notifications");
        var channelId = ChannelId.Create("global", "notifications");
        await provider.Subscribe<string>(channelId, this);

        await base.OnActivateAsync(ct);
    }

    public Task OnPublished(string item)
    {
        // Handle notification
        return Task.CompletedTask;
    }
}
```

## Summary

Orleans provides rich additional systems:

1. **Grain Directory**: Distributed grain location tracking
2. **Placement**: Flexible grain placement strategies
3. **Timers/Reminders**: Scheduled execution
4. **Streams**: Managed pub/sub and event processing
5. **Transactions**: ACID transactions across grains
6. **Event Sourcing**: Event-driven state management
7. **Broadcast Channel**: Efficient cluster-wide pub/sub

Each system is optional - use what you need!

---

**Next**: [Key Abstractions](11-key-abstractions.md)
