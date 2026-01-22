# VAYRON Platform Vision

> **VAYRON** is the name of our platform, runtime, and the philosophical approach: Virtual objects with composable Virtues, transparently managed by the runtime.

---

## Core Principle

**Any C# type can become Virtual with a switch. Virtual objects gain Virtues (persistence, distribution, replication, etc.) without changing how developers write code.**

```csharp
// This is just a normal C# class
[Virtual]
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Balance { get; set; }
}

// Usage is normal C#
var customer = new Customer { Id = 1, Name = "Alice", Balance = 1000m };
customer.Balance -= 50m;  // This write is transparently virtualized
```

No special base classes. No special property patterns. No `GetField<T>()`. Just C#.

---

## What "Virtual" Means

A Virtual Object is a CLR object whose:

1. **Object header** has the VIRTUAL bit set (bit 31 of sync block)
2. **Memory** is managed by the VAYRON Memory System, not just the GC heap
3. **Field access** may be intercepted by the runtime for virtualization
4. **Method calls** may be intercepted for remote dispatch
5. **Lifetime** extends beyond the CLR object's GC lifetime (persistence)
6. **Identity** is stable across process restarts, machines, time (OID)

The CLR object you hold is either:
- **Pattern A**: A "lens" containing pointers into Voron's memory arena
- **Pattern B**: A synchronized copy that mirrors Voron's authoritative state

---

## Virtues: Composable Capabilities

Virtues are orthogonal capabilities that can be combined:

| Virtue | What It Provides | Backend |
|--------|------------------|---------|
| `[Persistent]` | Survives process restart | Voron |
| `[Distributed]` | Can exist on remote nodes | Orleans |
| `[Replicated(N)]` | N synchronized instances | Voron sync + Orleans |
| `[Versioned]` | History of changes preserved | Voron MVCC |
| `[Relational]` | Graph edges to other objects | Voron + native graph engine |
| `[Semantic]` | Vector embeddings for AI/search | Voron + vector index |
| `[Secure]` | AuthZ/AuthN enforced at runtime | Identity federation |

Virtues compose:

```csharp
[Virtual]
[Persistent]
[Distributed]
[Replicated(3)]
public class Account
{
    public decimal Balance { get; set; }
}
// This Account: persists locally, can migrate to remote nodes,
// has 3 synchronized replicas across the cluster
```

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         C# Developer Code                                │
│                                                                          │
│   [Virtual] class Foo { ... }                                           │
│   var foo = new Foo();                                                  │
│   foo.Bar = 42;  // Just normal code                                    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      VAYRON Runtime (our CLR fork)                       │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    Runtime Virtualization Layer                     │ │
│  │                                                                      │ │
│  │  • Object Header: VIRTUAL bit detection                             │ │
│  │  • Type Metadata: Virtue configuration per type                     │ │
│  │  • Field Interception: JIT helpers redirect to Memory System        │ │
│  │  • Method Interception: vtable/method-table for remote dispatch     │ │
│  │  • GC Integration: Collection hints to Memory System                │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                    │                                     │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    VAYRON Memory System                             │ │
│  │                                                                      │ │
│  │  Unified memory management for Virtual Objects                      │ │
│  │  • Object Identity (OID) - stable across time/space                 │ │
│  │  • State synchronization (local ↔ Voron ↔ remote)                   │ │
│  │  • Transaction coordination                                         │ │
│  │  • Graph/relation traversal (native code)                           │ │
│  │                                                                      │ │
│  │  ┌─────────────────────┐  ┌─────────────────────┐                   │ │
│  │  │   Voron Engine      │  │  Orleans Engine     │                   │ │
│  │  │   (embedded)        │  │  (embedded)         │                   │ │
│  │  │                     │  │                     │                   │ │
│  │  │ • Page management   │  │ • Silo = VM instance│                   │ │
│  │  │ • B-trees, indexes  │  │ • Grain = Object    │                   │ │
│  │  │ • MVCC transactions │  │ • Networking        │                   │ │
│  │  │ • Persistence       │  │ • Activation        │                   │ │
│  │  │ • Replication sync  │  │ • Remoting          │                   │ │
│  │  └─────────────────────┘  └─────────────────────┘                   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Runtime Modifications Required

### 1. Object Header: VIRTUAL Bit

```cpp
// In syncblk.h
#define BIT_SBLK_IS_VIRTUAL 0x80000000  // Bit 31

// Fast check anywhere in runtime
inline bool IsVirtualObject(Object* obj) {
    return (obj->GetHeader()->GetBits() & BIT_SBLK_IS_VIRTUAL) != 0;
}
```

### 2. Type Metadata: Virtue Configuration

Each type with `[Virtual]` or related attributes needs runtime-accessible metadata:

```cpp
// Conceptual - actual implementation TBD
struct VirtueMetadata {
    uint32_t Flags;           // Which virtues are active
    VoronTypeId StorageType;  // Voron schema identifier
    uint32_t ReplicaCount;    // For [Replicated(N)]
    // ... more per-virtue config
};

// Accessible from MethodTable or EEClass
VirtueMetadata* GetVirtueMetadata(MethodTable* pMT);
```

