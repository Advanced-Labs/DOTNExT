# DOTNExT Process Image Persistence

> **Document Type:** Vision & Technical Design
> **Version:** 2.0
> **Date:** 2025-12-10
> **Status:** STRATEGIC VISION - Novel capability for DOTNExT
> **Session:** Research session exploring CRIU-like capabilities in managed runtime
> **Key Update v2.0:** Reframed from Tasklet-specific to universal execution capture. Async was doorway, not destination. This is part of the universal execution model.

---

## 1. Executive Summary

This document describes a vision for **process image persistence** in DOTNExT - the ability to checkpoint a running managed process, serialize its complete state, and restore it later (potentially on a different machine). This is analogous to Linux CRIU but implemented **inside the runtime** rather than at the OS level.

**Key Insight:** The combination of GC heap walking + universal execution capture (techniques learned from Unwinder, but DOTNExT's own implementation) provides most of what we need. The missing pieces are orchestration and resurrection semantics.

**Evolution Context:**
- Started exploring via Async+ (Roslyn state machine persistence)
- Discovered Unwinder techniques enable capture at any safe point
- Realized we want universal capture, not just async
- Process Image Persistence is one application of the broader execution model

**Trade-off Philosophy:** DOTNExT trades speed/resources for capabilities. Checkpointing overhead is acceptable when AI is the bottleneck by orders of magnitude.

---

## 2. The Vision

### 2.1 What We Want

```csharp
// Checkpoint current process
var checkpoint = await ProcessImage.CaptureAsync();
await checkpoint.SaveToAsync("process-state.dnxi");

// Later, possibly different machine:
await ProcessImage.RestoreAsync("process-state.dnxi");
// Execution continues from checkpoint point
```

### 2.2 What This Enables

| Capability | Description |
|------------|-------------|
| **Process Migration** | Move running process between machines |
| **Hibernation** | Suspend to disk, resume later |
| **Fault Tolerance** | Checkpoint before risky operations |
| **Time Travel Debugging** | Restore to previous states |
| **VAYRON AI-Object Persistence** | Objects survive process restarts |
| **Distributed Execution** | Checkpoint on node A, resume on node B |

---

## 3. Why The VM Can Do This Internally

### 3.1 Linux CRIU vs VM-Internal

**CRIU (Linux):** Works from outside the process
- Kernel assistance required
- Process unaware of checkpointing
- Limited to what OS can capture

**VM-Internal (DOTNExT):** Works from inside the runtime
- No kernel changes needed
- Runtime has complete knowledge
- Works on any OS (including Windows)
- More control over what/how to capture

```
┌─────────────────────────────────────────────────────────────────┐
│  CRIU Approach (Linux only)                                     │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐       ┌─────────────┐                         │
│  │   Process   │ ◄───► │    Kernel   │ ◄───► CRIU              │
│  │  (unaware)  │       │  (assists)  │       (external)        │
│  └─────────────┘       └─────────────┘                         │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  VM-Internal Approach (DOTNExT - any OS)                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐   │
│  │   DOTNExT Runtime                                        │   │
│  │   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │   │
│  │   │  Managed    │  │  Checkpoint │  │  Serialize  │     │   │
│  │   │  Code       │  │  Manager    │  │  Engine     │     │   │
│  │   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘     │   │
│  │          │                │                │            │   │
│  │          └────────────────┴────────────────┘            │   │
│  │                           │                             │   │
│  │   ┌───────────────────────┴────────────────────────┐    │   │
│  │   │              Runtime Internals                  │    │   │
│  │   │  GC Heap │ Tasklets │ Type System │ JIT Cache  │    │   │
│  │   └────────────────────────────────────────────────┘    │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Windows Limitation - Why VM-Internal Matters

**Windows has no CRIU equivalent.** The kernel doesn't provide:
- Process memory snapshot/restore
- File descriptor capture
- Network state preservation

**But the VM doesn't need the kernel for managed state.** The runtime already tracks:
- All managed objects (GC)
- Execution state (Tasklets via Runtime-Async)
- Type information (metadata)

---

## 4. What We Have to Work With

### 4.1 GC Heap - All Managed Objects

The GC literally knows every managed object:

```cpp
// GC can enumerate all objects
void GCHeap::WalkHeap(ObjectWalker* walker)
{
    // Visits every allocated object
    // Knows: address, type, size, references
}
```

**For checkpoint:** Walk heap, serialize each object.

### 4.2 Execution State Capture (Unwinder-Inspired, DOTNExT Implementation)

Universal execution capture (DOTNExT's own construct, informed by Unwinder techniques) captures:
- All locals and temporaries
- Exact instruction pointer
- Register values
- Call chain
- Byrefs (stack pointers preserved)

**For checkpoint:** Serialize all captured execution state.

**Note:** DOTNExT may not use "Tasklets" directly. We study Unwinder techniques to inform our own design that supports universal capture at any safe point, not just async.

### 4.3 Type System - Metadata

Reflection provides:
- Type definitions
- Assembly information
- Field layouts

**For checkpoint:** Record type identity for deserialization.

### 4.4 What's Missing

| Component | Status | What's Needed |
|-----------|--------|---------------|
| Heap serialization | Available (GC walk) | Serialization format |
| Reference rebasing | Need to implement | Address → ID mapping |
| Execution state serialization | Techniques understood (Unwinder) | DOTNExT implementation |
| External resource handling | Need to design | Resurrection semantics |
| Type resolution on restore | Available (reflection) | Assembly loading |
| Generics support | Gap in Unwinder | Required for production |
| Exception handling | Gap in Unwinder | Required for production |

---

## 5. Process Image Format

### 5.1 Proposed Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  DOTNExT Process Image Format (.dnxi)                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Header                                                         │
│  ├── Magic: "DNXI" (DOTNExT Image)                             │
│  ├── Version: 1.0                                               │
│  ├── Timestamp: When captured                                   │
│  ├── Source node: Machine identifier                            │
│  ├── CLR version: Runtime compatibility                         │
│  └── Checksum: Integrity verification                           │
│                                                                 │
│  Assembly Table                                                 │
│  ├── [0] { Name, Version, PublicKeyToken, Location }           │
│  ├── [1] { Name, Version, PublicKeyToken, Location }           │
│  └── [N] ...                                                    │
│                                                                 │
│  Type Table                                                     │
│  ├── [0] { AssemblyIndex, TypeToken, FullName, FieldLayout }   │
│  ├── [1] ...                                                    │
│  └── [N] ...                                                    │
│                                                                 │
│  Object Table (serialized GC heap)                              │
│  ├── [0] { ObjectID, TypeIndex, SerializedFields }             │
│  │        Fields: { FieldToken, Value | ObjectRef }            │
│  ├── [1] ...                                                    │
│  └── [N] ...                                                    │
│                                                                 │
│  Static Field Table                                             │
│  ├── [0] { TypeIndex, FieldToken, Value | ObjectRef }          │
│  └── ...                                                        │
│                                                                 │
│  Execution Frame Table (captured execution state)               │
│  ├── [0] { FrameID, MethodToken, IP_Offset, Locals, Regs }     │
│  │        Locals: { SlotIndex, Value | ObjectRef }             │
│  │        Registers: { RegName, Value }                        │
│  │        (DOTNExT's own format, informed by Unwinder)         │
│  ├── [1] ...                                                    │
│  └── [N] ...                                                    │
│                                                                 │
│  Execution Pathway Table (captured pathways)                    │
│  ├── [0] { PathwayID, FrameIDs[] }  // Main pathway            │
│  ├── [1] { PathwayID, FrameIDs[] }  // Background pathway 1    │
│  └── ...                                                        │
│                                                                 │
│  External Resource Table                                        │
│  ├── [0] { ResourceID, Kind, ReconnectionInfo }                │
│  └── ...                                                        │
│                                                                 │
│  Root References (entry points)                                 │
│  ├── Main Execution Pathway                                     │
│  ├── Background Execution Pathways                              │
│  └── Static roots                                               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 Reference Serialization

Objects reference each other by address. On restore, addresses differ.

**Solution: Object ID table**

```
Capture:
  Object at 0x1000 → ObjectID 1
  Object at 0x2000 → ObjectID 2
  0x1000.field → 0x2000 becomes: ObjectID 1.field → ObjectRef(2)

Restore:
  Allocate ObjectID 1 at 0x5000
  Allocate ObjectID 2 at 0x6000
  Fixup: 0x5000.field → 0x6000
```

---

## 6. The Checkpoint/Restore API

### 6.1 Capture API

```csharp
public static class ProcessImage
{
    /// <summary>
    /// Capture current process state.
    /// </summary>
    public static async Task<ProcessCheckpoint> CaptureAsync(
        CheckpointOptions? options = null)
    {
        // 1. Request all threads reach safe points
        // 2. Walk GC heap, serialize all objects
        // 3. Capture all Tasklets (async continuations)
        // 4. Serialize static fields
        // 5. Record assembly references
        // 6. Package into ProcessCheckpoint
    }

    /// <summary>
    /// Save checkpoint to storage.
    /// </summary>
    public static async Task SaveAsync(
        ProcessCheckpoint checkpoint,
        Stream destination);

    /// <summary>
    /// Load and restore from checkpoint.
    /// </summary>
    public static async Task RestoreAsync(
        Stream source,
        RestoreOptions? options = null)
    {
        // 1. Load checkpoint data
        // 2. Load required assemblies
        // 3. Reconstruct type system
        // 4. Deserialize GC heap (allocate objects, fixup refs)
        // 5. Reconstruct Tasklets
        // 6. Call resurrection handlers
        // 7. Resume Tasklets
    }
}

public class CheckpointOptions
{
    /// <summary>Include JIT'd code cache (larger but faster restore).</summary>
    public bool IncludeJitCache { get; set; }

    /// <summary>Types to exclude from capture.</summary>
    public IEnumerable<Type>? ExcludedTypes { get; set; }

    /// <summary>Custom filter for objects.</summary>
    public Func<object, bool>? ObjectFilter { get; set; }

    /// <summary>Whether to capture thread state.</summary>
    public bool IncludeThreadState { get; set; } = true;
}

public class RestoreOptions
{
    /// <summary>How to handle missing assemblies.</summary>
    public AssemblyResolutionMode AssemblyResolution { get; set; }

    /// <summary>How to handle resurrection failures.</summary>
    public ResurrectionFailureMode FailureMode { get; set; }

    /// <summary>Custom resurrection context.</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
```

---

## 7. Resurrection Semantics

### 7.1 The Problem: External State

```
Before checkpoint:                After restore:
- File handle 0x100              - File might not exist
- Socket to server:8080          - Server might be down
- Database connection            - Transaction timed out
- Lock held                      - Lock holder gone
- Timer scheduled                - Timer context invalid
```

### 7.2 The Solution: IResurrectable

```csharp
/// <summary>
/// Marks a type as having resurrection semantics.
/// </summary>
public interface IResurrectable
{
    /// <summary>
    /// Called when object is restored from checkpoint.
    /// Return true if resurrection successful.
    /// </summary>
    Task<bool> OnResurrectAsync(ResurrectionContext context);

    /// <summary>
    /// Called if OnResurrectAsync returns false or throws.
    /// Perform compensation/cleanup.
    /// </summary>
    Task OnResurrectionFailedAsync(ResurrectionContext context);
}

public class ResurrectionContext
{
    /// <summary>When the checkpoint was captured.</summary>
    public DateTime CheckpointTime { get; }

    /// <summary>When restoration is happening.</summary>
    public DateTime ResurrectionTime { get; }

    /// <summary>Time elapsed.</summary>
    public TimeSpan Elapsed => ResurrectionTime - CheckpointTime;

    /// <summary>Original machine identifier.</summary>
    public string SourceNode { get; }

    /// <summary>Current machine identifier.</summary>
    public string TargetNode { get; }

    /// <summary>Whether on same machine.</summary>
    public bool IsSameMachine => SourceNode == TargetNode;

    /// <summary>Custom metadata from RestoreOptions.</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
```

### 7.3 Resurrection Patterns

**Pattern 1: Reconnect-on-Access**

```csharp
public class ResilientConnection : IResurrectable
{
    private Connection? _connection;
    private bool _isValid;

    public Task<bool> OnResurrectAsync(ResurrectionContext ctx)
    {
        _isValid = false;  // Force reconnect on next use
        return Task.FromResult(true);
    }

    public async Task<Connection> GetConnectionAsync()
    {
        if (!_isValid)
        {
            _connection = await ReconnectAsync();
            _isValid = true;
        }
        return _connection;
    }
}
```

**Pattern 2: Idempotent Operations**

```csharp
public class IdempotentOperation
{
    public Guid OperationId { get; }  // Survives checkpoint

    public async Task<Result> ExecuteAsync()
    {
        // Check if already executed
        var existing = await OperationLog.FindAsync(OperationId);
        if (existing != null) return existing;

        // Execute and record
        var result = await DoExecuteAsync();
        await OperationLog.RecordAsync(OperationId, result);
        return result;
    }
}
```

**Pattern 3: Temporal Versioning**

```csharp
public class TemporalReference<T>
{
    public Guid ObjectId { get; }
    public VectorClock Version { get; }

    public async Task<(T Value, Conflict? Conflict)> ResolveAsync()
    {
        var current = await Store.GetAsync<T>(ObjectId);
        if (current.Version.IsNewerThan(Version))
        {
            return (current, new Conflict(Version, current.Version));
        }
        return (current, null);
    }
}
```

---

## 8. Language Support (Future)

### 8.1 New Keywords

```csharp
// 'resilient' - marks resurrection-aware code block
resilient async Task ProcessOrder(Order order)
{
    // Everything here is checkpoint-safe

    external var db = await Database.ConnectAsync();  // Marked external

    checkpoint;  // Explicit safe point

    await db.CommitAsync();
}

// 'external' - marks resources needing reconnection
external class DatabaseConnection : IResurrectable
{
    // Runtime knows this needs special handling
}
```

### 8.2 Implicit Checkpointing

With "everything async" model:

```csharp
// In DOTNExT, methods are implicitly checkpoint-capable
public int Add(int a, int b)
{
    // Runtime can checkpoint at any safe point
    // Even without explicit async
    return a + b;
}
```

---

## 9. Universal Execution Model (Beyond "Everything Async")

### 9.1 The Insight

**Async was the doorway, not the destination.**

We started with async because Roslyn's state machine codegen made execution state visible. But the goal was never "better async" - it was **runtime control over execution state**.

If we capture execution state at any safe point (using techniques from Unwinder), we get something far more powerful than "everything async":

```csharp
// What looks like synchronous code:
public int Compute(int x)
{
    var result = 0;
    for (int i = 0; i < x; i++)
    {
        result += Process(i);
    }
    return result;
}

// Could be implicitly:
[MethodImpl(MethodImplOptions.ImplicitAsync)]
public int Compute(int x)
{
    var result = 0;
    for (int i = 0; i < x; i++)
    {
        __CheckSafePoint();  // Inserted by JIT
        result += Process(i);
    }
    return result;
}
```

### 9.2 What This Enables

- **Preemptive scheduling** - Like BEAM, yield at any safe point
- **Transparent checkpointing** - Capture at any safe point
- **Fair execution** - No code can monopolize
- **BEAM-like processes** on .NET
- **AI-controlled execution** - From managed space, AI can fork, rollback, speculate
- **Execution as data** - Pathways are first-class entities that can be inspected, compared, migrated

**Trade-off:** Runtime overhead at safe points. Acceptable because AI is the bottleneck by orders of magnitude.

### 9.3 Sync Scopes and Checkpointing

**The `sync` keyword affects checkpointing:**

```csharp
void ProcessData()
{
    // Checkpoint possible here

    sync
    {
        // NO checkpoint possible inside sync scope
        // Must complete atomically
        CriticalOperation();
    }

    // Checkpoint possible here again
}

var result = sync ProcessData();  // Entire call tree: no checkpoints
```

**Sync scope semantics for Process Image:**
- Sync scopes are atomic units - cannot checkpoint mid-scope
- Checkpoint must occur before entering or after exiting sync scope
- If process needs checkpointing during sync scope, it must wait
- `sync` methods and `sync` call-sites both create atomic boundaries

**Design consideration:** Should a running sync scope prevent process-wide checkpoint?
- Option A: Wait for all sync scopes to complete before checkpoint
- Option B: Sync scopes are pathway-local; other pathways can checkpoint
- Option C: Force-complete sync scope (potential semantic violation)

**Recommended:** Option B - sync scope is per-pathway. Process checkpoint waits only for that pathway's sync scopes.

---

## 10. Implementation Roadmap

### Phase 1: Proof of Concept
- [ ] GC heap serialization prototype
- [ ] Simple object graph capture/restore
- [ ] No Tasklets, just heap

### Phase 2: Execution State
- [ ] Execution frame capture (DOTNExT's own design, informed by Unwinder)
- [ ] Single method checkpoint/restore (any code, not just async)
- [ ] Basic resurrection handling

### Phase 3: Full Process
- [ ] Multi-thread support
- [ ] Static field handling
- [ ] Assembly resolution
- [ ] Cross-machine restore

### Phase 4: Integration
- [ ] VAYRON integration
- [ ] Resurrection semantics refinement
- [ ] Performance optimization

---

## 11. Open Questions

1. **Execution frame format** - DOTNExT's own design; what's the optimal structure?
2. **Generics support** - Unwinder gap; required for production use
3. **Exception handling** - Unwinder gap; required for production use
4. **Native resources** - How to handle P/Invoke state?
5. **Thread affinity** - Some code requires specific threads
6. **Finalizers** - How to handle pending finalizers?
7. **JIT code** - Regenerate or serialize?

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-RnD-Primer.md | Overall R&D context; async-as-doorway clarification |
| DOTNExT-Execution-Pathways.md | Universal execution model this enables |
| DOTNExT-Unwinder-Async2-Analysis.md | Unwinder techniques we're studying |
| DOTNExT-Unified-SafePoints.md | Safe points for checkpointing |
| Erlang-BEAM-Architecture-Reference.md | BEAM's process model inspiration |
| Vision-VAYRON-Platform.md | VAYRON uses this for persistence |

---

*This document describes a novel capability for DOTNExT: process image persistence implemented inside the managed runtime. This enables VAYRON's vision of objects that survive process restarts and migrate between machines.*

*Version 2.1 - 2025-12-10 - Added sync scope considerations for checkpointing*

*Version 2.0 - 2025-12-10 - Reframed: async was doorway not destination; universal execution model; DOTNExT's own constructs*

*Version 1.0 - 2025-12-08 - Initial vision*
