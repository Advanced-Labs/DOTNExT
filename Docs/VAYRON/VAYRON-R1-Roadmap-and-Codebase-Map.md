# VAYRON R1 Roadmap and Codebase Map

> **Document Purpose:** Implementation roadmap and detailed analysis of existing DOTNExT infrastructure relevant to VAYRON implementation.

---

## Part I: Development Roadmap

### Phase 1: Open the CLR (Microkernel Bring-up) - *Current*

**Goal:** DDS routing works; DefaultDrivers proxy canonical CLR behavior.

**Deliverables:**
- DDS routing bit (`BIT_SBLK_DDS_NONDEFAULT`) implemented in object headers
- `ops_root*` resolution infrastructure (side-table initially)
- `ObjectModelDevice` interface + DefaultDriver
- `FieldAccessDevice` interface + DefaultDriver
- Intrinsic helpers for prototype testing (`VFieldRead`, `VFieldWrite`)
- Test suite validating no regression + basic non-default routing

**Key Activities:**
- Routing bit semantics + ops_root resolution
- ObjectModelDevice default driver (current CLR model)
- FieldAccessDevice default driver
- Syscall/intrinsic prototype tier

### Phase 2: Persistence Vertical Slice (First Non-Default Driver)

**Goal:** `[Virtual + Persistent]` (or C= equivalent) survives restart.

**Deliverables:**
- StorageDevice contract becomes real (VContext-aware)
- Voron-backed StorageDriver (uses existing Voron analysis below)
- Pattern B as initial ObjectModel (activation copy + durable body)
- FlushPersist mode: dirty tracking → batch commit on flush/turn-end
- Body layer v1: tagged field map encoding
- Validate: create -> mutate -> restart -> materialize by VUID

**See:** [Phase2/02-Phase2-StorageDevice-Voron.md](./Phase2/02-Phase2-StorageDevice-Voron.md) for detailed plan.

### Phase 3: Relational Substrate

**Goal:** Graph structure support for VObjects.

**Deliverables:**
- RelationalDevice driver (edges + reverse edges)
- Initial indexing may be backed by storage engine structures, later specialized

### Phase 4: Distribution

**Goal:** VObjects can execute remotely.

**Deliverables:**
- CallDispatchDevice real implementation
- Activation/placement driver family (Scynapse-derived)
- Single-writer rule enforced initially

### Phase 5+: Replication + Versioning + Semantic Hardening

**Deliverables:**
- Replication policies, quorum modes
- VersionDevice (checkpoint/read-at/diff/replay)
- SemanticDevice (embeddings/index), once metadata/layer slots are stable

**Cross-cutting (Early):** Security wiring should exist early at enforcement points (even permissive at first), to avoid bolt-on penalties.

---

## Part II: Codebase Analysis

### 1. Repository Overview

The DOTNExT repository is a VMR (Virtual Monolithic Repository) containing:

- **.NET Runtime** (`src/runtime/`) - CLR, JIT, GC, BCL
- **Roslyn** (`src/roslyn/`) - C#/VB compilers
- **SDK** (`src/sdk/`) - dotnet CLI, MSBuild tasks
- **Scynapse** (`src/Scynapse/`) - Forked Orleans distributed actor framework
- **RavenDB/Voron** (`src/Raven/`) - Document database with Voron storage engine

This combination provides the foundation for VAYRON's three key integration engines:
1. **CLR** - The extensibility target
2. **Voron** - Gen-0 StorageDevice driver
3. **Scynapse** - Gen-0 CallDispatch/Placement driver family

---

### 2. CLR Object Header Infrastructure

#### 2.1 Object Header Bit Layout

**Location:** `src/runtime/src/coreclr/vm/syncblk.h`

```cpp
// Current bit allocation in m_SyncBlockValue (32-bit):
//
// Bits 0-25:  SyncBlock index (26 bits = ~67M sync blocks)
// Bit 26:    BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX
// Bit 27:    BIT_SBLK_SPIN_LOCK
// Bits 28-29: BIT_SBLK_GC_RESERVE (2 bits)
// Bit 30:    Reserved
// Bit 31:    BIT_SBLK_UNUSED (0x80000000) <- AVAILABLE FOR DDS
```

