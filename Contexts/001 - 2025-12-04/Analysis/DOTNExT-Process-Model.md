# DOTNExT Process Model

> **Document Type:** Architecture Design & Analysis
> **Version:** 1.0
> **Date:** 2025-12-10
> **Status:** DESIGN - Defining the fundamental process abstraction for DOTNExT VOS
> **Prerequisite Reading:** DOTNExT-Singularity-Midori-Research.md, DOTNExT-Execution-Pathways.md

---

## 1. Executive Summary

This document defines **what a "Process" is in DOTNExT** - the fundamental unit of execution, isolation, and identity in the DOTNExT Virtual Operating System.

**Key Questions Addressed:**
- What is the relationship between Processes and Pathways?
- What isolation model do we use?
- What are process states and lifecycle?
- How do explicit and implicit processes relate?

**Informed by:** Singularity's Software Isolated Processes (SIPs), Midori's ultra-lightweight processes, BEAM's process model, and DOTNExT's unique AI-first requirements.

---

## 2. Terminology Clarification

Before defining our process model, let's clarify the hierarchy:

```
DOTNExT VOS Execution Hierarchy:

┌─────────────────────────────────────────────────────────────────┐
│  VM Node                                                        │
│  └── Process (isolation boundary, identity, resource container) │
│      └── Pathway (execution flow, captured state, schedulable)  │
│          └── Frame (single stack frame, captured at safe point) │
└─────────────────────────────────────────────────────────────────┘
```

| Term | Definition |
|------|------------|
| **VM Node** | A running DOTNExT runtime instance |
| **Process** | Isolation boundary with identity; contains one or more Pathways |
| **Pathway** | A flow of execution (captured frames); the scheduling unit |
| **Frame** | A single captured stack frame within a Pathway |

---

## 3. Design Principles

Based on Singularity/Midori lessons and DOTNExT requirements:

### 3.1 From Singularity
- **Software isolation** - Type safety + VCOM provides isolation, not hardware
- **Cheap processes** - Creation should be ~thousands of cycles, not millions
- **Sealed boundaries** - Process boundaries enable static analysis

### 3.2 From Midori
- **Ultra-lightweight** - Many fine-grained processes per classical application
- **Async everything** - No blocking within process; message passing between
- **Single-threaded per process** - Eliminates internal concurrency hazards

### 3.3 DOTNExT-Specific
- **AI controllability** - Processes/Pathways are first-class data for AI manipulation
- **Universal capture** - All execution state capturable at any safe point
- **Distribution transparency** - Processes can migrate between nodes
- **Explicit + implicit** - Both developer-declared and runtime-managed processes

---

## 4. The DOTNExT Process

### 4.1 Definition

**A DOTNExT Process is:**
- An **isolation boundary** (logical, via VCOM + type system)
- An **identity container** (UUID, name, capabilities)
- A **resource accounting unit** (CPU budget, memory, I/O)
- A **failure domain** (abandonment affects only this process)
- Contains **one or more Pathways** (execution flows)

### 4.2 Process vs Pathway

| Aspect | Process | Pathway |
|--------|---------|---------|
| **What it is** | Container/boundary | Execution flow |
| **Identity** | UUID, persistent | UUID, may be ephemeral |
| **Scheduling** | Resource budgeted | Actually scheduled |
| **Isolation** | Boundary enforcement | Within process boundary |
| **Multiplicity** | Contains 1+ Pathways | Contains 1+ Frames |
| **Migration** | Unit of migration | Migrates with process |
| **Failure** | Failure domain | Can fail independently? |

### 4.3 Why Both?

**Process provides:**
- Coarse-grained isolation boundary
- Resource accounting container
- Identity and capability holder
- Migration unit

**Pathway provides:**
- Fine-grained execution tracking
- Actual scheduling unit
- Checkpoint granularity
- Parallel execution within process

**Example:**
```
Process: OrderProcessor (UUID: xxx)
├── Pathway: MainFlow (processing order 123)
├── Pathway: ValidationCheck (parallel validation)
└── Pathway: NotificationSender (async notification)

All three Pathways share the Process's:
- Capabilities (can access Order database)
- Resource budget (CPU allocation)
- Isolation boundary (can't see other processes)
```

---

## 5. Process Types

