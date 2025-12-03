# Systems and Subsystems Map

## Overview

Orleans is composed of multiple interconnected systems that work together to provide the virtual actor runtime. This document maps all major systems, their responsibilities, and how they interact.

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Application Code                          │
│                     (Grain Implementations)                      │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────┴────────────────────────────────────┐
│                      Grain Interface Layer                       │
│            (IGrain interfaces, GrainReference proxies)           │
└────────────────────────────┬────────────────────────────────────┘
                             │
        ┌────────────────────┴────────────────────┐
        │                                         │
┌───────┴────────┐                      ┌────────┴────────┐
│  Client APIs   │                      │  Runtime Core   │
│ - GrainFactory │                      │   - Catalog     │
│ - Cluster      │                      │   - Scheduler   │
│   Client       │                      │   - Activation  │
└───────┬────────┘                      └────────┬────────┘
        │                                        │
        └────────────┬───────────────────────────┘
                     │
     ┌───────────────┴───────────────┐
     │      Messaging System         │
     │   - MessageCenter             │
     │   - Message Routing           │
     └───────────────┬───────────────┘
                     │
     ┌───────────────┴───────────────────────────┐
     │                                           │
┌────┴─────────┐  ┌──────────────┐  ┌──────────┴────────┐
│  Networking  │  │    Grain     │  │    Clustering     │
│  - Silo      │  │   Directory  │  │    - Membership   │
│    Connections│  │   - DHT      │  │    - Health       │
│  - Gateway   │  │   - Locator  │  │      Monitor      │
└────┬─────────┘  └──────┬───────┘  └──────────┬────────┘
     │                   │                     │
     └───────────────────┴─────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
    ┌────┴─────────┐            ┌───────┴──────┐
    │ Serialization│            │   Storage    │
    │  - Codecs    │            │  - IStorage  │
    │  - Copiers   │            │  - Providers │
    └──────────────┘            └──────────────┘
