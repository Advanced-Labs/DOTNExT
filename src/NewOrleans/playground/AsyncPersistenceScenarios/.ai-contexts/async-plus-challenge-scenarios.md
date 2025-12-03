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
| **R1: Roslyn+ Cross-Session Persistence** | ✅ PASS | 2025-12-02 | Real Roslyn+ generated code, result=94 |
| **C1: Cross-Session Persistence** | ✅ PASS | 2025-12-01 | Legacy hand-coded state machine |

> **Note**: C1 uses a hand-coded state machine and is kept for reference only. All future development focuses on Roslyn+ generated code (R1 pattern).

### Key Learnings from R1

1. **Awaiters can't be serialized** - TaskAwaiter<T> holds internal state
2. **Re-run from beginning** - After restoration, workflow re-runs with restored field values
3. **Class state machines** - Roslyn generates CLASS (not struct) by default
4. **Persistence Method ID** - Uses fully qualified method name (Namespace.Class.Method)

---

## Part 4: Roslyn+ Planned Scenarios Analysis

All scenarios below use Roslyn+ generated [Persistable] code (not hand-coded state machines).

### C2: Multiple Concurrent Workflows

| Aspect | Details |
|--------|---------|
| **Purpose** | Verify isolation between concurrent workflow instances - critical for production use where many workflows run simultaneously |
| **Value** | HIGH - Essential for any real-world deployment; validates Orleans grain isolation works with Async+ persistence |
| **Risk Assessment** | LOW (70% success likelihood) - R1 proves basic persistence works; main risk is grain ID collision or race conditions in checkpoint storage |
| **Potential Failures** | 1) Grain ID conflicts if using same method ID for different instances; 2) RavenDB transaction conflicts; 3) Cross-contamination of checkpoint data |
| **Mitigation Strategy** | 1) Ensure unique grain IDs per workflow instance; 2) Add workflow instance ID to PersistenceMethodId; 3) Verify RavenDB document isolation |
| **Success Advancement** | Enables production deployment of parallel workflows; validates scalability; proves Orleans+RavenDB+Roslyn+ stack is ready for concurrent workloads |

### C3: Nested Async Calls

| Aspect | Details |
|--------|---------|
| **Purpose** | Verify checkpointing works with nested [Persistable] method calls - common pattern in real applications |
| **Value** | HIGH - Real workflows often call other async methods; validates recursive persistence |
| **Risk Assessment** | MEDIUM (55% success likelihood) - Nested state machines may have interaction issues; checkpoint ordering may be complex |
| **Potential Failures** | 1) Inner method completion clears outer method checkpoint; 2) Nested grain activations cause deadlocks; 3) State machine field references become stale after inner restore |
| **Mitigation Strategy** | 1) Use separate grain IDs for nested calls; 2) Track call hierarchy in persistence; 3) Ensure inner completion doesn't affect outer state; 4) Consider "call stack" tracking in persistence |
| **Success Advancement** | Enables composition of [Persistable] methods; supports modular workflow design; proves system handles real-world complexity |

### C4: Exception Recovery

| Aspect | Details |
|--------|---------|
| **Purpose** | Verify exception state is correctly persisted and re-thrown after restoration |
| **Value** | HIGH - Production reliability requires proper error handling across restarts |
| **Risk Assessment** | MEDIUM (60% success likelihood) - Exception serialization is tricky; stack traces may be lost; catch blocks may re-execute incorrectly |
| **Potential Failures** | 1) Exception not serializable; 2) Stack trace lost during serialization; 3) Catch block executed twice (once before crash, once after restore); 4) Finally blocks not executed properly |
| **Mitigation Strategy** | 1) Serialize exception type + message + data, not full exception; 2) Store exception state in checkpoint; 3) Track whether exception was already caught; 4) Test various exception scenarios (thrown, caught, rethrown) |
| **Success Advancement** | Production-ready error handling; enables retry patterns; supports long-running workflows with proper failure recovery |

### C5: Large State Serialization

| Aspect | Details |
|--------|---------|
| **Purpose** | Test serialization performance and limits with large state machine fields (10K+ element arrays, complex graphs) |
| **Value** | MEDIUM - Establishes performance baseline; identifies scaling limits before production |
| **Risk Assessment** | LOW (75% success likelihood) - JSON serialization is well-understood; main risks are timeout and memory pressure |
| **Potential Failures** | 1) Serialization timeout (default Orleans timeout); 2) Out-of-memory during large object graph serialization; 3) RavenDB document size limits; 4) Circular reference issues |
| **Mitigation Strategy** | 1) Configure appropriate timeouts; 2) Add chunked serialization for very large states; 3) Test RavenDB document limits (default 4MB); 4) Use [JsonIgnore] for non-essential large fields |
| **Success Advancement** | Establishes safe working limits; provides performance benchmarks; identifies when to use selective persistence (R2.1) |

