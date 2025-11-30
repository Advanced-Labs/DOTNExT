# NewOrleans Async+ Integration

**Author**: Claude Opus 4
**Date**: 2025-11-30
**Status**: Design Phase

---

## Executive Summary

With the Roslyn modification for async persistence now working (Challenge 7 verified), the next phase is to integrate async persistence with NewOrleans (our Orleans fork). This document describes the architecture for the **NewOrleans Async+ Driver** - a component that bridges Async+ capabilities (starting with persistence) to the Orleans runtime.

---

## Current State (What's Working)

### Roslyn Modification ✅
- Modified `AsyncMethodToStateMachineRewriter.cs` to inject checkpoint/restore calls
- `[Persistable]` attribute detection at compile time
- Automatic checkpoint injection before each await suspension
- State restoration check at MoveNext start
- **Challenge 7 verified**: 2 checkpoints created for `[Persistable]` methods, 0 for non-persistable

### In-Memory Persistence Service ✅
- `IAsyncPersistenceService` - agnostic interface in `DOTNExT.Persistence` namespace
- `InMemoryAsyncPersistenceService` - full observability, JSON file backing
- `AsyncPersistenceContext` - ambient context using `AsyncLocal<T>`
- Checkpoint/Restore cycle fully working

---

## The Goal

Replace the in-memory persistence with Orleans-backed persistence that:

1. **Uses Orleans Grains** for state storage (leveraging Orleans persistence providers)
2. **Configurable via DI** - add to Orleans host builder, specify storage provider
3. **Agnostic to Async+** - the `IAsyncPersistenceService` interface remains unchanged
4. **Extensible** - the "driver" pattern can support future Async+ augmentations

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           User Application                                   │
│                                                                             │
│   [Persistable]                                                              │
│   public async Task<T> MyWorkflowAsync() { ... }                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Roslyn-injected calls
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      DOTNExT.Persistence.Abstractions                        │
│                                                                             │
│   IAsyncPersistenceService                                                   │
│   - Checkpoint(stateMachine, state, methodId)                               │
│   - TryRestore(stateMachine, methodId) -> stateNumber                       │
│   - Complete(methodId, result)                                               │
│   - Fault(methodId, exception)                                               │
│                                                                             │
│   AsyncPersistenceContext.Current  (ambient context)                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Implementation
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      NewOrleans.AsyncPlus.Driver                             │
│                                                                             │
│   OrleansAsyncPersistenceService : IAsyncPersistenceService                  │
│   - Wraps IAsyncStatePersistenceGrain calls                                 │
│   - DI-injectable into Orleans silo/client                                  │
│   - Configurable storage provider name                                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Grain Calls
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Orleans Grain Layer                                     │
│                                                                             │
│   IAsyncStatePersistenceGrain (keyed by methodId)                           │
│   - SaveCheckpointAsync(state, serializedStateMachine)                      │
│   - TryGetCheckpointAsync() -> (state, serializedStateMachine)?             │
│   - CompleteAsync(result)                                                    │
│   - FaultAsync(exceptionInfo)                                                │
│   - ClearAsync()                                                             │
│                                                                             │
│   Uses Orleans storage provider (Memory, ADO.NET, Azure, etc.)              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Component Design

### 1. Grain Interfaces (in `NewOrleans.AsyncPlus.Abstractions`)

```csharp
namespace NewOrleans.AsyncPlus;

/// <summary>
/// Grain interface for persisting async state machine checkpoints.
/// One grain per workflow instance (keyed by methodId).
/// </summary>
public interface IAsyncStatePersistenceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Save a checkpoint at the given state number.
    /// </summary>
    Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName);

    /// <summary>
    /// Try to get the latest checkpoint for restoration.
    /// Returns null if no checkpoint exists.
    /// </summary>
    Task<AsyncStateCheckpoint?> TryGetCheckpointAsync();

    /// <summary>
    /// Mark the workflow as completed successfully.
    /// Optionally clears persisted state.
    /// </summary>
    Task CompleteAsync(byte[]? serializedResult);

    /// <summary>
    /// Mark the workflow as faulted.
    /// </summary>
    Task FaultAsync(string exceptionType, string message, string? stackTrace);

    /// <summary>
    /// Clear all persisted state for this workflow.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// DTO for checkpoint data.
/// </summary>
[GenerateSerializer]
public record AsyncStateCheckpoint(
    [property: Id(0)] int StateNumber,
    [property: Id(1)] byte[] SerializedStateMachine,
    [property: Id(2)] string StateMachineTypeName,
    [property: Id(3)] DateTime CheckpointTime
);
```