```

## Core Systems

### 1. Runtime System

**Location**: `src/Orleans.Runtime/`

**Purpose**: The heart of Orleans - manages the lifecycle and execution of grain activations on a silo.

#### Key Components

##### Catalog (`Catalog/Catalog.cs`)
- **Central registry** of all grain activations on this silo
- Maps `GrainId` → `ActivationData`
- Thread-safe with 32-way lock striping for concurrency
- Manages activation creation and destruction

**Key Operations**:
```csharp
TryGetGrainContext(GrainId) -> IGrainContext
RegisterGrainActivation(ActivationData) -> void
UnregisterGrainActivation(ActivationData) -> void
```

##### Activation System (`Activation/`, `Catalog/ActivationData.cs`)

**ActivationData**:
- Represents a single grain instance in memory
- Contains:
  - Grain instance object
  - Activation ID
  - Message queue
  - Execution state
  - Lifecycle state
- Implements `IGrainContext` interface

**IGrainActivator** (`IGrainActivator.cs`):
- Factory for creating grain instances
- Default: DI-based activator
- Supports custom activation logic

**ActivationCollector** (`ActivationCollector.cs`):
- Garbage collection for idle activations
- Configurable timeout (default: 2 hours)
- Marks activations for deactivation
- Respects grain lifecycle hooks

#### Subsystems

**Grain Lifecycle** (`GrainLifecycle.cs`):
- Observable lifecycle with stages
- Stages: `SetupState` → `Activate` → `Active`
- Components register for lifecycle events
- Example: State loading happens in `SetupState` stage

**Deactivation Management**:
- `DeactivationReason`: Structured reason codes
- Grace period for in-flight requests
- Cascading deactivation (optional)
- Monitoring and metrics

---

### 2. Messaging System

**Location**: `src/Orleans.Runtime/Messaging/`, `src/Orleans.Core/Networking/`

**Purpose**: Routes messages between grains, silos, and clients.

#### Key Components

##### MessageCenter (`MessageCenter.cs`)
- **Central message router** for a silo
- Dispatches incoming messages to:
  - Local grain activations
  - Remote silos
  - System targets
- Manages message timeouts
- Tracks in-flight requests

##### Message (`Message.cs`)
Core message type containing:
- **Header**: Target, sender, message type, correlation ID
- **Body**: Serialized `IInvokable` (method invocation)
- **Metadata**: Timestamps, expiration, trace context

**Message Types**:
- `Request`: Grain method invocation
- `Response`: Result from invocation
- `OneWay`: Fire-and-forget message

##### Request/Response Handling

**Request Flow**:
1. Client creates `IInvokable` (generated by codegen)
2. `GrainReference.InvokeAsync()` sends message
3. `MessageCenter` routes to target silo
4. Target silo dispatches to grain activation
5. `Invokable.Invoke()` calls grain method
6. Response message sent back with result

**Response Correlation**:
- Each request has unique correlation ID
- Response matches ID to complete `TaskCompletionSource`
- Timeout handling cancels pending requests

#### Subsystems

**Message Routing**:
- Local dispatch: Direct activation lookup
- Remote dispatch: Via networking system
- System targets: Special internal grains

**Timeout Management**:
- Per-message timeout configuration
- Default: 30 seconds
- Timeout exception propagates to caller

---

### 3. Networking System

**Location**: `src/Orleans.Core/Networking/`, `src/Orleans.Runtime/Networking/`

**Purpose**: Manages network connections between silos and clients.

#### Key Components

##### Connection Types

**SiloConnection** (`SiloConnection.cs`):
- Silo-to-silo persistent connections
- One connection per remote silo
- Bidirectional
- Automatic reconnection on failure

**GatewayConnection** (`GatewayConnection.cs`):
- Client-to-silo gateway connections
- Load-balanced across available gateways
- Handles client requests

**ConnectionListener** (`ConnectionListener.cs`):
- Accepts incoming connections
- Configurable endpoints
- TLS support

##### Protocol

**Wire Protocol**:
- Frame-based protocol
- Length-prefixed messages
- Efficient binary serialization
- Supports pipelining

**Connection Management**:
- Connection pooling
- Health checks (ping/pong)
- Graceful shutdown
- Circuit breaker pattern

#### Subsystems

**Network Protocol** (`Shared/`):
- Framing and deframing
- Buffer management
- Flow control

---

### 4. Clustering and Membership System

**Location**: `src/Orleans.Runtime/MembershipService/`

**Purpose**: Maintains cluster topology and detects failures.

#### Key Components

##### ClusterMembershipService (`ClusterMembershipService.cs`)
- **Authoritative source** of cluster membership
- Publishes `ClusterMembershipSnapshot` updates
- Coordinates with membership table
- Manages local silo lifecycle

##### MembershipTableManager (`MembershipTableManager.cs`)
- Manages the distributed membership table
- Interfaces with storage provider (Azure Table, SQL, etc.)
- Handles silo join/leave/death
- Enforces generation numbers for uniqueness

##### SiloHealthMonitor (`SiloHealthMonitor.cs`)
- Monitors health of other silos
- Probes silos periodically
- Detects failures and notifies cluster
- Configurable probe timeout

##### MembershipGossiper (`MembershipGossiper.cs`)
- Gossips membership changes across cluster
- Ensures eventual consistency
- Reduces load on membership table
- Configurable gossip interval

#### Membership States

```
Joining → Active → ShuttingDown → Stopping → Dead
```

- **Joining**: Silo is starting up
- **Active**: Silo is healthy and serving traffic
- **ShuttingDown**: Graceful shutdown in progress
- **Stopping**: Forceful shutdown
- **Dead**: Silo has failed or stopped

#### Subsystems

**Cluster Version Management**:
- Tracks cluster-wide protocol version
- Enables rolling upgrades
- Version negotiation

**Split-Brain Prevention**:
- Generation numbers prevent ghost silos
- Membership table acts as coordination point

---

### 5. Grain Directory System

**Location**: `src/Orleans.Runtime/GrainDirectory/`

**Purpose**: Distributed hash table (DHT) that tracks which silo hosts each grain activation.

#### Key Components

##### LocalGrainDirectory (`GrainDirectory.cs`)
- Partition of directory owned by this silo
- Stores `GrainId` → `GrainAddress` mappings
- Partitioned via consistent hashing
- In-memory with optional persistence

##### GrainDirectoryPartition (`GrainDirectoryPartition.cs`)
- Segment of directory managed by one silo
- Responsible for subset of grain ID space
- Handles registration and lookup
- Manages directory hand-offs during membership changes

##### GrainLocator (`GrainLocator/`)
- Finds or creates grain activations
- Orchestrates directory lookup + activation
- Caching layer for performance

**Locator Types**:
- `DhtGrainLocator`: DHT-based (default)
- `CachedGrainLocator`: Adds caching
- `ClientGrainLocator`: Client-side locator

##### Directory Caching
- **Adaptive caching**: Learns which grains are frequently accessed
- **Invalidation**: On grain migration or failure
- **TTL-based expiration**: Configurable cache lifetime

#### Directory Operations

**Register**:
1. Hash grain ID to determine owning silo
2. Send registration to directory owner
3. Store `GrainId → ActivationAddress` mapping
4. Return activation address

**Lookup**:
1. Check local cache
2. If miss, query directory owner
3. Cache result
4. Return activation address

**Unregister**:
- Remove entry on deactivation
- Can be lazy (TTL-based)

#### Subsystems

**Consistent Hashing** (`ConsistentRing/`):
- Maps grain IDs to responsible silos
- Minimizes remapping on membership changes
- Configurable virtual buckets

**Directory Handoff**:
- Transfer directory partition on silo failure
- Successor silo takes ownership
- No data loss (stored in membership table)

---

### 6. Placement System

**Location**: `src/Orleans.Runtime/Placement/`

**Purpose**: Decides where to activate new grain instances.

#### Key Components

##### PlacementStrategy (Abstract)
Base class for placement strategies:
- `RandomPlacement`: Random silo selection
- `PreferLocalPlacement`: Prefer local silo
- `HashBasedPlacement`: Consistent hash
- `ActivationCountPlacement`: Load balancing
- `ResourceOptimizedPlacement`: Resource-aware
- `StatelessWorkerPlacement`: Multi-activation support

##### IPlacementDirector
Interface for placement decision logic:
```csharp
Task<SiloAddress> OnAddActivation(
    PlacementStrategy strategy,
    PlacementTarget target,
    IPlacementContext context)
