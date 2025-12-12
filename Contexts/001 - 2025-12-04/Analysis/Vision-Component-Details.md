# DOTNExT Component Detailed Specifications

> **Document Type:** Component Detail
> **Version:** 1.0
> **Date:** 2025-12-05
> **Parent:** Vision-DOTNExT-Memory-Architecture.md

---

## 1. Central Memory System (CMS) - Detailed Specification

### 1.1 Core Purpose

The CMS is the **unified authority** over all memory in a DOTNExT process. It replaces the fragmented approach where managed heap, native allocators, and persistence systems operate independently.

### 1.2 Responsibilities Matrix

| Responsibility | Description | Interfaces With |
|---------------|-------------|-----------------|
| Universal tracking | Know about ALL memory allocations | Native allocators, MOM |
| Unified model | Single conceptual model for all memory | Everything |
| Coordination | Orchestrate between subsystems | MOM, ORION, Drivers |
| Driver management | Load, configure, route to drivers | Memory Drivers |
| Persistence strategy | Decide what/when/how to persist | Application, Drivers |
| Cross-node awareness | Know about remote Engrams | Network, other nodes |

### 1.3 Internal Data Structures

**Allocation Registry:**
```
┌─────────────────────────────────────────────────────────────┐
│                    ALLOCATION REGISTRY                       │
├─────────────────────────────────────────────────────────────┤
│ Native Allocations Table                                    │
│  • Address → { size, alloc_site, timestamp, tag }           │
│  • Lightweight, no UUID unless escalated                    │
├─────────────────────────────────────────────────────────────┤
│ Managed Allocations Index                                   │
│  • UUID → { current_address, type, level, status }          │
│  • Bidirectional: Address → UUID (for GC compaction)        │
├─────────────────────────────────────────────────────────────┤
│ Remote Engram Cache                                         │
│  • UUID → { origin_node, type_hint, last_seen, status }     │
│  • Known Engrams not currently local                        │
├─────────────────────────────────────────────────────────────┤
│ Persistence State                                           │
│  • UUID → { driver, location, version_stored, dirty }       │
│  • What's persisted where                                   │
└─────────────────────────────────────────────────────────────┘
```

**Node Registry:**
```
┌─────────────────────────────────────────────────────────────┐
│                      NODE REGISTRY                           │
├─────────────────────────────────────────────────────────────┤
│ Local Node Identity                                         │
│  • Node UUID                                                │
│  • Capabilities                                             │
│  • Domain membership (if any)                               │
├─────────────────────────────────────────────────────────────┤
│ Known Peers                                                 │
│  • Node UUID → { address, capabilities, trust_level }       │
│  • Last contact, connection status                          │
├─────────────────────────────────────────────────────────────┤
│ Domain Configuration                                        │
│  • Domain UUID (if clustered)                               │
│  • Role in domain                                           │
│  • Coordination rules                                       │
└─────────────────────────────────────────────────────────────┘
```

### 1.4 CMS APIs

**For MOM (Managed Object Manager):**
```cpp
// Called on every managed allocation
void CMS_RegisterManagedAllocation(
    void* address,
    MethodTable* type,
    GUID uuid,
    EngramLevel level
);

// Called when GC moves an object
void CMS_UpdateAddress(GUID uuid, void* newAddress);

// Called when object is collected
void CMS_NotifyDeath(GUID uuid);

// Check if object should be persisted
PersistenceHint CMS_GetPersistenceHint(MethodTable* type);
```

**For ORION:**
```cpp
// Get Engram data for graph operations
EngramView CMS_GetEngramView(GUID uuid);

// Query for Engrams matching criteria
EngramQueryResult CMS_QueryEngrams(EngramQuery query);

// Get remote Engram (may trigger fetch)
EngramView CMS_GetRemoteEngram(GUID uuid, FetchStrategy strategy);
```