### 3. Field Access Interception

Two approaches possible:

**Approach A: JIT Helper Interception**
```cpp
// Modify JIT_GetFieldAddr / JIT_SetFieldAddr
HCIMPL2(void*, JIT_GetFieldAddr, Object* obj, FieldDesc* pFD) {
    if (IsVirtualObject(obj)) {
        return VayronMemorySystem::GetFieldAddr(obj, pFD);
    }
    // Standard path
    return pFD->GetAddressGuaranteedInHeap(obj);
}
```

**Approach B: Write Barrier Extension**
```cpp
// Extend GC write barrier to also notify Memory System
void WriteBarrier(Object* obj, void* fieldAddr, Object* value) {
    // GC card table update...

    if (IsVirtualObject(obj)) {
        VayronMemorySystem::NotifyFieldWrite(obj, fieldAddr, value);
    }
}
```

### 4. Method Interception for Remote Dispatch

When a method is called on a Virtual Object that might be remote:

```cpp
// In method dispatch (simplified)
void* ResolveVirtualMethod(Object* obj, MethodDesc* pMD) {
    if (IsVirtualObject(obj) && MightBeRemote(obj)) {
        return GetRemoteDispatchStub(pMD);
    }
    // Standard vtable dispatch
    return obj->GetMethodTable()->GetSlot(pMD->GetSlot());
}
```

The remote dispatch stub:
1. Checks if object is currently local or remote
2. If local: call normally
3. If remote: serialize args, send to remote node, deserialize result

### 5. GC Integration

When a Virtual Object's CLR representation is collected:

```cpp
// In GC finalization or weak reference callback
void OnVirtualObjectCollected(Object* obj, VayronOid oid) {
    // Object is no longer activated in this VM
    // Memory System can:
    // - Release local memory (Pattern B's cached copy)
    // - Update activation state
    // - NOT delete from Voron (persistence survives GC)
    VayronMemorySystem::OnDeactivation(oid);
}
```

---

## Memory Patterns Deep Dive

### Pattern A: Pointer Indirection (Voron-Direct)

```
CLR Object (GC Heap)                 Voron Memory Arena
┌────────────────────────┐           ┌──────────────────────────────┐
│ SyncBlock (VIRTUAL=1)  │           │                              │
│ MethodTable*           │           │   Page 0x1000:               │
│ ─────────────────────  │           │   ┌────────────────────────┐ │
│ VayronMeta*        ────┼───┐       │   │ Id: 42                 │◄┼─┐
│ FieldPtrs[]:           │   │       │   │ Name: "Alice"          │◄┼─┼─┐
│   [0]: Id      ────────┼───┼───────┼──►│ Balance: 1000.00       │◄┼─┼─┼─┐
│   [1]: Name    ────────┼───┼───────┼───┼────────────────────────┘ │ │ │ │
│   [2]: Balance ────────┼───┼───────┼───┼──────────────────────────┘ │ │ │
└────────────────────────┘   │       │                                │ │ │
                             │       └────────────────────────────────┘ │ │
                             │                                          │ │
                             └──► VayronMeta knows which Voron page ────┘ │
                                  and can update pointers if Voron       │
                                  moves data                             │
                                                                         │
                                  Field read: deref pointer ─────────────┘
                                  Field write: call Voron API, Voron updates ptr
```

**Key implementation detail:** Voron must either:
- Guarantee stable addresses (pin pages)
- OR notify runtime when addresses change (callback to update FieldPtrs)

### Pattern B: Synchronized Copy (Mirrored)

```
CLR Object (GC Heap)                 Voron Memory Arena
┌────────────────────────┐           ┌──────────────────────────────┐
│ SyncBlock (VIRTUAL=1)  │           │                              │
│ MethodTable*           │           │   Page 0x1000:               │
│ ─────────────────────  │           │   ┌────────────────────────┐ │
│ VayronMeta*            │           │   │ Id: 42                 │ │
│ ─────────────────────  │  ◄─sync─► │   │ Name: "Alice"          │ │
│ Id: 42                 │           │   │ Balance: 1000.00       │ │
│ Name: "Alice"          │           │   └────────────────────────┘ │
│ Balance: 1000.00       │           │                              │
└────────────────────────┘           └──────────────────────────────┘

Field read:  Just read from CLR object (fast, local)
Field write: Write to CLR object, THEN sync to Voron
             OR write to Voron, THEN sync to CLR object
             (transaction semantics determine which)
```

**Key implementation detail:** Sync can be:
- **Eager**: Every write immediately syncs
- **Lazy**: Writes batched, sync on transaction commit
- **Event-driven**: Voron notifies on remote changes

---

## Orleans Integration: Naturalization

Orleans concepts map to our model:

| Orleans Concept | VAYRON Equivalent |
|----------------|-------------------|
| Silo | VM Instance (runtime process) |
| Grain Type | Type with `[Distributed]` virtue |
| Grain | Virtual Object instance |
| Grain Activation | Object "activated" (CLR object exists in some VM) |
| Grain State | Object fields (synced via Voron) |
| Grain Method | Method on Virtual Type |
| Grain Reference | Reference to possibly-remote object (OID-based) |

