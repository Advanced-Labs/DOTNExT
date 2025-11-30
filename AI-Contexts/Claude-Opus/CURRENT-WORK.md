# Current Work: Async State Machine Persistence

**Status**: Phase 3 IN PROGRESS - NewOrleans.AsyncPlus library created, Challenge 8 ready
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

### 🚧 Phase 3: Orleans Integration (IN PROGRESS)
See: `NewOrleans-AsyncPlus-Integration.md` for full design

**Goal**: Replace in-memory persistence with Orleans-backed persistence

**Completed**:
- ✅ `NewOrleans.AsyncPlus` library created at `src/NewOrleans/src/NewOrleans.AsyncPlus/`
- ✅ `IAsyncStatePersistenceGrain` grain interface with checkpoint/restore/complete/fault
- ✅ `AsyncStatePersistenceGrain` implementation with `IPersistentState<T>` for RavenDB
- ✅ `NewOrleansAsyncPersistenceService` with tracked tasks for sync-to-async bridge
- ✅ `DOTNExT.Persistence` namespace as canonical location for Roslyn-generated code
- ✅ `UseAsyncPlusPersistence()` silo builder extension
- ✅ Challenge 8 added to AsyncPersistenceScenarios

**Remaining**:
- [ ] RavenDB storage provider integration (currently uses memory storage placeholder)
- [ ] Test full workflow: Start silo → Run workflow → Verify grain persistence
- [ ] Add restore-after-crash test (simulate process restart)

---

## Files Modified/Created

### Roslyn (src/roslyn/)
- `src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs`
  - Added `_enablePersistence`, `_persistenceMethodId`, `_persistenceServiceLocal` fields
  - Added `InitializePersistenceServiceLocal()` - creates local BEFORE VisitBody
  - Added `GeneratePersistenceRestorationCheck()` - restoration at MoveNext start
  - Added `GenerateCheckpointCall()` - checkpoint before await
  - Modified `GenerateMoveNext()` and `GenerateAwaitForIncompleteTask()`

### NewOrleans.AsyncPlus Library (src/NewOrleans/src/NewOrleans.AsyncPlus/)
- `NewOrleans.AsyncPlus.csproj` - Library project with Orleans references
- `Abstractions/IAsyncStatePersistenceGrain.cs` - Grain interface
- `Abstractions/AsyncStateCheckpoint.cs` - DTOs with Orleans serialization
- `Abstractions/DOTNExTPersistence.cs` - Canonical `DOTNExT.Persistence` types
- `Grains/AsyncStatePersistenceGrain.cs` - Grain implementation
- `Services/NewOrleansAsyncPersistenceService.cs` - Orleans-backed persistence service
- `Extensions/AsyncPlusHostingExtensions.cs` - DI configuration

### AsyncPersistenceScenarios (src/NewOrleans/playground/AsyncPersistenceScenarios/)
- `Program.cs` - Menu-driven scenario runner, "Run All with Report", Challenge 8
- `AsyncPersistenceScenarios.csproj` - Added Orleans and AsyncPlus references
- `Services/IAsyncPersistenceService.cs` - Agnostic persistence interface
- `Services/InMemoryAsyncPersistenceService.cs` - Memory impl with events
- `Services/AsyncPersistenceContext.cs` - Ambient context for Roslyn code
- `Services/PersistableAsyncCompiler.cs` - Dynamic Roslyn compilation
- `TestWorkflows/BasicWorkflows.cs` - Test workflows
- `TestWorkflows/InstrumentedWorkflow.cs` - Hand-written state machine demo

---

## Next Steps (Phase 3 Remaining)

1. **RavenDB Storage Provider**:
   - Either use existing Orleans.Persistence.RavenDB package
   - Or create custom provider following Orleans storage provider pattern
   - Configure with `AddRavenDbGrainStorage("AsyncPlusStorage", ...)`

2. **End-to-End Testing**:
   - Run Challenge 8 with memory storage first
   - Verify checkpoints appear in grain state
   - Switch to RavenDB and verify documents in database

3. **Restore Testing**:
   - Stop silo mid-workflow
   - Restart silo
   - Resume workflow from checkpoint

---

## Self-Prompting for Next Session

**If you're a new Claude instance reading this:**

1. **Roslyn modification is WORKING** - Challenge 7 verified!
2. **NewOrleans.AsyncPlus library is CREATED** - All core components in place
3. **Challenge 8 is READY** - Just needs testing
4. **Next task**: Run Challenge 8, integrate RavenDB storage provider

**Library Location**: `src/NewOrleans/src/NewOrleans.AsyncPlus/`

**Key Components Already Created**:
- `IAsyncStatePersistenceGrain` - grain interface for state storage
- `AsyncStatePersistenceGrain` - uses `IPersistentState<T>` for Orleans storage
- `NewOrleansAsyncPersistenceService` - implements `IAsyncPersistenceService` using grains
- `DOTNExT.Persistence` namespace - canonical types for Roslyn-generated code
- Challenge 8 in `AsyncPersistenceScenarios/Program.cs`

**Don't**:
- Re-implement Roslyn modification (it's done!)
- Re-create the AsyncPlus library (it exists!)
- Change the core interfaces (they work)

**Do**:
- Test Challenge 8: Start silo → Run workflow → Verify checkpoints
- Add RavenDB storage provider configuration
- Test restore-after-crash scenario

---

*This document should be updated as implementation progresses.*
