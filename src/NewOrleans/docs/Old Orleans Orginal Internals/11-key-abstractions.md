# Key Abstractions

## Overview

This document catalogs the core interfaces and types that define Orleans' programming model and runtime contracts.

**Location**: `src/Orleans.Core.Abstractions/`

## Grain Programming Model

### IGrain

**Base marker interface** for all grain interfaces:

```csharp
public interface IGrain
{
    // Marker interface - no members
}
```

**Purpose**: Identifies grain interfaces for code generation and runtime.

### Grain Key Interfaces

**Typed grain keys**:

```csharp
public interface IGrainWithStringKey : IGrain { }
public interface IGrainWithGuidKey : IGrain { }
public interface IGrainWithIntegerKey : IGrain { }
public interface IGrainWithGuidCompoundKey : IGrain { }
public interface IGrainWithIntegerCompoundKey : IGrain { }
```

**Usage**:
```csharp
public interface IUserGrain : IGrainWithStringKey
{
    Task<string> GetName();
}

// Access via key
var grain = grainFactory.GetGrain<IUserGrain>("user-123");
```

### IGrainBase

**Base interface** for grain implementations:

```csharp
public interface IGrainBase
{
    IGrainContext GrainContext { get; }
}
```

**Grain Base Class**:
```csharp
public abstract class Grain : IGrainBase
{
    public IGrainContext GrainContext { get; set; }

    protected IGrainFactory GrainFactory { get; }
    protected IServiceProvider ServiceProvider { get; }

    protected virtual Task OnActivateAsync(CancellationToken ct);
    protected virtual Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct);

    protected void DeactivateOnIdle();
    protected IDisposable RegisterTimer(Func<object, Task> callback, object state, TimeSpan dueTime, TimeSpan period);
}
```

## Identity Types

### GrainId

**Unique identifier** for a grain:

```csharp
public readonly struct GrainId : IEquatable<GrainId>, IComparable<GrainId>
{
    public GrainType Type { get; }
    public IdSpan Key { get; }

    public static GrainId Create(string type, string key);
    public static GrainId Parse(string value);

    // Format: "grain.userprofile/user-123"
    public override string ToString();
}
```

**Components**:
- **Type**: Grain type identifier (e.g., `grain.userprofile`)
- **Key**: Unique key within that type

### GrainType

**Type identifier** for grains:

```csharp
public readonly struct GrainType : IEquatable<GrainType>
{
    public static GrainType Create(string value);

    // e.g., "grain.userprofile"
    public override string ToString();
}
```

### ActivationId

**Unique identifier** for a grain activation instance:

```csharp
public readonly struct ActivationId : IEquatable<ActivationId>
{
    public static ActivationId NewId();

    public Guid Key { get; }

    // GUID-based unique ID
    public override string ToString();
}
```

**Note**: Different from GrainId - multiple activations can exist for the same GrainId (e.g., stateless workers).

### SiloAddress

**Unique identifier** for a silo instance:

```csharp
public readonly struct SiloAddress : IEquatable<SiloAddress>
{
    public IPEndPoint Endpoint { get; }
    public int Generation { get; }

    // Format: "10.0.0.1:11111:5"
    public override string ToString();
}
```

**Generation**: Incremented on each silo restart to prevent "ghost" silos.

### GrainAddress

**Complete location** information for a grain:

```csharp
public readonly struct GrainAddress
{
    public GrainId GrainId { get; }
    public ActivationId ActivationId { get; }
    public SiloAddress SiloAddress { get; }
}
```

## Runtime Interfaces

### IGrainContext

**Runtime's view** of a grain activation:

```csharp
public interface IGrainContext : ITargetHolder
{
    // Identity
    GrainId GrainId { get; }
    ActivationId ActivationId { get; }
    GrainReference GrainReference { get; }

    // Activation
    object GrainInstance { get; }
    IServiceProvider ActivationServices { get; }

    // Lifecycle
    IGrainLifecycle ObservableLifecycle { get; }

    // Lifecycle methods
    void Activate(Dictionary<string, object> requestContext, CancellationToken cancellationToken);
    void Deactivate(DeactivationReason reason, CancellationToken cancellationToken);
    void Rehydrate(IRehydrationContext context);
    void Migrate(Dictionary<string, object> requestContext, CancellationToken cancellationToken);

    // Component access
    TComponent GetComponent<TComponent>();
    void SetComponent<TComponent>(TComponent component);
}
```

**Implementation**: `ActivationData` in `Orleans.Runtime`

### IGrainActivator

**Factory** for creating grain instances:

```csharp
public interface IGrainActivator
{
    object CreateInstance(IGrainContext context);
    void DisposeInstance(IGrainContext context, object instance);
}
```

**Default**: DI-based activator using `ActivatorUtilities`

### ITargetHolder

**Provides access** to grain instance:

```csharp
public interface ITargetHolder
{
    object GetTarget();
    TTarget GetTarget<TTarget>();
}
```

## Grain References

### GrainReference

**Base class** for all grain proxies:

```csharp
public abstract class GrainReference : IAddressable
{
    public GrainId GrainId { get; }

    protected ValueTask<T> InvokeAsync<T>(IInvokable request);
    protected Task InvokeOneWay(IInvokable request);

    // Serialization support
    public virtual string ToShortKeyString();
    public virtual Guid GetPrimaryKey();
    public virtual long GetPrimaryKeyLong();
    public virtual string GetPrimaryKeyString();
}
```

**Generated Proxy** (example):
```csharp
internal sealed class UserGrainProxy : GrainReference, IUserGrain
{
    Task<string> IUserGrain.GetName() =>
        InvokeAsync<string>(new UserGrain_GetName_Invokable());
}
```

### IGrainFactory

**Factory** for creating grain references:

```csharp
public interface IGrainFactory
{
    TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string grainClassNamePrefix = null);
    TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string grainClassNamePrefix = null);
    TGrainInterface GetGrain<TGrainInterface>(string primaryKey, string grainClassNamePrefix = null);
    TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string keyExtension, string grainClassNamePrefix = null);
}
```

## Lifecycle

### IGrainLifecycle

**Observable lifecycle** for grain activations:

```csharp
public interface IGrainLifecycle : ILifecycleObservable
{
    // Inherited from ILifecycleObservable
    IDisposable Subscribe(string observerName, int stage, ILifecycleObserver observer);
}

public interface ILifecycleObserver
{
    Task OnStart(CancellationToken ct);
    Task OnStop(CancellationToken ct);
}
```

**Lifecycle Stages**:
```csharp
public static class GrainLifecycleStage
{
    public const int First = int.MinValue;
    public const int SetupState = 1000;
    public const int Activate = 2000;
    public const int Last = int.MaxValue;
}
```

**Usage**:
```csharp
public override Task OnActivateAsync(CancellationToken ct)
{
    GrainContext.ObservableLifecycle.Subscribe(
        "MyComponent",
        GrainLifecycleStage.Activate,
        new MyLifecycleObserver());

    return base.OnActivateAsync(ct);
}
```

### DeactivationReason

**Structured reason** for grain deactivation:

```csharp
public readonly struct DeactivationReason
{
    public DeactivationReasonCode ReasonCode { get; }
    public string Description { get; }
}

public enum DeactivationReasonCode
{
    None,
    ApplicationRequested,
    ActivationIdle,
    ActivationUnresponsive,
    ShuttingDown,
    ActivationFailed,
    DuplicateActivation,
    MigrationRequested
}
```

## Placement

### PlacementStrategy

**Base class** for placement strategies:

```csharp
public abstract class PlacementStrategy
{
    // Marker class
}

// Concrete strategies
public class RandomPlacement : PlacementStrategy { }
public class PreferLocalPlacement : PlacementStrategy { }
public class HashBasedPlacement : PlacementStrategy { }
public class ActivationCountBasedPlacement : PlacementStrategy { }
public class StatelessWorkerPlacement : PlacementStrategy
{
    public int MaxLocal { get; }
}
```