**For Memory Drivers:**
```cpp
// Store Engram to driver
void CMS_Store(IMemoryDriver* driver, Engram engram);

// Retrieve Engram from driver
Engram CMS_Retrieve(IMemoryDriver* driver, GUID uuid);

// Sync Engram with stored version
SyncResult CMS_Sync(IMemoryDriver* driver, GUID uuid, SyncMode mode);
```

**For Native Code:**
```cpp
// Register native allocation (optional, for tracking)
void CMS_RegisterNativeAllocation(void* address, size_t size, const char* tag);

// Escalate native allocation to Engram
GUID CMS_EscalateToEngram(void* address, TypeHint hint);
```

### 1.5 Configuration Points

```json
{
  "CMS": {
    "trackNativeAllocations": true,
    "nativeTrackingThreshold": 1024,
    "defaultEngramLevel": 2,
    "persistenceDefaults": {
      "autoSave": true,
      "autoSaveInterval": "00:05:00",
      "defaultDriver": "memantics"
    },
    "remoteEngrams": {
      "cacheSize": 10000,
      "fetchStrategy": "lazy",
      "maxFetchTimeout": "00:00:30"
    }
  }
}
```

---

## 2. Managed Object Manager (MOM) - Detailed Specification

### 2.1 Evolution from GC

The MOM extends the GC with identity, lifecycle awareness, and integration hooks. It does NOT replace the GC's core algorithms - it wraps and extends them.

**Layered Architecture:**
```
┌─────────────────────────────────────────────────────────────┐
│                    MOM (New Layer)                          │
│    Identity · Lifecycle Events · ORION Integration          │
├─────────────────────────────────────────────────────────────┤
│                    GC (Existing)                            │
│    Allocation · Collection · Compaction · Generations       │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Extended Object Header

Building on the existing object header structure:

```
Standard .NET Object (64-bit):
┌─────────────────────────┐  ← -8 bytes
│  Alignment (4 bytes)    │
│  SyncBlockValue (4B)    │  ← Bit 31 = HasEngramData
├─────────────────────────┤  ← 0 (object pointer)
│  MethodTable* (8B)      │
├─────────────────────────┤
│  Instance fields        │
└─────────────────────────┘

DOTNExT Extended (via SyncBlock):
When HasEngramData = 1, SyncBlock contains:
┌─────────────────────────┐
│  Standard SyncBlock     │
│  • Lock info            │
│  • Hash code            │
├─────────────────────────┤
│  MOM Extension          │
│  • UUID (16 bytes)      │
│  • Engram Level (1B)    │
│  • Version (4B)         │
│  • Flags (1B)           │
│  • Reserved (2B)        │
└─────────────────────────┘
```

### 2.3 UUID Generation

**Strategy:** UUIDv7 (time-ordered)

**Why UUIDv7:**
- Sortable by creation time
- Intrinsic timestamp
- Good hash distribution
- 128-bit namespace (effectively infinite)

**Generation Points:**
1. Object allocation (for Engram-enabled types)
2. Escalation (when non-Engram becomes Engram)
3. Loading (when Engram arrives from elsewhere)

**Collision Handling:**
- UUIDv7 collisions are astronomically unlikely
- If detected: generate new UUID, record mapping
- Remote UUIDs are never overwritten locally

### 2.4 Write Barrier Extension

The MOM extends write barriers to feed ORION:

```cpp
// Standard write barrier (existing)
void WriteBarrier(Object** dst, Object* src) {
    *dst = src;
    UpdateCardTable(dst);  // For GC
}

