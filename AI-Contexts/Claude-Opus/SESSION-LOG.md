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
