# Layer Architecture

## Overview

Orleans follows a carefully designed layered architecture that separates concerns, enables independent evolution, and provides clear boundaries between components. This document describes each layer, their responsibilities, and how they relate to each other.

## Architectural Layers

```
┌─────────────────────────────────────────────────────────┐
│                   Layer 7: Providers                     │
│  (Clustering, Storage, Streaming, Reminders Providers)  │
└────────────────────────┬────────────────────────────────┘
                         │ Depends on
┌────────────────────────┴────────────────────────────────┐
│              Layer 6: Host Integration                   │
│     (Orleans.Client, Orleans.Server, Orleans.Sdk)       │
└────────────────────────┬────────────────────────────────┘
                         │ Depends on
┌────────────────────────┴────────────────────────────────┐
│          Layer 5: Code Generation (Compile-Time)        │
│              (Orleans.CodeGenerator)                     │
└─────────────────────────────────────────────────────────┘
                         ↓ Generates code for
┌────────────────────────┬────────────────────────────────┐
│               Layer 4: Runtime                          │
│              (Orleans.Runtime)                          │
└────────────────────────┬────────────────────────────────┘
                         │ Depends on
┌────────────────────────┴────────────────────────────────┐
│          Layer 3: Core Implementation                    │
│               (Orleans.Core)                            │
└────────────────────────┬────────────────────────────────┘
                         │ Depends on
┌────────────────────────┴────────────────────────────────┐
│            Layer 2: Serialization                       │
│  (Orleans.Serialization, Orleans.Serialization.Abs)    │
└────────────────────────┬────────────────────────────────┘
                         │ Depends on
┌────────────────────────┴────────────────────────────────┐
│          Layer 1: Core Abstractions                     │
│          (Orleans.Core.Abstractions)                    │
└─────────────────────────────────────────────────────────┘
```

## Layer 1: Core Abstractions

**Package**: `Orleans.Core.Abstractions`

**Purpose**: Platform-agnostic interfaces and base types that define the grain programming model.

### Responsibilities

- Define grain interface marker types (`IGrain`, `IGrainWithStringKey`, etc.)
- Define runtime-facing interfaces (`IGrainContext`, `IGrainBase`)
- Provide identity types (`GrainId`, `GrainType`, `ActivationId`, `SiloAddress`)
- Define core attributes (`[GenerateSerializer]`, `[Id]`, placement attributes)
- Declare system interfaces (lifecycle, placement, timers)

### Key Types

**Grain Interfaces**:
```csharp
public interface IGrain { }
public interface IGrainWithStringKey : IGrain { }
public interface IGrainWithGuidKey : IGrain { }
public interface IGrainWithIntegerKey : IGrain { }
public interface IGrainWithGuidCompoundKey : IGrain { }
```

**Grain Implementation Base**:
```csharp
public interface IGrainBase
{
    IGrainContext GrainContext { get; }
}
```

**Runtime Context**:
```csharp
public interface IGrainContext : ITargetHolder
{
    GrainId GrainId { get; }
    ActivationId ActivationId { get; }
    GrainReference GrainReference { get; }
    object GrainInstance { get; }
    IServiceProvider ActivationServices { get; }
    IGrainLifecycle ObservableLifecycle { get; }
    // ... lifecycle methods
}
```

**Identity Types**:
```csharp
public readonly struct GrainId
{
    public GrainType Type { get; }
    public IdSpan Key { get; }
}

public readonly struct GrainType
{
    // e.g., "grain.userprofile"
}

public readonly struct ActivationId
{
    // Unique GUID for this activation instance
}

public readonly struct SiloAddress
{
    public IPEndPoint Endpoint { get; }
    public int Generation { get; }
}
```

### No Dependencies

This layer has **zero dependencies** on other Orleans packages, making it:
- Easily referenced by grain interfaces
- Stable API surface
- Portable across platforms

### Usage

**Application Code**:
```csharp
// Reference only Orleans.Core.Abstractions
public interface IUserGrain : IGrainWithStringKey
{
    Task<string> GetName();
    Task SetName(string name);
}
```

---

## Layer 2: Serialization