### 5.1 Implicit Processes (Runtime-Managed)

Created automatically by the runtime:

```csharp
// When a VCOM grain activates, implicit process created
var order = vcom.Get<IOrder>(orderId);
await order.Process();  // Runs in grain's implicit process
```

**Characteristics:**
- Tied to VCOM grain activation
- Lifecycle managed by runtime
- Single Pathway typically
- Automatic resource management

### 5.2 Explicit Processes (Developer-Declared)

Created explicitly by developer:

```csharp
// Developer creates explicit process
var proc = Process.Create(config =>
{
    config.Name = "DataProcessor";
    config.Priority = ProcessPriority.High;
    config.ResourceBudget = new ResourceBudget(cpu: 1000, memory: 100.MB());
    config.Capabilities = [FileSystem.ReadOnly, Network.Outbound];
});

// Run code in the process
proc.Run(async () =>
{
    // This Pathway runs inside 'proc'
    await ProcessAllData();
});

// Lifecycle control
await proc.Suspend();
await proc.Save("checkpoint.dnxi");
await proc.Migrate(targetNode);
proc.Terminate();
```

**Characteristics:**
- Developer controls lifecycle
- Configurable priority, resources, capabilities
- Can contain multiple Pathways
- Explicit checkpoint/migrate/terminate

### 5.3 System Processes

Special processes for runtime services:

```
System Processes:
├── Scheduler (manages pathway scheduling)
├── GC Coordinator (coordinates garbage collection)
├── Migration Manager (handles process migration)
├── Capability Manager (manages security capabilities)
└── VNS Resolver (name system resolution)
```

---

## 6. Process States

```
┌─────────────────────────────────────────────────────────────────┐
│  Process State Machine                                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                    ┌─────────────┐                              │
│                    │   Created   │                              │
│                    └──────┬──────┘                              │
│                           │ Start                               │
│                           ▼                                     │
│  ┌──────────┐      ┌─────────────┐      ┌───────────┐          │
│  │Suspending│◄─────│   Running   │─────►│ Suspended │          │
│  └────┬─────┘      └──────┬──────┘      └─────┬─────┘          │
│       │                   │                   │                 │
│       │                   │ Checkpoint        │ Resume          │
│       │                   ▼                   │                 │
│       │            ┌─────────────┐            │                 │
│       └───────────►│ Checkpointed│◄───────────┘                 │
│                    └──────┬──────┘                              │
│                           │                                     │
│              ┌────────────┼────────────┐                       │
│              │            │            │                        │
│              ▼            ▼            ▼                        │
│       ┌───────────┐ ┌──────────┐ ┌───────────┐                 │
│       │ Persisted │ │Migrating │ │Hibernated │                 │
│       └───────────┘ └────┬─────┘ └───────────┘                 │
│                          │                                      │
│                          ▼                                      │
│                    ┌───────────┐                                │
│                    │  Resumed  │ (on target node)              │
│                    │ (Running) │                                │
│                    └───────────┘                                │
│                                                                 │
│                    ┌───────────┐                                │
│       Any state───►│Terminated │                               │
│                    └───────────┘                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 6.1 State Definitions

| State | Description |
|-------|-------------|
| **Created** | Process exists but hasn't started |
| **Running** | Pathways actively executing |
| **Suspending** | Waiting for Pathways to reach safe points |
| **Suspended** | All Pathways paused at safe points |
| **Checkpointed** | State captured, ready for persist/migrate |
| **Persisted** | Saved to storage |
| **Hibernated** | Saved to storage, resources released |
| **Migrating** | Being transferred to another node |
| **Terminated** | Process ended (success, failure, or abandoned) |

### 6.2 State Transitions

| Transition | Trigger | Notes |
|------------|---------|-------|
| Created → Running | `Start()` | Begin executing Pathways |
| Running → Suspending | `Suspend()` or preemption | Wait for safe points |
| Suspending → Suspended | All Pathways at safe points | State is consistent |
| Suspended → Checkpointed | `Checkpoint()` | Capture all state |
| Checkpointed → Persisted | `Save()` | Write to storage |
| Checkpointed → Migrating | `Migrate()` | Transfer to target |
| Checkpointed → Hibernated | `Hibernate()` | Save + release resources |
| Suspended → Running | `Resume()` | Continue execution |
| Persisted → Running | `Restore()` then `Resume()` | Load and continue |
| Hibernated → Running | `Wake()` | Restore resources and continue |
| Any → Terminated | `Terminate()` or abandonment | Process ends |

---

## 7. Isolation Model

### 7.1 Isolation Approach: Logical via VCOM

DOTNExT uses **logical isolation** rather than per-process heaps:

```
Singularity: Physical isolation (per-SIP heap)
Midori: Physical isolation (per-process heap)
DOTNExT: Logical isolation (shared heap, VCOM boundaries)
```

**Why logical isolation:**
- .NET GC is global; per-process heap is major change
- VCOM already provides actor-model isolation
- Pathways within process share state naturally
- Distribution adds physical isolation between nodes

### 7.2 How VCOM Provides Isolation

```
Process A                          Process B
┌─────────────────┐               ┌─────────────────┐
│                 │               │                 │
│  Local objects  │               │  Local objects  │
│  (private)      │               │  (private)      │
│                 │               │                 │
│     │           │               │           │     │
│     └──► VCOM ◄─┼───────────────┼──► VCOM ◄─┘     │
│         Proxy   │   (grain)     │   Proxy         │
│                 │               │                 │
└─────────────────┘               └─────────────────┘