**Key Finding:** Bit 31 (`BIT_SBLK_UNUSED = 0x80000000`) is explicitly documented as unused. This is the ideal location for the DDS routing bit.

#### 2.2 SyncBlock Table

**Location:** `src/runtime/src/coreclr/vm/syncblk.cpp`

The SyncBlock system provides per-object extended storage:

```cpp
class SyncBlock {
    InteropSyncBlockInfo* m_pInteropInfo;
    DWORD      m_dwHashCode;
    // ... monitor state, COM data, etc.
};

struct SyncTableEntry {
    SyncBlock* m_SyncBlock;
    Object*    m_Object;  // Weak back-reference
};
```

**Opportunity:** We can extend SyncTableEntry or SyncBlock to store `ops_root*` pointer, avoiding object layout changes for most objects.

#### 2.3 Object Class

**Location:** `src/runtime/src/coreclr/vm/object.h`

```cpp
class Object {
    MethodTable* m_pMethTab;  // First field after header

    ObjHeader* GetHeader() {
        return ((ObjHeader*)this) - 1;
    }

    MethodTable* GetMethodTable() { return m_pMethTab; }
    size_t GetSize();
    // ...
};
```

The object layout is:
```
[ObjHeader (8 bytes)] [MethodTable* (8 bytes)] [Fields...]
                       ^ Object pointer points here
```

---

### 3. Field Access Infrastructure

#### 3.1 FieldDesc

**Location:** `src/runtime/src/coreclr/vm/field.h`, `field.cpp`

```cpp
class FieldDesc {
    // Get field address within an object
    void* GetAddressGuaranteedInHeap(void* obj);

    // Field metadata
    DWORD GetOffset();
    CorElementType GetFieldType();
    MethodTable* GetApproxEnclosingMethodTable();
    // ...
};
```

#### 3.2 JIT Helpers for Field Access

**Location:** `src/runtime/src/coreclr/vm/jithelpers.cpp`

Key helpers that can be intercepted:

```cpp
// Get field address (for byref)
HCIMPL2(void*, JIT_GetFieldAddr, Object* obj, FieldDesc* pFD)

// Read object reference field
HCIMPL2(Object*, JIT_GetField, Object* obj, FieldDesc* pFD)

// Write object reference field
HCIMPL3(void, JIT_SetField, Object* obj, FieldDesc* pFD, Object* value)

// Write barrier
HCIMPL2(void, JIT_WriteBarrier, Object** dst, Object* ref)
```

**Strategy:** Modify these helpers to check the DDS routing bit and dispatch to drivers for non-default objects.

---

### 4. GC Integration Points

#### 4.1 Reference Scanning

**Location:** `src/runtime/src/coreclr/vm/gcenv.h`, `gc.cpp`

```cpp
// Callback for promoting references
typedef void (* promote_func)(Object**, ScanContext*, DWORD);

// GC interface to execution engine
class GCToEEInterface {
    void GcScanRoots(promote_func* fn, int condemned, ...);
    void GcStartWork(int condemned, int max_gen);
    // ...
};
```

#### 4.2 CGCDesc - Object Reference Layout

**Location:** `src/runtime/src/coreclr/vm/gcdesc.h`

```cpp
// Describes reference field layout for GC scanning
class CGCDesc {
    static CGCDesc* GetCGCDescFromMT(MethodTable* pMT);
    CGCDescSeries* GetHighestSeries();
    size_t GetNumSeries();
};
```

**For DDS:** Non-default ObjectModelDrivers must provide equivalent information via `ScanRefs()` interface.

#### 4.3 Write Barrier

**Location:** `src/runtime/src/coreclr/vm/gchelpers.cpp`

```cpp
// Standard write barrier
void SetObjectReference(Object** dst, Object* ref)
{
    *dst = ref;
    // Card table / remembered set update for generational GC
}
```

**Critical:** DDS FieldAccessDevice.WriteBarrier() must call through to proper GC write barrier or provide equivalent functionality.

