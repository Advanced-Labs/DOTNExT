# Messaging and Networking System

## Overview

Orleans' messaging and networking system handles all communication between grains, silos, and clients. It provides reliable, high-performance RPC over TCP connections.

**Locations**:
- `src/Orleans.Runtime/Messaging/`
- `src/Orleans.Core/Networking/`

## Key Concepts

### Message

The fundamental unit of communication in Orleans.

```csharp
public sealed class Message
{
    // Routing
    public GrainAddress TargetAddress { get; set; }
    public GrainAddress SendingAddress { get; set; }
    public SiloAddress TargetSilo { get; set; }

    // Payload
    public IInvokable BodyObject { get; set; }

    // Metadata
    public Guid CorrelationId { get; set; }
    public TimeSpan TimeToLive { get; set; }
    public DateTime Expiration { get; set; }
    public Direction Direction { get; set; }
    public Dictionary<string, object> RequestContext { get; set; }
}
```

### Message Types

**Direction**:
```csharp
public enum Direction
{
    Request,        // Method call
    Response,       // Result
    OneWay          // Fire-and-forget
}
```

## Core Components

### MessageCenter

**Purpose**: Central message router for the silo.

**Responsibilities**:
- Route outbound messages (local or remote)
- Dispatch inbound messages to grain activations
- Track in-flight requests
- Handle timeouts

**Message Flow**:
```
Outbound:
  GrainReference.InvokeAsync()
    → MessageCenter.SendMessage()
    → IsLocal? → Catalog.Dispatch()
    → IsRemote? → NetworkingSystem.Send()

Inbound:
  NetworkConnection.Receive()
    → MessageCenter.ReceiveMessage()
    → Catalog.Dispatch()
    → WorkItemGroup.Enqueue()
```

### Connection Management

**SiloConnection**:
- One persistent connection per remote silo
- Bidirectional (send and receive)
- Automatic reconnection on failure
- Connection pooling

**GatewayConnection**:
- Client-to-silo connections
- Load balanced across gateways
- Client multiplexes all requests over one connection

### Wire Protocol

**Frame Format**:
```
[Length: 4 bytes] [Header Length: 4 bytes] [Header] [Body]
```

**Header**: Contains routing and metadata (protobuf-encoded)
**Body**: Serialized IInvokable (Orleans.Serialization format)

**Benefits**:
- Length-prefixed for easy framing
- Header/body separation
- Streaming-friendly

## Request/Response Pattern

### Correlation

Each request gets a unique correlation ID:

```csharp
// Client sends request
var correlationId = Guid.NewGuid();
var request = new Message
{
    CorrelationId = correlationId,
    Direction = Direction.Request,
    BodyObject = invokable
};

// Track completion
var tcs = new TaskCompletionSource<Response>();
pendingRequests[correlationId] = tcs;

SendMessage(request);
return tcs.Task;
```

### Response Handling

```csharp
// Server sends response
var response = new Message
{
    CorrelationId = request.CorrelationId,
    Direction = Direction.Response,
    BodyObject = result
};

SendMessage(response);
```

```csharp
// Client receives response
void OnResponseReceived(Message response)
{
    if (pendingRequests.TryRemove(response.CorrelationId, out var tcs))
    {
        tcs.SetResult(response.BodyObject);
    }
}
```

### Timeout Handling

```csharp
// Set timeout timer
var timeout = Task.Delay(request.TimeToLive);
var completed = await Task.WhenAny(tcs.Task, timeout);

if (completed == timeout)
{
    // Timeout
    pendingRequests.TryRemove(correlationId, out _);
    throw new TimeoutException();
}
```

## Networking Layer

### Connection Lifecycle

```
Create Connection
  → TCP Connect
  → TLS Handshake (if configured)
  → Send Preamble (protocol version, silo address)
  → Receive Preamble
  → Start Send/Receive loops
  → [Active]
  → Disconnect or Failure
  → Reconnect (with exponential backoff)
```

### Send Pipeline

```
Message
  → Serialize (Orleans.Serialization)
  → Frame (add length prefix)
  → Write to Socket
  → TCP Send
```

### Receive Pipeline

```
TCP Receive
  → Read length prefix (4 bytes)
  → Read full frame
  → Deserialize
  → Route to MessageCenter
```

### Buffering

**Send**:
- Buffered writes for performance
- Configurable buffer size
- Flush on: buffer full, explicit flush, timer

**Receive**:
- Pipeline-based reading
- Zero-copy when possible
- Memory pooling

## Performance Optimizations

### Batching

Multiple small messages can be batched into one frame:
```
[Length] [Header1] [Body1] [Header2] [Body2] ...
```

### Compression

Optional compression for large payloads:
```csharp
siloBuilder.Configure<MessagingOptions>(options =>
{
    options.EnableMessageCompression = true;
    options.CompressionThreshold = 1024; // bytes
});
```

### Pooling

- Message objects pooled
- Buffers pooled (`ArrayPool<byte>`)
- Reduces GC pressure

## Configuration

```csharp
siloBuilder.Configure<MessagingOptions>(options =>
{
    // Timeouts
    options.ResponseTimeout = TimeSpan.FromSeconds(30);
    options.MaxForwardCount = 2;

    // Buffers
    options.BufferPoolBufferSize = 4096;

    // Retries
    options.ResendOnTimeout = false;
    options.MaxResendCount = 0;
});

siloBuilder.Configure<ConnectionOptions>(options =>
{
    // Limits
    options.ConnectionsPerEndpoint = 1;

    // Reconnection
    options.ConnectRetryDelay = TimeSpan.FromSeconds(1);
    options.MaxConnectRetryDelay = TimeSpan.FromSeconds(30);
});
```

## Summary

The Messaging and Networking System provides:

1. **Reliable** RPC between grains and silos
2. **High-performance** TCP connections
3. **Request/response** correlation
4. **Automatic** connection management
5. **Optimized** serialization and buffering

---

**Next**: [Persistence and State Management](08-persistence-state.md)
