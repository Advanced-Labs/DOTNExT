# Async+ Challenge Scenarios

## Overview

This document outlines scenarios to comprehensively test and challenge the Async+ paradigm - both current implementation and future capabilities that may require R&D.

---

## Part 1: Current Implementation Challenges

These scenarios test what Async+ should already handle with the current implementation.

### Tier 1: Core Functionality (Must Work)

#### Scenario C1: Basic Cross-Session Persistence
**Purpose**: Verify checkpoints survive process restarts
**Test**:
1. Start silo, run [Persistable] workflow, checkpoint at state 1
2. Kill process abruptly (simulating crash)
3. Restart silo
4. Workflow should restore from checkpoint and complete

**Key Validation**:
- RavenDB contains checkpoint data
- State machine fields are correctly restored
- Workflow resumes from correct state (not from beginning)

#### Scenario C2: Multiple Concurrent Workflows
**Purpose**: Verify isolation between concurrent workflow instances
**Test**:
1. Start 5 parallel [Persistable] workflows with different IDs
2. Each workflow has different input values
3. Checkpoint all at various states
4. Verify each workflow's checkpoint contains correct data

**Key Validation**:
- No cross-contamination between workflow checkpoints
- All workflows complete with correct results
- RavenDB shows 5 distinct grain states

#### Scenario C3: Nested Async Calls
**Purpose**: Verify checkpointing with nested awaits
**Test**:
```csharp
[Persistable]
async Task<int> Outer(int x)
{
    var a = await Inner1(x);      // Checkpoint
    var b = await Inner2(a);      // Checkpoint
    return await Combine(a, b);   // Checkpoint
}
```
**Key Validation**:
- Each await point generates a checkpoint
- Nested return values are preserved across checkpoints

#### Scenario C4: Exception Recovery
**Purpose**: Verify exception state is preserved
**Test**:
1. Workflow throws at step 2
2. Checkpoint should capture exception state
3. On restore, exception should re-throw correctly

**Key Validation**:
- Exception type preserved
- Stack trace available
- Workflow marked as faulted in persistence

---

### Tier 2: Robustness (Should Work)

#### Scenario C5: Large State Machine Fields
**Purpose**: Test serialization limits
**Test**:
- Workflow with large arrays (10K+ elements)
- Complex object graphs
- Measure serialization/deserialization time

**Key Validation**:
- No timeout on serialization
- Data integrity preserved
- Reasonable performance (<1s for 1MB state)

#### Scenario C6: Silo Failover Mid-Checkpoint
**Purpose**: Test atomicity of checkpoint operation
**Test**:
1. Start 2-silo cluster
2. Run workflow, trigger checkpoint
3. Kill silo hosting the grain mid-checkpoint
4. Verify other silo can recover

**Key Validation**:
- Either checkpoint completes fully or not at all
- No partial/corrupt state
- Workflow can continue on surviving silo

#### Scenario C7: Checkpoint Version Migration
**Purpose**: Test backward compatibility
**Test**:
1. Checkpoint workflow with v1 state machine shape
2. Update workflow code (add new field)
3. Restore should handle missing field gracefully

**Key Validation**:
- New fields get default values
- Removed fields don't crash deserialization
- Migration path documented

---

### Tier 3: Multi-Node (Advanced Current)

#### Scenario C8: Multi-Silo Checkpoint Visibility
**Purpose**: Verify cross-silo grain state access
**Test**:
1. 3-silo cluster with RavenDB
2. Workflow runs on Silo1, checkpoints
3. Query grain state from Silo2 and Silo3
4. All silos see same checkpoint data

**Key Validation**:
- RavenDB provides consistent view
- No stale reads across silos

#### Scenario C9: Grain Reactivation on Different Silo
**Purpose**: Test grain mobility with persistent state
**Test**:
1. Workflow checkpoints on Silo1
2. Deactivate grain on Silo1
3. Force reactivation on Silo2
4. Workflow should restore and continue

**Key Validation**:
- State follows grain to new silo
- No data loss in migration

---

## Part 2: Future Capabilities (R&D Required)

These scenarios represent features that would enhance Async+ but may require significant research and development.

### Research Area R1: Distributed Workflow Orchestration

#### Scenario R1.1: Workflow Saga Pattern
**Vision**: Chain of [Persistable] workflows with compensation
```csharp
[Persistable]
async Task<OrderResult> PlaceOrder(Order order)
{
    var payment = await ProcessPayment(order);     // Step 1
    var inventory = await ReserveInventory(order); // Step 2
    var shipping = await ArrangeShipping(order);   // Step 3
    return new OrderResult(payment, inventory, shipping);
}
// If Step 3 fails, automatically compensate Steps 1 & 2
```

**R&D Questions**:
- How to define compensation actions?
- Should compensation be automatic or explicit?
- How to handle partial compensation failures?

**Complexity**: High - requires workflow definition language extensions

