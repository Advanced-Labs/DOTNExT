# Assessment: Async/Await as Distributed Computing Primitive

**Author**: Claude Opus
**Date**: 2025-11-28
**Context**: Review of GPT-5.1 conversation about exploiting async/await machinery for distributed computing

---

## Executive Summary

The core idea—treating C# async state machines as serializable, migratable units of computation—is **technically sound and genuinely interesting**. However, the GPT-5.1 response contains some oversimplifications and misses important nuances. I'll give you my honest take.

---

## Where GPT-5.1 Was Right

### 1. The Fundamental Insight Is Valid

The async/await transformation produces exactly what you'd want for mobile computation:
- **Explicit suspension points** (await boundaries)
- **Heap-allocated state** (locals hoisted to fields)
- **Small protocol surface** (`IAsyncStateMachine.MoveNext()` + builder APIs)
- **Tolerance for arbitrary delay** (that's literally what async is for)

This is not hand-wavy. The compiler genuinely does produce something that looks like a resumable process.

### 2. The Extensibility Points Are Real

The two hooks GPT mentioned are legitimate:
- `[AsyncMethodBuilder(typeof(...))]` for custom builders
- Custom awaiters via the `GetAwaiter()` pattern

You can absolutely intercept every suspension and resumption. Orleans already uses custom schedulers and `TaskScheduler` integration—extending this to checkpoint at await boundaries is architecturally coherent.

### 3. Runtime-Async Is Real

The dotnet/runtime `runtime-async` work (tracked in issue #109632) is genuine. It aims to make the CLR understand async methods natively rather than relying purely on compiler lowering. This would give you:
- Better debugging
- More efficient execution
- Potentially better introspection APIs

---

## Where GPT-5.1 Oversimplified or Missed Things

### 1. Serialization Is Not Trivial

GPT glossed over the serialization challenge with "reflection into the compiler-generated state machine." This understates the difficulty:

**The state machine captures**:
- Locals (hoisted to fields)
- `this` reference if instance method
- Captured closures
- Awaiter state

**The hard part**: What if those locals include:
- Open `DbConnection`?
- Native handles?
- References to other in-progress async operations?
- Closures capturing mutable state from outer scopes?

You'd need to:
1. **Constrain what async methods can capture** (similar to Durable Functions' rules)
2. **Provide serialization surrogates** for non-serializable types
3. **Handle re-acquisition** of resources on resume

This is *the* hard engineering problem, and it's not a "just use reflection" situation.

### 2. Orleans Already Uses Async Deeply—But Differently

GPT said Orleans "leans hard on Tasks and async/await, but at the 'normal .NET' level, not in some unusual way." This is accurate but misleading in context.

Orleans does something sophisticated:
- **Per-grain TaskScheduler** enforcing turn-based execution
- **Reentrancy control** via `[Reentrant]` attribute
- **Timers/reminders** that integrate with async

But critically: **Orleans does NOT serialize async state machines**. Grain state is explicit (`Grain<TState>`). The async flow is ephemeral—if a silo dies mid-await, the work is lost (unless you're using Durable Task Framework on top).

What you're proposing would make the async flow itself persistent/migratable.

### 3. The Durable Functions Comparison Is Apt But Different

Durable Functions uses **replay-based** execution:
1. Orchestrator runs from the beginning
2. Completed awaits are replayed from history (no actual work done)
3. First un-replayed await actually executes

Your proposal is **snapshot-based**:
1. Serialize actual state machine at await point
2. Resume from snapshot without replay

**Trade-offs**:

| Aspect | Replay (Durable Functions) | Snapshot (Your Proposal) |
|--------|---------------------------|-------------------------|
| State size | History grows with length | Fixed snapshot size |
| Code changes | Can change code between replays (carefully) | Must match exact state machine shape |
| Debugging | Easy to reason about (replay trace) | Harder (black-box resume) |
| Performance | Re-executes logic on replay | Direct resume |

Neither is strictly better. Replay is simpler to reason about; snapshot is more efficient for long-running flows.

### 4. The Erlang Comparison Is Illuminating But Limited

The analogy:
- Erlang process ≈ async state machine instance
- Erlang mailbox ≈ pending awaitables
- Erlang scheduler ≈ your custom TaskScheduler

**What Erlang has that .NET lacks**:
- Processes are **truly isolated** (share nothing by default)
- Serialization is built into the runtime (every term is serializable)
- Location transparency is native (PIDs work across nodes)

Your C# async approach would need to **enforce** isolation that Erlang gets for free. This is doable (constrained async methods, grain-like boundaries) but it's design work, not automatic.

---

## My Honest Assessment of the Idea

### Is This Technically Feasible?

**Yes, unambiguously.** Every piece needed exists:
- Custom async builders
- State machine reflection/manipulation
- Orleans' infrastructure for distribution
- Your dynamic grain loading (NewOrleans)

### Is This Novel?

**Partially.** The closest existing systems:
- **Durable Functions/Task Framework**: Replay-based durable workflows
- **Proto.Actor persistence**: Snapshot-based actor state (but not async state machines)
- **Akka persistence**: Event sourcing for actors

The specific idea of **using the compiler-generated async state machine as the persistence unit** is less explored. Most systems define their own workflow IR.

### Is This Worth It?

**My honest take: Yes, but scope it carefully.**

**The value proposition**:
1. Developers write normal async C#
2. The framework makes it distributed/durable
3. No special DSL to learn

**The risk**:
1. Complex implementation (state machine versioning, serialization constraints)
2. Debugging difficulty (where did my async resume? what state was it in?)
3. Performance overhead of checkpoint/restore

**My recommendation**: Start with a constrained subset:
1. Only async methods marked with a specific attribute
2. Only simple serializable local types
3. Only Orleans-aware awaitables

Then expand from there.

---

## How NewOrleans Positions You

Your existing work provides excellent foundations:

| NewOrleans Feature | How It Helps |
|--------------------|--------------|
| **Dynamic grain loading** | Hot-deploy workflow grains |
| **ALC isolation** | Different workflow versions coexist |
| **GTD** | Discover available workflow types |
| **Dynamic clients** | Access workflows without compile-time refs |
| **Package system** | Distribute workflow assemblies |

The natural extension:
- Workflow = special grain type
- Workflow state = async state machine snapshot
- Workflow resumption = load grain, deserialize state, call `MoveNext()`

---

## Concrete Next Steps (If You Pursue This)

### Experiment 1: Prove State Machine Capture Works

```csharp
[DistributedWorkflow]
public async Task<int> MyWorkflow(int input)
{
    var step1 = await DoStep1Async(input);  // checkpoint here
    var step2 = await DoStep2Async(step1);  // checkpoint here
    return step2;
}
```

Build a custom builder that:
1. Intercepts `AwaitOnCompleted`
2. Serializes the state machine fields via reflection
3. Logs the JSON/binary representation
4. Continues normally

This proves you can see the state.

### Experiment 2: Resume From Snapshot

Take the serialized state from Experiment 1. In a separate process:
1. Deserialize into a new state machine instance
2. Call `MoveNext()`
3. Observe it resumes correctly

This proves cross-process migration works.

### Experiment 3: Integrate with Orleans

Create a `WorkflowGrain` that:
1. Stores async state machine snapshot as grain state
2. On `OnActivateAsync`, restores from snapshot
3. On every await, checkpoints to grain state

This proves the Orleans integration model.

---

## Caveats and Gotchas

### State Machine Shape Changes

If you change the async method's code, the state machine type changes. You cannot deserialize old snapshots into new shapes. You need:
- Version numbers in snapshots
- Migration logic for version transitions
- Possibly dual-running old and new versions

### Awaiter State

The state machine holds references to awaiters. If an awaiter represents "waiting for RPC result X", you need the resumed node to understand what X means. This implies:
- Awaiters should be logical/symbolic (grain IDs, message IDs)
- Not concrete (open sockets, task completion sources)

### Exception Handling

What if the async method is mid-try-catch when checkpointed? The state machine tracks this via its `state` field. Make sure your serialization captures the full state machine semantics.

---

## Final Verdict

**This is a legitimate research direction with real potential value.** It's not vaporware or hand-waving—the technical foundations exist.

The question is whether the engineering cost is worth the ergonomic benefit of "just write async C# and get distributed durability." For many use cases, explicit state machines (Durable Functions, Saga patterns) are good enough. But for a platform like NewOrleans aiming to be a next-generation distributed runtime, this could be a differentiating feature.

**My suggestion**: Build Experiments 1-3 over a few days. If they work cleanly, you have something real. If they expose fundamental blockers, you'll know early.

---

## Questions I'd Want Answered

1. **What's the checkpoint overhead?** Serializing/deserializing every await could be expensive. Can you make it lazy (only checkpoint if needed)?

2. **How do you handle cancellation?** If a workflow is checkpointed and the original caller's CancellationToken fires, what happens?

3. **What about reentrancy?** If the async method calls itself recursively, you get multiple state machines. How do you track them?

4. **Can you use Roslyn to enforce constraints?** An analyzer that rejects async methods with non-serializable locals would prevent runtime surprises.

---

*This document is my honest assessment. I find the idea compelling and technically grounded, but the implementation complexity is non-trivial. The existing NewOrleans work provides a strong foundation to build on.*