### C6: Silo Failover Mid-Checkpoint

| Aspect | Details |
|--------|---------|
| **Purpose** | Test atomicity of checkpoint operation when silo dies during write |
| **Value** | HIGH - Critical for production HA; validates Orleans+RavenDB transaction semantics |
| **Risk Assessment** | HIGH (40% success likelihood) - Race conditions are hard to test; depends on RavenDB transaction behavior; requires multi-silo setup |
| **Potential Failures** | 1) Partial checkpoint written (corrupt state); 2) Other silo reads stale checkpoint; 3) Grain reactivation with inconsistent state; 4) Orleans grain directory inconsistency |
| **Mitigation Strategy** | 1) Use RavenDB transactions for atomic writes; 2) Add checkpoint version/sequence number; 3) Implement optimistic concurrency checks; 4) Test with artificial delays during checkpoint |
| **Success Advancement** | Production-ready HA; enables deployment in failure-prone environments; validates enterprise reliability requirements |

### C7: Checkpoint Version Migration

| Aspect | Details |
|--------|---------|
| **Purpose** | Test backward compatibility when workflow code changes between checkpoint and restore |
| **Value** | MEDIUM - Required for production systems that evolve; enables zero-downtime upgrades |
| **Risk Assessment** | MEDIUM (50% success likelihood) - Depends on serializer tolerance for schema changes; may require migration hooks |
| **Potential Failures** | 1) New field not initialized (NullReferenceException); 2) Removed field causes deserialization failure; 3) Type changes break deserialization; 4) Field order changes cause data corruption |
| **Mitigation Strategy** | 1) Use JSON serializer with default value handling; 2) Add version number to checkpoints; 3) Implement migration hooks in TryRestore; 4) Test common schema evolution scenarios |
| **Success Advancement** | Enables production deployments with rolling updates; supports long-running workflows across code versions; reduces operational risk |

### C8: Multi-Silo Checkpoint Visibility

| Aspect | Details |
|--------|---------|
| **Purpose** | Verify all silos in cluster can see checkpoint data consistently via RavenDB |
| **Value** | MEDIUM-HIGH - Required for distributed deployments; validates RavenDB as shared state store |
| **Risk Assessment** | LOW (70% success likelihood) - RavenDB handles consistency; main risk is stale reads or caching |
| **Potential Failures** | 1) Orleans grain caching returns stale checkpoint; 2) RavenDB replication lag; 3) Silo-local state not synced with storage; 4) Race between checkpoint write and grain reactivation |
| **Mitigation Strategy** | 1) Ensure grain state is read from storage on activation; 2) Use RavenDB "WaitForIndexesAfterSaveChanges" if needed; 3) Test read-after-write from different silos |
| **Success Advancement** | Validates distributed deployment model; enables horizontal scaling; proves Orleans+RavenDB integration is production-ready |

### C9: Grain Reactivation on Different Silo

| Aspect | Details |
|--------|---------|
| **Purpose** | Test grain mobility - deactivate on Silo1, reactivate on Silo2, verify state follows |
| **Value** | HIGH - Essential for cluster elasticity; enables load balancing and node maintenance |
| **Risk Assessment** | LOW-MEDIUM (65% success likelihood) - This is core Orleans behavior; main risk is Async+ state not properly tied to Orleans grain state |
| **Potential Failures** | 1) Checkpoint not found on new silo (wrong grain ID); 2) State machine type not available on new silo; 3) Persistence context not established on reactivation; 4) Grain directory points to wrong silo |
| **Mitigation Strategy** | 1) Verify grain ID is deterministic and cluster-wide; 2) Ensure compiled assembly available on all silos; 3) Test explicit grain deactivation followed by call from different silo |
| **Success Advancement** | Enables elastic scaling; supports rolling deployments; validates cloud-native deployment patterns |

---

### Priority Order for Implementation

Based on risk/value analysis:

1. **C2** (LOW risk, HIGH value) - Concurrent workflows
2. **C8** (LOW risk, MEDIUM-HIGH value) - Multi-silo visibility
3. **C3** (MEDIUM risk, HIGH value) - Nested async calls
4. **C9** (LOW-MEDIUM risk, HIGH value) - Grain mobility
5. **C4** (MEDIUM risk, HIGH value) - Exception recovery
6. **C5** (LOW risk, MEDIUM value) - Large state serialization
7. **C7** (MEDIUM risk, MEDIUM value) - Version migration
8. **C6** (HIGH risk, HIGH value) - Silo failover (save for last, most complex)

---

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
