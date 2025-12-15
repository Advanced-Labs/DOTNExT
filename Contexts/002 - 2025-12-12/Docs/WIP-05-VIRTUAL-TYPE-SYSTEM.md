# WIP-05: Virtual Type System (VTS)

> **Document Type:** Work In Progress - Architecture Design
> **Version:** 0.1
> **Date:** 2025-12-15
> **Status:** WIP - Initial conceptualization based on Louis's guidance
> **Context:** Critical infrastructure for multi-runtime, semantic-oriented platform

---

## 1. Executive Summary

The **Virtual Type System (VTS)** is a universal meta type system for the DOTNExT platform. It enables:
- Mapping between ANY type system (CLR, Python, JavaScript, etc.)
- Semantic-oriented typing with embeddings
- Multi-layered type representation
- Integration with VNS for type discovery

**Key insight from Louis:**

> "The VTS will be a universal type system over which everything and anything will be mappable to/from. The VTS typing will be multilayered and also Semantic-Oriented, using Semantic Encodings/Vectors (so yes, often 'embeddings') over all 'classical symbols and codes', and in those multi-layers and in-between those will be also semantically-augmented relations (e.g. dependencies/refs/genealogy/etc).. basically then the VTS is also 'Memantic Native' in nature... VTS is Memantic Typing I guess then."

**VTS = Memantic Typing** - A type system that is inherently graph-oriented, semantic-aware, and universal.

---

## 2. Why VTS?

### 2.1 The Multi-Runtime Challenge

The Vision includes a multi-runtime kernel architecture:

```
VCR (Virtual Core Runtime)
├── dotnext (CLR types)
├── Python runtime (Python types)
├── Node.js/V8 (JavaScript types)
├── WebAssembly (WASM types)
└── Future runtimes...
```

**Problem:** Each runtime has its own type system with different:
- Type representations
- Memory layouts
- Inheritance models
- Generics/templates
- Dynamic typing approaches

**Solution:** VTS as a universal layer above all runtime type systems.

### 2.2 The Semantic Gap

Traditional type systems are **syntactic**, not **semantic**:
- Type `Order` ≠ Type `Commande` (even if semantically equivalent)
- Type compatibility is structural/nominal, not meaning-based
- No understanding of what types MEAN

**VTS bridges this gap** with semantic encodings.

### 2.3 The VNS Integration Need

VNS (Virtual Name System) needs to:
- Discover types across runtimes
- Find semantically similar types
- Resolve type references universally

VTS provides the type layer VNS operates on.

---

## 3. VTS Design Principles

### 3.1 Universal Mappability

```
┌─────────────────────────────────────────────────────────────────┐
│                          VTS Layer                               │
│  Universal Type Representation                                   │
├─────────────────────────────────────────────────────────────────┤
     ↑           ↑           ↑           ↑           ↑
     │           │           │           │           │
  ┌──┴──┐   ┌───┴──┐   ┌───┴──┐   ┌───┴──┐   ┌────┴────┐
  │ CLR │   │Python│   │  JS  │   │ WASM │   │ Future  │
  │Types│   │Types │   │Types │   │Types │   │ Runtime │
  └─────┘   └──────┘   └──────┘   └──────┘   └─────────┘
```

**Any type from any runtime can map to VTS and back.**

### 3.2 Multi-Layered Representation

VTS types have multiple representation layers:

| Layer | Content | Purpose |
|-------|---------|---------|
| **Structural** | Fields, methods, inheritance | Traditional type info |
| **Semantic** | Embeddings, meaning vectors | AI/semantic search |
| **Relational** | Dependencies, genealogy | Graph connectivity |
| **Behavioral** | Contracts, invariants | Runtime behavior |
| **Source** | Code, documentation | Human understanding |

### 3.3 Semantic-Oriented

Types in VTS are not just structural descriptions - they carry meaning:

```
VTSType: Order
├── Structural: { fields, methods, inheritance }
├── Semantic Embedding: [0.23, -0.15, 0.87, ...]  // 512+ dims
├── Semantic Tags: ["commerce", "transaction", "purchase"]
├── Natural Language Desc: "A customer's request to buy items"
└── Relations:
    ├── Contains → OrderItem
    ├── PlacedBy → Customer
    └── SemanticallySimilarTo → [PurchaseOrder, Requisition]
```

### 3.4 Memantic Native