- Process A's local objects: only A can access
- Process B's local objects: only B can access
- VCOM objects: accessed via proxies, actor semantics
- Cross-process = cross-grain call
```

### 7.3 Isolation Guarantees

| Guarantee | Mechanism |
|-----------|-----------|
| No direct object sharing | Processes communicate via VCOM proxies |
| No state corruption | Actor model: one caller at a time per grain |
| Failure isolation | Process failure doesn't corrupt other processes |
| Capability-based access | Processes only access granted VCOM capabilities |

### 7.4 Future: Per-Process GC Regions?

For stronger isolation and faster collection:

```
Future consideration:
- Processes could have dedicated GC regions
- Region collected independently (no global STW for this process)
- Cross-region references tracked specially
- BEAM-like per-process GC benefits

Decision: Defer to later phase. Logical isolation sufficient initially.
```

---

## 8. Process Identity and Capabilities

### 8.1 Process Identity

```csharp
public class ProcessIdentity
{
    public Guid ProcessId { get; }        // Unique identifier
    public string Name { get; }           // Human-readable name
    public ProcessIdentity? Parent { get; }  // Spawning process
    public DateTime Created { get; }      // Creation timestamp
    public Guid OriginNode { get; }       // Where first created
    public Guid CurrentNode { get; }      // Where currently running
}
```

### 8.2 Capability Model (Midori-Inspired)

```csharp
public class ProcessCapabilities
{
    // Resource capabilities
    public ICpuBudget Cpu { get; }
    public IMemoryBudget Memory { get; }
    public IIoBudget IO { get; }

    // Access capabilities
    public IFileSystemCapability? FileSystem { get; }
    public INetworkCapability? Network { get; }
    public IVcomCapability Vcom { get; }  // Which grains accessible

    // Special capabilities
    public IProcessSpawnCapability? Spawn { get; }  // Can create children
    public IMigrationCapability? Migration { get; } // Can migrate
}

// Usage:
var proc = Process.Create(config =>
{
    config.Capabilities = new ProcessCapabilities
    {
        Cpu = CpuBudget.Limited(1000),
        Memory = MemoryBudget.Limited(100.MB()),
        FileSystem = FileSystemCapability.ReadOnly("/data"),
        Network = null,  // No network access
        Vcom = VcomCapability.Only<IOrderProcessor, IInventory>()
    };
});
```

---

## 9. Process Lifecycle Operations

### 9.1 Creation

```csharp
// Explicit creation
var proc = Process.Create(options);

// Implicit (via VCOM grain)
// Grain activation creates implicit process

// Child process (inherits some parent capabilities)
var child = currentProcess.SpawnChild(options);
```

### 9.2 Suspension and Resume

```csharp
// Suspend: all Pathways reach safe points, execution pauses
await proc.SuspendAsync();

// Resume: continue from where suspended
await proc.ResumeAsync();
```

### 9.3 Checkpoint and Persistence

```csharp
// Checkpoint: capture state without persisting
var checkpoint = await proc.CheckpointAsync();

