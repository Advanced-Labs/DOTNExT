# Current Work: Async State Machine Persistence

**Status**: Phase 3 COMPLETE - Ready for Testing
**Last Updated**: 2025-11-30
**Branch**: `claude/review-orleans-docs-01Laga2PuCwyirCKG8tmsCw3`

---

## What We're Building

**Goal**: Automatic persistence of async state machines for:
1. Pause/resume workflows
2. Crash recovery
3. Distributed execution (via Orleans)

**Approach**: Modify Roslyn to inject persistence calls into generated async state machines.

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