VTS is inherently **Memantic** (memory + semantic):
- Types are nodes in a graph
- Relations are edges
- Everything has semantic encoding
- Graph operations are native (not afterthought)

---

## 4. VTS Architecture

### 4.1 Core Components

```
┌─────────────────────────────────────────────────────────────────┐
│  VTS (Virtual Type System)                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Type Registry                                            │   │
│  │  - Central registry of all VTS types                      │   │
│  │  - Maps to/from runtime-specific types                    │   │
│  │  - Version management                                     │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Type Mappers                                             │   │
│  │  - CLR Type Mapper (MethodTable ↔ VTSType)               │   │
│  │  - Python Type Mapper (PyTypeObject ↔ VTSType)           │   │
│  │  - JS Type Mapper (V8 Hidden Class ↔ VTSType)            │   │
│  │  - (Pluggable for new runtimes)                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Semantic Engine                                          │   │
│  │  - Computes/stores embeddings                             │   │
│  │  - Semantic similarity search                             │   │
│  │  - Natural language type queries                          │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Relation Graph                                           │   │
│  │  - Type inheritance/implementation                        │   │
│  │  - Dependencies (uses, contains, references)              │   │
│  │  - Semantic relations (similar-to, opposite-of)           │   │
│  │  - Stored in graph database (Neo4j/AuraDB)               │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 VTSType Structure

```csharp
// Conceptual VTS Type representation
public class VTSType
{
    // === Identity ===
    public Guid TypeId { get; }           // Unique type identifier
    public string QualifiedName { get; }  // e.g., "MyApp.Orders.Order"
    public VTSTypeVersion Version { get; }

    // === Structural Layer ===
    public VTSStructure Structure { get; }
    // Members, inheritance, generics, etc.

    // === Semantic Layer ===
    public float[] SemanticEmbedding { get; }  // Vector embedding
    public string[] SemanticTags { get; }
    public string NaturalLanguageDescription { get; }

    // === Relational Layer ===
    public VTSRelation[] Relations { get; }
    // Dependencies, genealogy, semantic relations

    // === Source Layer ===
    public VTSSource Source { get; }
    // Original source code, documentation

    // === Runtime Mappings ===
    public Dictionary<RuntimeId, object> RuntimeMappings { get; }
    // CLR -> MethodTable*, Python -> PyTypeObject*, etc.
}
```

---

## 5. Multi-Runtime Type Mapping

### 5.1 CLR Type Mapping

```
CLR MethodTable                    VTSType
├── EEClass*                       ├── TypeId (Guid)
├── Name                      ↔    ├── QualifiedName
├── Fields[]                       ├── Structure.Fields[]
├── Methods[]                      ├── Structure.Methods[]
├── Interfaces[]                   ├── Structure.Interfaces[]
└── BaseType                       └── Relations.InheritsFrom
```

### 5.2 Python Type Mapping

```
PyTypeObject                       VTSType
├── tp_name                   ↔    ├── QualifiedName
├── tp_dict                        ├── Structure (dynamic)
├── tp_base                        ├── Relations.InheritsFrom
├── tp_methods                     ├── Structure.Methods[]
└── (dynamic)                      └── (snapshot at mapping time)
```

### 5.3 Cross-Runtime Type Resolution

When code in Runtime A needs a type from Runtime B:

```
1. VNS query: "Find type Order"
2. VTS resolves to VTSType (universal)
3. VTS maps VTSType to Runtime A's type system
4. Proxy/adapter generated if needed
5. Runtime A can use the type
```

---

## 6. Semantic Type Operations

### 6.1 Semantic Search

```csharp
// Find types by semantic meaning, not just name
var types = await VTS.SearchSemantic(
    query: "types that represent customer purchase transactions",
    maxResults: 10,
    threshold: 0.75f
);
// Returns: Order, PurchaseOrder, Transaction, Requisition, etc.
```

### 6.2 Type Similarity

```csharp
// Find semantically similar types
var similar = await VTS.FindSimilar(
    typeId: orderType.TypeId,
    maxResults: 5
);
// Uses cosine similarity on semantic embeddings
```

### 6.3 Semantic Type Compatibility

Beyond structural compatibility:

```csharp
// Can this CLR Order be used where Python Commande is expected?
var compatibility = await VTS.CheckSemanticCompatibility(
    source: clrOrderType,
    target: pythonCommandeType
);
// Returns: SemanticMatch (high similarity), StructuralMismatch (field differences)
```

---

## 7. VTS and VNS Integration

### 7.1 Type Resolution in VNS

```
VNS Address: vayron://Types/MyApp.Orders.Order