**Packages**:
- `Orleans.Serialization.Abstractions` (interfaces)
- `Orleans.Serialization` (implementation)

**Purpose**: Type-agnostic, high-performance serialization framework.

### Responsibilities

- Provide serialization infrastructure independent of Orleans
- Define codec interfaces (`IFieldCodec<T>`, `IBaseCodec<T>`)
- Implement built-in codecs for common types
- Support version tolerance and schema evolution
- Deep copying for immutability guarantees
- Object activation/construction

### Key Types

**Core APIs**:
```csharp
public sealed class Serializer
{
    void Serialize<T>(T value, Writer<byte> writer, Session session);
    T Deserialize<T>(Reader<byte> reader, Session session);
    T DeepCopy<T>(T value);
}
```

**Codec Interface**:
```csharp
public interface IFieldCodec<T>
{
    void WriteField(Writer<byte> writer, T value);
    T ReadValue(Reader<byte> reader, Field header);
}
```

**Session**:
```csharp
public sealed class Session
{
    // Reference tracking
    // Object pooling
    // Type caching
}
```

### Wire Format

```
[Field Header] [Field Value]

Field Header = [WireType (3 bits) | FieldId (varint)]
```

**Example**:
```
Serialized Person { Name = "Alice", Age = 30 }
→ [Header: WireType=LengthPrefixed, FieldId=0] [Length=5] "Alice"
  [Header: WireType=VarInt, FieldId=1] [30]
```

### Design Principles

1. **Standalone**: Can be used outside Orleans
2. **Performance**: Zero-allocation hot paths
3. **Version Tolerance**: Field IDs enable schema evolution
4. **Extensibility**: Custom codecs for user types

### Dependencies

- `Orleans.Core.Abstractions` (for `[Id]` attribute only)
- System libraries only

---

## Layer 3: Core Implementation

**Package**: `Orleans.Core`

**Purpose**: Client-side Orleans implementation and shared runtime components.

### Responsibilities

- Client-side grain references and proxies
- Request context management
- Configuration system
- Diagnostics and metrics
- Networking primitives (shared with runtime)
- Message types
- Client connection management

### Key Components

**GrainReference** (`GrainReference.cs`):
```csharp
public abstract class GrainReference
{
    protected ValueTask<T> InvokeAsync<T>(IInvokable request);
    protected Task InvokeOneWay(IInvokable request);
}
```
- Base class for all generated grain proxies
- Handles message creation and sending
- Serializes invocations

**GrainFactory** (`GrainFactory.cs`):
```csharp
public class GrainFactory : IGrainFactory
{
    public TGrainInterface GetGrain<TGrainInterface>(Guid key);
    public TGrainInterface GetGrain<TGrainInterface>(string key);
    public TGrainInterface GetGrain<TGrainInterface>(long key);
}
```
- Creates grain reference instances
- Maps grain IDs to strongly-typed references

**IClusterClient** (`IClusterClient.cs`):
```csharp
public interface IClusterClient
{
    IGrainFactory Grains { get; }
    IStreamProvider GetStreamProvider(string name);
    Task Close();
}
```
- Client entry point
- Manages connection to cluster

**RequestContext** (`RequestContext.cs`):
```csharp
public static class RequestContext
{
    public static void Set(string key, object value);
    public static object Get(string key);
    public static void Clear();
}
```
- Per-request contextual data
- Automatically propagates across grain calls

### Networking Primitives

**Message Types**:
- `Message`: Core message structure
- `IInvokable`: Method invocation payload
- Request/response correlation

**Connection**:
- `Connection` abstract base
- Frame-based protocol
- TLS support

### Configuration System

```csharp
public class ClientConfiguration
{
    public TimeSpan ResponseTimeout { get; set; }
    public List<Uri> Gateways { get; set; }
    // ...
}
```

### Dependencies

- `Orleans.Core.Abstractions`
- `Orleans.Serialization`
- System libraries

### Usage

**Client Application**:
```csharp
var client = new ClientBuilder()
    .UseLocalhostClustering()
    .Build();

await client.Connect();

var grain = client.GetGrain<IUserGrain>("alice");
await grain.SetName("Alice Smith");
```

