# Async+ : Automatic Async State Machine Persistence

**Version**: 1.1 (January 2026)
**Status**: Proof of Concept - 7 of 9 core scenarios verified

---

## Conditional Compilation

**IMPORTANT**: The Async+ Roslyn modifications are guarded by the `ASYNC_PLUS` conditional compilation symbol.

| Build Mode | Symbol Defined | Behavior |
|------------|----------------|----------|
| **Standard** | No | Identical to original Roslyn - no Async+ code compiled |
| **Async+ Enabled** | Yes (`ASYNC_PLUS`) | Full persistence codegen for `[Persistable]` methods |

### To Enable Async+

Add `ASYNC_PLUS` to the compiler's DefineConstants when building Roslyn:

```xml
<DefineConstants>$(DefineConstants);ASYNC_PLUS</DefineConstants>
```

### To Disable Async+ (Standard Codegen)

Simply build Roslyn without defining `ASYNC_PLUS`. The compiler will be identical to upstream Roslyn.

---

## Overview

Async+ is a Roslyn compiler modification that automatically injects persistence and restoration logic into C# async state machines. By marking an async method with `[Persistable]`, the compiler generates code that:

1. **Checkpoints** state machine fields at each `await` point
2. **Restores** state machine fields on restart, enabling workflow continuation
3. **Completes/Faults** to track final workflow state

This enables:
- **Pause/Resume workflows** across process restarts
- **Crash recovery** without losing in-flight work
- **Distributed execution** when combined with Orleans or other runtimes

Async+ is **technology-agnostic** at its core. The Roslyn modifications emit calls to `IAsyncPersistenceService`, which can be implemented by different **drivers** (Orleans, file-based, Redis, etc.).

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           ASYNC+ CORE                                        │
│  (Roslyn Compiler + DOTNExT.Persistence namespace)                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌────────────────────────┐              ┌─────────────────────────┐        │
│  │   [Persistable]        │              │ IAsyncPersistenceService│        │
│  │   Attribute            │              │ (Interface Contract)    │        │
│  └────────────────────────┘              └─────────────────────────┘        │
│           │                                         ▲                       │
│           ▼                                         │                       │
│  ┌────────────────────────────────────────────────────────────────┐        │
│  │                    ROSLYN MODIFICATION                          │        │
│  │  AsyncMethodToStateMachineRewriter.cs                          │        │
│  │                                                                 │        │
│  │  Injects:                                                       │        │
│  │  • TryRestore<T>(ref T, methodId) at MoveNext start            │        │
│  │  • Checkpoint(object, stateNumber, methodId) before each await │        │
│  │                                                                 │        │
│  └────────────────────────────────────────────────────────────────┘        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                    ╔═══════════════╧═══════════════╗
                    ║     DRIVERS (Implementations)  ║
                    ╠═══════════════════════════════╣
                    ║                               ║
    ┌───────────────▼────────────────┐  ┌──────────▼──────────────────────┐
    │   Orleans Driver               │  │   Future Drivers                │
    │   (NewOrleans.AsyncPlus)       │  │                                 │
    ├────────────────────────────────┤  ├─────────────────────────────────┤
    │ • NewOrleansAsyncPersistenceService│ • FileAsyncPersistenceService  │
    │ • AsyncStatePersistenceGrain   │  │ • RedisAsyncPersistenceService  │
    │ • RavenDbGrainStorage          │  │ • SqlAsyncPersistenceService    │
    └────────────────────────────────┘  └─────────────────────────────────┘
```

---

## Core Components

### 1. `[Persistable]` Attribute

Located in `DOTNExT.Persistence` namespace.

```csharp
namespace DOTNExT.Persistence;

