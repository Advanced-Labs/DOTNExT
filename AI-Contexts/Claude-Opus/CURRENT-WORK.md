# Current Work: Async State Machine Persistence

**Status**: Phase 3 COMPLETE + Option A Implemented (Awaiting Build Verification)
**Last Updated**: 2025-12-01
**Branch**: `claude/review-orleans-changes-01NupGvm45sCJfU1V2Newo9X`

---

## What We're Building

**Goal**: Automatic persistence of async state machines for:
1. Pause/resume workflows
2. Crash recovery
3. Distributed execution (via Orleans)

**Approach**: Modify Roslyn to inject persistence calls into generated async state machines.

---

## 🎉 C1 Cross-Session Persistence NOW WORKING! (2025-12-01)

**Scenario C1 verified:**
- Input: 42
- Expected result: 94 (42×2 + 10)
- **Actual result: 94** ✅

The cross-session persistence demonstrates:
- Checkpoint survives silo restart
- State machine fields correctly restored
- Workflow resumes from checkpoint state

### Bugs Fixed in This Session

| # | Bug | Root Cause | Fix |
|---|-----|------------|-----|
| 1 | StateNumber=0 deserialized as -1 | Orleans JSON uses `DefaultValueHandling.Ignore`, skipping `StateNumber=0` | Added `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]` to `StateNumber` |
| 2 | NullReferenceException on restore | Restoration jumped to `Label_AwaitPointN` expecting awaiter result, but awaiter wasn't serialized | Added `justRestored` flag to jump to `Label_StartOpN` and re-run async operation |
| 3 | Stale checkpoint after ClearAsync | `ClearStateAsync()` only clears storage, leaves in-memory state intact | Reset all `_state.State` fields to defaults after `ClearStateAsync()` |
| 4 | Restored values lost (struct boxing) | Struct passed as `object` gets boxed; `SetValue` modifies copy, not original | Changed `InstrumentedSimpleWorkflow_StateMachine` from `struct` to `class` |
| 5 | Compilation errors from #4 | `ref this` not allowed in class, non-nullable field | Store `this` in local var for `ref`, initialize `workflowId = ""` |

---

## ✅ Struct Boxing Fix: Option A Implemented (2025-12-01)

**Previous Problem**: Bug #4 (struct boxing) was "fixed" in hand-coded test by changing struct to class. But real Roslyn generates **structs**.

**Solution Status**: ✅ **Option A IMPLEMENTED** - Generic `TryRestore<T>(ref T, string)` method added
- Interface updated with generic method
- NewOrleansAsyncPersistenceService implements it
- Roslyn codegen updated to use `F.CurrentType` for generic method construction
- **Awaiting TAI verification** that Roslyn build succeeds

### Root Cause Analysis

Current Roslyn+ generates (in `AsyncMethodToStateMachineRewriter.cs` line 877):
```csharp
F.Convert(F.SpecialType(SpecialType.System_Object), F.This())
```

Which produces:
```csharp
var restoredState = _persistenceService.TryRestore((object)this, methodId);
```

For struct state machines:
1. `(object)this` **boxes** the struct
2. `TryRestore` deserializes into the boxed copy
3. Original struct fields remain **unchanged**
4. Restoration fails silently

### Three Fix Options Analyzed

#### Option A: Pass by Ref (Generic Method) ✅ RECOMMENDED

**Interface Change:**
```csharp
public interface IAsyncPersistenceService
{
    void Checkpoint(object stateMachine, int stateNumber, string methodId);
    int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId);
    void Complete(string methodId, object? result);
    void Fault(string methodId, Exception exception);
}
```

**Roslyn Would Emit:**
```csharp
var restoredState = _persistenceService.TryRestore(ref this, methodId);
if (restoredState >= 0)
{
    __state = restoredState;
    // 'this' is already updated by TryRestore
}
```

**Implementation:**
```csharp
public int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId)
{
    var checkpoint = GetCheckpointSync(methodId);
    if (checkpoint == null) return -1;

    // Deserialize and assign directly to ref parameter
    stateMachine = JsonSerializer.Deserialize<TStateMachine>(checkpoint.Data)!;
    return checkpoint.StateNumber;
}
```