// MOM extended write barrier
void WriteBarrierMOM(Object** dst, Object* src, Object* container) {
    WriteBarrier(dst, src);  // Standard behavior

    if (container->HasEngramData()) {
        // Calculate field offset
        size_t fieldOffset = (byte*)dst - (byte*)container;

        // Notify ORION of relationship change
        ORION_RecordRelation(
            MOM_GetUUID(container),
            fieldOffset,
            src ? MOM_GetUUID(src) : GUID_NULL
        );
    }
}
```

### 2.5 Lifecycle Events

MOM generates events for CMS and ORION:

| Event | Trigger | Data | Consumers |
|-------|---------|------|-----------|
| Birth | Allocation | UUID, type, level | CMS, ORION |
| Mutation | Field write | UUID, field, old→new | ORION |
| Move | GC compaction | UUID, old→new address | CMS |
| Death | Collection | UUID | CMS, ORION |
| Escalation | Level upgrade | UUID, old→new level | CMS |

### 2.6 Integration with CGCDesc

MOM leverages CGCDesc (GC descriptor) for relationship discovery:

```cpp
// Get all reference fields for a type
void MOM_EnumerateReferences(Object* obj, ReferenceCallback callback) {
    MethodTable* mt = obj->GetMethodTable();
    CGCDesc* gcDesc = CGCDesc::GetCGCDescFromMT(mt);

    if (gcDesc == nullptr) return;  // No references

    size_t numSeries = gcDesc->GetNumSeries();
    CGCDescSeries* series = gcDesc->GetLowestSeries();

    for (size_t i = 0; i < numSeries; i++) {
        byte* start = (byte*)obj + series->startoffset;
        byte* end = start + series->seriessize;

        for (byte* ptr = start; ptr < end; ptr += sizeof(Object*)) {
            Object* ref = *(Object**)ptr;
            if (ref != nullptr) {
                callback(obj, (size_t)(ptr - (byte*)obj), ref);
            }
        }
        series++;
    }
}
```

---

## 3. ORION (Object Relationship and Intelligence Network) - Detailed Specification

### 3.1 Core Purpose

ORION is the **graph and semantic engine** for the DOTNExT object model. It maintains the "web" of relationships between objects and provides query capabilities over that structure.

### 3.2 Graph Model

**Nodes:**
- Every Engram (Level 2+) is a node
- Node data: UUID, type, semantic embedding (if Level 4+)

**Edges:**
- Every object reference is an edge
- Edge data: source UUID, field offset, field name, target UUID
- Optional: relationship type, semantic embedding, weight

**Graph Structure:**
```
┌─────────────────────────────────────────────────────────────┐
│                     ORION GRAPH STORE                        │
├─────────────────────────────────────────────────────────────┤
│ Node Index                                                  │
│  • UUID → { type, embedding_ptr, metadata }                 │
│  • Type → { list of UUIDs }                                 │
├─────────────────────────────────────────────────────────────┤
│ Edge Index                                                  │
│  • Source UUID → { list of (field, target UUID) }           │
│  • Target UUID → { list of (source UUID, field) } [reverse] │
├─────────────────────────────────────────────────────────────┤
│ Semantic Index (Level 4+)                                   │
│  • Vector index for similarity search                       │
│  • Supports: objects, fields, relationships                 │
├─────────────────────────────────────────────────────────────┤
│ Query Cache                                                 │
│  • Recent query results                                     │
│  • Invalidated on graph changes                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 ORION APIs

**Graph Manipulation (internal, fed by MOM):**
```cpp
void ORION_AddNode(GUID uuid, TypeInfo type, void* embedding);
void ORION_RemoveNode(GUID uuid);
void ORION_RecordRelation(GUID source, size_t fieldOffset, GUID target);
void ORION_RemoveRelation(GUID source, size_t fieldOffset);
void ORION_UpdateEmbedding(GUID uuid, void* newEmbedding);
```

**Graph Query (application-facing):**
```csharp
// Basic navigation
IEnumerable<EngramRef> GetReferences(Guid uuid);
IEnumerable<EngramRef> GetReferencedBy(Guid uuid);

// Path finding
IEnumerable<GraphPath> FindPaths(Guid from, Guid to, int maxDepth);

// Pattern matching
IEnumerable<EngramRef> Match(GraphPattern pattern);

// Semantic search (Level 4+)
IEnumerable<EngramRef> FindSimilar(Guid uuid, float threshold);
IEnumerable<EngramRef> FindByEmbedding(float[] embedding, int topK);

// Typed queries
IEnumerable<T> Query<T>(Expression<Func<T, bool>> predicate);
```