/// <summary>
/// Marks an async method for automatic persistence.
/// Roslyn looks for this attribute to enable persistence codegen.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class PersistableAttribute : Attribute
{
    /// <summary>
    /// Optional custom ID prefix for this workflow type.
    /// </summary>
    public string? IdPrefix { get; set; }

    /// <summary>
    /// If true, checkpoints at every await. If false, only at marked points.
    /// Default is true.
    /// </summary>
    public bool AutoCheckpoint { get; set; } = true;
}
```

**Usage:**
```csharp
[Persistable]
public async Task<int> LongRunningWorkflow(int input)
{
    var step1 = await ComputeStep1(input);      // Checkpoint 0
    var step2 = await ComputeStep2(step1);      // Checkpoint 1
    var result = await FinalStep(step2);         // Checkpoint 2
    return result;
}
```

### 2. `IAsyncPersistenceService` Interface

The contract between Roslyn-generated code and persistence implementations.

```csharp
namespace DOTNExT.Persistence;

/// <summary>
/// Interface that persistence services must implement.
/// This is the contract between Roslyn-generated code and persistence implementations.
///
/// NOTE: Methods are sync because Roslyn-generated MoveNext() is sync.
/// Implementations should handle async internally (e.g., fire-and-forget, tracked tasks).
/// </summary>
public interface IAsyncPersistenceService
{
    /// <summary>
    /// Called before suspending at an await point.
    /// The state machine should be serializable at this point.
    /// </summary>
    /// <param name="stateMachine">The state machine instance (boxed for struct state machines)</param>
    /// <param name="stateNumber">The await point state number (0, 1, 2, ...)</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    void Checkpoint(object stateMachine, int stateNumber, string methodId);

    /// <summary>
    /// Checks if there's persisted state to restore and applies it.
    /// Uses ref parameter to properly handle struct state machines without boxing issues.
    ///
    /// This is the preferred method for Roslyn+ generated code.
    /// </summary>
    /// <typeparam name="TStateMachine">The state machine type (struct or class)</typeparam>
    /// <param name="stateMachine">Ref to the state machine - will be replaced with restored state</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <returns>The state to resume from, or -1 if no restoration</returns>
    int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId);

    /// <summary>
    /// Called when async method completes successfully.
    /// </summary>
    void Complete(string methodId, object? result);

    /// <summary>
    /// Called when async method faults.
    /// </summary>
    void Fault(string methodId, Exception exception);

    // Events for observability
    event EventHandler<CheckpointEventArgs>? OnCheckpoint;
    event EventHandler<RestoreEventArgs>? OnRestore;
    event EventHandler<CompleteEventArgs>? OnComplete;
    event EventHandler<FaultEventArgs>? OnFault;
}
```

### 3. `AsyncPersistenceContext` Ambient Context

Provides access to the current persistence service without modifying method signatures.

```csharp
namespace DOTNExT.Persistence;

/// <summary>
/// Ambient context for async persistence.
/// Allows Roslyn-generated state machine code to access the persistence service
/// without modifying method signatures.
/// </summary>
public static class AsyncPersistenceContext
{
    private static readonly AsyncLocal<IAsyncPersistenceService?> _current = new();
    private static readonly AsyncLocal<string?> _workflowId = new();

    /// <summary>
    /// Gets the current persistence service for this async flow.
    /// Returns null if no persistence is configured.
    /// </summary>
    public static IAsyncPersistenceService? Current => _current.Value;

    /// <summary>
    /// Gets the current workflow instance ID for this async flow.
    /// Used to isolate persistence for concurrent workflow instances.
    /// </summary>
    public static string? WorkflowId => _workflowId.Value;

    /// <summary>
    /// Sets the persistence service and workflow ID for the current async flow.
    /// Returns a disposable that restores the previous values.
    /// </summary>
    public static IDisposable SetCurrent(IAsyncPersistenceService? service, string? workflowId)
    {
        var previousService = _current.Value;
        var previousWorkflowId = _workflowId.Value;
        _current.Value = service;
        _workflowId.Value = workflowId;
        return new ContextScope(previousService, previousWorkflowId, true);
    }

    /// <summary>
    /// Indicates if persistence is enabled for the current context.
    /// </summary>
    public static bool IsEnabled => _current.Value != null;