#### Scenario R1.2: Long-Running Human-in-the-Loop
**Vision**: Workflows that suspend for external input
```csharp
[Persistable]
async Task<ApprovalResult> ApprovalWorkflow(Request req)
{
    await NotifyManager(req);
    var approval = await WaitForHumanApproval(req.Id); // Could be days
    if (approval.Approved)
        await ProcessApproved(req);
    return approval;
}
```

**R&D Questions**:
- How to handle "await" that spans days/weeks?
- Timer/reminder system integration?
- External signal mechanism for resumption?

**Complexity**: Medium - needs external signal infrastructure

---

### Research Area R2: State Machine Optimization

#### Scenario R2.1: Selective Field Persistence
**Vision**: Mark which fields need persistence
```csharp
[Persistable]
async Task Process()
{
    [Persist] var importantData = await GetData();
    [Transient] var cache = BuildCache(importantData); // Don't save this
    // ...
}
```

**R&D Questions**:
- Attribute-based or convention-based?
- How to rebuild transient state on restore?
- Roslyn modifications needed?

**Complexity**: Medium - Roslyn changes + runtime support

#### Scenario R2.2: Incremental Checkpointing
**Vision**: Only save changed fields, not entire state
**Benefit**: Reduce I/O for large state machines
**R&D Questions**:
- Change tracking mechanism?
- Delta storage format?
- Merge strategy on restore?

**Complexity**: High - significant Roslyn and runtime changes

---

### Research Area R3: Debugging & Observability

#### Scenario R3.1: Checkpoint Replay/Debugging
**Vision**: Replay workflow execution from any checkpoint
```csharp
// Developer tool
await WorkflowDebugger.ReplayFrom("workflow-123", checkpointId: 2);
```

**R&D Questions**:
- How to capture enough context for replay?
- Side-effect isolation during replay?
- Integration with VS debugger?

**Complexity**: High - tooling infrastructure

#### Scenario R3.2: Checkpoint History/Audit Trail
**Vision**: Keep history of all checkpoints, not just latest
**Benefit**: Debugging, compliance, undo
**R&D Questions**:
- Storage strategy (keep all vs. rolling window)?
- Query API for checkpoint history?
- Performance impact?

**Complexity**: Medium - storage design + API

---

### Research Area R4: Performance & Scaling

#### Scenario R4.1: Checkpoint Batching
**Vision**: Batch multiple workflow checkpoints into single storage operation
**Benefit**: Reduce RavenDB round-trips in high-throughput scenarios
**R&D Questions**:
- Batching window (time vs. count)?
- Failure semantics for batch?
- Per-workflow isolation within batch?

**Complexity**: Medium - Orleans grain coordination

#### Scenario R4.2: Lazy Checkpoint Loading
**Vision**: Only load checkpoint data when actually needed
**Benefit**: Faster grain activation for large states
**R&D Questions**:
- Streaming deserialization?
- Partial state machine reconstruction?

**Complexity**: High - fundamental architecture change

---

## Part 3: Implementation Status

### ✅ COMPLETED

| Scenario | Status | Date | Notes |
|----------|--------|------|-------|
| **C1: Cross-Session Persistence** | ✅ PASS | 2025-12-01 | Hand-coded state machine, result=94 |
| **R1: Roslyn+ Cross-Session Persistence** | ✅ PASS | 2025-12-02 | Real Roslyn+ generated code, result=94 |

### Key Learnings from R1

1. **Awaiters can't be serialized** - TaskAwaiter<T> holds internal state
2. **Re-run from beginning** - After restoration, workflow re-runs with restored field values
3. **Class state machines** - Roslyn generates CLASS (not struct) by default
4. **Persistence Method ID** - Uses fully qualified method name (Namespace.Class.Method)

### Recommended Next Steps

1. **C2: Multiple Concurrent Workflows** - Validates isolation
2. **C3: Nested Async Calls** - Common real-world pattern

### Short-Term (Following Month)
4. **C4: Exception Recovery** - Production reliability
5. **C5: Large State Serialization** - Performance baseline
6. **C8: Multi-Silo Visibility** - Distributed correctness

### Medium-Term (Quarter)
7. **C6: Silo Failover** - HA validation
8. **C7: Version Migration** - Upgrade path
9. **C9: Grain Mobility** - Cluster elasticity

### R&D Pipeline
- **R1.2: Human-in-the-Loop** - Expands use cases significantly
- **R3.2: Checkpoint History** - Valuable for debugging
- **R2.1: Selective Persistence** - Performance optimization

---

## Scenario Combinations for Real-World Testing

### Combo 1: E-Commerce Order Flow
Combines: C1, C2, C3, C4
- Multiple concurrent orders
- Nested calls (payment, inventory, shipping)
- Failure recovery on payment decline

### Combo 2: Approval Pipeline
Combines: C1, C5, R1.2 (when ready)
- Manager approval workflow
- Large document attachments
- Multi-day suspension

### Combo 3: High-Availability Batch Processing
Combines: C2, C6, C8, C9
- 1000s of concurrent jobs
- Silo failures mid-processing
- Automatic recovery and redistribution