**Example Queries:**
```csharp
// Find all objects referencing this one
var referrers = orion.GetReferencedBy(myUuid);

// Find path between two objects
var paths = orion.FindPaths(orderUuid, customerUuid, maxDepth: 5);

// Find similar objects (semantic)
var similar = orion.FindSimilar(productUuid, threshold: 0.85f);

// Complex pattern
var pattern = GraphPattern.Create()
    .Node<Order>(o => o.Status == "Pending")
    .Edge("Customer")
    .Node<Customer>(c => c.Region == "EU");
var matches = orion.Match(pattern);
```

### 3.4 Semantic Encoding Integration

**Embedding Sources:**
1. **Application-provided:** Explicitly set embeddings
2. **Computed:** Runtime-computed from object content
3. **Inherited:** From type-level embeddings
4. **Relationship-based:** Derived from graph position

**Embedding Storage:**
```cpp
struct SemanticData {
    float* objectEmbedding;      // Whole-object semantic
    FieldEmbedding* fields;      // Per-field (optional)
    RelationEmbedding* edges;    // Per-relationship (optional)
    size_t embeddingDim;         // Vector dimension
};
```

**Vector Index:**
- ORION maintains a vector index (HNSW or similar)
- Supports approximate nearest neighbor search
- Updated incrementally as embeddings change

### 3.5 NOT ORION's Responsibility