---

### 5. Voron Storage Engine Analysis

#### 5.1 Architecture Overview

**Location:** `src/Raven/src/Voron/`

Voron is a page-based, MVCC, ACID storage engine with:

| Component | Purpose | Key File |
|-----------|---------|----------|
| StorageEnvironment | Main entry point | `StorageEnvironment.cs` |
| Transaction | Read/write isolation | `Impl/Transaction.cs` |
| LowLevelTransaction | Page-level operations | `Impl/LowLevelTransaction.cs` |
| AbstractPager | Storage abstraction | `Impl/Paging/AbstractPager.cs` |
| WriteAheadJournal | Durability | `Impl/Journal/WriteAheadJournal.cs` |
| Tree | B-Tree data structure | `Data/BTrees/Tree.cs` |

#### 5.2 Key Abstractions for StorageDevice

**AbstractPager Interface:**
```csharp
public abstract class AbstractPager : IDisposable {
    // Get raw page pointer
    public virtual byte* AcquirePagePointer(
        IPagerLevelTransactionState tx, long pageNumber);

    // Allocate pages
    public abstract void AllocateMorePages(ref PagerState state, long pageNumber);

    // Batch writes
    public virtual I4KbBatchWrites BatchWriter();
}
```

**Transaction Model:**
```csharp
public class Transaction : IDisposable {
    // Read/write access to trees
    public Tree ReadTree(string name);
    public Tree CreateTree(string name);

    // Commit/rollback
    public void Commit();
    public void Dispose();  // Rollback if not committed
}
```

**Tree Operations:**
```csharp
public class Tree {
    // Key-value operations
    public void Add(Slice key, byte[] value);
    public void Add(Slice key, long value);
    public ReadResult Read(Slice key);
    public bool Delete(Slice key);

    // Iteration
    public IIterator Iterate();
}
```

#### 5.3 MVCC Implementation

**Active Transactions Tracking:**
```csharp
// Location: Voron/Util/ActiveTransactions.cs
public class ActiveTransactions {
    private long _oldestTransaction;  // Determines page cleanup eligibility

    public void Add(LowLevelTransaction tx);
    public void Remove(long txId);
}
```

**Single-Writer Guarantee:**
```csharp
// Location: StorageEnvironment.cs
private readonly SemaphoreSlim _transactionWriter = new(1, 1);

public Transaction WriteTransaction() {
    _transactionWriter.Wait();  // Only one writer at a time
    // ...
}
```

#### 5.4 Durability Mechanism

**Write-Ahead Journal:**
```csharp
// Location: Voron/Impl/Journal/WriteAheadJournal.cs
public class WriteAheadJournal {
    // Write transaction to journal before data file
    public void WriteToJournal(LowLevelTransaction tx, ...);

    // Recovery on startup
    public void RecoverDatabase(TransactionHeader* header, Action<...> recovery);
}
```

#### 5.5 Voron -> StorageDevice Mapping

| VAYRON Concept | Voron Implementation |
|----------------|---------------------|
| `Persist(obj, vuid)` | `tree.Add(vuid.ToSlice(), Serialize(obj))` |
| `Materialize(vuid)` | `tree.Read(vuid.ToSlice())` -> Deserialize |
| `BeginTransaction()` | `env.WriteTransaction()` |
| `CommitTransaction()` | `tx.Commit()` |
| `IsDirty(obj)` | Track in side-table (not Voron native) |
| MVCC reads | Transaction snapshot isolation |

---

### 6. Scynapse (Orleans Fork) Analysis

#### 6.1 Grain System Overview

**Location:** `src/Scynapse/src/Orleans.Core/`

Orleans provides:
- **Grains** - Virtual actors with single-threaded execution
- **Silos** - Cluster nodes hosting grain activations
- **Placement** - Strategy for where grains run
- **Messaging** - Cross-silo RPC

#### 6.2 Key Abstractions for CallDispatchDevice