    // ... internal ContextScope class for disposal
}
```

**Usage:**
```csharp
// Set up persistence context before calling [Persistable] methods
using (AsyncPersistenceContext.SetCurrent(persistenceService, workflowId))
{
    var result = await MyPersistableWorkflow(input);
}
```

---

## Roslyn Compiler Modification

The core modification is in `AsyncMethodToStateMachineRewriter.cs` in the Roslyn compiler.

### Location
```
src/roslyn/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs
```

### Key Modifications

#### 1. Detection of [Persistable] Attribute

```csharp
// In constructor
_enablePersistence = method.GetAttributes().Any(a =>
    a.AttributeClass?.Name == "PersistableAttribute" ||
    a.AttributeClass?.ToDisplayString() == "DOTNExT.Persistence.PersistableAttribute");

if (_enablePersistence)
{
    _persistenceMethodId = $"{method.ContainingType.ToDisplayString()}.{method.Name}";
    Log($"[DOTNExT-Roslyn] Found [Persistable] on: {_persistenceMethodId}");
}
```

#### 2. Restoration Check at MoveNext Start

Generated code pattern:
```csharp
// At the start of MoveNext()
var persistenceService = AsyncPersistenceContext.Current;
if (persistenceService != null && cachedState == -1)
{
    var restoredState = persistenceService.TryRestore<TStateMachine>(ref this, methodId);
    if (restoredState >= 0)
    {
        // Reset state to -1 so workflow re-runs from beginning
        // with restored field values
        this.<>1__state = -1;
    }
}
```

**Key insight**: We do NOT jump to the restored state number because awaiters cannot be serialized. Instead, the workflow re-runs from the beginning, but with restored intermediate values (hoisted fields), so idempotent operations complete quickly.

#### 3. Checkpoint Before Each Await

```csharp
// Before each await suspension point
if (persistenceService != null)
{
    persistenceService.Checkpoint((object)this, stateNumber, methodId);
}
```

### State Machine Field Handling

The state machine contains:
- **Hoisted locals**: Intermediate values captured from the method
- **Parameters**: Method arguments hoisted to fields
- **`<>1__state`**: Current state number
- **Awaiters**: Task awaiter fields (NOT serializable)
- **Builder**: AsyncTaskMethodBuilder (NOT serializable)

Only hoisted locals and parameters are serialized. Awaiters and builders are transient.

---

## Serialization Strategy

### What Gets Serialized

| Field Type | Serialized | Notes |
|------------|------------|-------|
| Hoisted locals (primitives) | Yes | `int`, `string`, `bool`, etc. |
| Hoisted locals (enums) | Yes | |
| Method parameters | Yes | Hoisted to fields |
| `<>1__state` | Yes | State number |
| Awaiter fields | **No** | Cannot serialize `TaskAwaiter` |
| Builder fields | **No** | Cannot serialize `AsyncTaskMethodBuilder` |
| Captured `this` | **No** | Reference to outer class |
| Persistence service refs | **No** | Infrastructure |

### Serialization Implementation

```csharp
private static byte[] SerializeStateMachine(object stateMachine)
{
    var type = stateMachine.GetType();
    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    var fieldData = new Dictionary<string, object?>();
    foreach (var field in fields)
    {
        var fieldName = field.Name;
        var typeName = field.FieldType.Name;

        // Skip transient fields
        if (fieldName.Contains("__awaiter") || typeName.Contains("Awaiter")) continue;
        if (fieldName.Contains("__builder") || typeName.Contains("MethodBuilder")) continue;
        if (fieldName.Contains("<>4__this")) continue;  // Captured outer 'this'
        if (typeName.Contains("IAsyncPersistenceService")) continue;

        if (IsSerializableType(field.FieldType, value))
        {
            fieldData[field.Name] = field.GetValue(stateMachine);
        }
    }

    return JsonSerializer.SerializeToUtf8Bytes(fieldData);
}
```

### Struct Boxing Considerations

C# async methods generate **struct** state machines for performance. This creates a boxing challenge:

```csharp
// Problem: Boxing loses modifications
object boxed = structStateMachine;           // Box
service.TryRestore(boxed, methodId);         // Modifies boxed copy
// structStateMachine unchanged!