| Pros | Cons |
|------|------|
| Most efficient - no boxing, no copying | Generic method on interface adds complexity |
| Clean semantics - `ref` clearly indicates mutation | `ref this` works for structs but NOT for classes (CS1605) |
| Single atomic operation | Test code using class state machines needs workaround |
| Type-safe deserialization | Interface becomes harder to mock in tests |
| Works naturally with struct state machines | |

**Class Workaround (for hand-coded tests):**
```csharp
// For class-based state machines (test only):
var sm = this;
var restoredState = _persistenceService.TryRestore(ref sm, methodId);
// Then copy fields from sm to this (ugly but test-only)
```

---

#### Option B: Return Restored Value

**Interface Change:**
```csharp
public interface IAsyncPersistenceService
{
    void Checkpoint(object stateMachine, int stateNumber, string methodId);
    (int stateNumber, TStateMachine? restored) TryRestore<TStateMachine>(string methodId);
    void Complete(string methodId, object? result);
    void Fault(string methodId, Exception exception);
}
```

**Roslyn Would Emit:**
```csharp
var (restoredState, restored) = _persistenceService.TryRestore<StateMachineType>(methodId);
if (restoredState >= 0)
{
    this = restored!;  // Struct assignment - copies all fields
    __state = restoredState;
}
```

**Implementation:**
```csharp
public (int, TStateMachine?) TryRestore<TStateMachine>(string methodId)
{
    var checkpoint = GetCheckpointSync(methodId);
    if (checkpoint == null) return (-1, default);

    var restored = JsonSerializer.Deserialize<TStateMachine>(checkpoint.Data);
    return (checkpoint.StateNumber, restored);
}
```

| Pros | Cons |
|------|------|
| Clear semantics - pure function returns new value | `this = value` only works in structs, not classes |
| No ref parameter complexity | Requires nullable return type handling |
| Roslyn codegen is straightforward | Extra tuple allocation (minor) |
| Type-safe | **Cannot support class state machines at all** |

**Critical Limitation:** `this = value` is a compile error in class instance methods. This approach ONLY works for structs.

---

#### Option C: Return Field Dictionary

**Interface Change:**
```csharp
public interface IAsyncPersistenceService
{
    void Checkpoint(object stateMachine, int stateNumber, string methodId);
    (int stateNumber, IReadOnlyDictionary<string, object>? fields) TryRestore(string methodId);
    void Complete(string methodId, object? result);
    void Fault(string methodId, Exception exception);
}
```

**Roslyn Would Emit:**
```csharp
var (restoredState, fields) = _persistenceService.TryRestore(methodId);
if (restoredState >= 0)
{
    // Roslyn emits one assignment per field
    this.input = (int)fields["input"];
    this.<>1__state = (int)fields["<>1__state"];
    this.<step1>5__1 = (int)fields["<step1>5__1"];
    this.<step2>5__2 = (int)fields["<step2>5__2"];
    // ... etc for all hoisted locals
}
```

**Implementation:**
```csharp
public (int, IReadOnlyDictionary<string, object>?) TryRestore(string methodId)
{
    var checkpoint = GetCheckpointSync(methodId);
    if (checkpoint == null) return (-1, null);

    // Deserialize to dictionary
    var fields = JsonSerializer.Deserialize<Dictionary<string, object>>(checkpoint.Data);
    return (checkpoint.StateNumber, fields);
}
```

| Pros | Cons |
|------|------|
| Works for both structs AND classes | Boxing for each field value |
| No generic method needed | Runtime type casting per field |
| Interface stays simple | Performance overhead (dictionary lookup per field) |
| Non-generic, easier to mock | **Complex Roslyn codegen** - must emit N assignments |
| | Field name strings must match exactly (fragile) |
| | Compiler-generated names like `<>1__state` are tricky |

---

### Comparison Matrix

| Criterion | Option A (ref) | Option B (return) | Option C (dict) |
|-----------|---------------|-------------------|-----------------|
| **Struct support** | ✅ Full | ✅ Full | ✅ Full |
| **Class support** | ⚠️ Workaround | ❌ None | ✅ Full |
| **Performance** | ⭐⭐⭐ Best | ⭐⭐ Good | ⭐ Worst |
| **Interface simplicity** | ⭐⭐ Generic | ⭐⭐ Generic | ⭐⭐⭐ Simple |
| **Roslyn codegen complexity** | ⭐⭐⭐ Simple | ⭐⭐ Moderate | ⭐ Complex |
| **Type safety** | ⭐⭐⭐ Full | ⭐⭐⭐ Full | ⭐ Cast at runtime |
| **Future maintenance** | ⭐⭐⭐ Easy | ⭐⭐ Medium | ⭐ Field names brittle |

