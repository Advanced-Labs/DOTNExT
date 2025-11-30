# Current Work: Async State Machine Persistence

**Status**: Phase 2 COMPLETE - Roslyn working! Phase 3 (Orleans Integration) starting
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

### 🚧 Phase 3: Orleans Integration (STARTING)
See: `NewOrleans-AsyncPlus-Integration.md` for full design

**Goal**: Replace in-memory persistence with Orleans-backed persistence

**Key Components**:
1. `IAsyncStatePersistenceGrain` - Orleans grain for state storage
2. `NewOrleansAsyncPersistenceService` - implements `IAsyncPersistenceService` using grains
3. `UseAsyncPlusPersistence()` - silo builder extension
4. **RavenDB storage** - real durable persistence (in-memory defeats the purpose)

---

## Files Modified/Created

### Roslyn (src/roslyn/)
- `src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs`
  - Added `_enablePersistence`, `_persistenceMethodId`, `_persistenceServiceLocal` fields
  - Added `InitializePersistenceServiceLocal()` - creates local BEFORE VisitBody
  - Added `GeneratePersistenceRestorationCheck()` - restoration at MoveNext start
  - Added `GenerateCheckpointCall()` - checkpoint before await
  - Modified `GenerateMoveNext()` and `GenerateAwaitForIncompleteTask()`

### AsyncPersistenceScenarios (src/NewOrleans/playground/AsyncPersistenceScenarios/)
- `Program.cs` - Menu-driven scenario runner, "Run All with Report"
- `Services/IAsyncPersistenceService.cs` - Agnostic persistence interface
- `Services/InMemoryAsyncPersistenceService.cs` - Memory impl with events
- `Services/AsyncPersistenceContext.cs` - Ambient context for Roslyn code
- `Services/PersistableAsyncCompiler.cs` - Dynamic Roslyn compilation
- `TestWorkflows/BasicWorkflows.cs` - Test workflows
- `TestWorkflows/InstrumentedWorkflow.cs` - Hand-written state machine demo

---

## Next Steps (Phase 3)

1. **Add Orleans references to AsyncPersistenceScenarios.csproj**:
   ```xml
   <ProjectReference Include="..\..\src\Orleans.Server\Orleans.Server.csproj" />
   <ProjectReference Include="..\..\src\Orleans.Runtime\Orleans.Runtime.csproj" />
   <OrleansBuildTimeCodeGen>true</OrleansBuildTimeCodeGen>
   ```

2. **Create grain interface and implementation**:
   - `Orleans/IAsyncStatePersistenceGrain.cs`
   - `Orleans/AsyncStatePersistenceGrain.cs`

3. **Create Orleans persistence service**:
   - `Services/NewOrleansAsyncPersistenceService.cs`

4. **Add Challenge 8**: Orleans-backed persistence test
   - Start Orleans silo
   - Run `[Persistable]` workflow with Orleans persistence
   - Verify checkpoints stored in grain state

---

## Self-Prompting for Next Session

**If you're a new Claude instance reading this:**

1. **Roslyn modification is WORKING** - Challenge 7 verified!
2. **Next task**: Orleans integration (Phase 3)
3. **Read**: `NewOrleans-AsyncPlus-Integration.md` for the full design
4. **Key insight**: User wants single-silo Orleans first, then distributed

**Don't**:
- Re-implement Roslyn modification (it's done!)
- Change the `IAsyncPersistenceService` interface (it works)
- Forget `<OrleansBuildTimeCodeGen>true</OrleansBuildTimeCodeGen>` for Orleans

**Do**:
- Follow the pattern in `PluginGrainScenarios/Program.cs` for Orleans host setup
- Use `SiloHelper.BuildSingleSilo()` pattern
- Add grain types for async state persistence
- Keep Async+ agnostic to Orleans (driver pattern)

---

*This document should be updated as implementation progresses.*