**Grain Reference:**
```csharp
// Location: Orleans.Core/Core/GrainReference.cs
public class GrainReference : IAddressable {
    internal GrainId GrainId { get; }
    internal IGrainReferenceRuntime Runtime { get; }

    // Invoke method on grain (may be remote)
    public Task<TResult> InvokeMethodAsync<TResult>(
        int methodId, object[] args);
}
```

**Method Invocation Pipeline:**
```csharp
// Location: Orleans.Core/Runtime/GrainMethodInvoker.cs
internal class GrainMethodInvoker {
    public Task<object> Invoke(
        IGrainContext grainContext,
        InvokeMethodRequest request) {

        // Execute through filter pipeline
        var context = new IncomingCallFilterContext(grainContext, request);
        return InvokeWithFilters(context);
    }
}
```

**Placement Directors:**
```csharp
// Location: Orleans.Core/Placement/IPlacementDirector.cs
public interface IPlacementDirector {
    Task<SiloAddress> OnAddActivation(
        PlacementStrategy strategy,
        PlacementTarget target,
        IPlacementContext context);
}
```

#### 6.3 Interception/Filter System

```csharp
// Location: Orleans.Core/Filters/
public interface IIncomingGrainCallFilter {
    Task Invoke(IIncomingGrainCallContext context);
}

public interface IOutgoingGrainCallFilter {
    Task Invoke(IOutgoingGrainCallContext context);
}
```

**This is directly applicable to VAYRON:** The filter pipeline provides interception points for method calls, exactly what CallDispatchDevice needs.

#### 6.4 Grain Lifecycle

```csharp
// Location: Orleans.Core/Core/IGrainContext.cs
public interface IGrainContext {
    GrainId GrainId { get; }
    GrainAddress Address { get; }
    ActivationId ActivationId { get; }

    // Lifecycle
    void Activate(Dictionary<string, object> requestContext);
    void Deactivate(DeactivationReason reason);
}
```

#### 6.5 Orleans -> VAYRON Mapping

| VAYRON Concept | Orleans Equivalent |
|----------------|-------------------|
| VUID | GrainId |
| VObject activation | Grain activation |
| PlacementDevice | IPlacementDirector |
| CallDispatchDevice.Invoke | GrainMethodInvoker.Invoke |
| Remote reference | GrainReference |
| Single-writer | Single-threaded grain turn execution |
| Interception hooks | IIncoming/OutgoingGrainCallFilter |

---

### 7. Integration Opportunities

#### 7.1 Voron as StorageDevice Engine

**Data Model:**
```
VUID Tree:        vuid -> serialized object bytes
Type Index Tree:  (typeId, vuid) -> empty  // For type-based queries
Dirty Set:        vuid -> timestamp        // Pending persistence
```

**Transaction Integration:**
```csharp
// Wrap Voron transaction as IStorageOps transaction
class VoronStorageOps : IStorageOps {
    private Transaction _voronTx;

    public void BeginTransaction() {
        _voronTx = _env.WriteTransaction();
    }

    public bool CommitTransaction() {
        _voronTx.Commit();
        return true;
    }
}
```

#### 7.2 Scynapse as CallDispatchDevice Engine

**Approach:**
1. VObject with `[Distributed]` gets a GrainReference internally
2. Method calls route through Orleans message pipeline
3. Single-writer guarantee from Orleans grain model

**Mapping:**
```csharp
class OrleansCallDispatchOps : ICallDispatchOps {
    public object Invoke(object obj, MethodInfo method, object[] args) {
        var grainRef = GetGrainReference(obj);
        return grainRef.InvokeMethodAsync(method.MetadataToken, args);
    }
}
```

#### 7.3 Combined: Persistent Distributed Objects

With both engines integrated:

```csharp
[Virtual]
[Persistent]      // -> Voron StorageDevice
[Distributed]     // -> Orleans CallDispatchDevice
public class Customer {
    public int Id { get; set; }
    public string Name { get; set; }
}

// Usage:
var customer = await VRuntime.Materialize<Customer>(customerId);
customer.Name = "Updated";  // -> Tracked dirty, persisted to Voron
await customer.CalculateBalance();  // -> May execute remotely via Orleans
```