### Decision: Option A (Pass by ref)

**Rationale:**
1. Real Roslyn generates structs - `ref this` works perfectly
2. Best performance - no boxing, no copying, no dictionary overhead
3. Simplest Roslyn codegen - just change the call to use `ref this`
4. Type-safe deserialization
5. Class support limitation is acceptable since production code uses structs

---

## 🎉 MAJOR MILESTONE: Roslyn Modification WORKING! (2025-11-30)

**Challenge 7 verified:**
```
[[Persistable]] checkpoints created: 2
Non-Persistable checkpoints created: 0
*** MODIFIED ROSLYN VERIFIED: [[Persistable]] has checkpoints, Non-Persistable does not ***
```

The modified Roslyn compiler (v42.42.42.42) now:
- Detects `[Persistable]` attribute on async methods
- Injects `AsyncPersistenceContext.Current` checkpoint calls at each await
- Injects state restoration check at MoveNext start
- Only affects `[Persistable]` methods - others unchanged

**Bug Fixed**: `_persistenceServiceLocal` was created AFTER `VisitBody()` processed awaits.
Fixed by adding `InitializePersistenceServiceLocal()` called BEFORE `VisitBody()`.

---

## Current State Summary

### ✅ Phase 1: Test Framework (COMPLETE)
- `AsyncPersistenceScenarios` project with Spectre.Console menu
- `IAsyncPersistenceService` interface
- `InMemoryAsyncPersistenceService` with events and JSON file backing
- 5 test workflows (Simple, Order, Nested, Exception, Loop)

### ✅ Phase 2: Roslyn Modification (COMPLETE)
- Modified `AsyncMethodToStateMachineRewriter.cs`
- `[Persistable]` attribute detection
- Checkpoint injection before await suspension
- State restoration check at MoveNext start
- Challenge 6: Hand-written instrumented state machine (validates approach)
- Challenge 7: Dynamic compilation with modified Roslyn (WORKING!)
- "Run All with Report" feature for comprehensive testing

### ✅ Phase 3: Orleans Integration (COMPLETE)
See: `NewOrleans-AsyncPlus-Integration.md` for full design

**Goal**: Replace in-memory persistence with Orleans-backed persistence

**Completed**:
- ✅ `NewOrleans.AsyncPlus` library created at `src/NewOrleans/src/NewOrleans.AsyncPlus/`
- ✅ `IAsyncStatePersistenceGrain` grain interface with checkpoint/restore/complete/fault
- ✅ `AsyncStatePersistenceGrain` implementation with `IPersistentState<T>`
- ✅ `NewOrleansAsyncPersistenceService` with tracked tasks for sync-to-async bridge
- ✅ `DOTNExT.Persistence` namespace as canonical location for Roslyn-generated code
- ✅ `UseAsyncPlusPersistence()` silo builder extension
- ✅ `RavenDbGrainStorage` - Custom Orleans storage provider for RavenDB
- ✅ `AddRavenDbGrainStorage()` and `UseAsyncPlusPersistenceWithRavenDb()` extensions
- ✅ Challenge 8 added to AsyncPersistenceScenarios (menu + Run All)
- ✅ RavenDB.Client package added to Directory.Packages.props

---

## TAI (Test AI) Instructions

### Prerequisites
1. **RavenDB** running on `http://localhost:8080` (standard port)
2. **Modified Roslyn** built in `src/roslyn/`

### Build Steps
```bash
cd /home/user/DOTNExT

# 1. Build modified Roslyn first
cd src/roslyn
dotnet build src/Compilers/CSharp/Portable/Microsoft.CodeAnalysis.CSharp.csproj

# 2. Build NewOrleans.AsyncPlus library
cd ../NewOrleans/src/NewOrleans.AsyncPlus
dotnet build

# 3. Build AsyncPersistenceScenarios
cd ../../playground/AsyncPersistenceScenarios
dotnet build
```

### Test Commands
```bash
# Run the test scenarios
cd /home/user/DOTNExT/src/NewOrleans/playground/AsyncPersistenceScenarios
dotnet run
```