```

##### PlacementService (`PlacementService.cs`)
- Coordinates placement decisions
- Queries available silos
- Invokes appropriate director
- Returns selected silo address

#### Placement Process

1. **Trigger**: Grain call to non-existent activation
2. **Director selection**: Based on grain's placement attribute
3. **Candidate filtering**: Remove incompatible silos
4. **Selection**: Director chooses from candidates
5. **Activation**: Create grain on selected silo
6. **Registration**: Update directory

#### Subsystems

**SiloStatistics**:
- Tracks resource usage per silo
- CPU, memory, activation count
- Used by placement directors

**PlacementTarget**:
- Describes grain being placed
- Includes type, ID, and requirements

---

### 7. Scheduler System

**Location**: `src/Orleans.Runtime/Scheduler/`

**Purpose**: Schedules and executes grain turns efficiently.

#### Key Components

##### ActivationTaskScheduler (`ActivationTaskScheduler.cs`)
- Custom `TaskScheduler` per activation
- Ensures single-threaded execution
- Integrates with Orleans scheduler

##### WorkItemGroup (`WorkItemGroup.cs`)
- Queue of work items for an activation
- FIFO execution order
- Manages turn execution state
- Reentrancy support

##### OrleansTaskScheduler
- Global scheduler for the silo
- Thread pool management
- Prioritization support
- Load balancing across activations

#### Work Item Types

**RequestWorkItem**:
- Grain method invocation
- Highest priority

**SystemWorkItem**:
- Internal system tasks
- Timers, reminders
- Background tasks

#### Execution Model

1. **Enqueue**: Work item added to activation's queue
2. **Schedule**: Activation scheduled on thread pool
3. **Execute Turn**: Process one work item
4. **Await**: If work item awaits, pause and reschedule
5. **Resume**: Continue after await completes
6. **Complete**: Work item finishes, process next

#### Subsystems

**Turn Execution**:
- Captured ExecutionContext
- Request context propagation
- Exception handling

**Reentrancy Management**:
- Interleaving for `[Reentrant]` grains
- `[AlwaysInterleave]` method attribute
- Conditional interleaving predicates

---

### 8. Serialization System

**Location**: `src/Orleans.Serialization/`, `src/Orleans.Serialization.Abstractions/`

**Purpose**: High-performance, version-tolerant serialization.

#### Key Components

##### Serializer (`Serializer.cs`)
Main entry point:
- `Serialize<T>(T value, Session)`
- `Deserialize<T>(Reader<byte>, Session)`
- `DeepCopy<T>(T value)`

##### FieldCodec<T> (`IFieldCodec.cs`)
Serializes individual types:
- `WriteField(Writer, T value)`
- `ReadValue(Reader)`
- Generated or hand-written

##### Session (`Session.cs`)
- Scoped serialization context
- Reference tracking (prevent cycles)
- Object pooling
- Serializer cache

##### Codec Types

**Value Codecs**:
- Primitive types (int, string, etc.)
- Value types (struct, record)
- Immutable types

**Reference Codecs**:
- Classes with identity
- Reference tracking via session
- Handles circular references

**Collection Codecs**:
- Arrays, lists, dictionaries
- Optimized for common types

#### Wire Format

```
[Field Header] [Field Value]