### 2. Grain Implementation (in `NewOrleans.AsyncPlus.Grains`)

```csharp
namespace NewOrleans.AsyncPlus.Grains;

/// <summary>
/// Grain state for async persistence.
/// </summary>
[GenerateSerializer]
public class AsyncStatePersistenceGrainState
{
    [Id(0)] public int StateNumber { get; set; } = -1;
    [Id(1)] public byte[]? SerializedStateMachine { get; set; }
    [Id(2)] public string? StateMachineTypeName { get; set; }
    [Id(3)] public DateTime? CheckpointTime { get; set; }
    [Id(4)] public bool IsCompleted { get; set; }
    [Id(5)] public bool IsFaulted { get; set; }
    [Id(6)] public byte[]? Result { get; set; }
    [Id(7)] public string? FaultExceptionType { get; set; }
    [Id(8)] public string? FaultMessage { get; set; }
}

/// <summary>
/// Implementation of async state persistence using Orleans grain storage.
/// </summary>
public class AsyncStatePersistenceGrain : Grain<AsyncStatePersistenceGrainState>, IAsyncStatePersistenceGrain
{
    public async Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName)
    {
        State.StateNumber = stateNumber;
        State.SerializedStateMachine = serializedStateMachine;
        State.StateMachineTypeName = stateMachineTypeName;
        State.CheckpointTime = DateTime.UtcNow;
        await WriteStateAsync();
    }

    public Task<AsyncStateCheckpoint?> TryGetCheckpointAsync()
    {
        if (State.StateNumber < 0 || State.SerializedStateMachine == null)
            return Task.FromResult<AsyncStateCheckpoint?>(null);

        return Task.FromResult<AsyncStateCheckpoint?>(new AsyncStateCheckpoint(
            State.StateNumber,
            State.SerializedStateMachine,
            State.StateMachineTypeName!,
            State.CheckpointTime ?? DateTime.UtcNow
        ));
    }

    public async Task CompleteAsync(byte[]? serializedResult)
    {
        State.IsCompleted = true;
        State.Result = serializedResult;
        // Optionally clear state machine data to save space
        State.SerializedStateMachine = null;
        await WriteStateAsync();
    }

    public async Task FaultAsync(string exceptionType, string message, string? stackTrace)
    {
        State.IsFaulted = true;
        State.FaultExceptionType = exceptionType;
        State.FaultMessage = message;
        await WriteStateAsync();
    }

    public async Task ClearAsync()
    {
        await ClearStateAsync();
    }
}
```

### 3. Orleans Driver Service (in `NewOrleans.AsyncPlus.Driver`)