Resolution:
1. VNS parses address
2. Queries VTS for type by qualified name
3. VTS returns VTSType with all layers
4. VNS can return:
   - Type metadata for reflection
   - Runtime-specific type for instantiation
   - Semantic info for AI operations
```

### 7.2 Dynamic Type Access

This is what Louis described for coding against VNS without CLR typing:

```csharp
// Developer writes (dynamic, but with full IntelliSense)
var order = vns.MyApp.Orders["ORD-123"];
order.Customer = customer;  // IDE knows Customer is valid

// Under the hood:
// 1. vns.MyApp.Orders resolved via VNS
// 2. VTS provides type info for IntelliSense
// 3. At compile time: left dynamic OR codegen'd to typed
// 4. At runtime: VTS handles cross-runtime type conversion
```

**Key insight from Louis:**

> "Those dynamic coded references/access/calls/etc over VNS addresses can either be left as if and that/those types will take care of the remote/security/routing/type-conversion between different runtimes type systems present on the VNS/etc at runtime, or those can be replaced via codegen as typed."

---

## 8. VTS and Memantics Integration

### 8.1 VTS Types as Engram Content

When extracting Engrams:
- Objects have VTS types
- Types are part of the Code/Types layer
- Type relations are part of the graph

### 8.2 Type Metadata in Memantic Graph

```
Neo4j Graph:
(Order:VTSType {id: "...", name: "Order"})
  -[:INHERITS_FROM]-> (BaseEntity:VTSType)
  -[:CONTAINS]-> (OrderItem:VTSType)
  -[:REFERENCED_BY]-> (OrderService:VTSType)
  -[:SEMANTICALLY_SIMILAR {score: 0.92}]-> (PurchaseOrder:VTSType)
```

### 8.3 Memantic Metadata on Types

Every VTS type carries Memantic Metadata (see WIP-06):
- UUID
- Names/namespaces
- Dependencies
- Relations
- Semantic embeddings
- Security classifications

---

## 9. Implementation Phases

### Phase 1: CLR Type Mapping
- [ ] VTSType core structure
- [ ] CLR MethodTable ↔ VTSType mapper
- [ ] Basic type registry

### Phase 2: Semantic Layer
- [ ] Embedding computation for types
- [ ] Semantic search implementation
- [ ] Neo4j integration for type graph

### Phase 3: VNS Integration
- [ ] VNS type resolution
- [ ] Dynamic access with IntelliSense
- [ ] IDE extension support

### Phase 4: Multi-Runtime
- [ ] Python type mapper
- [ ] JavaScript type mapper
- [ ] Cross-runtime proxies

### Phase 5: Advanced Semantics
- [ ] Semantic compatibility checking
- [ ] Type evolution tracking
- [ ] AI-driven type suggestions

---

## 10. Open Questions

### Design Questions
1. How to handle types that don't map cleanly between runtimes?
2. Embedding model choice (custom trained vs off-the-shelf)?
3. Version compatibility across VTS type changes?
4. Performance of dynamic type resolution?

### Implementation Questions
5. Where does VTS run (VOS service vs kernel)?
6. Caching strategy for type mappings?
7. How to update embeddings as types evolve?
8. Integration with existing reflection APIs?

---

## 11. Relation to Other Systems

| System | VTS Provides | VTS Uses |
|--------|--------------|----------|
| **VNS** | Type metadata, resolution | Name resolution |
| **VCOM** | Type identity, compatibility | Object types |
| **VEE** | Type info at runtime | Execution context |
| **MMS** | Type as graph nodes | Storage |
| **VSS** | Type security metadata | Security policy |

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| WIP-01-MEMANTICS-MEMORY-SYSTEM.md | VTS is Memantic-native |
| WIP-03-MULTI-RUNTIME-KERNEL-ARCHITECTURE.md | VTS enables multi-runtime |
| WIP-06-MEMANTIC-METADATA.md | VTS types carry Memantic Metadata |
| 02-CONSOLIDATED-VISION.md | VTS in overall architecture |

---

*This document captures the VTS (Virtual Type System) concept as described by Louis - a universal, semantic-oriented, Memantic-native type system that enables the multi-runtime vision of DOTNExT.*

*Version 0.1 - 2025-12-15 - Initial conceptualization*