Field Header = WireType (3 bits) + FieldId (varint)
WireType = {LengthPrefixed, VarInt, Fixed32, Fixed64, ...}
```

- **Schema evolution**: Field IDs enable adding/removing fields
- **Compact encoding**: Variable-length integers
- **Type manifests**: Type information embedded as needed

#### Subsystems

**Copiers** (`IDeepCopier.cs`):
- Deep copy for immutability guarantees
- Shared logic with serialization

**Activators** (`IActivator.cs`):
- Object construction
- Supports constructors and factories

**Codec Provider**:
- Discovers codecs at runtime
- Generated codecs registered automatically

---

### 9. Code Generation System

**Location**: `src/Orleans.CodeGenerator/`

**Purpose**: Generates serialization and RPC infrastructure at compile-time.

(Detailed documentation in [Code Generation System](04-codegen-system.md))

#### Generated Code

1. **Serializers**: `FieldCodec<T>` implementations
2. **Copiers**: `IDeepCopier<T>` implementations
3. **Proxies**: Client-side grain interface implementations
4. **Invokables**: Server-side method dispatchers
5. **Activators**: Object factories
6. **Metadata**: Type manifests

---

### 10. Persistence System

**Location**: `src/Orleans.Runtime/Facet/`, `src/Orleans.Runtime/Storage/`

**Purpose**: Abstracts grain state persistence.

#### Key Components

##### IStorage<TState> (`IStorage.cs`)
Interface for grain state:
```csharp
Task ReadStateAsync()
Task WriteStateAsync()
Task ClearStateAsync()
TState State { get; }
string Etag { get; }
```

##### Grain<TState>
Base class for stateful grains:
- Automatic state injection
- `ReadStateAsync()`, `WriteStateAsync()`, `ClearStateAsync()` helpers
- Lifecycle integration

##### Storage Providers
Pluggable backends:
- **Memory**: In-memory (development)
- **ADO.NET**: SQL Server, PostgreSQL, MySQL
- **Azure**: Blob Storage, Table Storage, Cosmos DB
- **AWS**: DynamoDB, S3
- **Custom**: Implement `IGrainStorage`

#### State Lifecycle

1. **Activation**: `ReadStateAsync()` called automatically
2. **Modification**: Application updates `State` object
3. **Persistence**: Explicit `WriteStateAsync()` call
4. **Deactivation**: Optional auto-save

#### Subsystems

**Etag Optimistic Concurrency**:
- Provider returns etag on read
- Write includes expected etag
- Conflict detection on mismatch

**State Serialization**:
- JSON (default)
- Custom serializers supported

---

## System Interactions

### Grain Call Flow

```
Client
  → GrainReference.Method()
  → InvokeAsync(Invokable)
  → Create Message
  → MessageCenter.SendMessage()
  → Networking.Send()
  ↓