**Attributes**:
```csharp
[RandomPlacement]
[PreferLocalPlacement]
[HashBasedPlacement]
[ActivationCountBasedPlacement]
[StatelessWorker(maxLocalWorkers: 10)]
```

### IPlacementDirector

**Placement decision logic**:

```csharp
public interface IPlacementDirector
{
    Task<SiloAddress> OnAddActivation(
        PlacementStrategy strategy,
        PlacementTarget target,
        IPlacementContext context);
}
```

## Storage

### IStorage<TState>

**Grain state storage** abstraction:

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

### IPersistentState<TState>

**Persistent state** for grain injection:

```csharp
public interface IPersistentState<TState> : IStorage<TState>
{
    string Name { get; }
    bool RecordExists { get; }
}
```

**Usage**:
```csharp
public MyGrain(
    [PersistentState("state", "StorageProvider")]
    IPersistentState<MyState> state)
{
    _state = state;
}
```

### IGrainStorage

**Storage provider** interface:

```csharp
public interface IGrainStorage
{
    Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState);
    Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState);
    Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState);
}
```

## Timers and Reminders

### IDisposable (Timer)

**Timer handle**:

```csharp
IDisposable RegisterTimer(
    Func<object, Task> callback,
    object state,
    TimeSpan dueTime,
    TimeSpan period);
```

### IGrainReminder

**Reminder handle**:

```csharp
public interface IGrainReminder
{
    string ReminderName { get; }
}
```

### IRemindable

**Reminder callback** interface:

```csharp
public interface IRemindable : IGrain
{
    Task ReceiveReminder(string reminderName, TickStatus status);
}
```

## Serialization

### IFieldCodec<T>

**Type-specific serializer**:

```csharp
public interface IFieldCodec<T>
{
    void WriteField<TBufferWriter>(
        ref Writer<TBufferWriter> writer,
        uint fieldIdDelta,
        Type expectedType,
        T value) where TBufferWriter : IBufferWriter<byte>;

    T ReadValue<TInput>(ref Reader<TInput> reader, Field field);
}
```

### IDeepCopier<T>

**Deep copy** implementation:

```csharp
public interface IDeepCopier<T>
{
    T DeepCopy(T input, CopyContext context);
}
```

### IActivator<T>

**Object activator**:

```csharp
public interface IActivator<T>
{
    T Create();
}
```

## Attributes

### Serialization

```csharp
[GenerateSerializer]    // Mark type for code generation
[Id(N)]                 // Field ID for versioning
[Immutable]             // Skip deep copying
[Alias("type.name")]    // Stable type name
```

### Grain Configuration

```csharp
[Reentrant]                              // Allow interleaved execution
[AlwaysInterleave]                       // Method-level reentrancy
[MayInterleave(nameof(InterleaveFilter))]// Custom interleaving
[StorageProvider(ProviderName = "...")]  // Specify storage provider
```

### Placement

```csharp
[RandomPlacement]
[PreferLocalPlacement]
[HashBasedPlacement]
[ActivationCountBasedPlacement]
[StatelessWorker(maxLocalWorkers: N)]
```

## Summary

Key abstractions in Orleans:

**Grain Model**:
- `IGrain`, `IGrainBase`, `Grain<TState>`
- Grain interfaces with typed keys

**Identity**:
- `GrainId`, `GrainType`, `ActivationId`
- `SiloAddress`, `GrainAddress`

**Runtime**:
- `IGrainContext`, `IGrainActivator`
- `GrainReference`, `IGrainFactory`

**Lifecycle**:
- `IGrainLifecycle`, `DeactivationReason`

**Storage**:
- `IStorage<T>`, `IPersistentState<T>`
- `IGrainStorage`

**Serialization**:
- `IFieldCodec<T>`, `IDeepCopier<T>`
- `IActivator<T>`

These abstractions provide:
- Clear contracts between layers
- Type safety
- Extensibility points
- Testability

---

**Next**: [Directory Structure Guide](12-directory-structure.md)
