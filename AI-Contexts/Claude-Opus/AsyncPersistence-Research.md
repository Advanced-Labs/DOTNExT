# Async State Machine Persistence Research

**Author**: Claude Opus
**Date**: 2025-11-28
**Purpose**: Research findings on Roslyn async codegen and design for automatic persistence

---

## Table of Contents

1. [Roslyn Async Internals](#roslyn-async-internals)
2. [State Machine Anatomy](#state-machine-anatomy)
3. [Implementation Options](#implementation-options)
4. [Recommended Approach](#recommended-approach)
5. [Persistence Service Design](#persistence-service-design)
6. [Test Scenario Design](#test-scenario-design)
7. [Custom Roslyn Compiler Integration](#custom-roslyn-compiler-integration)

---

## Roslyn Async Internals

### Key Files in Roslyn

```
src/roslyn/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/
├── AsyncRewriter.cs                           # Entry point, orchestrates transformation
├── AsyncMethodToStateMachineRewriter.cs       # Generates MoveNext() body
├── AsyncMethodBuilderMemberCollection.cs      # Builder API abstraction
├── AsyncStateMachine.cs                       # The generated state machine type
└── AsyncIteratorMethodToStateMachineRewriter.cs # For async iterators
```

### Transformation Flow

```
User Code:                          Generated Code:
─────────────                       ──────────────

async Task<int> FooAsync()          class <>FooAsync_d__0 : IAsyncStateMachine
{                                   {
    var x = await Step1();              int <>1__state;
    var y = await Step2(x);             AsyncTaskMethodBuilder<int> <>t__builder;
    return x + y;                       int <x>5__1;  // hoisted local
}                                       int <y>5__2;  // hoisted local
                                        TaskAwaiter<int> <>u__1; // awaiter field

                                        void MoveNext()
                                        {
                                            switch (<>1__state)
                                            {
                                                case 0: goto Label0;
                                                case 1: goto Label1;
                                            }

                                            // state -1: first run
                                            var awaiter = Step1().GetAwaiter();
                                            if (!awaiter.IsCompleted)
                                            {
                                                <>1__state = 0;
                                                <>u__1 = awaiter;
                                                builder.AwaitOnCompleted(...);
                                                return;
                                            }
                                        Label0:
                                            <x>5__1 = awaiter.GetResult();

                                            awaiter = Step2(<x>5__1).GetAwaiter();
                                            if (!awaiter.IsCompleted)
                                            {
                                                <>1__state = 1;
                                                ...
                                            }
                                        Label1:
                                            <y>5__2 = awaiter.GetResult();

                                            <>1__state = -2; // finished
                                            builder.SetResult(<x>5__1 + <y>5__2);
                                        }
                                    }
```

### Key Roslyn Code Points

**1. State Field Generation** (`AsyncRewriter.cs:141-142`):
```csharp
stateField = F.StateMachineField(
    F.SpecialType(SpecialType.System_Int32),
    GeneratedNames.MakeStateMachineStateFieldName(),
    isPublic: true);
_builderField = F.StateMachineField(
    _asyncMethodBuilderMemberCollection.BuilderType,
    GeneratedNames.AsyncBuilderFieldName(),
    isPublic: true);
```

**2. State Transition at Await** (`AsyncMethodToStateMachineRewriter.cs:459-461`):
```csharp
blockBuilder.Add(
    // this.state = cachedState = stateForLabel
    GenerateSetBothStates(stateNumber));
```

**3. Awaiter Storage** (`AsyncMethodToStateMachineRewriter.cs:467-473`):
```csharp
blockBuilder.Add(
    // this.<>t__awaiter = $awaiterTemp
    F.Assignment(
        F.Field(F.This(), awaiterField),
        awaiterTemp));
```

**4. Resume Label Generation** (`AsyncMethodToStateMachineRewriter.cs:491-496`):
```csharp
blockBuilder.Add(F.Label(resumeLabel));
blockBuilder.Add(F.NoOp(NoOpStatementFlavor.AwaitResumePoint));
```

---

## State Machine Anatomy

### Fields Generated

| Field | Type | Purpose |
|-------|------|---------|
| `<>1__state` | int | Current state (-1=running, -2=finished, 0+=await point) |
| `<>t__builder` | AsyncTaskMethodBuilder<T> | The builder instance |
| `<>u__1`, `<>u__2`, ... | Various | Awaiter fields (one per awaiter type used) |
| `<localName>5__N` | Various | Hoisted locals (variables live across awaits) |
| `<>4__this` | T | Captured `this` (if instance method) |
| Parameters | Various | Method parameters (hoisted) |

### State Values

| Value | Meaning |
|-------|---------|
| -1 | Not started or running (between awaits) |
| -2 | Finished (completed or faulted) |
| 0, 1, 2, ... | Suspended at await point N |

### What We Need to Persist

To restore an async method mid-execution:
1. **State number** - which await point
2. **All hoisted locals** - variables alive across awaits
3. **Parameters** - original method parameters (hoisted)
4. **`this` reference** - if instance method (as identifier, not object)

What we **don't** persist:
- Awaiter fields - these are transient (recreated on resume)
- Builder field - recreated
- Cached locals - temporary

---

## Implementation Options

### Option A: Custom AsyncMethodBuilder (No Roslyn Mod)

Use `[AsyncMethodBuilder(typeof(PersistableAsyncMethodBuilder<>))]`:

```csharp
[PersistableWorkflow]
[AsyncMethodBuilder(typeof(PersistableAsyncMethodBuilder<>))]
public async Task<int> MyWorkflowAsync(int input)
{
    var step1 = await DoStep1Async(input);
    var step2 = await DoStep2Async(step1);
    return step2;
}
```

Custom builder hooks:
- `AwaitOnCompleted` - intercept, checkpoint before scheduling
- `Start` - check for restoration

**Pros**: Works with stock Roslyn
**Cons**:
- Need attribute on every method
- Can only intercept at builder boundaries
- No access to hoisted locals from builder

### Option B: Modify Roslyn AsyncRewriter (RECOMMENDED)

Inject persistence calls directly into generated `MoveNext()`:

```csharp
// BEFORE each await (in GenerateAwaitForIncompleteTask):
if (persistenceService != null)
{
    persistenceService.Checkpoint(this, stateNumber, hoistedLocals);
}

// AT START of MoveNext (in GenerateMoveNext):
if (persistenceService != null && persistenceService.ShouldRestore(this))
{
    persistenceService.Restore(this, out <>1__state, out hoistedLocals);
}
```

**Pros**:
- Full control over generated code
- Access to all state machine fields
- Can be made conditional (attribute-driven)
- Transparent to user code

**Cons**:
- Requires modified Roslyn
- Need to distribute custom compiler

### Option C: Source Generator + Custom Builder

Generate wrapper methods:

```csharp
// User writes:
[Persistable]
public async Task<int> MyWorkflowAsync(int input) { ... }

// Generator produces:
public Task<int> __Persistable_MyWorkflowAsync(int input)
{
    return PersistenceRuntime.Execute(
        () => MyWorkflowAsync(input),
        stateId: "MyWorkflowAsync",
        input);
}
```

**Pros**: Works with stock compiler
**Cons**: Extra indirection, complex

### Option D: IL Rewriting (Post-Compilation)

Use Mono.Cecil to modify compiled IL.

**Pros**: Works with any compiled code
**Cons**: Complex, debugging issues, toolchain complexity

---

## Recommended Approach

**Option B: Modify Roslyn AsyncRewriter** is recommended because:

1. We have Roslyn source in DOTNExT
2. Maximum control over generated code
3. Can access all state machine fields directly
4. Can be conditional based on attributes
5. Clean integration path to C* superset

### Modification Points in Roslyn

**File**: `AsyncMethodToStateMachineRewriter.cs`

**1. Add field for persistence service** (line ~95):
```csharp
private readonly FieldSymbol? _persistenceServiceField;
```

**2. Modify `GenerateMoveNext`** (line ~133):
- Add restoration check at method start
- Wire up persistence service field

**3. Modify `GenerateAwaitForIncompleteTask`** (line ~446):
- Add checkpoint call before state transition

**4. Add new method `GenerateCheckpointCall`**:
```csharp
private BoundStatement GenerateCheckpointCall(StateMachineState stateNumber)
{
    // if (_persistenceService != null)
    //     _persistenceService.Checkpoint(this, stateNumber);
}
```

### Persistence Service Interface

```csharp
namespace DOTNExT.Persistence
{
    /// <summary>
    /// Service interface for async state machine persistence.
    /// Injected via DI - if null, no persistence occurs.
    /// </summary>
    public interface IAsyncPersistenceService
    {
        /// <summary>
        /// Called before suspending at an await point.
        /// </summary>
        void Checkpoint<TStateMachine>(
            ref TStateMachine stateMachine,
            int stateNumber,
            string methodId)
            where TStateMachine : IAsyncStateMachine;

        /// <summary>
        /// Called at MoveNext start to check if restoration needed.
        /// </summary>
        bool TryRestore<TStateMachine>(
            ref TStateMachine stateMachine,
            string methodId,
            out int restoredState)
            where TStateMachine : IAsyncStateMachine;

        /// <summary>
        /// Called when async method completes successfully.
        /// </summary>
        void Complete(string methodId, object? result);

        /// <summary>
        /// Called when async method faults.
        /// </summary>
        void Fault(string methodId, Exception exception);
    }
}
```

---

## Persistence Service Design

### Memory-Based Implementation (Phase 1)

```csharp
public class InMemoryAsyncPersistenceService : IAsyncPersistenceService
{
    private readonly ConcurrentDictionary<string, StateMachineSnapshot> _snapshots = new();

    // Events for observability
    public event EventHandler<CheckpointEventArgs>? OnCheckpoint;
    public event EventHandler<RestoreEventArgs>? OnRestore;
    public event EventHandler<CompleteEventArgs>? OnComplete;

    public void Checkpoint<TStateMachine>(
        ref TStateMachine stateMachine,
        int stateNumber,
        string methodId)
        where TStateMachine : IAsyncStateMachine
    {
        var snapshot = SerializeStateMachine(ref stateMachine, stateNumber);
        _snapshots[methodId] = snapshot;
        OnCheckpoint?.Invoke(this, new CheckpointEventArgs(methodId, stateNumber, snapshot));
    }

    public bool TryRestore<TStateMachine>(
        ref TStateMachine stateMachine,
        string methodId,
        out int restoredState)
        where TStateMachine : IAsyncStateMachine
    {
        if (_snapshots.TryGetValue(methodId, out var snapshot))
        {
            DeserializeStateMachine(ref stateMachine, snapshot);
            restoredState = snapshot.State;
            OnRestore?.Invoke(this, new RestoreEventArgs(methodId, restoredState));
            return true;
        }
        restoredState = -1;
        return false;
    }

    private StateMachineSnapshot SerializeStateMachine<T>(ref T sm, int state)
        where T : IAsyncStateMachine
    {
        // Use reflection to extract all fields
        var type = typeof(T);
        var fields = new Dictionary<string, object?>();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            // Skip awaiter fields and builder
            if (field.Name.Contains("__awaiter") || field.Name.Contains("__builder"))
                continue;

            var value = field.GetValue(sm);
            fields[field.Name] = value;
        }

        return new StateMachineSnapshot
        {
            State = state,
            TypeName = type.AssemblyQualifiedName!,
            Fields = fields,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
```

### Orleans-Based Implementation (Phase 2)

```csharp
public class OrleansAsyncPersistenceService : IAsyncPersistenceService
{
    private readonly IGrainFactory _grainFactory;

    public void Checkpoint<TStateMachine>(...)
    {
        var grain = _grainFactory.GetGrain<IAsyncStateGrain>(methodId);
        await grain.SaveCheckpointAsync(snapshot);
    }
}

public interface IAsyncStateGrain : IGrainWithStringKey
{
    Task SaveCheckpointAsync(StateMachineSnapshot snapshot);
    Task<StateMachineSnapshot?> LoadCheckpointAsync();
    Task ClearAsync();
}
```

---

## Test Scenario Design

### Scenario Structure

```
playground/
├── AsyncPersistenceScenarios/
│   ├── AsyncPersistenceScenarios.csproj
│   ├── Program.cs                    # Main menu
│   ├── Scenarios/
│   │   ├── BasicCheckpoint.cs        # Challenge 1: Simple checkpoint/resume
│   │   ├── MultipleAwaits.cs         # Challenge 2: Multiple await points
│   │   ├── NestedAsync.cs            # Challenge 3: Async calling async
│   │   ├── ExceptionHandling.cs      # Challenge 4: Try/catch across awaits
│   │   ├── LoopsAndConditions.cs     # Challenge 5: Loops with awaits
│   │   ├── ProcessShutdownResume.cs  # Challenge 6: Actual process restart
│   │   └── OrleansIntegration.cs     # Challenge 7: Orleans grain persistence
│   └── TestWorkflows/
│       └── SampleWorkflows.cs        # Test async methods
```

### Sub-Menu for Scenario

```
╔══════════════════════════════════════════════════════════╗
║         ASYNC PERSISTENCE SCENARIO                        ║
╠══════════════════════════════════════════════════════════╣
║  1. Run Fresh (no persistence)                           ║
║  2. Run with Checkpointing (observe checkpoints)         ║
║  3. Run and Interrupt (simulate crash)                   ║
║  4. Resume from Last Checkpoint                          ║
║  5. View Persisted State                                 ║
║  6. Clear Persisted State                                ║
║  7. Back to Main Menu                                    ║
╚══════════════════════════════════════════════════════════╝
```

### Challenge Progression

#### Challenge 1: Basic Checkpoint
```csharp
[Persistable]
public async Task<int> BasicWorkflow(int input)
{
    Console.WriteLine($"Step 1: input = {input}");
    var step1 = await DelayAndReturn(input * 2);  // CHECKPOINT HERE

    Console.WriteLine($"Step 2: step1 = {step1}");
    var step2 = await DelayAndReturn(step1 + 10); // CHECKPOINT HERE

    Console.WriteLine($"Result: {step2}");
    return step2;
}
```

**Test**:
1. Run, observe checkpoints at each await
2. Interrupt between step 1 and step 2
3. Resume - should continue from step 2 without re-running step 1

#### Challenge 2: Multiple Types in State
```csharp
[Persistable]
public async Task<OrderResult> ProcessOrder(Order order)
{
    var validation = await ValidateAsync(order);     // Complex object
    var customer = await GetCustomerAsync(order.CustomerId);  // Another type
    var total = await CalculateTotalAsync(order, customer);   // Depends on both
    return new OrderResult { Total = total, Customer = customer };
}
```

**Test**: Serialize/deserialize complex types correctly

#### Challenge 3: Nested Async Calls
```csharp
[Persistable]
public async Task<int> OuterWorkflow(int x)
{
    var a = await InnerWorkflow(x);     // What happens here?
    var b = await InnerWorkflow(a);
    return b;
}

[Persistable]
public async Task<int> InnerWorkflow(int x)
{
    return await DelayAndReturn(x * 2);
}
```

**Test**: Each async method has independent persistence

#### Challenge 4: Exception Handling
```csharp
[Persistable]
public async Task<int> WorkflowWithTryCatch(int input)
{
    try
    {
        var step1 = await MayFailAsync(input);
        return step1;
    }
    catch (Exception ex)
    {
        var fallback = await FallbackAsync(input);  // Checkpoint in catch?
        return fallback;
    }
}
```

**Test**: State includes exception handling context

#### Challenge 5: Loops
```csharp
[Persistable]
public async Task<int> LoopWorkflow(int iterations)
{
    int sum = 0;
    for (int i = 0; i < iterations; i++)
    {
        sum += await DelayAndReturn(i);  // Multiple checkpoints, same await
    }
    return sum;
}
```

**Test**: Loop variable `i` and `sum` correctly persisted

#### Challenge 6: Process Restart
- Actually terminate the process mid-workflow
- Restart process
- Resume from persisted state

#### Challenge 7: Orleans Integration
- Persist to Orleans grain state
- Works across silo restart

---

## Custom Roslyn Compiler Integration

### Option 1: Programmatic Compilation (RECOMMENDED for Prototype)

```csharp
public class PersistableAsyncCompiler
{
    public Assembly CompileAndLoad(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // Use our modified Roslyn
        var compilation = CSharpCompilation.Create(
            "DynamicWorkflow",
            new[] { syntaxTree },
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithAsyncPersistence(true));  // Our new option

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
            throw new CompilationException(result.Diagnostics);

        ms.Seek(0, SeekOrigin.Begin);
        return AssemblyLoadContext.Default.LoadFromStream(ms);
    }
}
```

**Scenario Integration**:
```csharp
// In scenario:
var compiler = new PersistableAsyncCompiler();
var workflowCode = File.ReadAllText("TestWorkflows/BasicWorkflow.cs");
var assembly = compiler.CompileAndLoad(workflowCode);
var workflowType = assembly.GetType("TestWorkflows.BasicWorkflow");
var method = workflowType.GetMethod("RunAsync");
```

### Option 2: Custom MSBuild Tasks (Production)

```xml
<Project>
  <PropertyGroup>
    <UseDOTNExTCompiler>true</UseDOTNExTCompiler>
  </PropertyGroup>

  <Target Name="UseDOTNExTRoslyn" BeforeTargets="CoreCompile">
    <PropertyGroup>
      <CscToolPath>$(DOTNExTSDKPath)/compiler</CscToolPath>
    </PropertyGroup>
  </Target>
</Project>
```

### Option 3: dotnet CLI Integration

Requires building a complete SDK, more complex.

### Recommendation for Prototype

**Use programmatic compilation**:
1. Scenario loads workflow source as text
2. Compiles using modified Roslyn in-process
3. Loads resulting assembly
4. Executes workflow

This avoids the complexity of toolchain integration while proving the concept.

---

## Next Steps

1. **Create test scenario project** with menu structure
2. **Implement InMemoryAsyncPersistenceService** with full observability
3. **Create simple test workflows** for each challenge
4. **Modify Roslyn** to inject persistence calls (conditional on attribute)
5. **Build programmatic compilation wrapper**
6. **Test each challenge progressively**

---

## Open Questions

1. **Awaiter Serialization**: Some awaiters can't be serialized. How to handle?
   - Answer: Don't serialize awaiters. On resume, the await must be re-evaluated.

2. **`this` Reference**: How to restore instance method's `this`?
   - Answer: Store identifier (e.g., grain ID), not object. Resolve on resume.

3. **Thread Context**: ExecutionContext, SynchronizationContext?
   - Answer: These flow automatically when MoveNext is called.

4. **Determinism**: What if awaited task produces different result on replay?
   - Answer: This is by design. Only state is persisted, not results. The resumed workflow will use new results.

---

*This document captures research findings. Implementation proceeds from here.*