Remote Silo
  → Networking.Receive()
  → MessageCenter.ReceiveMessage()
  → Catalog.GetGrainContext() [or create]
  → Scheduler.QueueWorkItem()
  → WorkItemGroup.Execute()
  → Invokable.Invoke(grain)
  → Grain.Method()
  → Create Response Message
  → Send back to client
```

### Activation Creation Flow

```
Grain Call to non-existent activation
  → GrainDirectory.Lookup()
  → Not found
  → PlacementService.GetPlacementDecision()
  → PlacementDirector.OnAddActivation()
  → Selected silo address
  → Send activation request to silo
  → Catalog.RegisterGrainActivation()
  → IGrainActivator.CreateInstance()
  → GrainLifecycle.OnStart()
  → ReadStateAsync() [if stateful]
  → Activation ready
  → GrainDirectory.Register()
  → Process queued message
```

### Failure Detection Flow

```
SiloHealthMonitor
  → Probe remote silo (periodic)
  → Timeout or failure
  → SuspectingSilo()
  → Verify with other silos
  → Consensus reached
  → DeclareDeadSilo()
  → MembershipTableManager.MarkDead()
  → ClusterMembershipService.Update()
  → Publish snapshot
  → All silos notified
  → Local cleanup:
      - Close connections
      - Invalidate directory cache
      - Cleanup waiting messages
      - Hand-off directory partition
```

## System Dependencies

```
Application Layer
    ↓
Abstractions Layer (Orleans.Core.Abstractions)
    ↓
┌───────────┬─────────────┬────────────┐
│ Client    │ Serialization│  CodeGen  │
│ (Core)    │              │  (compile) │
└────┬──────┴──────┬───────┴─────┬──────┘
     │             │             │
     └─────────────┴─────────────┘
                   ↓
            Runtime Layer
     ┌──────────┬─────────┬─────────┐
     │ Catalog  │Messaging│Clustering│
     │          │Networking│         │
     └────┬─────┴────┬────┴────┬────┘
          │          │         │
          └──────────┴─────────┘
                    ↓
           Provider Layer
     ┌──────────┬─────────┬─────────┐
     │ Storage  │Streaming│ Reminders│
     └──────────┴─────────┴─────────┘
```

## Summary

Orleans' systems work together to provide:

1. **Runtime**: Manages grain lifecycle and execution
2. **Messaging**: Routes calls between grains
3. **Networking**: Manages physical connections
4. **Clustering**: Maintains topology and health
5. **Directory**: Locates grain activations
6. **Placement**: Decides where to create grains
7. **Scheduler**: Executes grain turns efficiently
8. **Serialization**: Marshals data across boundaries
9. **CodeGen**: Generates infrastructure code
10. **Persistence**: Stores grain state

Each system is designed for:
- **Modularity**: Clear boundaries and interfaces
- **Performance**: Optimized hot paths
- **Reliability**: Fault tolerance built-in
- **Extensibility**: Pluggable implementations

---

**Next**: [Layer Architecture](03-layer-architecture.md)