// Persist: save to storage
await checkpoint.SaveAsync("process-state.dnxi");

// Or combined:
await proc.SaveAsync("process-state.dnxi");

// Restore:
var proc = await Process.RestoreAsync("process-state.dnxi");
await proc.ResumeAsync();
```

### 9.4 Migration

```csharp
// Migrate to specific node
await proc.MigrateAsync(targetNodeId);

// Migrate with hints
await proc.MigrateAsync(new MigrationOptions
{
    PreferredNode = nodeId,
    DataLocality = true,  // Prefer node with relevant data
    Urgent = true         // Migrate ASAP
});
```

### 9.5 Termination

```csharp
// Graceful termination
await proc.TerminateAsync(timeout: 5.Seconds());

// Immediate termination (abandonment)
proc.Abandon();

// Termination with reason
proc.Terminate(TerminationReason.Completed);
proc.Terminate(TerminationReason.Error, exception);
proc.Terminate(TerminationReason.Abandoned);
```

---

## 10. Processes and Pathways

### 10.1 Relationship

```
Process contains Pathways:

Process (OrderProcessor)
│
├── Pathway: Main          [Running]
│   ├── Frame: ProcessOrder:42
│   ├── Frame: ValidateOrder:18
│   └── Frame: CheckInventory:7
│
├── Pathway: Validator     [Suspended]
│   └── Frame: DeepValidation:103
│
└── Pathway: Notifier      [Running]
    ├── Frame: SendEmail:22
    └── Frame: FormatMessage:9
```

### 10.2 Pathway Operations on Process

```csharp
// Spawn new Pathway within Process
var pathway = proc.SpawnPathway(async () =>
{
    await DoWork();
});

// List Pathways
foreach (var p in proc.Pathways)
{
    Console.WriteLine($"{p.Id}: {p.State}");
}

// Suspend specific Pathway
await proc.Pathways[0].SuspendAsync();

// Wait for Pathway
await pathway.WaitAsync();
```

### 10.3 Process-Wide vs Pathway-Specific Operations

| Operation | Process-Wide | Pathway-Specific |
|-----------|--------------|------------------|
| Suspend | All Pathways pause | One Pathway pauses |
| Resume | All Pathways continue | One Pathway continues |
| Checkpoint | All Pathways captured | One Pathway captured |
| Terminate | Process ends | Pathway ends, process continues |
| Migrate | All Pathways move | N/A (Pathway moves with process) |

---

## 11. Open Questions

### 11.1 Process Granularity
- Should every VCOM grain be its own process? Or process per activation group?
- How many Pathways is typical per process?
- What's the overhead target per process?

### 11.2 Isolation Strength
- Is logical isolation sufficient for all cases?
- When would per-process heap be needed? (Note: may not be achievable as hosted runtime)
- How does distributed trust affect isolation requirements?

### 11.3 Failure Model
- Does Pathway failure terminate the Process?
- Can a Process have supervision strategy for its Pathways?
- How does abandonment propagate?
- **Key context:** OS process isolation protects us at VM Node level. We need intra-node isolation (Pathway A crashes, B continues) and inter-node resilience (Node X dies, Node Y takes over).

### 11.4 Resource Accounting
- How granular is resource tracking (per-process? per-pathway?)
- What resources are tracked (CPU, memory, I/O, network)?
- How are limits enforced?

### 11.5 Security Hook Points (Gen-1 Critical)
- What interception points exist for security enforcement?
- Method calls, object access, resource access - where do checks go?
- **For gen-1:** Hook points must exist even if no-ops, to avoid retrofitting later.

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Singularity-Midori-Research.md | Source of design principles |
| DOTNExT-Execution-Pathways.md | Pathway model detail |
| DOTNExT-Scheduler-Design.md | How processes/pathways are scheduled |
| DOTNExT-Distribution-Levels.md | Inter-node process model |
| DOTNExT-Security-Model.md | Capability model detail |

---

*This document defines the DOTNExT Process Model - the fundamental abstraction for isolation, identity, and execution in the DOTNExT Virtual Operating System.*

*Version 1.1 - 2025-12-11 - Added security hook points consideration, clarified failure model context (hosted runtime benefits from OS isolation)*

*Version 1.0 - 2025-12-10 - Initial process model definition*