```csharp
namespace NewOrleans.AsyncPlus;

/// <summary>
/// Orleans-backed implementation of IAsyncPersistenceService.
/// Bridges the Async+ abstraction to Orleans grains.
/// </summary>
public class OrleansAsyncPersistenceService : IAsyncPersistenceService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OrleansAsyncPersistenceService> _logger;

    public OrleansAsyncPersistenceService(
        IGrainFactory grainFactory,
        ILogger<OrleansAsyncPersistenceService> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public void Checkpoint(object stateMachine, int stateNumber, string methodId)
    {
        // Note: This is sync but calls async grain - needs careful handling
        // Option 1: Fire-and-forget with error logging
        // Option 2: Block (not recommended for performance)
        // Option 3: Use SynchronizationContext if available

        CheckpointAsync(stateMachine, stateNumber, methodId).GetAwaiter().GetResult();
    }

    private async Task CheckpointAsync(object stateMachine, int stateNumber, string methodId)
    {
        var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
        var serialized = SerializeStateMachine(stateMachine);
        var typeName = stateMachine.GetType().AssemblyQualifiedName!;

        await grain.SaveCheckpointAsync(stateNumber, serialized, typeName);

        _logger.LogDebug("Checkpoint saved for {MethodId} at state {State}", methodId, stateNumber);
        OnCheckpoint?.Invoke(this, new CheckpointEventArgs(methodId, stateNumber));
    }

    public int TryRestore(object stateMachine, string methodId)
    {
        return TryRestoreAsync(stateMachine, methodId).GetAwaiter().GetResult();
    }

    private async Task<int> TryRestoreAsync(object stateMachine, string methodId)
    {
        var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
        var checkpoint = await grain.TryGetCheckpointAsync();

        if (checkpoint == null)
            return -1;

        DeserializeIntoStateMachine(stateMachine, checkpoint.SerializedStateMachine);

        _logger.LogDebug("Restored {MethodId} to state {State}", methodId, checkpoint.StateNumber);
        OnRestore?.Invoke(this, new RestoreEventArgs(methodId, checkpoint.StateNumber));

        return checkpoint.StateNumber;
    }

    public void Complete(string methodId, object? result)
    {
        CompleteAsync(methodId, result).GetAwaiter().GetResult();
    }

    private async Task CompleteAsync(string methodId, object? result)
    {
        var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
        var serialized = result != null ? SerializeResult(result) : null;
        await grain.CompleteAsync(serialized);

        _logger.LogDebug("Workflow {MethodId} completed", methodId);
        OnComplete?.Invoke(this, new CompleteEventArgs(methodId, result, true));
    }

    public void Fault(string methodId, Exception exception)
    {
        FaultAsync(methodId, exception).GetAwaiter().GetResult();
    }

    private async Task FaultAsync(string methodId, Exception exception)
    {
        var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
        await grain.FaultAsync(exception.GetType().FullName!, exception.Message, exception.StackTrace);

        _logger.LogWarning(exception, "Workflow {MethodId} faulted", methodId);
        OnFault?.Invoke(this, new FaultEventArgs(methodId, exception));
    }

    // Events for observability
    public event EventHandler<CheckpointEventArgs>? OnCheckpoint;
    public event EventHandler<RestoreEventArgs>? OnRestore;
    public event EventHandler<CompleteEventArgs>? OnComplete;
    public event EventHandler<FaultEventArgs>? OnFault;

    // Serialization helpers (use same approach as InMemoryAsyncPersistenceService)
    private byte[] SerializeStateMachine(object stateMachine) { /* reflection-based */ }
    private void DeserializeIntoStateMachine(object stateMachine, byte[] data) { /* reflection-based */ }
    private byte[]? SerializeResult(object result) { /* JSON or binary */ }
}
```

### 4. DI Extension Methods (in `NewOrleans.AsyncPlus.Driver`)

```csharp
namespace NewOrleans.AsyncPlus;

public static class AsyncPlusHostingExtensions
{
    /// <summary>
    /// Adds Async+ persistence support to the Orleans silo.
    /// </summary>
    public static ISiloBuilder UseAsyncPlusPersistence(
        this ISiloBuilder siloBuilder,
        string storageName = "AsyncPlusStorage")
    {
        siloBuilder.ConfigureServices(services =>
        {
            // Register the Orleans-backed persistence service
            services.AddSingleton<IAsyncPersistenceService, OrleansAsyncPersistenceService>();

            // Optionally configure storage name
            services.Configure<AsyncPlusOptions>(options =>
            {
                options.StorageProviderName = storageName;
            });
        });

        return siloBuilder;
    }

    /// <summary>
    /// Adds Async+ persistence support to an Orleans client.
    /// </summary>
    public static IClientBuilder UseAsyncPlusPersistence(this IClientBuilder clientBuilder)
    {
        clientBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IAsyncPersistenceService, OrleansAsyncPersistenceService>();
        });

        return clientBuilder;
    }
}

public class AsyncPlusOptions
{
    public string StorageProviderName { get; set; } = "Default";
}
```

---

## Project Structure Options

### Option A: Build in AsyncPersistenceScenarios Project (Recommended for now)

Advantages:
- Faster iteration with existing test infrastructure
- All Async+ code in one place
- Can test without full Orleans cluster