---

### 8. Implementation Feasibility Assessment

#### 8.1 What's Straightforward

| Area | Why |
|------|-----|
| Header bit repurposing | BIT_SBLK_UNUSED explicitly available |
| Side-table for ops_root | SyncBlock pattern exists |
| Default drivers | Just wrap existing code |
| Voron integration | Clean API, works out-of-process today |
| Orleans integration | Well-abstracted, filter-based |

#### 8.2 What Requires Care

| Area | Challenge | Mitigation |
|------|-----------|------------|
| GC object relocation | Must update ops_root table | Hook into existing GC relocation path |
| JIT codegen changes | Complex, risky | Defer to Phase 2+, use helpers for Phase 1 |
| Write barrier correctness | GC depends on it | Default driver always calls real barrier |
| Serialization | CLR objects have complex graphs | Start with simple value types |

#### 8.3 What's Hard (Phase 3+)

| Area | Complexity |
|------|------------|
| True alternate object layouts | GC/JIT need per-model maps |
| Zero-copy Voron <-> CLR | Memory mapping, pinning |
| Distributed transactions | 2PC across Voron + Orleans |
| Live migration | Object identity across process boundaries |

---

### 9. Key Files Reference

#### CLR Core
```
src/runtime/src/coreclr/vm/
├── syncblk.h/cpp          # Object header, SyncBlock
├── object.h/cpp           # Object class
├── field.h/cpp            # FieldDesc
├── methodtable.h/cpp      # Type metadata
├── jithelpers.cpp         # JIT helper functions
├── jitinterface.cpp       # JIT-VM interface
├── gcenv.h                # GC environment
└── gchelpers.cpp          # GC helpers

src/runtime/src/coreclr/gc/
├── gc.cpp                 # GC core
├── gcenv.ee.cpp           # GC-EE interface
└── gcdesc.h               # Reference layout
```

#### Voron
```
src/Raven/src/Voron/
├── StorageEnvironment.cs          # Main API
├── Impl/
│   ├── Transaction.cs             # High-level tx
│   ├── LowLevelTransaction.cs     # Page-level tx
│   ├── Paging/AbstractPager.cs    # Storage abstraction
│   └── Journal/WriteAheadJournal.cs  # Durability
└── Data/BTrees/Tree.cs            # B-Tree
```

#### Scynapse
```
src/Scynapse/src/Orleans.Core/
├── Core/
│   ├── GrainReference.cs          # Remote reference
│   ├── IGrainContext.cs           # Activation context
│   └── Grain.cs                   # Base class
├── Runtime/
│   └── GrainMethodInvoker.cs      # Method dispatch
├── Placement/
│   └── IPlacementDirector.cs      # Placement strategy
└── Filters/
    └── IGrainCallFilter.cs        # Interception
```

---

### 10. Next Steps for Phase 1

1. **Validate Header Bit** - Confirm BIT_SBLK_UNUSED is truly unused
2. **Prototype Side Table** - SyncBlockIndex-keyed hash map in test harness
3. **Implement Default Drivers** - Verify wrapping works
4. **DDS Routing Tests** - Validate routing bit + default driver passthrough
5. **Performance Baseline** - Measure overhead of routing check

**Phase 2 (after Phase 1 complete):**
- Integration test with Voron persistence (StorageDevice becomes real)

See [Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md](./Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md) for detailed implementation plan.

---

## Appendix: Glossary

| Term | Definition |
|------|------------|
| DDS | Device Driver System - the pluggability mechanism |
| SAL | Software Abstraction Layer - conceptual layer DDS implements |
| OpsRoot | Per-object driver dispatch table root |
| VObject | Virtualized object instance (runtime view) |
| Varia | Whole object across space+time (conceptual) |
| VUID | Virtual Unique Identifier (UUID v7) |
| DefaultDriver | Driver that proxies standard CLR behavior |
| Device Class | Interface contract for a runtime concern |
| VMR | Virtual Monolithic Repository |

---

*VAYRON R1 Roadmap and Codebase Map - Advanced-Labs/DOTNExT*