---

## Layer 4: Runtime

**Package**: `Orleans.Runtime`

**Purpose**: Server-side silo implementation - the heart of Orleans.

### Responsibilities

- Host and manage grain activations (Catalog)
- Schedule grain execution (Scheduler)
- Route messages (MessageCenter)
- Manage cluster membership (ClusterMembershipService)
- Track grain locations (GrainDirectory)
- Make placement decisions (PlacementService)
- Persist grain state (Storage)
- Execute timers and reminders
- System internal grains (SystemTarget)

### Major Subsystems

See [Systems and Subsystems Map](02-systems-and-subsystems.md) for details.

**Key Classes**:
- `Silo`: Main silo host
- `Catalog`: Activation registry
- `ActivationData`: Grain activation representation
- `MessageCenter`: Message routing
- `ClusterMembershipService`: Cluster management
- `LocalGrainDirectory`: Grain location directory
- `OrleansTaskScheduler`: Work scheduler

### Silo Lifecycle

```
Create Silo
  → Initialize services
  → Start networking
  → Join cluster (Membership)
  → Start accepting work
  → [Running]
  → Graceful shutdown
  → Stop accepting work
  → Drain activations
  → Leave cluster
  → Dispose
```

### Dependencies

- `Orleans.Core` (includes abstractions and serialization)
- Provider packages (pluggable)

### Usage

**Silo Application**:
```csharp
var silo = new SiloHostBuilder()
    .UseLocalhostClustering()
    .ConfigureApplicationParts(parts =>
        parts.AddApplicationPart(typeof(UserGrain).Assembly))
    .Build();

await silo.StartAsync();
// Silo now hosting grains
```

---

## Layer 5: Code Generation

**Package**: `Orleans.CodeGenerator`

**Type**: Roslyn Source Generator (compile-time)

**Purpose**: Generate serialization and RPC infrastructure code at build time.

### Responsibilities

- Generate `IFieldCodec<T>` implementations
- Generate `IDeepCopier<T>` implementations
- Generate grain proxy classes (`GrainReference` subclasses)
- Generate invokable classes (`IInvokable` implementations)
- Generate activators and metadata

### How It Works

1. **Discovery**: Scans compilation for:
   - Types with `[GenerateSerializer]`
   - Grain interfaces (inheriting `IGrain`)
   - Methods needing invokable wrappers

2. **Analysis**: Builds model of types and methods

3. **Code Generation**: Uses Roslyn `SyntaxFactory` to generate C# code

4. **Output**: Single file `{AssemblyName}.orleans.g.cs`

### Generated Code Example

**Input**:
```csharp
public interface IMyGrain : IGrainWithStringKey
{
    Task<int> GetCount();
}
```

**Generated Proxy**:
```csharp
internal sealed class MyGrainProxy : GrainReference, IMyGrain
{
    public MyGrainProxy(GrainReferenceShared shared) : base(shared) { }

    async Task<int> IMyGrain.GetCount()
    {
        var request = new MyGrain_GetCount_Invokable();
        return await base.InvokeAsync<int>(request);
    }
}
```

**Generated Invokable**:
```csharp
[GenerateSerializer]
internal sealed class MyGrain_GetCount_Invokable : IInvokable
{
    public ValueTask<Response> Invoke(ITargetHolder target)
    {
        var grain = (IMyGrain)target.GetTarget();
        return grain.GetCount().AsValueTask();
    }
}
```

### Integration

**MSBuild**:
```xml
<Analyzer Include="Orleans.CodeGenerator.dll" />
```

Runs during compilation, before `csc.exe` finishes.

### Dependencies

- Roslyn APIs (`Microsoft.CodeAnalysis`)
- `Orleans.Core.Abstractions` (for types to generate against)

Detailed documentation: [Code Generation System](04-codegen-system.md)

---

## Layer 6: Host Integration

**Packages**:
- `Orleans.Client` (client metapackage)
- `Orleans.Server` (server metapackage)
- `Orleans.Sdk` (client + server metapackage)
- `Orleans.Hosting.*` (integration with hosting frameworks)

