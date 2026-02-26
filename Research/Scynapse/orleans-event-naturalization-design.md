# Orleans Event Naturalization Enhancement
## Design Specification for DOTNExT Fork

**Version**: 1.0  
**Status**: Design Phase  
**Context**: This document captures the complete design for enhancing Microsoft Orleans with naturalized event syntax, developed for the DOTNExT platform. This work builds upon and complements the State Properties Enhancement (StateTask<T>) design.
**Audience**: AI assistants and developers working on DOTNExT

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem Statement](#2-problem-statement)
3. [Solution Overview](#3-solution-overview)
4. [Orleans Features That Enable This](#4-orleans-features-that-enable-this)
5. [What It Looks Like When Complete](#5-what-it-looks-like-when-complete)
6. [The EventTask Type](#6-the-eventtask-type)
7. [Critical Limitations & Findings](#7-critical-limitations--findings)
8. [Code Generation Specifications](#8-code-generation-specifications)
9. [Attribute System](#9-attribute-system)
10. [Integration with StateTask](#10-integration-with-statetask)
11. [Implementation Phases](#11-implementation-phases)
12. [Open Questions & Future Work](#12-open-questions--future-work)

---

## 1. Executive Summary

This project extends Orleans grain development by enabling **naturalized event syntax** for distributed pub/sub while leveraging Orleans Streams as the underlying transport.

**The core innovation**: Developers declare event intentions on their grain implementations; code generation automatically creates the necessary stream wiring, subscription management, and client-side `EventTask<T>` wrappers that provide intuitive syntax.

**Key benefits**:
- Reduced boilerplate (no manual stream provider/ID management)
- Single source of truth (event declaration drives everything)
- Integration with Orleans Streams (durable, checkpointed, scalable)
- Clean client API via `EventTask<T>`
- Integration with `StateTask<T>` for auto-publish on property changes

**Critical constraint**: Unlike properties, C# does not support `partial event` declarations. This fundamentally shapes our design approach—we cannot mirror the StateTask pattern exactly.

---

## 2. Problem Statement

### 2.1 The Distributed Event Impedance Mismatch

C# events are designed for in-process, synchronous, delegate-based pub/sub:

```csharp
// Local C# event pattern
public class Player
{
    public event Action<int> ScoreChanged;
    
    public void UpdateScore(int delta)
    {
        _score += delta;
        ScoreChanged?.Invoke(_score);  // Synchronous, in-process
    }
}

// Local subscription
player.ScoreChanged += score => Console.WriteLine($"Score: {score}");
```

This pattern breaks completely in distributed systems:

| C# Events (Local) | Distributed Reality |
|-------------------|---------------------|
| `event Action<T>` declaration | Subscriptions must survive grain deactivation |
| `+= handler` subscription | Handlers are delegates (not serializable across network) |
| `?.Invoke(x)` raising | Must cross silo boundaries asynchronously |
| Synchronous execution | Must be async, potentially durable |
| In-memory subscriber list | Subscribers may be on different machines |
| Automatic GC cleanup | Explicit subscription lifecycle management |

### 2.2 Current Orleans Streams Pattern (Verbose)

Orleans Streams solve the distributed pub/sub problem but require significant boilerplate:

**Publisher side (Grain)**:
```csharp
public class PlayerGrain : Grain, IPlayerGrain
{
    private IAsyncStream<int>? _scoreChangedStream;
    
    public override Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("SMS");
        var streamId = StreamId.Create("PlayerEvents", this.GetPrimaryKeyString());
        _scoreChangedStream = streamProvider.GetStream<int>(streamId);
        return base.OnActivateAsync(ct);
    }
    
    public async Task UpdateScoreAsync(int delta)
    {
        _score += delta;
        await _scoreChangedStream!.OnNextAsync(_score);
    }
}
```

**Subscriber side (Client or another Grain)**:
```csharp
// Client subscription
var streamProvider = client.GetStreamProvider("SMS");
var streamId = StreamId.Create("PlayerEvents", "player-1");
var stream = streamProvider.GetStream<int>(streamId);

var handle = await stream.SubscribeAsync(
    async (score, token) => 
    {
        Console.WriteLine($"Score changed: {score}");
    });

// Must store handle to unsubscribe later
// Must manage stream provider name and stream ID consistently
// Must handle resubscription after client restart
```

### 2.3 Problems

1. **Boilerplate**: Every event requires stream provider lookup, stream ID construction, stream retrieval
2. **Stringly-typed**: Stream provider names and namespace strings can diverge
3. **No discoverability**: Client must know stream naming conventions out-of-band
4. **Lifecycle complexity**: Subscription handles must be managed manually
5. **No compile-time safety**: Wrong stream ID = silent failure (no events received)
6. **Impedance mismatch**: Looks nothing like C# events despite serving same purpose

---

## 3. Solution Overview

### 3.1 Design Philosophy

Since C# doesn't support `partial event`, we take a different approach than StateTask:

- **StateTask<T>**: Mimics property access (`await grain.Name`, `await (grain.Name << value)`)
- **EventTask<T>**: Does NOT try to mimic `+=` syntax (it can't work—see Section 7). Instead, provides a clean, explicit API that's clearly async and distributed.

### 3.2 Developer Writes (Minimal)

**Grain Implementation**:
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Event declaration via attribute (not partial event - that doesn't exist)
    [Event]
    private EventSource<int> ScoreChanged => Events.Source<int>();
    
    [Event(Durable = false)]  // Transient, in-memory only
    private EventSource<string> ChatMessage => Events.Source<string>();
    
    // Integration with StateTask: auto-raise on property change
    [State(Persisted = true, NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    public async Task UpdateScoreAsync(int delta)
    {
        Score += delta;
        await ScoreChanged.RaiseAsync(Score);  // Explicit raise
    }
    
    public async Task SendChatAsync(string message)
    {
        await ChatMessage.RaiseAsync(message);
    }
}
```

### 3.3 Code Generation Produces

1. **Stream wiring**: Automatic stream provider and ID management in grain
2. **Interface extension**: Subscription methods on the grain interface
3. **Proxy enhancement**: `EventTask<T>` properties for client-side subscription
4. **Optional**: Auto-raise integration with `[State(NotifyOnChange = true)]`

### 3.4 Client Gets Clean API

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Subscribe to events
var handle = await player.ScoreChanged.SubscribeAsync(
    async score => Console.WriteLine($"Score: {score}")
);

// With checkpoint for replay
var handle = await player.ScoreChanged.SubscribeAsync(
    async score => ProcessScore(score),
    resumeFrom: lastCheckpoint
);

// Unsubscribe
await handle.UnsubscribeAsync();

// Fluent filtering (optional advanced feature)
await player.ScoreChanged
    .Where(score => score > 1000)
    .SubscribeAsync(async score => CelebrateMilestone(score));
```

---

## 4. Orleans Features That Enable This

Orleans provides several features that make event naturalization possible:

### 4.1 Streams (Core Transport)

Orleans Streams provide the distributed pub/sub infrastructure:

| Stream Feature | Event Naturalization Use |
|----------------|-------------------------|
| `IAsyncStream<T>` | Transport for event payloads |
| `StreamId` | Unique identification per grain + event |
| `OnNextAsync()` | Raising events from grain |
| `SubscribeAsync()` | Client/grain subscription |
| `StreamSequenceToken` | Checkpointing for replay |
| Implicit multiplexing | Many logical streams over fewer queues |

**Key Stream Capabilities**:
- **Durable delivery**: Events survive silo failures (with appropriate provider)
- **Checkpointing**: Subscribers can resume from a saved position
- **Backpressure**: Queue-based providers handle burst traffic
- **Provider flexibility**: Azure Event Hubs, Amazon Kinesis, in-memory, etc.

### 4.2 Grain Call Filters (Auto-Publish on State Change)

Filters can intercept grain calls and auto-publish events:

```csharp
public class StateChangePublishingFilter : IIncomingGrainCallFilter
{
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var stateBefore = CaptureState(context.Grain);
        await context.Invoke();
        var stateAfter = CaptureState(context.Grain);
        
        foreach (var change in ComputeDiff(stateBefore, stateAfter))
        {
            await PublishPropertyChangedEvent(context.Grain, change);
        }
    }
}
```

This enables `[State(NotifyOnChange = true)]` to automatically raise events.

### 4.3 Implicit Stream Subscriptions

Orleans supports declarative subscription via attributes:

```csharp
// Current Orleans pattern
[ImplicitStreamSubscription("PlayerScoreChanged")]
public class LeaderboardGrain : Grain, ILeaderboardGrain, IAsyncObserver<int>
{
    public Task OnNextAsync(int score, StreamSequenceToken? token)
    {
        // Handle event
    }
}
```

We can build on this for naturalized syntax:

```csharp
// Naturalized (code-generated wiring)
[SubscribesTo<IPlayerGrain>(nameof(IPlayerGrain.ScoreChanged))]
public partial class LeaderboardGrain : Grain, ILeaderboardGrain
{
    // This method is discovered and wired by codegen
    private Task OnPlayerScoreChanged(string playerId, int score)
    {
        return UpdateLeaderboard(playerId, score);
    }
}
```

### 4.4 Persistence (Durable Subscriptions)

Stream subscriptions can be persisted:

```csharp
// Subscription survives grain deactivation
var handle = await stream.SubscribeAsync(observer);
// handle.HandleId can be stored and used to resume
```

### 4.5 Request Context (Event Correlation)

Correlation IDs and other metadata can flow through events:

```csharp
// Publisher
RequestContext.Set("CorrelationId", Guid.NewGuid());
await ScoreChanged.RaiseAsync(newScore);

// Subscriber receives same correlation context
var correlationId = RequestContext.Get("CorrelationId");
```

---

## 5. What It Looks Like When Complete

### 5.1 Simple Event (In-Memory/Transient)

**Developer writes**:
```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [Event(Durable = false)]
    private EventSource<string> ChatMessage => Events.Source<string>();
    
    public async Task SendChatAsync(string message)
    {
        await ChatMessage.RaiseAsync(message);
    }
}
```

**After codegen, the effective interface is**:
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    // Developer-written
    Task SendChatAsync(string message);
    
    // Generated - explicit subscription methods
    Task<EventSubscriptionHandle<string>> SubscribeChatMessageAsync(
        StreamSequenceToken? resumeFrom = null);
}
```

**After codegen, the effective implementation is**:
```csharp
public partial class PlayerGrain
{
    // Generated stream field
    private IAsyncStream<string>? _chatMessage_stream;
    
    // Generated EventSource implementation
    private EventSource<string> ChatMessage => new EventSource<string>(
        () => _chatMessage_stream ?? throw new InvalidOperationException("Grain not activated")
    );
    
    // Generated activation wiring
    private void InitializeEventStreams_Generated()
    {
        var streamProvider = this.GetStreamProvider("TransientEvents");
        var streamId = StreamId.Create(
            "IPlayerGrain.ChatMessage", 
            this.GetPrimaryKeyString()
        );
        _chatMessage_stream = streamProvider.GetStream<string>(streamId);
    }
}
```

**Generated proxy includes EventTask property**:
```csharp
internal sealed class Proxy_IPlayerGrain : GrainReference, IPlayerGrain
{
    // Standard method proxies
    public Task SendChatAsync(string message) { /* invoke */ }
    
    // EventTask property for subscription
    public EventTask<string> ChatMessage => new EventTask<string>(
        this,
        StreamId.Create("IPlayerGrain.ChatMessage", this.GetPrimaryKeyString()),
        "TransientEvents"  // Stream provider name
    );
}
```

**Client usage**:
```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Subscribe to chat messages
var handle = await player.ChatMessage.SubscribeAsync(
    async message => Console.WriteLine($"Chat: {message}")
);

// Send a message (from another client/grain)
await player.SendChatAsync("Hello world!");

// Unsubscribe when done
await handle.UnsubscribeAsync();
```

### 5.2 Durable Event with Checkpointing

**Developer writes**:
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [Event(Durable = true, StreamProvider = "AzureEventHubs")]
    private EventSource<ScoreChangedEvent> ScoreChanged => Events.Source<ScoreChangedEvent>();
    
    public async Task UpdateScoreAsync(int delta)
    {
        _score += delta;
        await ScoreChanged.RaiseAsync(new ScoreChangedEvent(_score, delta, DateTime.UtcNow));
    }
}

[GenerateSerializer]
public record ScoreChangedEvent(
    [property: Id(0)] int NewScore,
    [property: Id(1)] int Delta,
    [property: Id(2)] DateTime Timestamp
);
```

**Client usage with checkpointing**:
```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Load last checkpoint from your storage
var lastToken = await LoadCheckpointAsync("player-1-score-subscription");

// Subscribe from checkpoint (replay missed events)
var handle = await player.ScoreChanged.SubscribeAsync(
    async (evt, token) => 
    {
        await ProcessScoreChange(evt);
        await SaveCheckpointAsync("player-1-score-subscription", token);
    },
    resumeFrom: lastToken
);
```

### 5.3 Integration with StateTask (Auto-Raise on Property Change)

This is where Event Naturalization integrates with the existing State Properties Enhancement:

**Developer writes**:
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;
    
    // Property with auto-notification
    [State(Persisted = true, StateProperty = nameof(_state), NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    // The event that gets raised automatically
    [Event]
    private EventSource<PropertyChangedEvent<int>> ScoreChanged => Events.Source<PropertyChangedEvent<int>>();
    
    public Task UpdateScoreAsync(int delta)
    {
        Score += delta;  // This automatically raises ScoreChanged!
        return Task.CompletedTask;
    }
}
```

**Generated property implementation with auto-raise**:
```csharp
public partial class PlayerGrain
{
    public partial int Score
    {
        get => _state.State.Score;
        set
        {
            var oldValue = _state.State.Score;
            if (!EqualityComparer<int>.Default.Equals(oldValue, value))
            {
                _state.State.Score = value;
                // Auto-raise event (fire-and-forget to avoid blocking setter)
                _ = ScoreChanged.RaiseAsync(new PropertyChangedEvent<int>(oldValue, value));
            }
        }
    }
}
```

**Client subscribes to property changes**:
```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Subscribe to score changes
await player.ScoreChanged.SubscribeAsync(async change =>
{
    Console.WriteLine($"Score changed from {change.OldValue} to {change.NewValue}");
});

// Any modification triggers the event
await player.SetScore(100);  // Event raised
await (player.Score << 200); // Event raised
await player.UpdateScoreAsync(50); // Event raised
```

### 5.4 Grain-to-Grain Subscription

**Subscriber grain**:
```csharp
public partial class LeaderboardGrain : Grain, ILeaderboardGrain
{
    private readonly Dictionary<string, int> _scores = new();
    private readonly List<EventSubscriptionHandle<PropertyChangedEvent<int>>> _handles = new();
    
    public async Task TrackPlayerAsync(string playerId)
    {
        var player = GrainFactory.GetGrain<IPlayerGrain>(playerId);
        
        var handle = await player.ScoreChanged.SubscribeAsync(async change =>
        {
            _scores[playerId] = change.NewValue;
            await RecalculateRankings();
        });
        
        _handles.Add(handle);
    }
    
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Clean up subscriptions
        foreach (var handle in _handles)
        {
            await handle.UnsubscribeAsync();
        }
        await base.OnDeactivateAsync(reason, ct);
    }
}
```

---

## 6. The EventTask Type

### 6.1 Purpose

`EventTask<T>` wraps a grain's event stream into a client-friendly type that:
- Provides clean subscription API
- Hides stream provider and ID details
- Manages subscription lifecycle
- Optionally supports fluent filtering/transformation

### 6.2 Core Implementation

```csharp
namespace Orleans;

/// <summary>
/// Client-side handle for subscribing to grain events.
/// This does NOT mimic C# event syntax (which is impossible for async distributed scenarios).
/// Instead, it provides an explicit, clear API for distributed pub/sub.
/// </summary>
/// <typeparam name="T">The event payload type.</typeparam>
public readonly struct EventTask<T>
{
    private readonly GrainReference _grainRef;
    private readonly StreamId _streamId;
    private readonly string _streamProviderName;
    
    /// <summary>
    /// Creates a new EventTask for a grain event.
    /// </summary>
    /// <param name="grainRef">The grain reference (proxy) that publishes this event.</param>
    /// <param name="streamId">The stream ID for this event.</param>
    /// <param name="streamProviderName">The name of the stream provider to use.</param>
    internal EventTask(
        GrainReference grainRef,
        StreamId streamId,
        string streamProviderName)
    {
        _grainRef = grainRef;
        _streamId = streamId;
        _streamProviderName = streamProviderName;
    }
    
    /// <summary>
    /// Subscribes to this event with a simple async handler.
    /// </summary>
    /// <param name="handler">Async callback invoked for each event.</param>
    /// <param name="resumeFrom">Optional token to resume from a checkpoint.</param>
    /// <returns>A handle that can be used to unsubscribe.</returns>
    public async Task<EventSubscriptionHandle<T>> SubscribeAsync(
        Func<T, Task> handler,
        StreamSequenceToken? resumeFrom = null)
    {
        var streamProvider = _grainRef.GetStreamProvider(_streamProviderName);
        var stream = streamProvider.GetStream<T>(_streamId);
        
        var orleansHandle = await stream.SubscribeAsync(
            (payload, token) => handler(payload),
            resumeFrom
        );
        
        return new EventSubscriptionHandle<T>(orleansHandle);
    }
    
    /// <summary>
    /// Subscribes to this event with a handler that receives the sequence token.
    /// Use this overload when you need to checkpoint your position.
    /// </summary>
    /// <param name="handler">Async callback invoked with payload and token.</param>
    /// <param name="resumeFrom">Optional token to resume from a checkpoint.</param>
    /// <returns>A handle that can be used to unsubscribe.</returns>
    public async Task<EventSubscriptionHandle<T>> SubscribeAsync(
        Func<T, StreamSequenceToken?, Task> handler,
        StreamSequenceToken? resumeFrom = null)
    {
        var streamProvider = _grainRef.GetStreamProvider(_streamProviderName);
        var stream = streamProvider.GetStream<T>(_streamId);
        
        var orleansHandle = await stream.SubscribeAsync(handler, resumeFrom);
        
        return new EventSubscriptionHandle<T>(orleansHandle);
    }
    
    // ═══════════════════════════════════════════════════════════════
    // FLUENT FILTERING (OPTIONAL ADVANCED FEATURE)
    // Enables: player.ScoreChanged.Where(s => s > 100).SubscribeAsync(...)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Filters events before they reach the subscriber.
    /// The filter runs client-side (events are still transmitted, just not delivered to handler).
    /// </summary>
    /// <param name="predicate">Filter predicate. Only events where this returns true are delivered.</param>
    /// <returns>A filtered EventTask.</returns>
    public FilteredEventTask<T> Where(Func<T, bool> predicate)
    {
        return new FilteredEventTask<T>(this, predicate, x => x);
    }
    
    /// <summary>
    /// Transforms events before they reach the subscriber.
    /// </summary>
    /// <typeparam name="TResult">The transformed type.</typeparam>
    /// <param name="selector">Transformation function.</param>
    /// <returns>A transformed EventTask.</returns>
    public FilteredEventTask<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return new FilteredEventTask<TResult>(
            new EventTask<T>(_grainRef, _streamId, _streamProviderName),
            _ => true,
            selector
        );
    }
    
    // ═══════════════════════════════════════════════════════════════
    // OPERATOR OVERLOADS
    // Limited support for += style (see Section 7 for why full += doesn't work)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Enables: await (player.ScoreChanged + handler)
    /// Note: This uses + not +=. See documentation for why += cannot work.
    /// </summary>
    public static Task<EventSubscriptionHandle<T>> operator +(
        EventTask<T> eventTask, 
        Func<T, Task> handler)
    {
        return eventTask.SubscribeAsync(handler);
    }
}
```

### 6.3 EventSubscriptionHandle

```csharp
namespace Orleans;

/// <summary>
/// Handle for managing an event subscription lifecycle.
/// </summary>
/// <typeparam name="T">The event payload type.</typeparam>
public readonly struct EventSubscriptionHandle<T>
{
    private readonly StreamSubscriptionHandle<T> _orleansHandle;
    
    internal EventSubscriptionHandle(StreamSubscriptionHandle<T> orleansHandle)
    {
        _orleansHandle = orleansHandle;
    }
    
    /// <summary>
    /// The underlying Orleans stream subscription handle ID.
    /// Can be persisted for later resumption.
    /// </summary>
    public Guid HandleId => _orleansHandle.HandleId;
    
    /// <summary>
    /// Unsubscribes from the event stream.
    /// </summary>
    public Task UnsubscribeAsync()
    {
        return _orleansHandle.UnsubscribeAsync();
    }
    
    /// <summary>
    /// Resumes this subscription with a new handler.
    /// Use after reactivation to reconnect to the stream.
    /// </summary>
    public Task<StreamSubscriptionHandle<T>> ResumeAsync(
        Func<T, StreamSequenceToken?, Task> handler)
    {
        return _orleansHandle.ResumeAsync(handler);
    }
}
```

### 6.4 FilteredEventTask (Fluent Support)

```csharp
namespace Orleans;

/// <summary>
/// An EventTask with filtering and/or transformation applied.
/// Filters execute client-side after events are received.
/// </summary>
public readonly struct FilteredEventTask<T>
{
    private readonly object _sourceEventTask;  // EventTask<TSource> (boxed to avoid generic complexity)
    private readonly Func<object, bool> _predicate;
    private readonly Func<object, T> _selector;
    
    internal FilteredEventTask<T>(
        object sourceEventTask,
        Func<object, bool> predicate,
        Func<object, T> selector)
    {
        _sourceEventTask = sourceEventTask;
        _predicate = predicate;
        _selector = selector;
    }
    
    /// <summary>
    /// Subscribes with the filter and transformation applied.
    /// </summary>
    public Task<EventSubscriptionHandle<T>> SubscribeAsync(Func<T, Task> handler)
    {
        // Implementation subscribes to source and applies filter/transform
        // Details omitted for brevity
        throw new NotImplementedException("See full implementation");
    }
    
    /// <summary>
    /// Adds additional filtering.
    /// </summary>
    public FilteredEventTask<T> Where(Func<T, bool> predicate)
    {
        var currentPredicate = _predicate;
        var currentSelector = _selector;
        return new FilteredEventTask<T>(
            _sourceEventTask,
            obj => currentPredicate(obj) && predicate(currentSelector(obj)),
            _selector
        );
    }
    
    /// <summary>
    /// Adds transformation.
    /// </summary>
    public FilteredEventTask<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        var currentSelector = _selector;
        return new FilteredEventTask<TResult>(
            _sourceEventTask,
            _predicate,
            obj => selector(currentSelector(obj))
        );
    }
}
```

### 6.5 EventSource (Grain-Side)

```csharp
namespace Orleans;

/// <summary>
/// Grain-side event source for raising events.
/// </summary>
/// <typeparam name="T">The event payload type.</typeparam>
public readonly struct EventSource<T>
{
    private readonly Func<IAsyncStream<T>> _streamFactory;
    
    internal EventSource(Func<IAsyncStream<T>> streamFactory)
    {
        _streamFactory = streamFactory;
    }
    
    /// <summary>
    /// Raises the event, publishing to all subscribers.
    /// </summary>
    /// <param name="payload">The event payload.</param>
    public Task RaiseAsync(T payload)
    {
        return _streamFactory().OnNextAsync(payload);
    }
    
    /// <summary>
    /// Raises the event with an explicit sequence token.
    /// </summary>
    public Task RaiseAsync(T payload, StreamSequenceToken token)
    {
        return _streamFactory().OnNextAsync(payload, token);
    }
    
    /// <summary>
    /// Signals an error to all subscribers.
    /// </summary>
    public Task RaiseErrorAsync(Exception error)
    {
        return _streamFactory().OnErrorAsync(error);
    }
    
    /// <summary>
    /// Signals completion (no more events) to all subscribers.
    /// </summary>
    public Task CompleteAsync()
    {
        return _streamFactory().OnCompletedAsync();
    }
}

/// <summary>
/// Factory for creating EventSource instances within grains.
/// </summary>
public static class Events
{
    /// <summary>
    /// Creates an EventSource. Must be called from within a grain.
    /// The actual stream is wired by generated code.
    /// </summary>
    public static EventSource<T> Source<T>()
    {
        // This is a marker method - actual implementation is provided by codegen
        throw new InvalidOperationException(
            "Events.Source<T>() must be used with [Event] attribute. " +
            "The actual implementation is generated at compile time."
        );
    }
}
```

---

## 7. Critical Limitations & Findings

This section documents important constraints discovered during design.

### 7.1 LIMITATION: C# Does Not Support `partial event`

**Finding**: No version of C# (including C# 13) supports `partial event` declarations.

The `partial` modifier is legal only on:
- **Types** (classes, structs, interfaces, records) — C# 2.0+
- **Methods** — C# 3.0+
- **Properties** — C# 13 / .NET 9

Events are explicitly NOT in this list.

```csharp
// ❌ ILLEGAL - Will not compile in any C# version
public partial event Action<int> ScoreChanged;

// ❌ ILLEGAL - Same problem
public partial event Func<int, Task> ScoreChanged;
```

**Impact**: We cannot mirror the StateTask pattern where developers write `partial` declarations and codegen provides implementations.

**Workaround**: Use attribute-based declaration on a property or field:

```csharp
// ✓ LEGAL - Attribute on property returning EventSource<T>
[Event]
private EventSource<int> ScoreChanged => Events.Source<int>();

// ✓ LEGAL - Attribute on the class with event metadata
[RaisesEvent("ScoreChanged", typeof(int))]
public partial class PlayerGrain { ... }
```

### 7.2 LIMITATION: The `+=` Operator Cannot Return a Value

**Finding**: C# `+=` for events cannot be made to return an awaitable value.

**Why developers want this**:
```csharp
// Intuitive syntax that CANNOT work
await (player.ScoreChanged += async score => { ... });
```

**Why it fails**:

For real C# events, `+=` invokes the `add` accessor which returns `void`:
```csharp
public event Action<int> ScoreChanged
{
    add { /* returns void */ }
    remove { /* returns void */ }
}
```

For operator overloading, `+=` is synthesized from `+`:
```csharp
// a += b  desugars to  a = a + b
```

This means `player.ScoreChanged + handler` would need to return a new `EventTask<T>` that gets assigned back to `player.ScoreChanged`. But `ScoreChanged` is a read-only property—there's no setter.

Even if we added a setter, the semantics would be wrong: we'd be replacing the EventTask, not adding a subscription.

**What DOES work**:

```csharp
// Option 1: Explicit method call (recommended)
var handle = await player.ScoreChanged.SubscribeAsync(async score => { ... });

// Option 2: + operator (not +=) returning Task<Handle>
var handle = await (player.ScoreChanged + (async score => { ... }));
```

Option 2 works because `+` can return any type, including `Task<EventSubscriptionHandle<T>>`. But note:
- It uses `+` not `+=`
- The parentheses around the handler are required
- The semantics are unusual (`+` typically means "combine", not "subscribe")

**Recommendation**: Use explicit `SubscribeAsync()`. The `+` operator is provided for convenience but `SubscribeAsync()` is clearer.

### 7.3 LIMITATION: Fluent Filtering is Client-Side Only

**Finding**: `.Where()` and `.Select()` filters execute on the subscriber side, not the publisher side.

```csharp
await player.ScoreChanged
    .Where(score => score > 1000)  // This filter runs CLIENT-SIDE
    .SubscribeAsync(handler);
```

**Impact**: 
- All events are still transmitted over the network
- Filtering happens after receipt, not at source
- For high-volume events, this may be inefficient

**Why**: Orleans Streams don't support server-side filtering per subscriber. Each subscriber receives all events.

**Future possibility**: Server-side filtered streams would require Orleans runtime changes.

### 7.4 LIMITATION: Event Declaration Syntax Options

Given the `partial event` limitation, here are the viable declaration syntaxes:

**Option A: Property returning EventSource<T>** (Recommended)
```csharp
[Event]
private EventSource<int> ScoreChanged => Events.Source<int>();
```
- Pro: Clear, type-safe, discoverable via IntelliSense
- Pro: `Events.Source<T>()` is a marker that codegen replaces
- Con: Slightly more verbose than ideal

**Option B: Field with attribute**
```csharp
[Event]
private readonly EventSource<int> _scoreChanged;
```
- Pro: Minimal syntax
- Con: Requires generated initialization
- Con: Naming convention needed (field vs property name)

**Option C: Class-level attribute**
```csharp
[RaisesEvent("ScoreChanged", typeof(int))]
[RaisesEvent("HealthChanged", typeof(int))]
public partial class PlayerGrain { ... }
```
- Pro: All events declared in one place
- Con: Stringly-typed event names
- Con: No IntelliSense for `ScoreChanged` until codegen runs

**Option D: Interface declaration** (Mirror of StateTask approach)
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    // Events declared on interface as special methods
    EventTask<int> ScoreChanged { get; }
}
```
- Pro: Events are part of the grain contract
- Pro: Client sees events directly on interface
- Con: Requires generated implementation on grain

**Recommendation**: Option A for grain implementation, combined with Option D for interface exposure.

### 7.5 FINDING: Stream Provider Configuration

Events need to specify which stream provider to use:

| Event Type | Stream Provider | Characteristics |
|------------|-----------------|-----------------|
| Transient (in-memory) | "SMS" (Simple Message Streams) | Fast, no persistence, lost on silo restart |
| Durable | "AzureEventHubs", "AmazonKinesis", etc. | Persistent, replayable, higher latency |

The `[Event]` attribute should specify this:

```csharp
[Event(Durable = false)]  // Uses default transient provider
private EventSource<int> ScoreChanged => Events.Source<int>();

[Event(Durable = true, StreamProvider = "AzureEventHubs")]
private EventSource<AuditEvent> AuditLog => Events.Source<AuditEvent>();
```

---

## 8. Code Generation Specifications

### 8.1 Inputs (What Drives Code Generation)

The code generator scans for:

1. **`[Event]` attributes** on properties/fields returning `EventSource<T>`
2. **`[State(NotifyOnChange = true)]`** attributes on properties (generates companion event)
3. **`[RaisesEvent]`** class-level attributes (alternative declaration style)

### 8.2 Detection Rules

**An event triggers code generation if**:
- Property/field has `[Event]` attribute AND
- Return type is `EventSource<T>` AND
- Containing class inherits from `Grain` AND
- Containing class implements `IGrainWithXXXKey`

**For each qualifying event, the generator produces**:
1. Stream field and initialization code
2. EventSource implementation (replaces `Events.Source<T>()` marker)
3. Interface subscription methods
4. Proxy `EventTask<T>` property

### 8.3 Output Files

For a grain `PlayerGrain : IPlayerGrain` with `ScoreChanged` event:

| File | Contents |
|------|----------|
| `IPlayerGrain.Events.g.cs` | Interface extension with subscription methods and EventTask properties |
| `PlayerGrain.Events.g.cs` | Stream fields, initialization, EventSource implementations |
| `Proxy_IPlayerGrain.Events.g.cs` | Proxy extension with EventTask properties |

### 8.4 Naming Conventions

| Source | Generated Interface Method | Generated Proxy Property |
|--------|---------------------------|-------------------------|
| `ScoreChanged` event | `SubscribeScoreChangedAsync()` | `EventTask<T> ScoreChanged` |
| `[Event(Name = "Points")]` | `SubscribePointsAsync()` | `EventTask<T> Points` |

### 8.5 Stream ID Convention

Stream IDs are constructed deterministically:

```csharp
// Pattern: "{InterfaceName}.{EventName}"
// Key: Grain's primary key

StreamId.Create("IPlayerGrain.ScoreChanged", grain.GetPrimaryKeyString())
```

This ensures:
- Unique stream per grain instance per event
- Client can construct same ID from grain reference
- No collision between different event types

---

## 9. Attribute System

### 9.1 EventAttribute

```csharp
namespace Orleans;

/// <summary>
/// Marks a property as a grain event source.
/// The property must return EventSource&lt;T&gt;.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class EventAttribute : Attribute
{
    /// <summary>
    /// If true, events are published through a durable stream provider.
    /// If false, uses in-memory Simple Message Streams.
    /// Default: true
    /// </summary>
    public bool Durable { get; init; } = true;
    
    /// <summary>
    /// Name of the stream provider to use.
    /// Only used when Durable = true.
    /// Default: "Default" (uses the default durable provider)
    /// </summary>
    public string StreamProvider { get; init; } = "Default";
    
    /// <summary>
    /// Custom name for the generated subscription methods.
    /// Default: uses the property name.
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Stream namespace override.
    /// Default: "{InterfaceName}.{EventName}"
    /// </summary>
    public string? StreamNamespace { get; init; }
}
```

### 9.2 RaisesEventAttribute (Alternative Declaration)

```csharp
namespace Orleans;

/// <summary>
/// Declares that a grain raises an event.
/// Alternative to property-based [Event] declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RaisesEventAttribute : Attribute
{
    public string EventName { get; }
    public Type PayloadType { get; }
    
    public RaisesEventAttribute(string eventName, Type payloadType)
    {
        EventName = eventName;
        PayloadType = payloadType;
    }
    
    /// <summary>
    /// If true, events are published through a durable stream provider.
    /// </summary>
    public bool Durable { get; init; } = true;
    
    /// <summary>
    /// Name of the stream provider to use.
    /// </summary>
    public string StreamProvider { get; init; } = "Default";
}
```

### 9.3 SubscribesToAttribute (Grain-to-Grain)

```csharp
namespace Orleans;

/// <summary>
/// Declares that a grain subscribes to events from another grain type.
/// The grain must have a method matching the expected signature.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SubscribesToAttribute<TGrain> : Attribute
    where TGrain : IGrain
{
    public string EventName { get; }
    
    public SubscribesToAttribute(string eventName)
    {
        EventName = eventName;
    }
    
    /// <summary>
    /// Name of the handler method in this grain.
    /// Default: "On{GrainName}{EventName}" (e.g., "OnPlayerScoreChanged")
    /// </summary>
    public string? HandlerMethod { get; init; }
}
```

### 9.4 Usage Examples

```csharp
// ═══════════════════════════════════════════════════════════════
// PUBLISHER GRAIN
// ═══════════════════════════════════════════════════════════════

public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Simple transient event
    [Event(Durable = false)]
    private EventSource<string> ChatMessage => Events.Source<string>();
    
    // Durable event with default provider
    [Event]
    private EventSource<int> ScoreChanged => Events.Source<int>();
    
    // Durable event with specific provider
    [Event(StreamProvider = "AzureEventHubs")]
    private EventSource<AuditEvent> AuditLog => Events.Source<AuditEvent>();
    
    // Custom naming
    [Event(Name = "Points")]
    private EventSource<int> ScoreChanged2 => Events.Source<int>();
}

// ═══════════════════════════════════════════════════════════════
// SUBSCRIBER GRAIN (Grain-to-Grain)
// ═══════════════════════════════════════════════════════════════

[SubscribesTo<IPlayerGrain>("ScoreChanged")]
public partial class LeaderboardGrain : Grain, ILeaderboardGrain
{
    // Codegen looks for this method and wires it up
    private Task OnPlayerScoreChanged(string playerId, int newScore)
    {
        _scores[playerId] = newScore;
        return RecalculateRankings();
    }
}

// ═══════════════════════════════════════════════════════════════
// STATE + EVENT INTEGRATION
// ═══════════════════════════════════════════════════════════════

public partial class PlayerGrain : Grain, IPlayerGrain
{
    // This property auto-raises an event when changed
    [State(Persisted = true, NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    // Codegen creates this event automatically due to NotifyOnChange
    // [Event]
    // private EventSource<PropertyChangedEvent<int>> ScoreChanged => ...;
}
```

---

## 10. Integration with StateTask

### 10.1 How StateTask and EventTask Complement Each Other

| StateTask<T> | EventTask<T> |
|--------------|--------------|
| Point-in-time state access | Stream of state changes over time |
| Request/response pattern | Pub/sub pattern |
| `await grain.Score` (get current) | `await grain.ScoreChanged.SubscribeAsync(...)` (get notified of changes) |
| Client pulls | Grain pushes |

### 10.2 The `NotifyOnChange` Bridge

The `[State(NotifyOnChange = true)]` attribute creates integration between the two:

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [State(Persisted = true, NotifyOnChange = true)]
    public partial int Score { get; set; }
}
```

This generates:

1. **StateTask property** (from State enhancement):
```csharp
// Client can get/set
await player.SetScore(100);
int score = await player.Score;
```

2. **EventTask property** (from Event enhancement):
```csharp
// Client can subscribe
await player.ScoreChanged.SubscribeAsync(evt => ...);
```

3. **Auto-raise in setter** (integration):
```csharp
public partial int Score
{
    set
    {
        var old = _state.State.Score;
        _state.State.Score = value;
        _ = ScoreChanged.RaiseAsync(new PropertyChangedEvent<int>(old, value));
    }
}
```

### 10.3 PropertyChangedEvent Type

```csharp
namespace Orleans;

/// <summary>
/// Event payload for property change notifications.
/// Used with [State(NotifyOnChange = true)].
/// </summary>
[GenerateSerializer]
public readonly record struct PropertyChangedEvent<T>(
    [property: Id(0)] T OldValue,
    [property: Id(1)] T NewValue
)
{
    /// <summary>
    /// Convenience property for accessing just the new value.
    /// </summary>
    public T Value => NewValue;
    
    /// <summary>
    /// Checks if the value actually changed.
    /// </summary>
    public bool HasChanged => !EqualityComparer<T>.Default.Equals(OldValue, NewValue);
}
```

### 10.4 Complete Integration Example

```csharp
// ═══════════════════════════════════════════════════════════════
// GRAIN DEFINITION
// ═══════════════════════════════════════════════════════════════

public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task UpdateScoreAsync(int delta);
}

public partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerData> _state;
    
    // Property with persistence AND change notification
    [State(Persisted = true, StateProperty = nameof(_state), NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    // Property without notification
    [State(Persisted = true, StateProperty = nameof(_state))]
    public partial string Name { get; set; }
    
    public Task UpdateScoreAsync(int delta)
    {
        Score += delta;  // Setter auto-raises ScoreChanged event
        return Task.CompletedTask;
    }
}

// ═══════════════════════════════════════════════════════════════
// CLIENT USAGE
// ═══════════════════════════════════════════════════════════════

var player = client.GetGrain<IPlayerGrain>("player-1");

// Get current state (StateTask)
int currentScore = await player.Score;

// Set state (StateTask) - this also raises ScoreChanged
await (player.Score << 100);

// Subscribe to future changes (EventTask)
await player.ScoreChanged.SubscribeAsync(async change =>
{
    Console.WriteLine($"Score: {change.OldValue} → {change.NewValue}");
});

// This triggers both:
// 1. State persistence
// 2. ScoreChanged event
await player.UpdateScoreAsync(50);
```

---

## 11. Implementation Phases

### Phase 1: Core EventTask Infrastructure

**Goal**: EventTask<T>, EventSource<T>, and basic runtime support

**Deliverables**:
1. `EventTask<T>` struct implementation
2. `EventSource<T>` struct implementation  
3. `EventSubscriptionHandle<T>` struct
4. `PropertyChangedEvent<T>` record
5. Unit tests with mock streams

**Validation**:
```csharp
// Test EventTask in isolation
var eventTask = new EventTask<int>(mockGrainRef, streamId, "SMS");

var handle = await eventTask.SubscribeAsync(async value =>
{
    receivedValues.Add(value);
});

// Verify subscription works
await mockStream.OnNextAsync(42);
Assert.Contains(42, receivedValues);
```

### Phase 2: Event Declaration & Code Generation

**Goal**: Generate stream wiring from [Event] attributes

**Deliverables**:
1. `[Event]` attribute implementation
2. Source generator to scan for [Event] properties
3. Stream field and initialization generation
4. EventSource replacement (replace `Events.Source<T>()` marker)
5. Integration tests with real Orleans

**Validation**:
```csharp
// Developer writes
public partial class TestGrain : Grain, ITestGrain
{
    [Event(Durable = false)]
    private EventSource<int> NumberEvent => Events.Source<int>();
    
    public Task RaiseNumber(int n) => NumberEvent.RaiseAsync(n);
}

// After codegen:
// - _numberEvent_stream field exists
// - Events.Source<int>() replaced with working EventSource
// - Event actually publishes to stream
```

### Phase 3: Interface & Proxy Enhancement

**Goal**: Generate subscription methods and EventTask properties on proxy

**Deliverables**:
1. Interface extension generator (subscription methods)
2. Proxy enhancement generator (EventTask properties)
3. Stream ID convention implementation
4. End-to-end client subscription tests

**Validation**:
```csharp
// Client code works
var grain = client.GetGrain<ITestGrain>("test-1");

var handle = await grain.NumberEvent.SubscribeAsync(async n =>
{
    Console.WriteLine(n);
});

await grain.RaiseNumber(42);  // Subscriber receives 42
```

### Phase 4: StateTask Integration

**Goal**: `[State(NotifyOnChange = true)]` auto-raises events

**Deliverables**:
1. Detection of NotifyOnChange in State codegen
2. Automatic companion event generation
3. Setter modification to raise event
4. Integration tests combining State and Event

**Validation**:
```csharp
// Developer writes
[State(NotifyOnChange = true)]
public partial int Score { get; set; }

// Setting Score raises ScoreChanged automatically
await (grain.Score << 100);

// Subscriber receives PropertyChangedEvent<int>
await grain.ScoreChanged.SubscribeAsync(async evt =>
{
    Assert.Equal(100, evt.NewValue);
});
```

### Phase 5: Advanced Features

**Goal**: Fluent API, grain-to-grain subscriptions, checkpointing

**Deliverables**:
1. `FilteredEventTask<T>` implementation
2. `.Where()` and `.Select()` methods
3. `[SubscribesTo]` attribute and wiring
4. Checkpoint/resume documentation and helpers
5. Performance testing

**Validation**:
```csharp
// Fluent filtering works
await grain.ScoreChanged
    .Where(e => e.NewValue > 1000)
    .Select(e => $"High score: {e.NewValue}")
    .SubscribeAsync(async msg => Celebrate(msg));

// Grain-to-grain subscription works
[SubscribesTo<IPlayerGrain>("ScoreChanged")]
public partial class LeaderboardGrain : Grain, ILeaderboardGrain
{
    private Task OnPlayerScoreChanged(string playerId, int score) => ...;
}
```

### Phase 6: Polish & Production Readiness

**Goal**: Complete solution ready for production use

**Deliverables**:
1. Comprehensive error messages and diagnostics
2. Roslyn analyzers (warn on common mistakes)
3. Performance optimization
4. Documentation
5. Sample applications

---

## 12. Open Questions & Future Work

### 12.1 Open Questions

**Q1: Default stream provider**
Should there be a global default stream provider, or require explicit specification?
```csharp
// Option A: Global default
services.AddOrleans(builder => builder.SetDefaultEventStreamProvider("AzureEventHubs"));

// Option B: Always explicit
[Event(StreamProvider = "AzureEventHubs")]  // Required
```

**Q2: Event payload serialization**
Should event payloads require `[GenerateSerializer]`, or auto-generate?
```csharp
// Current Orleans requirement
[GenerateSerializer]
public record ScoreChangedEvent([property: Id(0)] int Score);

// Could we auto-generate for simple types?
[Event]
private EventSource<int> ScoreChanged => ...;  // int is already serializable
```

**Q3: Subscription cleanup**
When a subscriber grain deactivates, should subscriptions auto-unsubscribe?
```csharp
// Current: Manual cleanup required
public override async Task OnDeactivateAsync(...)
{
    await _handle.UnsubscribeAsync();
}

// Potential: Auto-cleanup option
[SubscribesTo<IPlayerGrain>("ScoreChanged", AutoUnsubscribe = true)]
```

**Q4: Multiple handlers per event**
Should a grain be able to have multiple handlers for the same event from another grain?
```csharp
// Current Orleans Streams: Yes (multiple SubscribeAsync calls)
// With [SubscribesTo]: Only one handler method per event
```

**Q5: Error handling in event delivery**
How should subscriber errors be handled?
```csharp
await player.ScoreChanged.SubscribeAsync(async score =>
{
    throw new Exception("Handler failed!");  // What happens?
});
```
Options: Retry, dead-letter, propagate to publisher, log and continue.

### 12.2 Future Work

**1. Server-side filtering**
```csharp
// Future: Filter at source, not just client-side
[Event(ServerFilter = "score > 1000")]
private EventSource<int> HighScores => ...;
```

**2. Event batching**
```csharp
// Future: Batch multiple events for efficiency
await player.ScoreChanged
    .Buffer(TimeSpan.FromSeconds(1))  // Collect for 1 second
    .SubscribeAsync(async batch => ProcessAll(batch));
```

**3. Event replay/sourcing**
```csharp
// Future: Replay all historical events
await player.ScoreChanged.ReplayFromBeginning(async evt =>
{
    // Process every event that ever occurred
});
```

**4. Typed event contracts**
```csharp
// Future: Events as first-class interface members
public partial interface IPlayerGrain : IGrainWithStringKey
{
    // Declared on interface, not just implementation
    event EventTask<int> ScoreChanged;
}
```

**5. Reactive Extensions integration**
```csharp
// Future: Full Rx support
IObservable<int> scores = player.ScoreChanged.AsObservable();
scores
    .Where(s => s > 1000)
    .Throttle(TimeSpan.FromSeconds(1))
    .Subscribe(s => ...);
```

**6. Cross-grain transactions with events**
```csharp
// Future: Events participate in distributed transactions
await using var tx = await TransactionClient.BeginTransaction();
await playerA.TransferPoints(playerB, 100);
// ScoreChanged events for both players are transactional
await tx.CommitAsync();
```

---

## Appendix A: Complete Generated Code Example

For a grain defined as:

```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task UpdateScoreAsync(int delta);
    Task SendChatAsync(string message);
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [State(Persisted = true, NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    [Event(Durable = false)]
    private EventSource<string> ChatMessage => Events.Source<string>();
    
    public Task UpdateScoreAsync(int delta)
    {
        Score += delta;
        return Task.CompletedTask;
    }
    
    public Task SendChatAsync(string message)
    {
        return ChatMessage.RaiseAsync(message);
    }
}
```

### Generated: IPlayerGrain.Events.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace MyGame.Grains
{
    partial interface IPlayerGrain
    {
        // Subscription method for ScoreChanged (from NotifyOnChange)
        global::System.Threading.Tasks.Task<global::Orleans.EventSubscriptionHandle<global::Orleans.PropertyChangedEvent<int>>> 
            SubscribeScoreChangedAsync(global::Orleans.Streams.StreamSequenceToken? resumeFrom = null);
        
        // Subscription method for ChatMessage
        global::System.Threading.Tasks.Task<global::Orleans.EventSubscriptionHandle<string>> 
            SubscribeChatMessageAsync(global::Orleans.Streams.StreamSequenceToken? resumeFrom = null);
    }
}
```

### Generated: PlayerGrain.Events.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace MyGame.Grains
{
    partial class PlayerGrain
    {
        // ═══════════════════════════════════════════════════════════════
        // STREAM FIELDS
        // ═══════════════════════════════════════════════════════════════
        
        private global::Orleans.Streams.IAsyncStream<global::Orleans.PropertyChangedEvent<int>>? _scoreChanged_stream;
        private global::Orleans.Streams.IAsyncStream<string>? _chatMessage_stream;
        
        // ═══════════════════════════════════════════════════════════════
        // EVENT SOURCE IMPLEMENTATIONS
        // ═══════════════════════════════════════════════════════════════
        
        // ScoreChanged event source (generated due to NotifyOnChange)
        private global::Orleans.EventSource<global::Orleans.PropertyChangedEvent<int>> ScoreChanged 
            => new global::Orleans.EventSource<global::Orleans.PropertyChangedEvent<int>>(
                () => _scoreChanged_stream ?? throw new global::System.InvalidOperationException("Grain not activated"));
        
        // ChatMessage event source (replaces Events.Source<string>() marker)
        private global::Orleans.EventSource<string> ChatMessage_Generated
            => new global::Orleans.EventSource<string>(
                () => _chatMessage_stream ?? throw new global::System.InvalidOperationException("Grain not activated"));
        
        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION (called from OnActivateAsync)
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeEventStreams_Generated()
        {
            // ScoreChanged stream (durable by default)
            {
                var streamProvider = this.GetStreamProvider("Default");
                var streamId = global::Orleans.Streams.StreamId.Create(
                    "IPlayerGrain.ScoreChanged", 
                    this.GetPrimaryKeyString());
                _scoreChanged_stream = streamProvider.GetStream<global::Orleans.PropertyChangedEvent<int>>(streamId);
            }
            
            // ChatMessage stream (transient)
            {
                var streamProvider = this.GetStreamProvider("SMS");
                var streamId = global::Orleans.Streams.StreamId.Create(
                    "IPlayerGrain.ChatMessage", 
                    this.GetPrimaryKeyString());
                _chatMessage_stream = streamProvider.GetStream<string>(streamId);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SCORE PROPERTY WITH AUTO-RAISE (from State + NotifyOnChange)
        // ═══════════════════════════════════════════════════════════════
        
        public partial int Score
        {
            get => _state.State.Score;
            set
            {
                var oldValue = _state.State.Score;
                if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(oldValue, value))
                {
                    _state.State.Score = value;
                    _ = ScoreChanged.RaiseAsync(new global::Orleans.PropertyChangedEvent<int>(oldValue, value));
                }
            }
        }
    }
}
```

### Generated: Proxy_IPlayerGrain.Events.g.cs

```csharp
// <auto-generated />
#nullable enable

namespace OrleansCodeGen.MyGame.Grains
{
    partial class Proxy_IPlayerGrain
    {
        // ═══════════════════════════════════════════════════════════════
        // EVENT TASK PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Event raised when Score property changes.
        /// </summary>
        public global::Orleans.EventTask<global::Orleans.PropertyChangedEvent<int>> ScoreChanged 
            => new global::Orleans.EventTask<global::Orleans.PropertyChangedEvent<int>>(
                this,
                global::Orleans.Streams.StreamId.Create("IPlayerGrain.ScoreChanged", this.GetPrimaryKeyString()),
                "Default");
        
        /// <summary>
        /// Chat message event.
        /// </summary>
        public global::Orleans.EventTask<string> ChatMessage 
            => new global::Orleans.EventTask<string>(
                this,
                global::Orleans.Streams.StreamId.Create("IPlayerGrain.ChatMessage", this.GetPrimaryKeyString()),
                "SMS");
        
        // ═══════════════════════════════════════════════════════════════
        // SUBSCRIPTION METHODS (interface implementation)
        // ═══════════════════════════════════════════════════════════════
        
        public global::System.Threading.Tasks.Task<global::Orleans.EventSubscriptionHandle<global::Orleans.PropertyChangedEvent<int>>> 
            SubscribeScoreChangedAsync(global::Orleans.Streams.StreamSequenceToken? resumeFrom = null)
        {
            return ScoreChanged.SubscribeAsync(_ => global::System.Threading.Tasks.Task.CompletedTask, resumeFrom);
        }
        
        public global::System.Threading.Tasks.Task<global::Orleans.EventSubscriptionHandle<string>> 
            SubscribeChatMessageAsync(global::Orleans.Streams.StreamSequenceToken? resumeFrom = null)
        {
            return ChatMessage.SubscribeAsync(_ => global::System.Threading.Tasks.Task.CompletedTask, resumeFrom);
        }
    }
}
```

---

## Appendix B: Comparison with StateTask

| Aspect | StateTask<T> | EventTask<T> |
|--------|--------------|--------------|
| **Purpose** | Remote property access | Remote event subscription |
| **C# parallel** | Properties | Events |
| **Partial support** | Yes (C# 13) | No (not in any C# version) |
| **Declaration** | `public partial T Prop { get; set; }` | `[Event] EventSource<T> Event => ...` |
| **Client read** | `await grain.Prop` | N/A (subscribe instead) |
| **Client write** | `await (grain.Prop << value)` | N/A (grain raises) |
| **Client subscribe** | N/A | `await grain.Event.SubscribeAsync(...)` |
| **Underlying Orleans** | RPC methods | Streams |
| **Data flow** | Request/response | Pub/sub |
| **Persistence** | Via IPersistentState | Via stream provider |

---

## Appendix C: Error Messages & Diagnostics

The code generator should produce helpful diagnostics:

| Code | Severity | Message |
|------|----------|---------|
| EVT001 | Error | `[Event] attribute requires property to return EventSource<T>` |
| EVT002 | Error | `Events.Source<T>() can only be used with [Event] attribute` |
| EVT003 | Warning | `Event property '{name}' is public; consider making it private` |
| EVT004 | Error | `Stream provider '{name}' is not configured` |
| EVT005 | Error | `[SubscribesTo] handler method '{name}' not found` |
| EVT006 | Warning | `[State(NotifyOnChange = true)] generates PropertyChangedEvent<T>; consider [Event] for custom payload` |
| EVT007 | Error | `Event payload type '{type}' must be serializable` |

---

*End of Document*