// Solution: Generic ref method
service.TryRestore<TStateMachine>(ref this, methodId);  // No boxing
```

The Roslyn modification uses the generic `TryRestore<T>(ref T, string)` method for struct state machines to avoid this issue.

---

## Workflow Lifecycle

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     WORKFLOW LIFECYCLE                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  FRESH START (no persisted state)                                       │
│  ────────────────────────────────                                       │
│  1. MoveNext() called                                                   │
│  2. TryRestore returns -1 (no checkpoint)                               │
│  3. Workflow executes from beginning                                    │
│  4. At each await: Checkpoint(state 0, 1, 2, ...)                       │
│  5. Complete() called on success, Fault() on exception                  │
│                                                                          │
│  RESUMPTION (persisted state exists)                                    │
│  ─────────────────────────────────────                                  │
│  1. MoveNext() called                                                   │
│  2. TryRestore returns N (last checkpoint state)                        │
│  3. State machine fields restored (intermediate values)                 │
│  4. State reset to -1 (restart from beginning)                          │
│  5. Workflow re-runs, but restored values skip redundant computation    │
│  6. Continues checkpointing, eventually completes                       │
│                                                                          │
│  EXAMPLE:                                                                │
│  ─────────                                                               │
│  [Persistable] async Task<int> Workflow(int x)                          │
│  {                                                                       │
│      var a = await Step1(x);    // Checkpoint 0: saves x, a             │
│      var b = await Step2(a);    // Checkpoint 1: saves x, a, b          │
│      return a + b;              // Complete: saves result               │
│  }                                                                       │
│                                                                          │
│  If crash after checkpoint 1:                                           │
│  - Restore loads x, a, b                                                │
│  - Step1 re-runs but a already has value                               │
│  - Step2 re-runs but b already has value (if idempotent)               │
│  - Method completes with correct result                                 │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Checkpoint Data Format

The persisted checkpoint contains:

```csharp
public sealed record AsyncStateCheckpoint
{
    /// <summary>
    /// State machine state number (0, 1, 2, ...)
    /// </summary>
    public required int StateNumber { get; init; }

    /// <summary>
    /// Serialized state machine fields (JSON)
    /// </summary>
    public required byte[] SerializedStateMachine { get; init; }

    /// <summary>
    /// Assembly-qualified type name for deserialization
    /// </summary>
    public required string StateMachineTypeName { get; init; }

