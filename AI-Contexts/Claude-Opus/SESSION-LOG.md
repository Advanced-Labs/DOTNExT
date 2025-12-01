# Session Log

## Session: 2025-11-28 (Initial)

### Context
User shared a conversation with GPT-5.1 about using async/await machinery for distributed computing. Asked for honest assessment.

### What Was Discussed

1. **Initial Assessment**: Reviewed GPT-5.1's analysis of async/await as distributed computing primitive
   - Confirmed technical soundness of core idea
   - Identified oversimplifications (serialization complexity, Orleans' actual async usage)
   - Created `AsyncDistributedComputing-Assessment.md`

2. **Vision Expansion**: User shared much grander vision:
   - Multiple persistence levels (soft/hard)
   - VM/CLR redesign with continuous bookkeeping
   - DOTNExT as Meta-OS (Android-like architecture)
   - C* as universal IL / transpilation target
   - Semantic memory concepts (Memantics, Affinitics, Synaptics)
   - Modular VM extensions (kernel-like)

3. **Version Management Strategy**: Critical insight from user
   - Don't hot-swap state machines mid-execution
   - Old states drain/abort with old code version
   - New calls route to new version
   - Explicit migration if needed

### Artifacts Created

| File | Purpose |
|------|---------|
| `AsyncDistributedComputing-Assessment.md` | Initial assessment of async/await idea |
| `DOTNExT-Vision.md` | Comprehensive vision document |
| `CONTINUATION-PROTOCOL.md` | Inter-context-window survival guide |
| `SESSION-LOG.md` | This file |

### Key Insights

1. The user's vision is much broader than "just" async/await for distribution
2. Evolution path: lib → codegen → VM is critical design principle
3. Context window management is priority - documentation must survive resets
4. AI collaboration is the key enabler - wouldn't be realistic otherwise

### What's Next

- [ ] Update async assessment with version drainage strategy
- [ ] Detailed analysis of specific implementation stages
- [ ] Roslyn exploration for augmentation points
- [ ] Prototype experiments

### Open Questions from This Session

1. Memory bookkeeping overhead - needs benchmarking
2. GC integration with OID-based references
3. C* language scope - what extensions needed
4. Semantic encoding approach for Memantics

---

## Session: 2025-11-28 (Continued)

### Context
User requested deep research into async/await persistence implementation - specifically how to:
- Modify Roslyn to inject persistence calls
- Design test scenarios for incremental challenges
- Integrate with Orleans for single-silo persistence
- Handle programmatic compilation with custom Roslyn

### What Was Discussed

1. **Roslyn Async Internals Research**
   - Analyzed `AsyncRewriter.cs` - entry point for async transformation
   - Analyzed `AsyncMethodToStateMachineRewriter.cs` - MoveNext generation
   - Analyzed `AsyncMethodBuilderMemberCollection.cs` - builder API
   - Documented state machine anatomy (fields, state values, hoisted locals)

2. **Implementation Options Evaluated**
   - Option A: Custom AsyncMethodBuilder (no Roslyn mod) - too limited
   - **Option B: Modify Roslyn AsyncRewriter (RECOMMENDED)** - full control
   - Option C: Source Generator + Custom Builder - complex
   - Option D: IL Rewriting - too fragile

3. **Persistence Service Design**
   - `IAsyncPersistenceService` interface with Checkpoint/TryRestore/Complete/Fault
   - Memory-based implementation for Phase 1 with full observability
   - Orleans-based implementation for Phase 2

4. **Test Scenario Design** - 7 progressive challenges:
   - Challenge 1: Basic checkpoint (simple await, interrupt, resume)
   - Challenge 2: Multiple types in state (complex objects)
   - Challenge 3: Nested async calls (independent persistence)
   - Challenge 4: Exception handling (try/catch across awaits)
   - Challenge 5: Loops with awaits
   - Challenge 6: Process restart (actual termination and resume)
   - Challenge 7: Orleans integration (grain state persistence)

5. **Custom Roslyn Compiler Integration**
   - **Recommended: Programmatic compilation** for prototype
   - Scenario loads workflow source, compiles with modified Roslyn, loads assembly
   - Avoids toolchain complexity while proving concept

### Artifacts Created

| File | Purpose |
|------|---------|
| `AsyncPersistence-Research.md` | Comprehensive research document with Roslyn analysis, implementation options, service design, scenario design |

### Key Insights

1. **Roslyn modification points are clear**:
   - `GenerateMoveNext` - add restoration check at start
   - `GenerateAwaitForIncompleteTask` - add checkpoint call before state transition
   - New `GenerateCheckpointCall` method needed

2. **State machine anatomy**:
   - State field (int): -1=running, -2=finished, 0+=await point
   - Hoisted locals as fields (anything alive across awaits)
   - Awaiter fields (transient, don't need to persist)

3. **Programmatic compilation is the right prototype approach**:
   - Avoids toolchain integration complexity
   - Tests the core concept
   - Can evolve to MSBuild integration later

### What's Next (Immediate)

- [ ] Create `AsyncPersistenceScenarios` project
- [ ] Implement `IAsyncPersistenceService` and memory impl
- [ ] Create basic test workflows
- [ ] Start Roslyn modification (conditional on `[Persistable]` attribute)
- [ ] Build programmatic compilation wrapper

### Open Questions

1. **Awaiter handling on resume**: Must re-await, can't serialize awaiter
2. **Instance method `this`**: Store identifier, resolve on resume
3. **Loop iteration**: Same await, different state - need unique keys

---

## Session: 2025-11-30 (Roslyn Working + Orleans Design)

### Context
Continued from previous session. The Roslyn modification was failing Challenge 7 (dynamic compilation) - no checkpoints were being created even though the modified Roslyn was loaded.

### What Was Discussed

1. **Diagnosed Challenge 7 Failure**
   - Added diagnostic output to Roslyn code to trace execution
   - Discovered: Types WERE being resolved correctly
   - But: `_persistenceServiceLocal` was null when `GenerateAwaitForIncompleteTask` was called

2. **Found Root Cause: Execution Order Bug**
   - `VisitBody(body)` was called BEFORE `GeneratePersistenceRestorationCheck()`
   - `VisitBody` processes await expressions, calling `GenerateAwaitForIncompleteTask`
   - But `_persistenceServiceLocal` was only created later in `GeneratePersistenceRestorationCheck`
   - Result: Checkpoint injection was skipped because local was null

3. **Fixed the Bug**
   - Added `InitializePersistenceServiceLocal()` method
   - Called it in `GenerateMoveNext()` BEFORE `VisitBody(body)`
   - Now `_persistenceServiceLocal` exists when await expressions are processed

4. **Challenge 7 Now Working!**
   ```
   [[Persistable]] checkpoints created: 2
   Non-Persistable checkpoints created: 0
   *** MODIFIED ROSLYN VERIFIED ***
   ```

5. **New Direction: Orleans Integration**
   - User wants to move from in-memory persistence to Orleans-backed
   - Designed `NewOrleans Async+ Driver` architecture
   - Uses grains for state storage, configurable via DI
   - Driver pattern allows future Async+ augmentations

### Artifacts Created/Modified

| File | Purpose |
|------|---------|
| `NewOrleans-AsyncPlus-Integration.md` | **NEW** - Full design for Orleans integration |
| `CURRENT-WORK.md` | Updated with Phase 2 complete, Phase 3 starting |
| `SESSION-LOG.md` | This entry |
| `AsyncMethodToStateMachineRewriter.cs` | Added `InitializePersistenceServiceLocal()`, fixed execution order |
| `Program.cs` | Added "Run All with Report" feature |

### Key Insights

1. **Execution order matters in Roslyn**: `VisitBody` processes the body and captures info into bound nodes. Any state needed during that traversal must be initialized first.

2. **Diagnostic output is essential**: The Console.Error.WriteLine statements in the Roslyn code were crucial for understanding the failure.

3. **Driver pattern for Async+**: Orleans integration should be a "driver" that implements `IAsyncPersistenceService`, keeping Async+ agnostic to Orleans.

### What's Next

1. Add Orleans references to AsyncPersistenceScenarios
2. Create `IAsyncStatePersistenceGrain` and grain implementation
3. Create `OrleansAsyncPersistenceService` that wraps grain calls
4. Add Challenge 8: Orleans-backed persistence test
5. (Optional) Clean up diagnostic output from Roslyn code

### Open Questions

1. **Sync-over-async**: The `IAsyncPersistenceService` interface has sync methods (because Roslyn generates sync calls), but Orleans grains are async. Need to handle this carefully.

2. **Storage provider configuration**: How to let users configure which Orleans storage provider to use for Async+ state.

---

## Session: 2025-12-01 (C1 Cross-Session Persistence Working)

### Context
Continued from previous session. Phase 3 (Orleans Integration) was structurally complete but the C1 Cross-Session Persistence scenario was failing. This session focused on debugging and fixing all the issues preventing successful checkpoint/restore across silo restarts.

### What Was Discussed

1. **C1 Scenario Testing & Bug Hunting**
   - Ran C1 scenario repeatedly, each fix revealing the next bug
   - Total of 5 bugs found and fixed in this session
   - Final result: C1 passes with correct output (input=42 → result=94)

2. **Bug #1: StateNumber=0 Deserialized as -1**
   - Orleans JSON serializer uses `DefaultValueHandling.Ignore`
   - `StateNumber=0` (first await point) was skipped because 0 is the int default
   - Fix: Added `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]` to StateNumber property

3. **Bug #2: NullReferenceException on Restore**
   - Restoration jumped to `Label_AwaitPoint0` expecting awaiter result
   - But awaiter wasn't serialized - can't serialize `TaskAwaiter<T>`
   - Fix: Added `justRestored` flag and new `Label_StartOpN` labels to re-run async operations

4. **Bug #3: Stale Checkpoint After ClearAsync**
   - `ClearStateAsync()` only clears storage, sets `RecordExists=false`
   - But in-memory `_state.State` object retains old values
   - Fix: Explicitly reset all `_state.State` fields after `ClearStateAsync()`

5. **Bug #4: Restored Values Lost (Struct Boxing)**
   - `InstrumentedSimpleWorkflow_StateMachine` was a struct
   - When passed as `object` to `TryRestore`, it gets boxed
   - `SetValue` via reflection modifies the boxed copy, not the original
   - Fix: Changed state machine from `struct` to `class`

6. **Bug #5: Compilation Errors from #4**
   - CS1605: `ref this` not allowed in class
   - CS8618: Non-nullable field `workflowId` not initialized
   - Fix: Store `this` in local variable for `ref`, initialize `workflowId = ""`

### Artifacts Modified

| File | Change |
|------|--------|
| `AsyncStateCheckpoint.cs` | Added `[JsonProperty]` to `StateNumber` |
| `InstrumentedWorkflow.cs` | struct→class, justRestored flag, Label_StartOpN, ref fixes |
| `AsyncStatePersistenceGrain.cs` | ClearAsync resets in-memory state |
| `RavenDbGrainStorage.cs` | Enhanced debug logging (file-based) |
| `CURRENT-WORK.md` | Updated with bugs table |

### Key Insights

1. **Orleans JSON Serialization Quirk**: `DefaultValueHandling.Ignore` is a common source of bugs when default values (0, false, null) are meaningful.

2. **Struct Boxing with Reflection**: A subtle bug - structs passed as `object` get boxed, and reflection modifications affect the copy. Critical for any serialization/deserialization involving structs.

3. **ClearStateAsync Semantics**: Orleans' `ClearStateAsync` is about storage, not memory. Must manually reset the `State` object if you need both cleared.

4. **Awaiter Non-Serialization**: Awaiters can't be serialized. On restoration, must re-initiate the async operation rather than trying to recover the awaiter state.

5. **Hand-coded vs Roslyn-generated State Machines**: The struct→class workaround works for hand-coded but real Roslyn generates structs. Production needs different approach.

### What's Next

1. Update AI-Context documents ✅
2. Analyze: Continue with hand-coded state machines or switch to Roslyn+ for C2-C9?
3. Implement scenarios C2-C9:
   - C2: Multiple Concurrent Workflows
   - C3: Nested Async Calls
   - C4: Exception Recovery
   - C5: Large State Serialization
   - C6: Silo Failover (multi-silo)
   - C7: Version Migration
   - C8: Multi-Silo Visibility
   - C9: Grain Mobility

### Open Questions

1. **Hand-coded vs Roslyn+**: User raised critical question - should we test with actual Roslyn+ generated code instead of hand-coded state machines? Analysis needed.

2. **Struct Boxing in Production**: Real Roslyn generates structs. How will the Orleans integration handle this for real code?

3. **Awaiter Re-execution**: Current approach re-runs async operations on restore. Is this semantically correct for all scenarios?

---

## Session: 2025-12-01 (Continued - Option A Implementation)

### Context
Continuing from earlier session where C1 was working but struct boxing was identified as critical issue. User requested:
1. Document full Option A/B/C analysis in AI-Context
2. Implement Option A (pass by ref with generic method)
3. Create new R1 scenario using actual Roslyn+ generated code
4. Add extensive logging to help debugging

### What Was Discussed

1. **Option A Analysis Documented** - Full comparison of three approaches for fixing struct boxing:
   - Option A: Pass by ref (generic method) - RECOMMENDED
   - Option B: Return restored value
   - Option C: Return field dictionary

2. **Option A Implementation** - Updated interface and implementation:
   - `IAsyncPersistenceService.TryRestore<TStateMachine>(ref TStateMachine, string)` added
   - Old non-generic method marked `[Obsolete]`
   - `NewOrleansAsyncPersistenceService` updated with `DeserializeStateMachine<T>`

3. **Roslyn Codegen Updates** - Modified `AsyncMethodToStateMachineRewriter.cs`:
   - Added `GetGenericTryRestoreMethod()` to find new interface method
   - Updated `GeneratePersistenceRestorationCheck` to use generic method with `ref this`
   - Added file-based logging (`Log()`, `LogToFile()`, `LogGeneratedCodeDescription()`)

4. **R1 Scenario Created** - New `RoslynPlusCrossSession.cs`:
   - Compiles [Persistable] workflow with actual Roslyn+ at runtime
   - Tests struct state machine restoration (unlike C1 which used class workaround)
   - Extensive logging to `roslyn-plus-scenario.log`

5. **Roslyn Compilation Error Fix** - TAI reported 14→10 compilation errors:
   - Error: `this.stateMachineType` doesn't exist on `AsyncMethodToStateMachineRewriter`
   - Root cause: `stateMachineType` is defined in `StateMachineRewriter` but
     `AsyncMethodToStateMachineRewriter : MethodToStateMachineRewriter : MethodToClassRewriter`
   - Fix: Changed all `this.stateMachineType` to `F.CurrentType`
   - `F.CurrentType` is the correct way to access state machine type in this class hierarchy

### Artifacts Modified

| File | Change |
|------|--------|
| `DOTNExTPersistence.cs` | Added generic `TryRestore<T>`, marked old obsolete |
| `NewOrleansAsyncPersistenceService.cs` | Implemented generic method, added `DeserializeStateMachine<T>` |
| `AsyncMethodToStateMachineRewriter.cs` | Added generic method support, logging, **fixed `stateMachineType` → `F.CurrentType`** |
| `RoslynPlusCrossSession.cs` | **NEW** - R1 scenario for Roslyn+ testing |
| `Program.cs` | Added R1 to self-managing scenarios menu |
| `CURRENT-WORK.md` | Full Option A/B/C analysis documented |
| `SESSION-LOG.md` | This entry |

### Key Insights

1. **Class Hierarchy Matters**: `AsyncMethodToStateMachineRewriter` doesn't inherit from `StateMachineRewriter`, so `stateMachineType` field isn't available. Must use `F.CurrentType` instead.

2. **F.CurrentType is the Pattern**: Looking at `MethodToStateMachineRewriter.TypeMap` property confirms this:
   ```csharp
   get { return ((SynthesizedContainer)F.CurrentType).TypeMap; }
   ```

3. **Generic Method Construction**: To call a generic method in bound tree:
   ```csharp
   var constructed = genericMethod.Construct(F.CurrentType);
   F.Call(receiver, constructed, args...);
   ```

4. **Ref Parameters in Bound Tree**: F.Call handles ref parameters automatically from method symbol definitions - no special handling needed when calling `TryRestore<T>(ref T, string)`.

### What's Next

1. TAI to verify Roslyn build succeeds with `F.CurrentType` fix
2. TAI to run R1 scenario and verify struct restoration works
3. Complete C2-C9 scenarios using Roslyn+ approach

### Open Questions

1. **R1 Build Verification**: Need TAI to build Roslyn and verify the fix compiles
2. **Roslyn+ Runtime Loading**: Does R1 scenario correctly load the modified Roslyn DLLs at runtime?

---

## Session Template (Copy for New Sessions)

```markdown
## Session: YYYY-MM-DD

### Context
[What brought us here]

### What Was Discussed
[Key topics]

### Artifacts Created/Modified
[Files changed]

### Key Insights
[Important learnings]

### What's Next
[Continuation points]

### Open Questions
[Unresolved items]
```