**Purpose**: Integration with .NET hosting models and dependency injection.

### Responsibilities

- .NET Generic Host integration
- Dependency injection configuration
- Builder APIs (`IClientBuilder`, `ISiloHostBuilder`)
- Metapackages for easy referencing

### Client Package

**Orleans.Client**:
- References `Orleans.Core`
- References `Orleans.Serialization`
- Provides `ClientBuilder`

**Usage**:
```csharp
var client = new ClientBuilder()
    .UseLocalhostClustering()
    .ConfigureApplicationParts(parts => ...)
    .ConfigureServices(services => ...)
    .Build();
```

### Server Package

**Orleans.Server**:
- References `Orleans.Runtime`
- Provides `SiloHostBuilder`
- Includes core providers (in-memory clustering, etc.)

**Usage**:
```csharp
var host = new HostBuilder()
    .UseOrleans((ctx, siloBuilder) =>
    {
        siloBuilder.UseLocalhostClustering();
        siloBuilder.ConfigureApplicationParts(parts => ...);
    })
    .Build();
```

### SDK Package

**Orleans.Sdk**:
- Metapackage including both Client and Server
- Includes CodeGenerator
- Includes default providers
- For applications that host both client and server

### Hosting Integration

**ASP.NET Core**:
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder => { ... });

var app = builder.Build();
```

**Generic Host**:
```csharp
var host = Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder => { ... })
    .Build();
```

### Dependencies

- Client: `Orleans.Core`, `Orleans.Serialization`
- Server: `Orleans.Runtime`, `Orleans.Core`
- SDK: Both of the above + CodeGenerator

---

## Layer 7: Provider Layer

**Purpose**: Pluggable implementations for clustering, storage, streaming, and reminders.

### Provider Categories

#### Clustering Providers

Implement `IMembershipTable` to store cluster membership:

- **Orleans.Clustering.AdoNet**: SQL Server, PostgreSQL, MySQL
- **Orleans.Clustering.AzureStorage**: Azure Table Storage
- **Orleans.Clustering.DynamoDB**: AWS DynamoDB
- **Orleans.Clustering.Consul**: HashiCorp Consul
- **Orleans.Clustering.ZooKeeper**: Apache ZooKeeper

**Configuration**:
```csharp
siloBuilder.UseAdoNetClustering(options =>
{
    options.ConnectionString = "...";
    options.Invariant = "System.Data.SqlClient";
});
```

#### Storage Providers

Implement `IGrainStorage` for grain state persistence:

- **Orleans.Persistence.Memory**: In-memory (dev only)
- **Orleans.Persistence.AdoNet**: SQL databases
- **Orleans.Persistence.AzureStorage**: Blob/Table/Cosmos
- **Orleans.Persistence.DynamoDB**: AWS DynamoDB

**Configuration**:
```csharp
siloBuilder.AddAzureBlobGrainStorage("Default", options =>
{
    options.ConnectionString = "...";
    options.ContainerName = "grainstate";
});
```

**Grain Usage**:
```csharp
[StorageProvider(ProviderName = "Default")]
public class UserGrain : Grain, IUserGrain
{
    private readonly IPersistentState<UserState> _state;

