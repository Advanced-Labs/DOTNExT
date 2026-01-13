# NewOrleans Events v1
## Design Specification for DOTNExT Fork

**Version**: 1.1  
**Status**: Design Phase  
**Context**: This document specifies event naturalization for Orleans grains using Simple Message Streams (SMS) as transport. This is the "transient" event type — fast, in-memory, non-durable.  
**Audience**: AI assistants and developers working on DOTNExT  
**Prerequisites**: Familiarity with Orleans grains, interfaces, proxies, and the State Properties Enhancement.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem Statement](#2-problem-statement)
3. [Solution Overview](#3-solution-overview)
4. [The Core Pattern](#4-the-core-pattern)
5. [Developer Experience](#5-developer-experience)
6. [Subscription Architecture](#6-subscription-architecture)
7. [Code Generation Specifications](#7-code-generation-specifications)
8. [Complete Generated Code Example](#8-complete-generated-code-example)
9. [Client Usage Patterns](#9-client-usage-patterns)
10. [Failure Handling](#10-failure-handling)
11. [Limitations (V1 Scope)](#11-limitations-v1-scope)
12. [Integration with State Properties](#12-integration-with-state-properties)
13. [Implementation Checklist](#13-implementation-checklist)

---

## 1. Executive Summary

NewOrleans Events v1 enables developers to declare standard C# events on grain classes and have them work transparently across the distributed Orleans system.

**Core mechanism**: 
- Developer declares a public event on the grain class
- Codegen adds that event to the grain interface
- The grain and its client proxy each have **different implementations** of the same interface event
- Simple Message Streams (SMS) transport events from grain to remote subscribers

**Key design decisions**:
- **Decoupled subscription**: Remote subscription is explicit via `SubscribeToXxxAsync()`; the `+=` operator is purely local
- **Subscription objects**: `IEventSubscription<T>` provides a rich handle for managing subscriptions
- **No blocking**: `+=` never blocks; all remote operations are async
- **Transient**: Events are in-memory only, not persisted
- **Best-effort delivery**: SMS handles fan-out; no guaranteed delivery

---

## 2. Problem Statement

### 2.1 The Distributed Event Gap

C# events work naturally within a single process:

```csharp
// Local event - works perfectly
public class Player
{
    public event EventHandler<string>? ChatMessage;
    
    public void SendChat(string msg)
    {
        ChatMessage?.Invoke(this, msg);  // All local subscribers notified
    }
}

// Local subscription
player.ChatMessage += (sender, msg) => Console.WriteLine(msg);
```

In Orleans, the "player" is a grain that may be on a different machine than the subscriber. Standard C# events cannot cross this boundary because:

1. Delegates are not serializable
2. `+=` is synchronous but remote subscription must be async
3. `Invoke()` is synchronous but remote notification must be async
4. Subscribers may be on different silos or be non-grain clients

### 2.2 Current Orleans Streams (Verbose)

Orleans Streams solve distributed pub/sub but require boilerplate:

**Grain (publisher):**
```csharp
public class PlayerGrain : Grain, IPlayerGrain
{
    private IAsyncStream<string>? _chatStream;
    
    public override Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider("SMS");
        var streamId = StreamId.Create("PlayerGrain.Chat", this.GetPrimaryKeyString());
        _chatStream = provider.GetStream<string>(streamId);
        return base.OnActivateAsync(ct);
    }
    
    public Task SendChat(string msg)
    {
        return _chatStream!.OnNextAsync(msg);
    }
}
```

**Client (subscriber):**
```csharp
var provider = client.GetStreamProvider("SMS");
var streamId = StreamId.Create("PlayerGrain.Chat", "player-1");
var stream = provider.GetStream<string>(streamId);

await stream.SubscribeAsync((msg, token) => 
{
    Console.WriteLine(msg);
    return Task.CompletedTask;
});
```

**Problems:**
- Stream provider name is stringly-typed
- Stream ID convention must be known by both sides
- No compile-time connection between grain and subscriber
- Doesn't look like C# events at all

---

## 3. Solution Overview

### 3.1 What the Developer Writes

**Grain class:**
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Standard C# event declaration
    public event EventHandler<string>? ChatMessage;
    
    public Task SendChatAsync(string message)
    {
        // Raise event using standard C# pattern
        ChatMessage?.Invoke(this, message);
        return Task.CompletedTask;
    }
}
```

**Interface (partial — only custom methods):**
```csharp
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
}
```

### 3.2 What Codegen Produces

1. **Interface extension**: Adds event declaration + subscription method signatures
2. **Grain extension**: Adds stream infrastructure and bridges dev's event to stream
3. **Proxy extension**: Implements local event + subscription management

### 3.3 What the Client Sees

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Step 1: Create remote subscription (async, explicit)
await using var subscription = await player.SubscribeToChatMessageAsync();

// Step 2: Attach local handlers (sync, purely local)
player.ChatMessage += (sender, msg) => Console.WriteLine($"Message: {msg}");

// Events now flow: grain raises → SMS stream → subscription → local handlers
```

---

## 4. The Core Pattern

### 4.1 Decoupled Subscription Architecture

The key insight of this design is **separating remote subscription from local handler attachment**:

| Operation | What It Does | Blocking? | Creates Network Traffic? |
|-----------|--------------|-----------|--------------------------|
| `await player.SubscribeToChatMessageAsync()` | Creates remote SMS subscription | No (async) | Yes |
| `player.ChatMessage += handler` | Attaches local handler to proxy event | No | No |
| `player.ChatMessage -= handler` | Detaches local handler | No | No |
| `await subscription.UnsubscribeAsync()` | Removes remote subscription | No (async) | Yes |

**Why this matters:**
- `+=` is truly non-blocking (pure local operation)
- Multiple local handlers can share one remote subscription
- Subscription lifecycle is explicit and manageable
- Natural async pattern for all remote operations

### 4.2 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INTERFACE                                       │
│                                                                             │
│  public partial interface IPlayerGrain : IGrainWithStringKey                │
│  {                                                                          │
│      event EventHandler<string>? ChatMessage;  // Added by codegen          │
│      Task SendChatAsync(string message);       // Developer-written         │
│                                                                             │
│      // Subscription management (added by codegen)                          │
│      Task<IEventSubscription<string>> SubscribeToChatMessageAsync();        │
│      Task<IEventSubscription<string>> SubscribeToChatMessageAsync(          │
│          Func<string, Task> handler);                                       │
│  }                                                                          │
└─────────────────────────────────────────────────────────────────────────────┘
                    │                                   │
        Implemented by                       Implemented by
                    │                                   │
                    ▼                                   ▼
┌──────────────────────────────────┐  ┌──────────────────────────────────────┐
│          GRAIN CLASS             │  │           PROXY CLASS                │
│                                  │  │                                      │
│  event ChatMessage               │  │  event ChatMessage                   │
│    │                             │  │    │                                 │
│    ├─► Local handlers            │  │    └─► Purely local handlers         │
│    │   (dev's own += usage)      │  │        (no remote interaction)       │
│    │                             │  │                                      │
│    └─► Bridge handler            │  │  SubscribeToChatMessageAsync()       │
│        (publishes to SMS)        │  │    │                                 │
│                                  │  │    └─► Creates SMS subscription      │
│  PUBLISHES TO ──────────────────────────────────► SUBSCRIBES TO            │
│                    SMS Stream                                              │
│                                  │  │  Subscription receives events        │
│                                  │  │    │                                 │
│                                  │  │    └─► Raises local ChatMessage      │
└──────────────────────────────────┘  └──────────────────────────────────────┘
```

### 4.3 Event Flow

```
GRAIN                              SMS                         PROXY/CLIENT
──────────────────────────────────────────────────────────────────────────────

                                                   SubscribeToChatMessageAsync()
                                                              │
                                                              ▼
                                              Proxy subscribes to SMS stream
                                              StreamId: "IPlayerGrain.ChatMessage.{key}"
                                                              │
                                                              ▼
                                                   Returns IEventSubscription<string>

                                                   client attaches: ChatMessage += handler

ChatMessage?.Invoke(this, "Hi")
         │
         ▼
    Local handlers invoked
    (if any in-grain += usage)
         │
         ▼
    Bridge handler invoked
         │
         ▼
    _stream.OnNextAsync("Hi")
         │
         └─────────────── SMS ────────────────────►  Subscription receives "Hi"
                                                              │
                                                              ▼
                                                   Proxy raises ChatMessage event
                                                              │
                                                              ▼
                                                   Local handler("Hi") invoked
```

---

## 5. Developer Experience

### 5.1 Grain Development — What You Write

**The simplest case:**

```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
    Task TakeDamageAsync(int amount);
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private int _health = 100;
    
    // Standard C# events — that's it!
    public event EventHandler<string>? ChatMessage;
    public event EventHandler<int>? HealthChanged;
    
    public Task SendChatAsync(string message)
    {
        // Raise event using standard C# pattern
        ChatMessage?.Invoke(this, message);
        return Task.CompletedTask;
    }
    
    public Task TakeDamageAsync(int amount)
    {
        _health = Math.Max(0, _health - amount);
        HealthChanged?.Invoke(this, _health);
        return Task.CompletedTask;
    }
}
```

**That's the entire grain implementation.** No stream setup, no provider names, no stream IDs.

### 5.2 Combined with State Properties

Since State Properties is already implemented, they compose naturally:

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // State Properties (from existing implementation)
    public partial string Name { get; set; }
    public partial int Score { get; set; }
    
    // Events
    public event EventHandler<string>? ChatMessage;
    public event EventHandler<int>? ScoreChanged;
    
    public Task AddPointsAsync(int points)
    {
        Score += points;
        ScoreChanged?.Invoke(this, Score);  // Explicit raise
        return Task.CompletedTask;
    }
}
```

### 5.3 Opting Out

For events that should stay local-only:

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // This gets distributed via SMS
    public event EventHandler<string>? ChatMessage;
    
    // This stays local — no codegen, no streams
    [NotEvent]
    public event EventHandler? DiagnosticTick;
}
```

---

## 6. Subscription Architecture

### 6.1 The IEventSubscription Interface

```csharp
namespace Orleans;

/// <summary>
/// Represents an active subscription to a grain event.
/// Disposing this object unsubscribes from the remote stream.
/// </summary>
/// <typeparam name="T">The event payload type.</typeparam>
public interface IEventSubscription<T> : IAsyncDisposable
{
    /// <summary>
    /// The underlying Orleans stream subscription handle.
    /// </summary>
    StreamSubscriptionHandle<T> Handle { get; }
    
    /// <summary>
    /// Whether this subscription is currently active.
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// The StreamId this subscription is listening to.
    /// </summary>
    StreamId StreamId { get; }
    
    /// <summary>
    /// Unsubscribe from the remote stream.
    /// After calling this, no more events will be received.
    /// </summary>
    ValueTask UnsubscribeAsync();
}
```

### 6.2 Implementation

```csharp
namespace Orleans.Runtime;

/// <summary>
/// Default implementation of IEventSubscription.
/// </summary>
internal sealed class EventSubscription<T> : IEventSubscription<T>
{
    private readonly StreamSubscriptionHandle<T> _handle;
    private readonly StreamId _streamId;
    private bool _isActive = true;
    
    public EventSubscription(StreamSubscriptionHandle<T> handle, StreamId streamId)
    {
        _handle = handle;
        _streamId = streamId;
    }
    
    public StreamSubscriptionHandle<T> Handle => _handle;
    public bool IsActive => _isActive;
    public StreamId StreamId => _streamId;
    
    public async ValueTask UnsubscribeAsync()
    {
        if (_isActive)
        {
            await _handle.UnsubscribeAsync();
            _isActive = false;
        }
    }
    
    public ValueTask DisposeAsync() => UnsubscribeAsync();
}
```

### 6.3 Why This Design?

| Alternative | Problem |
|-------------|---------|
| Return raw `StreamSubscriptionHandle<T>` | Less discoverable, no `IAsyncDisposable`, harder to extend |
| Return `Task` only | Can't unsubscribe, can't check status |
| `+=` creates subscription | Blocking (sync-over-async), couples local/remote concerns |

The `IEventSubscription<T>` approach:
- Clean `await using` pattern for automatic cleanup
- Explicit lifecycle management
- Room for future extensions (e.g., `IObservable<T> AsObservable()`)
- Matches modern C# patterns

---

## 7. Code Generation Specifications

### 7.1 Detection Rules

An event triggers NewOrleans Event codegen if ALL of:
- Event is declared `public`
- Event is in a class that inherits from `Grain`
- The class implements at least one `IGrainWithXXXKey` interface
- Event does NOT have `[NotEvent]` attribute (escape hatch)

### 7.2 What Gets Generated

For each qualifying event `{EventName}` of type `EventHandler<{T}>`:

| Target | Generated |
|--------|-----------|
| **Interface** | `event EventHandler<{T}>? {EventName};` |
| **Interface** | `Task<IEventSubscription<{T}>> SubscribeTo{EventName}Async();` |
| **Interface** | `Task<IEventSubscription<{T}>> SubscribeTo{EventName}Async(Func<{T}, Task> handler);` |
| **Grain** | Stream field: `__{eventName}_stream` |
| **Grain** | Bridge handler attachment in lifecycle |
| **Grain** | Subscription method implementations |
| **Proxy** | Local event with standard add/remove |
| **Proxy** | Internal `__Raise{EventName}(T payload)` method |
| **Proxy** | Subscription methods that connect stream to local event |

### 7.3 Naming Conventions

| Source | Generated |
|--------|-----------|
| Event `ChatMessage` | Stream namespace: `"{InterfaceName}.{EventName}"` |
| Event `ChatMessage` | Stream key: grain's primary key |
| Event `ChatMessage` | Subscribe method: `SubscribeToChatMessageAsync` |
| Event `ChatMessage` | Grain stream field: `__chatMessage_stream` |
| Event `ChatMessage` | Proxy raise method: `__RaiseChatMessage` |

### 7.4 Stream ID Convention

```csharp
StreamId.Create(
    "{GrainInterfaceName}.{EventName}",   // Namespace
    grain.GetPrimaryKeyString()            // Key (or appropriate key type)
)

// Example:
StreamId.Create("IPlayerGrain.ChatMessage", "player-1")
```

### 7.5 Output Files

For a grain `PlayerGrain : IPlayerGrain` with event `ChatMessage`:

| File | Contents |
|------|----------|
| `IPlayerGrain.Events.g.cs` | Interface event declaration + subscribe method signatures |
| `PlayerGrain.Events.g.cs` | Stream field, bridge setup, subscribe implementation |
| `Proxy_IPlayerGrain.Events.g.cs` | Local event, raise method, subscription methods |

---

## 8. Complete Generated Code Example

### 8.1 Developer Writes

```csharp
// IPlayerGrain.cs
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
    Task UpdateScoreAsync(int delta);
}

// PlayerGrain.cs
public partial class PlayerGrain : Grain, IPlayerGrain
{
    private int _score;
    
    // Two events
    public event EventHandler<string>? ChatMessage;
    public event EventHandler<int>? ScoreChanged;
    
    public Task SendChatAsync(string message)
    {
        ChatMessage?.Invoke(this, message);
        return Task.CompletedTask;
    }
    
    public Task UpdateScoreAsync(int delta)
    {
        _score += delta;
        ScoreChanged?.Invoke(this, _score);
        return Task.CompletedTask;
    }
}
```

### 8.2 Generated: IPlayerGrain.Events.g.cs

```csharp
// <auto-generated />
// NewOrleans Events v1 - Generated event interface members
#nullable enable

namespace MyGame.Grains
{
    public partial interface IPlayerGrain
    {
        /// <summary>
        /// Event raised when a chat message is sent.
        /// Attach local handlers using +=, but first create a subscription
        /// with SubscribeToChatMessageAsync().
        /// </summary>
        event global::System.EventHandler<string>? ChatMessage;
        
        /// <summary>
        /// Event raised when score changes.
        /// </summary>
        event global::System.EventHandler<int>? ScoreChanged;
        
        /// <summary>
        /// Subscribe to ChatMessage events from this grain.
        /// The returned subscription will trigger the local ChatMessage event
        /// when events are received.
        /// </summary>
        /// <returns>
        /// A subscription object. Dispose to unsubscribe.
        /// </returns>
        global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            SubscribeToChatMessageAsync();
        
        /// <summary>
        /// Subscribe to ChatMessage events with a direct async handler.
        /// Both the provided handler AND local += handlers will be invoked.
        /// </summary>
        /// <param name="handler">Async handler invoked for each event.</param>
        /// <returns>A subscription object. Dispose to unsubscribe.</returns>
        global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            SubscribeToChatMessageAsync(
                global::System.Func<string, global::System.Threading.Tasks.Task> handler);
        
        /// <summary>
        /// Subscribe to ScoreChanged events from this grain.
        /// </summary>
        global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            SubscribeToScoreChangedAsync();
        
        /// <summary>
        /// Subscribe to ScoreChanged events with a direct async handler.
        /// </summary>
        global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            SubscribeToScoreChangedAsync(
                global::System.Func<int, global::System.Threading.Tasks.Task> handler);
    }
}
```

### 8.3 Generated: PlayerGrain.Events.g.cs

```csharp
// <auto-generated />
// NewOrleans Events v1 - Generated grain event infrastructure
#nullable enable

namespace MyGame.Grains
{
    public partial class PlayerGrain : global::Orleans.ILifecycleParticipant<global::Orleans.IGrainLifecycle>
    {
        // ═══════════════════════════════════════════════════════════════
        // STREAM INFRASTRUCTURE
        // ═══════════════════════════════════════════════════════════════
        
        private global::Orleans.Streams.IAsyncStream<string>? __chatMessage_stream;
        private global::Orleans.Streams.IAsyncStream<int>? __scoreChanged_stream;
        
        // Bridge handlers (so we can detach if needed)
        private global::System.EventHandler<string>? __chatMessage_bridge;
        private global::System.EventHandler<int>? __scoreChanged_bridge;
        
        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE PARTICIPATION
        // ═══════════════════════════════════════════════════════════════
        
        void global::Orleans.ILifecycleParticipant<global::Orleans.IGrainLifecycle>.Participate(
            global::Orleans.IGrainLifecycle lifecycle)
        {
            lifecycle.Subscribe<PlayerGrain>(
                global::Orleans.GrainLifecycleStage.Activate,
                ct => { __InitializeNewOrleansEvents(); return global::System.Threading.Tasks.Task.CompletedTask; },
                ct => { __CleanupNewOrleansEvents(); return global::System.Threading.Tasks.Task.CompletedTask; }
            );
        }
        
        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════
        
        private void __InitializeNewOrleansEvents()
        {
            var streamProvider = this.GetStreamProvider("SMS");
            var grainKey = this.GetPrimaryKeyString();
            
            // ─────────────────────────────────────────────────────────────
            // ChatMessage event setup
            // ─────────────────────────────────────────────────────────────
            
            var chatMessageStreamId = global::Orleans.Streams.StreamId.Create(
                "IPlayerGrain.ChatMessage", grainKey);
            __chatMessage_stream = streamProvider.GetStream<string>(chatMessageStreamId);
            
            // Bridge: when dev raises ChatMessage, also publish to stream
            __chatMessage_bridge = (sender, payload) =>
            {
                // Fire-and-forget publish to stream
                _ = __PublishToStreamAsync(__chatMessage_stream, payload);
            };
            
            // Attach bridge to dev's event
            ChatMessage += __chatMessage_bridge;
            
            // ─────────────────────────────────────────────────────────────
            // ScoreChanged event setup
            // ─────────────────────────────────────────────────────────────
            
            var scoreChangedStreamId = global::Orleans.Streams.StreamId.Create(
                "IPlayerGrain.ScoreChanged", grainKey);
            __scoreChanged_stream = streamProvider.GetStream<int>(scoreChangedStreamId);
            
            __scoreChanged_bridge = (sender, payload) =>
            {
                _ = __PublishToStreamAsync(__scoreChanged_stream, payload);
            };
            
            ScoreChanged += __scoreChanged_bridge;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════
        
        private void __CleanupNewOrleansEvents()
        {
            if (__chatMessage_bridge != null)
            {
                ChatMessage -= __chatMessage_bridge;
                __chatMessage_bridge = null;
            }
            
            if (__scoreChanged_bridge != null)
            {
                ScoreChanged -= __scoreChanged_bridge;
                __scoreChanged_bridge = null;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // STREAM PUBLISHING HELPER
        // ═══════════════════════════════════════════════════════════════
        
        private async global::System.Threading.Tasks.Task __PublishToStreamAsync<T>(
            global::Orleans.Streams.IAsyncStream<T>? stream, 
            T payload)
        {
            if (stream == null) return;
            
            try
            {
                await stream.OnNextAsync(payload);
            }
            catch (global::System.Exception ex)
            {
                // Log but don't fail the grain operation
                // Orleans SMS handles dead subscribers internally
                this.GetLogger().LogWarning(
                    ex, 
                    "NewOrleans Event: Failed to publish to stream. " +
                    "Some remote subscribers may not receive this event."
                );
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SUBSCRIPTION METHODS (interface implementation)
        // ═══════════════════════════════════════════════════════════════
        
        async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            global::MyGame.Grains.IPlayerGrain.SubscribeToChatMessageAsync()
        {
            if (__chatMessage_stream == null)
            {
                throw new global::System.InvalidOperationException(
                    "NewOrleans Event infrastructure not initialized.");
            }
            
            var streamId = __chatMessage_stream.StreamId;
            var handle = await __chatMessage_stream.SubscribeAsync(
                (payload, token) => global::System.Threading.Tasks.Task.CompletedTask
            );
            
            return new global::Orleans.Runtime.EventSubscription<string>(handle, streamId);
        }
        
        async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            global::MyGame.Grains.IPlayerGrain.SubscribeToChatMessageAsync(
                global::System.Func<string, global::System.Threading.Tasks.Task> handler)
        {
            if (__chatMessage_stream == null)
            {
                throw new global::System.InvalidOperationException(
                    "NewOrleans Event infrastructure not initialized.");
            }
            
            var streamId = __chatMessage_stream.StreamId;
            var handle = await __chatMessage_stream.SubscribeAsync(
                (payload, token) => handler(payload)
            );
            
            return new global::Orleans.Runtime.EventSubscription<string>(handle, streamId);
        }
        
        async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            global::MyGame.Grains.IPlayerGrain.SubscribeToScoreChangedAsync()
        {
            if (__scoreChanged_stream == null)
            {
                throw new global::System.InvalidOperationException(
                    "NewOrleans Event infrastructure not initialized.");
            }
            
            var streamId = __scoreChanged_stream.StreamId;
            var handle = await __scoreChanged_stream.SubscribeAsync(
                (payload, token) => global::System.Threading.Tasks.Task.CompletedTask
            );
            
            return new global::Orleans.Runtime.EventSubscription<int>(handle, streamId);
        }
        
        async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            global::MyGame.Grains.IPlayerGrain.SubscribeToScoreChangedAsync(
                global::System.Func<int, global::System.Threading.Tasks.Task> handler)
        {
            if (__scoreChanged_stream == null)
            {
                throw new global::System.InvalidOperationException(
                    "NewOrleans Event infrastructure not initialized.");
            }
            
            var streamId = __scoreChanged_stream.StreamId;
            var handle = await __scoreChanged_stream.SubscribeAsync(
                (payload, token) => handler(payload)
            );
            
            return new global::Orleans.Runtime.EventSubscription<int>(handle, streamId);
        }
    }
}
```

### 8.4 Generated: Proxy_IPlayerGrain.Events.g.cs

```csharp
// <auto-generated />
// NewOrleans Events v1 - Generated proxy event implementation
#nullable enable

namespace OrleansCodeGen.MyGame.Grains
{
    internal partial class Proxy_IPlayerGrain
    {
        // ═══════════════════════════════════════════════════════════════
        // LOCAL EVENT HANDLERS
        // These are purely local — no remote interaction on += or -=
        // ═══════════════════════════════════════════════════════════════
        
        private global::System.EventHandler<string>? __chatMessage_localHandlers;
        private global::System.EventHandler<int>? __scoreChanged_localHandlers;
        
        // ═══════════════════════════════════════════════════════════════
        // ChatMessage EVENT
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Local event triggered when the remote subscription receives data.
        /// Use += to attach handlers, but first call SubscribeToChatMessageAsync()
        /// to create the remote subscription.
        /// </summary>
        public event global::System.EventHandler<string>? ChatMessage
        {
            add => __chatMessage_localHandlers += value;
            remove => __chatMessage_localHandlers -= value;
        }
        
        /// <summary>
        /// Internal: Raises the local ChatMessage event.
        /// Called by subscription when stream data arrives.
        /// </summary>
        internal void __RaiseChatMessage(string payload)
        {
            try
            {
                __chatMessage_localHandlers?.Invoke(this, payload);
            }
            catch
            {
                // Swallow handler exceptions to not break other subscribers
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ScoreChanged EVENT
        // ═══════════════════════════════════════════════════════════════
        
        public event global::System.EventHandler<int>? ScoreChanged
        {
            add => __scoreChanged_localHandlers += value;
            remove => __scoreChanged_localHandlers -= value;
        }
        
        internal void __RaiseScoreChanged(int payload)
        {
            try
            {
                __scoreChanged_localHandlers?.Invoke(this, payload);
            }
            catch
            {
                // Swallow handler exceptions
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SUBSCRIPTION METHODS
        // These create the actual remote SMS subscription
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Subscribe to ChatMessage events from this grain.
        /// When events arrive, the local ChatMessage event will be raised.
        /// </summary>
        public async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            SubscribeToChatMessageAsync()
        {
            var stream = __GetEventStream<string>("IPlayerGrain.ChatMessage");
            var streamId = stream.StreamId;
            
            var handle = await stream.SubscribeAsync((payload, token) =>
            {
                __RaiseChatMessage(payload);
                return global::System.Threading.Tasks.Task.CompletedTask;
            });
            
            return new global::Orleans.Runtime.EventSubscription<string>(handle, streamId);
        }
        
        /// <summary>
        /// Subscribe to ChatMessage events with a direct async handler.
        /// Both the provided handler AND local += handlers will be invoked.
        /// </summary>
        public async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<string>> 
            SubscribeToChatMessageAsync(
                global::System.Func<string, global::System.Threading.Tasks.Task> handler)
        {
            var stream = __GetEventStream<string>("IPlayerGrain.ChatMessage");
            var streamId = stream.StreamId;
            
            var handle = await stream.SubscribeAsync(async (payload, token) =>
            {
                // Call the direct handler first
                await handler(payload);
                
                // Then raise local event
                __RaiseChatMessage(payload);
            });
            
            return new global::Orleans.Runtime.EventSubscription<string>(handle, streamId);
        }
        
        /// <summary>
        /// Subscribe to ScoreChanged events from this grain.
        /// </summary>
        public async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            SubscribeToScoreChangedAsync()
        {
            var stream = __GetEventStream<int>("IPlayerGrain.ScoreChanged");
            var streamId = stream.StreamId;
            
            var handle = await stream.SubscribeAsync((payload, token) =>
            {
                __RaiseScoreChanged(payload);
                return global::System.Threading.Tasks.Task.CompletedTask;
            });
            
            return new global::Orleans.Runtime.EventSubscription<int>(handle, streamId);
        }
        
        /// <summary>
        /// Subscribe to ScoreChanged events with a direct async handler.
        /// </summary>
        public async global::System.Threading.Tasks.Task<global::Orleans.IEventSubscription<int>> 
            SubscribeToScoreChangedAsync(
                global::System.Func<int, global::System.Threading.Tasks.Task> handler)
        {
            var stream = __GetEventStream<int>("IPlayerGrain.ScoreChanged");
            var streamId = stream.StreamId;
            
            var handle = await stream.SubscribeAsync(async (payload, token) =>
            {
                await handler(payload);
                __RaiseScoreChanged(payload);
            });
            
            return new global::Orleans.Runtime.EventSubscription<int>(handle, streamId);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // HELPER: Get stream for event
        // ═══════════════════════════════════════════════════════════════
        
        private global::Orleans.Streams.IAsyncStream<T> __GetEventStream<T>(string eventNamespace)
        {
            var streamProvider = this.GetStreamProvider("SMS");
            var grainKey = this.GetPrimaryKeyString();
            var streamId = global::Orleans.Streams.StreamId.Create(eventNamespace, grainKey);
            return streamProvider.GetStream<T>(streamId);
        }
    }
}
```

---

## 9. Client Usage Patterns

### 9.1 Pattern A: Subscription + Local Handlers via +=

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Attach handlers first (nothing happens yet, no subscription)
player.ChatMessage += (s, msg) => Console.WriteLine($"Handler 1: {msg}");
player.ChatMessage += (s, msg) => Console.WriteLine($"Handler 2: {msg}");

// Create subscription — now handlers will fire when events arrive
await using var sub = await player.SubscribeToChatMessageAsync();

// Trigger events
await player.SendChatAsync("Hello!");
// Output:
// Handler 1: Hello!
// Handler 2: Hello!

// When 'sub' is disposed, remote subscription ends
// Local handlers remain attached but won't fire (no subscription)
```

### 9.2 Pattern B: Direct Async Handler (No +=)

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Subscribe with inline async handler
await using var sub = await player.SubscribeToChatMessageAsync(async msg =>
{
    Console.WriteLine(msg);
    await SaveToDbAsync(msg);  // Can do async work
});

await player.SendChatAsync("Hello!");
// Output: Hello!
```

### 9.3 Pattern C: Both Direct Handler + Local Handlers

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Attach local handler
player.ChatMessage += (s, msg) => UpdateUI(msg);

// Subscribe with direct async handler — BOTH fire
await using var sub = await player.SubscribeToChatMessageAsync(async msg =>
{
    await LogAsync(msg);  // This fires first
});

await player.SendChatAsync("Hello!");
// LogAsync called, then UpdateUI called
```

### 9.4 Pattern D: Subscription First, Handlers Later

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Create subscription first
var sub = await player.SubscribeToChatMessageAsync();

// ... later in code, maybe in response to user action ...
player.ChatMessage += (s, msg) => Console.WriteLine(msg);

// Works fine — subscription exists, handler starts receiving
await player.SendChatAsync("Hello!");  // Prints "Hello!"

// Cleanup
await sub.UnsubscribeAsync();
```

### 9.5 Pattern E: Handlers Without Subscription (Silent)

```csharp
var player = client.GetGrain<IPlayerGrain>("player-1");

// Attach handler but forget to subscribe
player.ChatMessage += (s, msg) => Console.WriteLine(msg);

// No subscription exists — handler never fires, no error
await player.SendChatAsync("Hello!");  // Nothing happens

// Analyzer NOE010 should warn about this
```

### 9.6 Pattern F: Grain-to-Grain Subscription

```csharp
public class ChatLogGrain : Grain, IChatLogGrain
{
    private readonly List<IEventSubscription<string>> _subscriptions = new();
    
    public async Task TrackPlayerAsync(string playerId)
    {
        var player = GrainFactory.GetGrain<IPlayerGrain>(playerId);
        
        var sub = await player.SubscribeToChatMessageAsync(async message =>
        {
            await LogMessageAsync(playerId, message);
        });
        
        _subscriptions.Add(sub);
    }
    
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        foreach (var sub in _subscriptions)
        {
            await sub.UnsubscribeAsync();
        }
        await base.OnDeactivateAsync(reason, ct);
    }
}
```

### 9.7 Pattern G: Multiple Grains, Shared Handler Logic

```csharp
var lobby = client.GetGrain<ILobbyGrain>("main-lobby");
var player1 = client.GetGrain<IPlayerGrain>("player-1");
var player2 = client.GetGrain<IPlayerGrain>("player-2");

// Common handler
void OnChat(object? sender, string msg)
{
    var source = sender switch
    {
        ILobbyGrain => "Lobby",
        IPlayerGrain p => $"Player",
        _ => "Unknown"
    };
    Console.WriteLine($"[{source}] {msg}");
}

// Attach to multiple grains
lobby.ChatMessage += OnChat;
player1.ChatMessage += OnChat;
player2.ChatMessage += OnChat;

// Subscribe to all
var subs = await Task.WhenAll(
    lobby.SubscribeToChatMessageAsync(),
    player1.SubscribeToChatMessageAsync(),
    player2.SubscribeToChatMessageAsync()
);

// Cleanup all
foreach (var sub in subs)
{
    await sub.UnsubscribeAsync();
}
```

---

## 10. Failure Handling

### 10.1 Stream Publishing Failures

When the grain raises an event and stream publishing fails:

```csharp
private async Task __PublishToStreamAsync<T>(IAsyncStream<T>? stream, T payload)
{
    try
    {
        await stream.OnNextAsync(payload);
    }
    catch (Exception ex)
    {
        // Log warning but don't fail the grain operation
        // The local event raise still succeeds
        _logger.LogWarning(ex, "Failed to publish event to remote subscribers");
    }
}
```

**Behavior**: Local handlers within the grain still execute. Remote notification is best-effort.

### 10.2 Dead Subscribers

Orleans SMS handles dead subscribers internally:
- Subscribers that disconnect are eventually cleaned up
- Failed deliveries trigger retries with backoff
- Eventually, dead subscriptions are removed
- The grain is never notified of lost subscribers

**No application-level heartbeat is needed.** Orleans relies on TCP keepalive and connection state.

### 10.3 Client Disconnection and Reconnection

When an Orleans client disconnects and reconnects:
1. The TCP connection drops
2. Orleans client automatically attempts reconnection (configurable)
3. Upon reconnection, **implicit SMS subscriptions are lost**
4. The client must call `SubscribeToXxxAsync()` again to resubscribe

**Pattern for robust reconnection:**

```csharp
public class ResilientSubscriber : IAsyncDisposable
{
    private readonly IPlayerGrain _grain;
    private IEventSubscription<string>? _subscription;
    
    public ResilientSubscriber(IPlayerGrain grain)
    {
        _grain = grain;
        _grain.ChatMessage += OnMessage;
    }
    
    public async Task ConnectAsync()
    {
        _subscription = await _grain.SubscribeToChatMessageAsync();
    }
    
    public async Task ReconnectAsync()
    {
        // Called after connection restored (e.g., from connection lost handler)
        if (_subscription != null)
        {
            try { await _subscription.UnsubscribeAsync(); } catch { }
        }
        _subscription = await _grain.SubscribeToChatMessageAsync();
    }
    
    private void OnMessage(object? sender, string msg)
    {
        Console.WriteLine(msg);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync();
        }
    }
}
```

### 10.4 Grain Deactivation

When a grain deactivates:
- Stream subscriptions to that grain's events remain registered in SMS
- When the grain reactivates, it reconnects to the same streams
- Existing subscribers continue receiving events when grain is active

---

## 11. Limitations (V1 Scope)

### 11.1 What V1 Does NOT Include

| Feature | V1 Status | Future Consideration |
|---------|-----------|----------------------|
| **Persistence** | ❌ Events lost on silo restart | Durable streams |
| **Security tokens** | ❌ Anyone knowing stream ID can subscribe | Token-based auth |
| **Guaranteed delivery** | ❌ Best-effort only | Acks and retry |
| **Event replay** | ❌ No history | Persistent streams |
| **Subscription persistence** | ❌ Lost on client restart | Stored subscriptions |
| **Backpressure** | ❌ Relies on SMS behavior | Explicit flow control |
| **Reactive extensions** | ❌ No IObservable<T> | `sub.AsObservable()` |

### 11.2 V1 Guarantees

| Aspect | Guarantee |
|--------|-----------|
| **Delivery** | Best-effort; may lose events on failures |
| **Ordering** | Preserved per-stream (SMS guarantees this) |
| **Latency** | Low (in-memory SMS) |
| **Throughput** | High (no persistence overhead) |
| **Local handler isolation** | Handler exceptions don't break other handlers |
| **Grain failure isolation** | Stream publish failures don't crash grain |

### 11.3 When NOT to Use V1 Events

- **Audit logs**: Use durable streams directly
- **Financial transactions**: Need guaranteed delivery
- **Critical notifications**: Need acknowledgment
- **Event sourcing**: Need replay capability
- **Compliance scenarios**: Need delivery guarantees

### 11.4 Ideal Use Cases for V1

- UI update notifications
- Real-time game events
- Chat messages (ephemeral)
- Progress updates
- Cache invalidation signals
- Debug/monitoring events
- Multiplayer game state sync

---

## 12. Integration with State Properties

### 12.1 Auto-Raise Events on Property Change

When combined with the State Properties Enhancement, events can be auto-raised:

```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // Property from State Properties Enhancement
    [State(NotifyOnChange = true)]  // New attribute flag
    public partial int Score { get; set; }
    
    // Event that gets auto-raised when Score changes
    public event EventHandler<int>? ScoreChanged;
}
```

### 12.2 Generated Property with Auto-Raise

When `NotifyOnChange = true`, the property setter raises the event:

```csharp
// Generated property implementation
public partial int Score
{
    get => _score_backing;
    set
    {
        var oldValue = _score_backing;
        if (!EqualityComparer<int>.Default.Equals(oldValue, value))
        {
            _score_backing = value;
            
            // Auto-raise the ScoreChanged event
            ScoreChanged?.Invoke(this, value);
        }
    }
}
```

### 12.3 Detection Rules for Auto-Raise

The code generator links properties to events by naming convention:

| Property | Expected Event |
|----------|---------------|
| `Score` | `ScoreChanged` |
| `Name` | `NameChanged` |
| `Health` | `HealthChanged` |

If `[State(NotifyOnChange = true)]` is set AND a matching event exists, codegen adds the raise call.

### 12.4 Full Integration Example

```csharp
// Developer writes
public partial class PlayerGrain : Grain, IPlayerGrain
{
    [State(NotifyOnChange = true)]
    public partial int Score { get; set; }
    
    [State(NotifyOnChange = true)]
    public partial string Name { get; set; }
    
    public event EventHandler<int>? ScoreChanged;
    public event EventHandler<string>? NameChanged;
    
    public Task AddPointsAsync(int points)
    {
        Score += points;  // Automatically raises ScoreChanged
        return Task.CompletedTask;
    }
}

// Client
var player = client.GetGrain<IPlayerGrain>("player-1");

await using var scoreSub = await player.SubscribeToScoreChangedAsync();
await using var nameSub = await player.SubscribeToNameChangedAsync();

player.ScoreChanged += (s, score) => Console.WriteLine($"Score: {score}");
player.NameChanged += (s, name) => Console.WriteLine($"Name: {name}");

await player.AddPointsAsync(100);       // Triggers ScoreChanged
await (player.Name << "Louis");         // Triggers NameChanged (via StateTask)
```

---

## 13. Implementation Checklist

### 13.1 Phase 1: Core Infrastructure

- [ ] Create `IEventSubscription<T>` interface
- [ ] Create `EventSubscription<T>` implementation
- [ ] Create `[NotEvent]` attribute for opting out
- [ ] Unit tests for `EventSubscription<T>`

### 13.2 Phase 2: Interface Code Generation

- [ ] Implement event detection in code generator (scan for `public event` on grains)
- [ ] Generate interface event declarations
- [ ] Generate interface `SubscribeTo{EventName}Async()` method signatures (both overloads)

### 13.3 Phase 3: Grain-Side Generation

- [ ] Generate stream fields (`__eventName_stream`)
- [ ] Generate bridge handlers (`__eventName_bridge`)
- [ ] Generate `ILifecycleParticipant` implementation
- [ ] Generate `__InitializeNewOrleansEvents()` method
- [ ] Generate `__CleanupNewOrleansEvents()` method
- [ ] Generate `__PublishToStreamAsync<T>()` helper
- [ ] Generate subscription method implementations

### 13.4 Phase 4: Proxy-Side Generation

- [ ] Generate local handler fields (`__eventName_localHandlers`)
- [ ] Generate event `add` accessor (pure local)
- [ ] Generate event `remove` accessor (pure local)
- [ ] Generate `__Raise{EventName}(T payload)` method
- [ ] Generate `SubscribeTo{EventName}Async()` methods (both overloads)
- [ ] Generate `__GetEventStream<T>()` helper

### 13.5 Phase 5: Testing

- [ ] Unit test: Event detection in code generator
- [ ] Unit test: Generated code compiles
- [ ] Integration test: Local event raise (in-grain)
- [ ] Integration test: Remote subscription via `SubscribeTo{EventName}Async()`
- [ ] Integration test: Local handlers via `+=`
- [ ] Integration test: Both direct handler and `+=` handlers
- [ ] Integration test: Unsubscription via `IEventSubscription.UnsubscribeAsync()`
- [ ] Integration test: Unsubscription via `await using`
- [ ] Integration test: Multiple subscribers
- [ ] Integration test: Grain deactivation/reactivation
- [ ] Integration test: Client disconnection (subscriptions lost)

### 13.6 Phase 6: State Properties Integration

- [ ] Add `NotifyOnChange` flag to `[State]` attribute
- [ ] Implement naming convention matching (property → event)
- [ ] Generate auto-raise code in property setters
- [ ] Test property change → event flow

### 13.7 Phase 7: Analyzers & Polish

- [ ] Analyzer NOE001: Unsupported event handler type
- [ ] Analyzer NOE002: `NotifyOnChange = true` but no matching event
- [ ] Analyzer NOE003: Event on non-partial class
- [ ] Analyzer NOE004: Event on grain without `IGrainWithXXXKey`
- [ ] Analyzer NOE010: Warning for `+=` without subscription in scope
- [ ] XML documentation on generated members
- [ ] Sample application demonstrating NewOrleans Events v1

---

## Appendix A: NotEvent Attribute

```csharp
namespace Orleans;

/// <summary>
/// Excludes a public event from NewOrleans Event code generation.
/// Use for events that should remain local-only or are handled differently.
/// </summary>
[AttributeUsage(AttributeTargets.Event, AllowMultiple = false)]
public sealed class NotEventAttribute : Attribute
{
}
```

**Usage:**
```csharp
public partial class PlayerGrain : Grain, IPlayerGrain
{
    // This will get NewOrleans Event codegen
    public event EventHandler<string>? ChatMessage;
    
    // This will NOT get codegen - stays local only
    [NotEvent]
    public event EventHandler? LocalDiagnosticEvent;
}
```

---

## Appendix B: Supported Event Handler Types

V1 supports these event handler signatures:

| Handler Type | Supported | Notes |
|--------------|-----------|-------|
| `EventHandler<T>` | ✅ | Primary supported type |
| `EventHandler` | ✅ | No payload (uses `EventArgs.Empty`) |
| `Action<T>` | ❌ | Non-standard for events |
| `Func<T, Task>` | ❌ | Use `SubscribeTo{EventName}Async(handler)` instead |
| Custom delegates | ❌ | V1 scope limitation |

---

## Appendix C: Stream Provider Configuration

NewOrleans Events v1 requires SMS (Simple Message Streams) to be configured:

```csharp
// In Silo configuration
siloBuilder.AddMemoryStreams("SMS");

// In Client configuration  
clientBuilder.AddMemoryStreams("SMS");
```

The stream provider name "SMS" is hardcoded in v1. Future versions may make this configurable.

---

## Appendix D: Diagnostic Messages

| Code | Severity | Message |
|------|----------|---------|
| NOE001 | Error | `Event '{name}' handler type '{type}' is not supported. Use EventHandler<T> or EventHandler.` |
| NOE002 | Warning | `[State(NotifyOnChange = true)] on property '{name}' but no matching '{name}Changed' event found.` |
| NOE003 | Warning | `Event '{name}' on non-partial class. Grain class should be partial for codegen.` |
| NOE004 | Warning | `Event '{name}' on grain without IGrainWithXXXKey interface. Cannot determine grain key type.` |
| NOE005 | Info | `Event '{name}' excluded from codegen due to [NotEvent] attribute.` |
| NOE010 | Warning | `Event handler attached to '{name}' but no subscription created. Call 'SubscribeTo{name}Async()' to receive events.` |

---

## Appendix E: Complete Before/After Comparison

### Before (Manual Orleans Streams)

```csharp
// Interface - must know about streams
public interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
    // No event declaration possible
}

// Grain - lots of boilerplate
public class PlayerGrain : Grain, IPlayerGrain
{
    private IAsyncStream<string>? _chatStream;
    
    public override Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider("SMS");
        var streamId = StreamId.Create("PlayerGrain.Chat", this.GetPrimaryKeyString());
        _chatStream = provider.GetStream<string>(streamId);
        return base.OnActivateAsync(ct);
    }
    
    public Task SendChatAsync(string message)
    {
        return _chatStream!.OnNextAsync(message);
    }
}

// Client - must know stream conventions
var provider = client.GetStreamProvider("SMS");
var streamId = StreamId.Create("PlayerGrain.Chat", "player-1");  // Must match!
var stream = provider.GetStream<string>(streamId);

await stream.SubscribeAsync((msg, token) => 
{
    Console.WriteLine(msg);
    return Task.CompletedTask;
});
```

### After (NewOrleans Events)

```csharp
// Interface - just declares methods
public partial interface IPlayerGrain : IGrainWithStringKey
{
    Task SendChatAsync(string message);
}

// Grain - standard C# event
public partial class PlayerGrain : Grain, IPlayerGrain
{
    public event EventHandler<string>? ChatMessage;
    
    public Task SendChatAsync(string message)
    {
        ChatMessage?.Invoke(this, message);
        return Task.CompletedTask;
    }
}

// Client - natural C# patterns
var player = client.GetGrain<IPlayerGrain>("player-1");

await using var sub = await player.SubscribeToChatMessageAsync();
player.ChatMessage += (_, msg) => Console.WriteLine(msg);

await player.SendChatAsync("Hello!");  // Prints "Hello!"
```

---

*End of Document*