To maintain clear boundaries:
- **NOT:** Memory allocation (that's MOM)
- **NOT:** Persistence to storage (that's CMS + Drivers)
- **NOT:** Cross-node transport (that's CMS)
- **NOT:** Raw memory tracking (that's CMS)

ORION is the **live graph** of objects currently in memory. Persistence snapshots the graph; loading restores it.

---

## 4. Memory Driver System - Detailed Specification

### 4.1 Driver Interface

```csharp
public interface IMemoryDriver
{
    // Identity
    string DriverId { get; }
    DriverCapabilities Capabilities { get; }

    // Core operations
    Task StoreAsync(Engram engram, StoreOptions options);
    Task<Engram> RetrieveAsync(Guid uuid, RetrieveOptions options);
    Task DeleteAsync(Guid uuid);

    // Batch operations
    Task StoreBatchAsync(IEnumerable<Engram> engrams);
    Task<IEnumerable<Engram>> RetrieveBatchAsync(IEnumerable<Guid> uuids);

    // Query (if supported)
    Task<IEnumerable<Engram>> QueryAsync(EngramQuery query);

    // Sync (if supported)
    Task<SyncResult> SyncAsync(Guid uuid, SyncMode mode);

    // Subscription (if supported)
    IDisposable Subscribe(EngramPattern pattern, Action<EngramEvent> handler);
}

[Flags]
public enum DriverCapabilities
{
    None = 0,
    Query = 1,
    Sync = 2,
    Subscribe = 4,
    Transactions = 8,
    SemanticSearch = 16,
    Distributed = 32,
    Versioning = 64
}
```

### 4.2 Built-in Drivers

| Driver | Target | Capabilities |
|--------|--------|--------------|
| `NativeEngramDriver` | DOTNExT binary format | All |
| `FileSystemDriver` | Local files | Basic |
| `JsonDriver` | JSON files | Basic + human readable |
| `SqlDriver` | SQL databases | Query, Transactions |
| `Neo4jDriver` | Neo4j | Query, SemanticSearch |
| `RedisDriver` | Redis | Fast, Distributed |
| `MemanticsDriver` | Memantics | All + Living Memory |

### 4.3 Driver Discovery and Loading

```csharp
// Built-in discovery
var driver = MemoryDrivers.Get("filesystem");

// Configuration-based
// In appsettings.json:
{
  "MemoryDrivers": {
    "default": "memantics",
    "drivers": {
      "backup": {
        "type": "filesystem",
        "path": "./backup"
      },
      "cache": {
        "type": "redis",
        "connection": "localhost:6379"
      }
    }
  }
}

// Plugin drivers (loaded from assembly)
MemoryDrivers.LoadDriver("MyCompany.CustomDriver.dll");
```

### 4.4 Usage Patterns

**Pattern 1: Explicit API**
```csharp
var driver = MemoryDrivers.Get("postgresql");
var engram = CMS.ExtractEngram(myObject);
await driver.StoreAsync(engram);
```

**Pattern 2: Attribute-Based**
```csharp
[Engram(Level = 3)]
[Persist(Driver = "memantics", Mode = PersistMode.OnChange)]
public class Order
{
    public Guid Id { get; set; }
    public Customer Customer { get; set; }
    public List<OrderItem> Items { get; set; }
}
// Persistence happens automatically based on attributes
```

**Pattern 3: Configuration-Based**
```json
{
  "Persistence": {
    "rules": [
      {
        "types": ["MyApp.Domain.*"],
        "driver": "memantics",
        "mode": "eventual"
      },
      {
        "types": ["MyApp.Cache.*"],
        "driver": "redis",
        "ttl": "01:00:00"
      }
    ]
  }
}
```

**Pattern 4: Implicit Defaults**
```csharp
// No attributes, no config - runtime chooses based on heuristics
// Types marked [Engram] get persisted to default driver
// Transient objects are not persisted
```

---

## 5. Memantics - Detailed Specification

### 5.1 Core Concept

Memantics is not just storage - it's **living semantic memory**. It differs from traditional databases in fundamental ways.

### 5.2 The Four Pillars

**Pillar 1: Everything is Semantic**
- All data has meaning (embeddings)
- Queries can be semantic (find similar, not just find exact)
- Relationships have semantic weight
- Types have semantic signatures

**Pillar 2: Code is Data**
- Type definitions are stored as Engrams
- Methods, IL, source - all persistable
- Replaces source code repositories
- Type evolution is first-class

**Pillar 3: Memory is Alive**
- Not just CRUD operations
- Access patterns affect storage
- Associations strengthen with use
- Unused memories can be "forgotten" (GC'd)
- Temporal awareness (versions, history)

**Pillar 4: Native Integration**
- Zero translation overhead with DOTNExT
- Same Engram format throughout
- Direct ORION ↔ Memantics bridge
- Optimal for the platform

### 5.3 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       MEMANTICS                              │
├─────────────────────────────────────────────────────────────┤
│                    Semantic Layer                            │
│  • Vector indices (multiple embedding spaces)               │
│  • Associative linking                                      │
│  • Temporal indexing                                        │
├─────────────────────────────────────────────────────────────┤
│                    Graph Layer                               │
│  • Persistent ORION-compatible graph                        │
│  • Type hierarchy graph                                     │
│  • Code relationship graph                                  │
├─────────────────────────────────────────────────────────────┤
│                    Storage Layer                             │
│  • Engram storage (binary, optimized)                       │
│  • Code/type storage                                        │
│  • Embedding storage                                        │
│  • Version/history storage                                  │
├─────────────────────────────────────────────────────────────┤
│                    Living Layer                              │
│  • Access tracking                                          │
│  • Association weights                                      │
│  • Decay/reinforcement                                      │
│  • Trigger system                                           │
└─────────────────────────────────────────────────────────────┘
```

### 5.4 Code/Type Storage

**What's Stored:**
```
Type Engram:
├── Type identity (name, namespace, assembly, version)
├── Type semantics (embedding of what this type "means")
├── Structure (fields, methods, properties)
├── IL bytecode (for methods)
├── Source code (optional, if available)
├── Dependencies (referenced types)
├── History (previous versions)
└── Usage metadata (how/where used)
```

**Type Evolution:**
```csharp
// Store current type
memantics.StoreType(typeof(Customer));

// Later, type changes...
memantics.StoreType(typeof(Customer));  // New version
// Old version preserved, migration path calculated
// Existing Customer Engrams can be migrated on access
```

**Repository Replacement:**
```csharp
// Traditional: code in files, tracked by git
// Memantics: code as typed semantic memory

// Query types by semantics
var paymentTypes = memantics.FindTypes(
    semanticQuery: "types that handle payment processing"
);

// Find methods similar to this one
var similarMethods = memantics.FindSimilarMethods(
    myMethod,
    threshold: 0.8f
);

// Get type history
var history = memantics.GetTypeHistory<Customer>();
```

### 5.5 Living Memory Behaviors

**Access-Based Reinforcement:**
```csharp
// Frequently accessed memories become "stronger"
var customer = memantics.Get<Customer>(uuid);
// Each access increases retrieval priority

// Rarely accessed memories decay
// After threshold, may be:
// - Compressed
// - Moved to cold storage
// - Eventually forgotten (if no references)
```

**Associative Linking:**
```csharp
// Accessing A then B creates/strengthens A→B association
memantics.Get(orderUuid);
memantics.Get(customerUuid);
// Order → Customer association weight increases

// Later: "what's related to this order?"
var associated = memantics.GetAssociated(orderUuid);
// Returns Customer (and others) ranked by association strength
```

**Triggers:**
```csharp
// Memory can trigger actions
[MemoryTrigger(OnAccess = true)]
public class SecurityAuditTrigger : IMemoryTrigger
{
    public void OnAccess(MemoryAccessContext context)
    {
        AuditLog.Record(context.Engram.UUID, context.Accessor);
    }
}

memantics.RegisterTrigger<SensitiveData>(new SecurityAuditTrigger());
```

### 5.6 Memantics Driver for DOTNExT

```csharp
public class MemanticsDriver : IMemoryDriver
{
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Query |
        DriverCapabilities.Sync |
        DriverCapabilities.Subscribe |
        DriverCapabilities.Transactions |
        DriverCapabilities.SemanticSearch |
        DriverCapabilities.Distributed |
        DriverCapabilities.Versioning;

    // Full integration with all Engram levels
    // Native format, zero translation
    // Semantic queries built-in
    // Living memory behaviors available
}
```

---

## 6. Component Interaction Diagrams

### 6.1 Object Lifecycle Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     APPLICATION                              │
│                    new MyObject()                            │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                         MOM                                  │
│  1. Allocate memory (via GC)                                │
│  2. Generate UUID                                           │
│  3. Set Engram marker bit                                   │
│  4. Store UUID in SyncBlock extension                       │
└──────────────────────────┬──────────────────────────────────┘
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
┌─────────────────────────┐   ┌─────────────────────────────┐
│          CMS            │   │          ORION              │
│  Register allocation    │   │  Add node to graph          │
│  Check persist hints    │   │                             │
└─────────────────────────┘   └─────────────────────────────┘
```

### 6.2 Relationship Recording Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     APPLICATION                              │
│               obj1.Reference = obj2                          │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    MOM Write Barrier                         │
│  1. Standard write barrier (GC card table)                  │
│  2. Check if source has Engram marker                       │
│  3. If yes: notify ORION                                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                        ORION                                 │
│  1. Record relationship (src UUID → field → tgt UUID)       │
│  2. Update indices                                          │
│  3. Invalidate affected query cache                         │
└─────────────────────────────────────────────────────────────┘
```

### 6.3 Persistence Flow

```
┌─────────────────────────────────────────────────────────────┐
│                      TRIGGER                                 │
│  (timer, memory pressure, explicit, lifecycle)              │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                         CMS                                  │
│  1. Identify what needs persisting                          │
│  2. Request Engram extraction from ORION                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                        ORION                                 │
│  1. Walk object graph from root(s)                          │
│  2. Collect objects into Engram structure                   │
│  3. Include relationship metadata                           │
│  4. Return Engram to CMS                                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                         CMS                                  │
│  1. Select appropriate driver (config, hints)               │
│  2. Route Engram to driver                                  │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    MEMORY DRIVER                             │
│  1. Transform Engram if needed (format)                     │
│  2. Store to destination                                    │
│  3. Report success/failure to CMS                           │
└─────────────────────────────────────────────────────────────┘
```

---

*This document provides detailed specifications for each major component of the DOTNExT memory architecture. It should be read in conjunction with the main vision document.*

*Version 1.0 - 2025-12-05*