    public UserGrain([PersistentState("user")] IPersistentState<UserState> state)
    {
        _state = state;
    }
}
```

#### Streaming Providers

Implement `IPersistentStreamProvider`:

- **Orleans.Streaming.EventHubs**: Azure Event Hubs
- **Orleans.Streaming.SQS**: AWS SQS
- **Orleans.Streaming.Kinesis**: AWS Kinesis
- **Orleans.Streaming.NATS**: NATS messaging

**Configuration**:
```csharp
siloBuilder.AddEventHubStreams("StreamProvider", options =>
{
    options.ConnectionString = "...";
    options.EventHubName = "events";
});
```

#### Reminder Providers

Implement `IReminderTable` for durable reminders:

- **Orleans.Reminders.AdoNet**: SQL databases
- **Orleans.Reminders.AzureStorage**: Azure Table Storage
- **Orleans.Reminders.DynamoDB**: AWS DynamoDB

**Configuration**:
```csharp
siloBuilder.UseAdoNetReminderService(options =>
{
    options.ConnectionString = "...";
    options.Invariant = "System.Data.SqlClient";
});
```

### Provider Design Pattern

All providers follow a common pattern:

1. **Extension method** on builder: `.UseXyzProvider()`
2. **Options class**: `XyzProviderOptions`
3. **Implementation**: Registered in DI container
4. **Lifecycle management**: Participate in silo/client lifecycle

### Dependencies

Providers depend on:
- Runtime abstractions (interfaces)
- External SDKs (Azure SDK, AWS SDK, etc.)

---

## Cross-Cutting Layers

### Testing

**Packages**:
- `Orleans.TestingHost`: In-memory cluster for testing
- `Orleans.Transactions.TestKit.*`: Transaction testing utilities

**Usage**:
```csharp
var cluster = new TestClusterBuilder()
    .AddSiloBuilderConfigurator<TestSiloConfigurator>()
    .Build();

await cluster.DeployAsync();

var grain = cluster.GrainFactory.GetGrain<IMyGrain>(0);
await grain.DoSomething();
```

### Transactions

**Package**: `Orleans.Transactions`

**Purpose**: Distributed ACID transactions across grains.

Provides:
- `ITransactionalState<T>`: Transactional grain state
- Transaction coordinator
- Distributed commit protocol

### Event Sourcing

**Package**: `Orleans.EventSourcing`

**Purpose**: Event-sourced grain state management.

Provides:
- `JournaledGrain<TState>`: Base class for event-sourced grains
- Event replay and state reconstruction
- Conditional event application

---

## Dependency Rules

### Strict Layering

Each layer can only depend on layers below it:

```
Providers → Host Integration → Runtime → Core → Serialization → Abstractions
               ↓                                      ↓
          CodeGenerator (build-time)           (standalone)
```

### Rationale

1. **Clear boundaries**: Easy to understand what depends on what
2. **Independent evolution**: Lower layers can evolve independently
3. **Substitutability**: Providers are pluggable
4. **Testability**: Can mock at layer boundaries

### Package References

**Client Application**:
```xml
<PackageReference Include="Orleans.Client" />
<!-- Automatically includes Core, Serialization, Abstractions -->
```

**Server Application**:
```xml
<PackageReference Include="Orleans.Server" />
<!-- Automatically includes Runtime, Core, Serialization, Abstractions -->
```

**Full Application**:
```xml
<PackageReference Include="Orleans.Sdk" />
<!-- Includes both Client and Server + CodeGenerator -->
```

**Grain Interfaces (Class Library)**:
```xml
<PackageReference Include="Orleans.Core.Abstractions" />
<PackageReference Include="Orleans.CodeGenerator.MSBuild" />
<!-- Lightweight, only what's needed for interfaces -->
```

---

## Design Benefits

### Modularity

- Each layer has clear responsibility
- Changes isolated to specific layers
- Easy to understand system structure

### Reusability

- Serialization layer can be used standalone
- Abstractions shared across client and server
- Providers are swappable

### Performance

- Compile-time code generation (no reflection)
- Zero-allocation serialization paths
- Efficient inter-layer communication

### Extensibility

- Custom providers at Layer 7
- Custom serializers at Layer 2
- Custom placement strategies at Layer 4

### Evolution

- Lower layers change less frequently
- Upper layers (providers) can evolve independently
- Clear API surface at each layer

---

## Summary

Orleans' layered architecture provides:

1. **Clear separation of concerns**: Each layer has a specific purpose
2. **Progressive dependencies**: Layers build on each other logically
3. **Flexibility**: Pluggable implementations at provider layer
4. **Reusability**: Serialization framework usable outside Orleans
5. **Performance**: Code generation at build time eliminates runtime overhead
6. **Maintainability**: Clear boundaries make the codebase manageable

Understanding these layers is essential for:
- Contributing to Orleans
- Troubleshooting issues
- Implementing custom providers
- Optimizing applications

---

**Next**: [Code Generation System](04-codegen-system.md)
