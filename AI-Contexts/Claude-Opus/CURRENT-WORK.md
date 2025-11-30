# Current Work: Async State Machine Persistence

**Status**: Research complete, ready for implementation
**Last Updated**: 2025-11-28
**Branch**: `claude/review-orleans-docs-01Laga2PuCwyirCKG8tmsCw3`

---

## What We're Building

**Goal**: Automatic persistence of async state machines for:
1. Pause/resume workflows
2. Crash recovery
3. Distributed execution (future)

**Approach**: Modify Roslyn to inject persistence calls into generated async state machines.

---

## Current State

### Completed Research

1. **Roslyn internals analyzed** - see `AsyncPersistence-Research.md`
   - Key files: `AsyncRewriter.cs`, `AsyncMethodToStateMachineRewriter.cs`
   - State machine structure understood
   - Modification points identified

2. **Implementation approach chosen**: Modify Roslyn (Option B)
   - Inject checkpoint calls before each await
   - Inject restoration check at MoveNext start
   - Conditional on `[Persistable]` attribute

3. **Persistence service designed**: `IAsyncPersistenceService`
   - DI-injected (null = no persistence)
   - Memory impl for Phase 1
   - Orleans impl for Phase 2

4. **Test scenario designed**: 7 progressive challenges
   - BasicCheckpoint → OrleansIntegration

---

## Immediate Next Steps

### Step 1: Create Test Project Structure

```bash
# In: /home/user/DOTNExT/src/NewOrleans/playground/

mkdir -p AsyncPersistenceScenarios/Scenarios
mkdir -p AsyncPersistenceScenarios/TestWorkflows
mkdir -p AsyncPersistenceScenarios/Services
```

Files to create:
- `AsyncPersistenceScenarios.csproj`
- `Program.cs` (main menu)
- `Services/IAsyncPersistenceService.cs`
- `Services/InMemoryAsyncPersistenceService.cs`
- `TestWorkflows/BasicWorkflows.cs`
- `Scenarios/BasicCheckpoint.cs`

### Step 2: Implement Persistence Service

```csharp
// Services/IAsyncPersistenceService.cs
namespace DOTNExT.Persistence;

public interface IAsyncPersistenceService
{
    void Checkpoint<TStateMachine>(
        ref TStateMachine stateMachine,
        int stateNumber,
        string methodId)
        where TStateMachine : IAsyncStateMachine;

    bool TryRestore<TStateMachine>(
        ref TStateMachine stateMachine,
        string methodId,
        out int restoredState)
        where TStateMachine : IAsyncStateMachine;

    void Complete(string methodId, object? result);
    void Fault(string methodId, Exception exception);

    // Observability
    event EventHandler<CheckpointEventArgs>? OnCheckpoint;
    event EventHandler<RestoreEventArgs>? OnRestore;
}
```

### Step 3: Create Test Workflows

```csharp
// TestWorkflows/BasicWorkflows.cs
public class BasicWorkflows
{
    [Persistable]
    public async Task<int> SimpleWorkflow(int input)
    {
        Console.WriteLine($"Step 1: input = {input}");
        var step1 = await Task.Delay(100).ContinueWith(_ => input * 2);

        Console.WriteLine($"Step 2: step1 = {step1}");
        var step2 = await Task.Delay(100).ContinueWith(_ => step1 + 10);

        Console.WriteLine($"Result: {step2}");
        return step2;
    }
}
```

### Step 4: Modify Roslyn (Core Work)

Files to modify:
1. `src/roslyn/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs`

Key changes:
- Add `_persistenceServiceField` field
- Modify `GenerateMoveNext()` to check for restoration at start
- Modify `GenerateAwaitForIncompleteTask()` to checkpoint before suspend

### Step 5: Build Programmatic Compiler

```csharp
// Services/PersistableAsyncCompiler.cs
public class PersistableAsyncCompiler
{
    public Assembly CompileAndLoad(string sourceCode, IAsyncPersistenceService? service)
    {
        // Use modified Roslyn to compile
        // Return loaded assembly
    }
}
```

---

## Key Roslyn Modification Points

### In `AsyncMethodToStateMachineRewriter.cs`

**1. Add field** (around line 95):
```csharp
private readonly FieldSymbol? _persistenceServiceField;
```

**2. Modify `GenerateMoveNext`** (line 133):
```csharp
internal void GenerateMoveNext(BoundStatement body, MethodSymbol moveNextMethod)
{
    // ... existing code ...

    // ADD: Restoration check at start
    if (ShouldPersist())
    {
        bodyBuilder.Add(GenerateRestorationCheck());
    }

    // ... rest of method ...
}
```

**3. Modify `GenerateAwaitForIncompleteTask`** (line 446):
```csharp
private BoundBlock GenerateAwaitForIncompleteTask(...)
{
    // ADD: Checkpoint before state transition
    if (ShouldPersist())
    {
        blockBuilder.Add(GenerateCheckpointCall(stateNumber));
    }

    blockBuilder.Add(GenerateSetBothStates(stateNumber));
    // ... rest of method ...
}
```

---

## Test Scenario Menu Structure

```
╔══════════════════════════════════════════════════════════════════╗
║                 ASYNC PERSISTENCE SCENARIOS                       ║
╠══════════════════════════════════════════════════════════════════╣
║  1. Challenge 1: Basic Checkpoint                                 ║
║  2. Challenge 2: Multiple Types in State                          ║
║  3. Challenge 3: Nested Async Calls                               ║
║  4. Challenge 4: Exception Handling                               ║
║  5. Challenge 5: Loops with Awaits                                ║
║  6. Challenge 6: Process Shutdown/Resume                          ║
║  7. Challenge 7: Orleans Integration                              ║
║  ─────────────────────────────────────────────────────────────── ║
║  Exit                                                             ║
╚══════════════════════════════════════════════════════════════════╝
```

Each challenge has sub-menu:
```
╔══════════════════════════════════════════════════════════════════╗
║              CHALLENGE 1: BASIC CHECKPOINT                        ║
╠══════════════════════════════════════════════════════════════════╣
║  1. Run Fresh (no persistence)                                    ║
║  2. Run with Checkpointing (observe)                              ║
║  3. Run and Interrupt (simulate crash)                            ║
║  4. Resume from Checkpoint                                        ║
║  5. View Persisted State                                          ║
║  6. Clear Persisted State                                         ║
║  7. Back                                                          ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## Dependencies and References

- Existing playground: `/home/user/DOTNExT/src/NewOrleans/playground/PluginGrainScenarios/`
- Roslyn source: `/home/user/DOTNExT/src/roslyn/`
- NewOrleans docs: `/home/user/DOTNExT/src/NewOrleans/NewOrleans.md`

---

## Self-Prompting for Next Session

**If you're a new Claude instance reading this:**

1. You are implementing async state machine persistence for DOTNExT
2. Research is complete - see `AsyncPersistence-Research.md`
3. Implementation approach is: Modify Roslyn AsyncRewriter
4. Start with Step 1 above: Create test project structure
5. User wants single-silo Orleans persistence initially, then distributed

**Key context:**
- This is part of larger DOTNExT vision (see `DOTNExT-Vision.md`)
- The async persistence is "soft persistence" in the tiered model
- Eventually leads to distributed workflow execution

**What NOT to do:**
- Don't use Option A (custom builder without Roslyn mod) - too limited
- Don't try IL rewriting - too fragile
- Don't skip the memory-based impl - needed for testing

---

*This document should be updated as implementation progresses.*