### Test Sequence
1. **"★★★ Run All with Report"** - Runs Challenges 1-8 automatically
   - Expected: All 8 challenges PASS
   - Challenge 7: Should show "MODIFIED ROSLYN VERIFIED"
   - Challenge 8: Should show Orleans checkpoints created

2. **Challenge 8 Manual Testing**:
   - Select "8. ★★★ Orleans/RavenDB Persistence"
   - "Start Silo with MemoryStorage" (quick test)
   - "Run [Persistable] Workflow on Orleans"
   - Verify "Orleans Checkpoint" messages appear
   - "View Grain State" - should show persisted state
   - "Stop Silo"

3. **RavenDB Integration Test** (if RavenDB running):
   - "Start Silo with RavenDB Storage"
   - "Run [Persistable] Workflow on Orleans"
   - Check RavenDB Studio: `http://localhost:8080` → Database "AsyncPersistenceTest"
   - Documents should appear under `orleans/async-persistence-test/grains/...`

### Expected Output (Run All Report)
```
Challenge 1: SimpleWorkflow              PASS       0
Challenge 2: ProcessOrderWorkflow        PASS       0
Challenge 3: OuterWorkflow               PASS       0
Challenge 4: ExceptionHandling           PASS       0
Challenge 5: LoopWorkflow                PASS       0
Challenge 6: InstrumentedStateMachine    PASS       2
Challenge 7: DynamicCompilation          PASS       2
Challenge 8: Orleans Persistence         PASS       2
```

Note: Challenges 1-5 show 0 checkpoints because they're NOT using [Persistable]
      and NOT using AsyncPersistenceContext. Challenges 6-8 use it properly.

---

## Files Modified/Created

### Roslyn (src/roslyn/)
- `src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs`

### NewOrleans.AsyncPlus Library (src/NewOrleans/src/NewOrleans.AsyncPlus/)
- `NewOrleans.AsyncPlus.csproj` - Library project with Orleans + RavenDB references
- `Abstractions/IAsyncStatePersistenceGrain.cs` - Grain interface
- `Abstractions/AsyncStateCheckpoint.cs` - DTOs with Orleans serialization
- `Abstractions/DOTNExTPersistence.cs` - Canonical `DOTNExT.Persistence` types
- `Grains/AsyncStatePersistenceGrain.cs` - Grain implementation
- `Services/NewOrleansAsyncPersistenceService.cs` - Orleans-backed persistence service
- `Extensions/AsyncPlusHostingExtensions.cs` - DI configuration + RavenDB extensions
- `Storage/RavenDbGrainStorage.cs` - RavenDB Orleans storage provider
- `Storage/RavenDbStorageOptions.cs` - RavenDB configuration options

### AsyncPersistenceScenarios (src/NewOrleans/playground/AsyncPersistenceScenarios/)
- `Program.cs` - Menu-driven scenario runner with Challenge 8
- `AsyncPersistenceScenarios.csproj` - Added Orleans and AsyncPlus references

### Directory.Packages.props
- Added `RavenDB.Client` version 6.0.105

---

## Key Extension Methods

```csharp
// Simple: Memory storage
siloBuilder.AddMemoryGrainStorage("AsyncPlusStorage")
           .UseAsyncPlusPersistence("AsyncPlusStorage");

// RavenDB storage (separate calls)
siloBuilder.AddRavenDbGrainStorage("AsyncPlusStorage", options => {
               options.Urls = new[] { "http://localhost:8080" };
               options.DatabaseName = "MyDatabase";
           })
           .UseAsyncPlusPersistence("AsyncPlusStorage");

// RavenDB storage (convenience method)
siloBuilder.UseAsyncPlusPersistenceWithRavenDb(options => {
               options.Urls = new[] { "http://localhost:8080" };
               options.DatabaseName = "AsyncPersistenceTest";
           });
```

---

## Troubleshooting

### "No checkpoints created"
- Ensure using modified Roslyn (check version 42.42.42.42)
- Ensure `AsyncPersistenceContext.SetCurrent()` is called
- Ensure method has `[Persistable]` attribute

### "RavenDB connection failed"
- Check RavenDB is running: `curl http://localhost:8080`
- Check database name matches configuration
- Check firewall/network settings

### "Orleans silo won't start"
- Check ports 11112 and 30001 are not in use
- Kill any previous silo processes
- Check Orleans logs for specific errors

---

*This document should be updated as implementation progresses.*