```csharp
// Orleans today:
public interface ICustomerGrain : IGrainWithIntegerKey {
    Task<decimal> GetBalance();
    Task Withdraw(decimal amount);
}

// VAYRON future:
[Virtual, Distributed]
public class Customer {
    public decimal Balance { get; set; }

    public void Withdraw(decimal amount) {
        Balance -= amount;
    }
}

// Usage - the runtime handles everything
Customer customer = VayronRuntime.Get<Customer>(customerId);
customer.Withdraw(50m);  // Might be local, might be remote - transparent
```

### What Orleans Brings to VAYRON

1. **Networking stack** - Silo-to-silo communication
2. **Placement strategies** - Where should an object activate?
3. **Activation lifecycle** - Idle deactivation, reactivation
4. **Cluster membership** - Which VMs are in the cluster?
5. **Streams** - Pub/sub for object events

### What VAYRON Changes About Orleans

1. **No interfaces required** - Just classes with attributes
2. **Properties work naturally** - Not just Task-returning methods
3. **Transparent proxies** - Runtime generates, not codegen
4. **Unified state** - Voron IS the state store, not a pluggable provider
5. **Unified identity** - OID works for persistence AND distribution

---

## Transactions Across Virtues

A single transaction might involve:

1. **Local field writes** (Memory System tracks)
2. **Voron persistence** (Voron transaction)
3. **Remote object modifications** (distributed transaction)
4. **Replication sync** (consistency protocol)

The Memory System coordinates:

```
Begin Transaction
    │
    ├─► Local writes tracked in transaction log
    │
    ├─► Voron transaction opened
    │
    ├─► Remote calls enlisted in distributed tx
    │
Commit
    │
    ├─► Voron commit (local durability)
    │
    ├─► Replication sync (consistency across replicas)
    │
    └─► Distributed commit (two-phase if needed)
```

---

## Development Phases

### Phase 1: Foundation (Single VM, Persistence Only)

**Goal:** One VM, `[Virtual, Persistent]` works, objects survive restart

- Runtime: VIRTUAL bit, type virtue metadata
- Runtime: Field write interception (JIT or write barrier)
- Voron: Embedded into runtime, C++/CLI or direct native calls
- Memory System: OID generation, object ↔ Voron mapping
- Memory System: Pattern A or B implementation
- GC: Deactivation hints to Memory System

**Validation:** Create object, set fields, restart process, object is there

### Phase 2: Relations (Graph Capabilities)

**Goal:** Objects can reference each other, graph traversal is fast

- OID-based references between Virtual Objects
- Native graph traversal (C++ in runtime)
- Voron indexes for edge traversal
- `[Relational]` virtue with edge types

**Validation:** Build object graph, traverse edges efficiently

### Phase 3: Distribution (Multi-VM)

**Goal:** Objects can exist on remote VMs, method calls work transparently

- Orleans networking integrated into runtime
- Remote method dispatch via vtable interception
- Activation/deactivation across cluster
- Placement strategies

**Validation:** Object on VM-A, call method from VM-B, it works

### Phase 4: Replication & Sync

**Goal:** Objects can have multiple synchronized instances

- Voron replication mechanisms activated
- Conflict resolution strategies
- `[Replicated(N)]` virtue
- Consistency levels (eventual, strong, etc.)

**Validation:** Modify object on VM-A, see change on VM-B

### Phase 5: Advanced Virtues

- `[Versioned]` - Time-travel queries
- `[Semantic]` - Vector embeddings, similarity search
- `[Secure]` - Runtime-enforced authorization

---

## Open Questions

1. **Pattern A vs B**: Which is primary? Support both?
   - Pattern B seems more pragmatic for reads
   - Pattern A might be needed for very large objects

2. **Voron integration depth**:
   - Embed as C++ library linked into runtime?
   - Keep as C# but with special runtime privileges?
   - Rewrite hot paths in C++?

3. **Orleans integration depth**:
   - Use Orleans as-is, adapt to our model?
   - Fork and deeply modify?
   - Take concepts but rewrite networking?

4. **Transaction semantics**:
   - ACID locally, eventual remotely?
   - Configurable per-type?
   - How do Voron transactions compose with distributed transactions?

5. **NewOrleans features**:
   - Which novel features (dynamic grains, etc.) carry over?
   - How do they map to the naturalized model?

---

## Appendix: Key Files in Current Codebase

### Runtime (to be modified)
- `src/runtime/src/coreclr/vm/syncblk.h` - Object header bits
- `src/runtime/src/coreclr/vm/object.h` - Object structure
- `src/runtime/src/coreclr/vm/jithelpers.cpp` - Field access helpers
- `src/runtime/src/coreclr/vm/methodtable.h` - Type metadata

### Voron (to be integrated)
- `src/Raven/src/Voron/` - Storage engine

### Orleans (to be integrated)
- `src/NewOrleans/` - Our Orleans fork with novel features

### Existing VAYRON Code (to be discarded or salvaged)
- `src/Vayron/` - Managed library approach (wrong architecture)
