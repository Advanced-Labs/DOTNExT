# Orleans Paradigms and Core Concepts

## Overview

Orleans is built on a specific set of design paradigms and principles that shape its architecture and implementation. Understanding these fundamental concepts is essential for working with Orleans internals.

## The Virtual Actor Model

### What is the Virtual Actor Model?

Orleans implements the **Virtual Actor Model**, an evolution of the traditional actor model specifically designed for distributed systems. The key innovation is the concept of "virtual" actors that have several unique properties.

### Core Characteristics

#### 1. Stable Identity

Every grain (Orleans' term for a virtual actor) has a **permanent, globally unique identity** independent of its physical location or current state:

- **GrainId**: Composed of a `GrainType` and a key (string, GUID, integer, or compound)
- Identity persists across:
  - Activations and deactivations
  - Server failures and restarts
  - Cluster reconfigurations
- Format: `{GrainType}/{Key}` (e.g., `grain.user/alice@example.com`)

#### 2. Automatic Lifecycle Management

Unlike traditional actors that must be explicitly created and destroyed, Orleans grains are **"virtual"** in that they:

- **Activate on demand**: The runtime creates an instance (activation) automatically on first access
- **Deactivate automatically**: Idle grains are removed from memory after a configurable timeout
- **Transparent to callers**: Clients don't need to know if a grain is currently activated
- **Always addressable**: You can call any grain by its identity, regardless of current state

#### 3. Location Transparency

Callers interact with grains without knowing or caring about their physical location:

- **Uniform access model**: Same code whether grain is local or remote
- **Automatic routing**: The runtime routes messages to the correct silo
- **Transparent migration**: Grains can move between silos without caller awareness
- **No distributed object references**: Grain references are serializable identifiers

#### 4. Single-Threaded Execution (Turn-Based Concurrency)

Each grain activation processes messages one at a time in a **turn-based** manner:

- **One turn at a time**: A grain never processes multiple messages concurrently (by default)
- **No locks needed**: Grain code doesn't need locks or synchronization primitives
- **Reentrancy optional**: Grains can opt into interleaved execution with `[Reentrant]`
- **Async by default**: Turns can `await`, allowing the scheduler to process other grains

### Key Benefits

1. **Simplified concurrency**: No locks, no race conditions, no deadlocks in grain code
2. **Automatic scaling**: Grains are distributed across available servers
3. **Elastic deployment**: Add/remove servers dynamically without code changes
4. **Fault tolerance**: Failed servers don't lose grain identities, just activations
5. **Natural partitioning**: Each grain ID naturally partitions state and computation

## Design Principles

### 1. Distributed by Default

Orleans assumes a distributed environment from the ground up:

- **No special "remote" APIs**: All grain calls are potentially remote
- **Network-aware**: Built-in timeout handling, retry logic, and failure detection
- **Serialization required**: All messages and state must be serializable
- **Cluster-first design**: Even single-server deployments use cluster protocols

### 2. Developer Productivity Over Control

Orleans prioritizes ease of use by handling complexity automatically:

- **Automatic code generation**: Proxies, serializers, and invokers generated at compile-time
- **Convention over configuration**: Sensible defaults for most scenarios
- **Implicit behavior**: Activation, placement, and routing happen automatically
- **Hide distribution**: Developers write sequential, single-threaded-looking code

### 3. Scale Up and Out

Orleans supports both vertical and horizontal scaling:

- **Vertical**: Efficient multithreading within a silo
- **Horizontal**: Elastic cluster with dynamic silo addition/removal
- **Adaptive placement**: Strategies for load balancing and affinity
- **Multi-activation support**: Stateless grains can have many concurrent instances

### 4. Fault Tolerance as a First-Class Concern

Built-in resilience mechanisms:

- **Cluster membership**: Automatic failure detection and recovery
- **Activation recovery**: Failed grains reactivate on healthy silos
- **Persistent state**: State outlives activation lifecycle
- **Persistent reminders**: Durable timers survive grain deactivation
- **No single point of failure**: Distributed directory and membership

### 5. Performance Through Code Generation

Orleans achieves high performance by generating optimal code at compile-time:

- **Zero reflection**: All serialization and invocation use generated code
- **Type-safe**: Compile-time validation of grain interfaces
- **Inlining-friendly**: Generated code is simple and optimizable
- **Minimal allocations**: Pooling and efficient buffer management

## Core Concepts

### Grains

**Grains** are the fundamental units of computation and state in Orleans.

#### Grain Identity

```
GrainId = GrainType + Key
Example: grain.userprofile/user-12345
```

- **GrainType**: Identifies the grain class (e.g., `grain.userprofile`)
- **Key**: Unique identifier within that type (string, GUID, int, or compound)

#### Grain Interfaces

Grains expose their functionality through interfaces:

```csharp
public interface IUserGrain : IGrainWithStringKey
{
    Task<string> GetName();
    Task SetName(string name);
}
```

- Must inherit from `IGrain` or specialized variants
- Methods must return `Task` or `Task<T>`
- Parameters must be serializable

#### Grain Implementations

```csharp
public class UserGrain : Grain, IUserGrain
{
    private string _name;

    public Task<string> GetName() => Task.FromResult(_name);

    public Task SetName(string name)
    {
        _name = name;
        return Task.CompletedTask;
    }
}
```

### Activations

An **activation** is a specific instance of a grain in memory on a particular silo.

Key points:
- One activation per grain identity per cluster (by default)
- Activations are created on-demand
- Activations can be deactivated when idle
- Activations are lightweight (just an object + metadata)

### Silos

A **silo** is an Orleans server process that hosts grain activations.

Responsibilities:
- Host and manage grain activations
- Route messages to grains
- Participate in cluster membership
- Maintain portion of grain directory
- Execute scheduled work (timers, reminders)

### Cluster

A **cluster** is a collection of silos working together.

Characteristics:
- Dynamic membership (silos join/leave at runtime)
- Shared grain namespace (any grain ID accessible from any silo)
- Distributed directory (grain location tracking)
- Fault-tolerant (continues operating despite failures)

### Turn-Based Concurrency Model

Orleans enforces a **turn-based** execution model for grain activations.

#### What is a Turn?

A **turn** is the execution of a single grain method invocation from entry to the first `await` or completion.

```csharp
public async Task ProcessOrder(Order order)
{
    // TURN 1: Synchronous execution
    ValidateOrder(order);
    _orders.Add(order);

    // Await: Turn 1 ends, scheduler can run other grains
    await _paymentGrain.ProcessPayment(order.Total);

    // TURN 2: Continues after payment completes
    SendConfirmationEmail(order);
    await WriteStateAsync();

    // TURN 3: Final turn
    return;
}
```

#### Turn Execution Rules

1. **Sequential by default**: Only one turn executes at a time per activation
2. **Await points**: Scheduler can switch to other grains during awaits
3. **No preemption**: A turn runs until it awaits or completes
4. **Message queue**: Incoming messages queue until current turn completes

#### Reentrancy

Grains can opt into **reentrancy** to allow interleaved execution:

```csharp
[Reentrant]
public class ReentrantGrain : Grain, IReentrantGrain
{
    // Can process multiple messages concurrently
}
```

Or per-method:

```csharp
public interface IMyGrain : IGrain
{
    [AlwaysInterleave]
    Task<int> GetCount(); // Can run anytime

    Task UpdateState(); // Sequential with other non-interleaved methods
}
```

### Request Context

Orleans provides a **RequestContext** for propagating information through call chains:

```csharp
RequestContext.Set("TraceId", traceId);
// Automatically flows to all downstream grain calls
```

- Similar to AsyncLocal in .NET
- Propagates across grain boundaries
- Serialized with messages
- Useful for tracing, authentication, etc.

### Grain References

A **GrainReference** is a typed, serializable handle to a grain:

```csharp
IUserGrain user = grainFactory.GetGrain<IUserGrain>("user-123");
```

- Can be stored, passed, and serialized
- Internally contains `GrainId`
- Proxy implementation generated by codegen
- Method calls converted to messages

## Architectural Implications

### No Shared State Between Grains

Each grain has its own isolated state:

- No shared memory between grains
- Communication only through messages
- Immutable messages (deep-copied by default)
- Natural isolation for testing and reasoning

### Asynchronous Everything

All grain interactions are asynchronous:

- Enables non-blocking I/O
- Efficient thread utilization
- Natural backpressure through queue depths
- Compatible with modern .NET async patterns

### State vs. Behavior Distribution

Orleans distributes both state and behavior:

- **State**: Each grain instance holds its own state
- **Behavior**: Code runs where the grain is activated
- Contrast with typical databases (centralized state, distributed queries)

### Elasticity and Resource Management

Orleans dynamically manages resources:

- **Activation**: Create grain instances on demand
- **Deactivation**: Remove idle grains to free memory
- **Placement**: Distribute grains across available silos
- **Migration**: Move grains to balance load (future feature)

## Comparison to Other Models

### vs. Traditional Actors (Akka, Erlang)

| Feature | Orleans | Traditional Actors |
|---------|---------|-------------------|
| Lifecycle | Virtual (automatic) | Explicit create/destroy |
| Identity | Permanent grain ID | Ephemeral actor reference |
| Location | Transparent | Must track actor location |
| State | Optional persistence | In-memory only (usually) |
| Concurrency | Turn-based | Message-based |
| Failure handling | Automatic reactivation | Supervision trees |

### vs. Microservices

| Feature | Orleans | Microservices |
|---------|---------|--------------|
| Unit of deployment | Silo (many grain types) | Service (one function) |
| Communication | Direct grain calls | HTTP/gRPC |
| State | Grain state | External database |
| Scaling | Per-grain-type | Per-service |
| Overhead | Low (in-process) | High (network) |

### vs. Serverless/FaaS

| Feature | Orleans | Serverless |
|---------|---------|-----------|
| State | First-class | External only |
| Cold start | Activation (~ms) | Container start (~seconds) |
| Identity | Stable grain ID | Stateless function |
| Billing | Server-based | Per-invocation |
| Placement control | Strategies available | Provider-controlled |

## Summary

Orleans' virtual actor model provides:

1. **Simplified distributed programming**: No explicit networking code
2. **Automatic resource management**: Activation, placement, routing handled by runtime
3. **Natural concurrency**: Turn-based execution eliminates locks
4. **Elastic scaling**: Add/remove servers dynamically
5. **Fault tolerance**: Built-in failure detection and recovery
6. **Developer productivity**: Write sequential code for distributed systems

These paradigms inform every design decision in Orleans, from the layered architecture to the code generation system to the runtime implementation.

---

**Next**: [Systems and Subsystems Map](02-systems-and-subsystems.md)