```
AsyncPersistenceScenarios/
├── Services/
│   ├── IAsyncPersistenceService.cs       # Existing
│   ├── InMemoryAsyncPersistenceService.cs # Existing
│   └── OrleansAsyncPersistenceService.cs  # NEW
├── Orleans/
│   ├── IAsyncStatePersistenceGrain.cs     # NEW
│   ├── AsyncStatePersistenceGrain.cs      # NEW
│   └── AsyncPlusHostingExtensions.cs      # NEW
└── Program.cs                              # Add Challenge 8
```

Requires adding to `AsyncPersistenceScenarios.csproj`:
```xml
<ProjectReference Include="..\..\src\Orleans.Server\Orleans.Server.csproj" />
<ProjectReference Include="..\..\src\Orleans.Runtime\Orleans.Runtime.csproj" />
<OrleansBuildTimeCodeGen>true</OrleansBuildTimeCodeGen>
```

### Option B: Build in PluginGrainScenarios Project

Advantages:
- Orleans infrastructure already configured
- Follows existing pattern

Disadvantages:
- Async+ code split across projects
- Less focused on persistence testing

### Option C: Create Separate Library Projects (Future)

For production:
```
NewOrleans.AsyncPlus.Abstractions/   # Interfaces, DTOs
NewOrleans.AsyncPlus.Grains/         # Grain implementations
NewOrleans.AsyncPlus.Driver/         # Service implementation
```

---

## Directory.Build.props Requirements

For Orleans code generation to work with project references (not NuGet):

```xml
<!-- Required in csproj or inherited Directory.Build.props -->
<PropertyGroup>
    <OrleansBuildTimeCodeGen>true</OrleansBuildTimeCodeGen>
</PropertyGroup>
```

The root `Directory.Build.props` at `/src/NewOrleans/` includes:
```xml
<Import Condition=" '$(OrleansBuildTimeCodeGen)' == 'true' "
        Project="$(MSBuildThisFileDirectory)src/Orleans.CodeGenerator/build/Microsoft.Orleans.CodeGenerator.props" />
```

This triggers the Orleans source generator for serializers and grain references.

---

## Implementation Plan

### Phase 1: Basic Orleans Integration
1. Add Orleans references to AsyncPersistenceScenarios
2. Implement `IAsyncStatePersistenceGrain` and grain
3. Implement `OrleansAsyncPersistenceService`
4. Add Challenge 8: Orleans-backed persistence test

### Phase 2: Configuration & Polish
1. Add `UseAsyncPlusPersistence()` extension methods
2. Support configurable storage providers
3. Add proper async handling (avoid sync-over-async)
4. Performance testing

### Phase 3: Library Extraction (Future)
1. Extract to separate library projects
2. NuGet packaging
3. Documentation

---

## Sync-Over-Async Considerations

The `IAsyncPersistenceService` interface has sync methods because:
- Roslyn-generated code calls them synchronously
- State machine `MoveNext()` can't be async

Options:
1. **GetAwaiter().GetResult()** - Simple but can deadlock in some contexts
2. **Fire-and-forget for checkpoints** - Checkpoint continues async, state machine proceeds
3. **Custom SynchronizationContext** - Complex but correct
4. **Async overload + sync wrapper** - Most flexible

Recommendation: Start with `GetAwaiter().GetResult()` for simplicity, optimize later if needed.

---

## Future Extensions (Async+ Beyond Persistence)

The "driver" pattern supports future Async+ augmentations:

```csharp
public interface IAsyncPlusDriver
{
    IAsyncPersistenceService? Persistence { get; }
    IAsyncDistributionService? Distribution { get; }  // Future: distribute work
    IAsyncTimelineService? Timeline { get; }          // Future: time-travel
    IAsyncSemanticService? Semantics { get; }         // Future: AI augmentation
}
```

---

## References

- `CURRENT-WORK.md` - Working Roslyn modification details
- `DOTNExT-Vision.md` - Long-term vision including persistence tiers
- `AsyncPersistence-Research.md` - Research on async state machine internals
- `/src/NewOrleans/playground/PluginGrainScenarios/` - Orleans project reference pattern

---

*This document defines the next phase of Async+ development: Orleans integration.*