    /// <summary>
    /// UTC timestamp of checkpoint
    /// </summary>
    public required DateTime CheckpointTimeUtc { get; init; }
}
```

Example serialized state machine fields (JSON):
```json
{
    "<>1__state": 1,
    "x": 7,
    "<a>5__1": 14,
    "<b>5__2": 21
}
```

---

## Design Principles

### 1. Technology Agnostic Core

Async+ core has **no dependencies** on Orleans, RavenDB, or any specific runtime:
- `[Persistable]` attribute: Plain C# attribute
- `IAsyncPersistenceService`: Pure interface
- `AsyncPersistenceContext`: Uses only `AsyncLocal<T>`
- Roslyn modification: Only emits interface calls

### 2. Sync Interface Methods

`IAsyncPersistenceService` methods are **synchronous** because:
- `MoveNext()` is synchronous (Roslyn-generated)
- Implementations can fire-and-forget checkpoints
- Tracked tasks ensure completion before restore

### 3. Ambient Context Pattern

Using `AsyncLocal<T>` allows persistence to flow through async call chains without modifying signatures:

```csharp
// Once set, flows through all async calls
using (AsyncPersistenceContext.SetCurrent(service, workflowId))
{
    await OuterMethod();  // Persistence enabled
        await InnerMethod();  // Still enabled
            await DeepMethod(); // Still enabled
}
```

### 4. Workflow Isolation

The `WorkflowId` in the context ensures concurrent workflow instances have isolated storage:

```csharp
// Concurrent workflows with different IDs
Task.WhenAll(
    RunWithContext(service, "workflow-1", () => MyWorkflow(1)),
    RunWithContext(service, "workflow-2", () => MyWorkflow(2)),
    RunWithContext(service, "workflow-3", () => MyWorkflow(3))
);
// Each workflow has its own checkpoint data
```

---

## Logging and Diagnostics

The Roslyn modification includes file-based logging for debugging:

```csharp
private static void LogToFile(string message)
{
    var logPath = Environment.GetEnvironmentVariable("DOTNEXT_ROSLYN_LOG")
        ?? Path.Combine(Path.GetTempPath(), "dotnext-roslyn-codegen.log");

    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    File.AppendAllText(logPath, $"[{timestamp}] {message}\n");
}
```

**Environment variable**: `DOTNEXT_ROSLYN_LOG` controls log file location.

**Log output examples**:
```
[DOTNExT-Roslyn] Found [Persistable] on: MyNamespace.MyClass.MyMethod
[DOTNExT-Roslyn] AsyncPersistenceContext resolved: True
[DOTNExT-Roslyn] IAsyncPersistenceService resolved: True
[DOTNExT-Roslyn] SUCCESS: Persistence injection will be enabled
[DOTNExT-Roslyn] GenerateAwaitForIncompleteTask: Adding checkpoint call for state 0
```

---

## Verified Scenarios

| Scenario | Description | Status |
|----------|-------------|--------|
| **R1** | Roslyn+ Cross-Session Persistence | PASS |
| **C1** | Basic Cross-Session (legacy hand-coded) | PASS |
| **C2** | Multiple Concurrent Workflows | PASS |
| **C3** | Nested Async Calls | PASS |
| **C4** | Exception Recovery | PASS |
| **C8** | Multi-Silo Checkpoint Visibility | PASS |
| **C9** | Grain Mobility | PASS |
| **C5** | Large State Serialization | Pending |
| **C6** | Silo Failover Mid-Checkpoint | Pending |
| **C7** | Checkpoint Version Migration | Pending |

---

## Future Drivers

Async+ can be extended with different persistence backends:

| Driver | Storage | Use Case |
|--------|---------|----------|
| **Orleans** | RavenDB/Orleans Storage | Distributed workflows |
| **File** | Local filesystem | Single-process workflows |
| **Redis** | Redis | High-throughput, TTL-based |
| **SQL** | SQL Server/PostgreSQL | Enterprise, transactions |
| **Dapr** | Dapr State Management | Microservices |
| **In-Memory** | Dictionary | Testing |

Each driver implements `IAsyncPersistenceService` with appropriate storage semantics.

---

## Implementation Checklist for New Drivers

1. **Implement `IAsyncPersistenceService`**
   - `Checkpoint`: Store serialized state machine
   - `TryRestore<T>`: Load and deserialize into state machine
   - `Complete`: Mark workflow complete, optionally clear state
   - `Fault`: Record exception details

2. **Handle Concurrent Access**
   - Multiple workflows may checkpoint simultaneously
   - Restore must wait for any pending checkpoint

3. **Provide Observability Events**
   - Fire `OnCheckpoint`, `OnRestore`, `OnComplete`, `OnFault`

4. **Test with Verified Scenarios**
   - Run C1-C9 scenarios against new driver

---

## Known Limitations

1. **Awaiters not serializable**: Workflow re-runs from start with restored field values
2. **Non-idempotent operations**: May produce different results on replay
3. **External state**: Side effects (HTTP calls, DB writes) may be repeated
4. **Complex object graphs**: Only simple types serialized by default
5. **Captured delegates**: Cannot serialize captured closures

---

## References

- Source: `src/roslyn/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/`
- Core types: `src/NewOrleans/src/NewOrleans.AsyncPlus/Abstractions/`
- Orleans driver: See `OrleansAsync+.md`
