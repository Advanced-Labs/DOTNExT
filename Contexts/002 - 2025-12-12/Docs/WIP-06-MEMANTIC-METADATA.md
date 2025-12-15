# WIP-06: Memantic Metadata (The Universal Fabric)

> **Document Type:** Work In Progress - Core Concept Definition
> **Version:** 0.1
> **Date:** 2025-12-15
> **Status:** WIP - Foundational concept that pervades the entire platform
> **Context:** Named by Louis to describe the pervasive metadata present throughout DOTNExT

---

## 1. Executive Summary

**Memantic Metadata** is the universal metadata fabric that pervades every aspect of the DOTNExT platform. It is the "thing" that:

- Wraps everything in memory
- Persists with objects into datastores
- Follows objects when transferred
- Informs the VNS
- Is used by VSS (Virtual Security System)
- Contains semantic encodings/embeddings
- Makes objects **Memantic Objects** or **Engrams**

**Louis's articulation:**

> "So what is that metadata thing which is right down into how things are wrapped and relating in memory, persisted with everything into Memantic Datastores via their drivers, following them when transferred, informing the VNS and the VNS being informed by it, some of that metadata serving for names/namespaces/addresses etc, and then used by our VSS (Virtual Security System) operating at all levels from the lowest to the highest, and which can also contain Semantic Encodings/Vector Embeddings, etc etc?"

**Something with Memantic Metadata is a Memantic Object.** These objects are Engrams in the Memantic (graph) Memories.

---

## 2. Why Memantic Metadata?

### 2.1 The Integration Problem

Traditional systems have separate, disconnected metadata:
- Object identity (runtime address - volatile)
- Persistence info (ORM mappings - separate)
- Security attributes (annotations - compile-time)
- Relations (foreign keys - database level)
- Semantics (none - afterthought)

**Result:** Information silos, impedance mismatches, integration complexity.

### 2.2 The DOTNExT Solution

Memantic Metadata provides **unified, pervasive metadata** that:
- Lives WITH the object at all times
- Survives ALL transformations (memory → disk → network → memory)
- Is understood by ALL platform components
- Enables ALL platform capabilities (VNS, VSS, persistence, distribution, AI)

### 2.3 The "Memantic Object"

An object WITH Memantic Metadata is a **Memantic Object**:
- Has persistent identity (UUID)
- Has semantic meaning (embeddings)
- Has relations to other objects
- Can be an Engram in the Memantic Memory graph
- Is a first-class citizen of the platform

---

## 3. Memantic Metadata Structure

### 3.1 Core Components

```
┌─────────────────────────────────────────────────────────────────┐
│  MEMANTIC METADATA                                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  IDENTITY                                                        │
│  ├── UUID (UUIDv7 recommended - time-ordered)                   │
│  ├── Names (can have multiple)                                  │
│  ├── Namespaces (hierarchical)                                  │
│  └── VNS Addresses (mappings)                                   │
│                                                                  │
│  TYPING                                                          │
│  ├── VTS Type Reference                                         │
│  ├── Version info                                               │
│  └── Runtime-specific type mapping                              │
│                                                                  │
│  OWNERSHIP & PROVENANCE                                          │
│  ├── Source/Creator                                             │
│  ├── Owner(s)                                                   │
│  ├── Signatures (cryptographic)                                 │
│  └── Genealogy (created-from, derived-from)                     │
│                                                                  │
│  DEPENDENCIES & REFERENCES                                       │
│  ├── Dependencies (what this needs)                             │
│  ├── References (what this points to)                           │
│  ├── Dependents (what needs this)                               │
│  └── Referenced-by (what points to this)                        │
│                                                                  │
│  RELATIONS                                                       │
│  ├── Typed relations (contains, uses, etc.)                     │
│  ├── Semantic relations (similar-to, opposite-of)               │
│  └── Custom relations (domain-specific)                         │
│  Note: All refs are relations, not all relations are refs       │
│                                                                  │
│  SEMANTIC ENCODING                                               │
│  ├── Vector Embedding (512+ dimensions)                         │
│  ├── Semantic Tags                                              │
│  ├── Natural Language Description                               │
│  └── Concept Mapping                                            │
│                                                                  │
│  SECURITY                                                        │
│  ├── Classification (public, private, etc.)                     │
│  ├── Access policy reference                                    │
│  ├── Encryption info                                            │
│  └── Audit trail reference                                      │
│                                                                  │
│  LIFECYCLE                                                       │
│  ├── Created timestamp                                          │
│  ├── Modified timestamp                                         │
│  ├── Version (for optimistic concurrency)                       │
│  └── State (active, archived, deleted)                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Conceptual Interface

```csharp
// Conceptual Memantic Metadata structure
public interface IMemanticMetadata
{
    // === Identity ===
    Guid UUID { get; }
    string[] Names { get; }
    string[] Namespaces { get; }
    VNSAddress[] VNSMappings { get; }

    // === Type ===
    VTSTypeRef VType { get; }
    Version TypeVersion { get; }

    // === Ownership ===
    MemanticIdentity Source { get; }
    MemanticIdentity[] Owners { get; }
    byte[] Signature { get; }
    Guid[] Genealogy { get; }  // Parent UUIDs

    // === References (subset of Relations) ===
    MemanticRef[] Dependencies { get; }
    MemanticRef[] References { get; }

    // === Relations (superset, includes refs) ===
    MemanticRelation[] Relations { get; }

    // === Semantic ===
    float[] SemanticEmbedding { get; }
    string[] SemanticTags { get; }
    string NaturalLanguageDescription { get; }

    // === Security ===
    SecurityClassification Classification { get; }
    PolicyRef AccessPolicy { get; }
    EncryptionInfo Encryption { get; }

    // === Lifecycle ===
    DateTime Created { get; }
    DateTime Modified { get; }
    long Version { get; }
    LifecycleState State { get; }
}
```

---

## 4. Where Memantic Metadata Lives

### 4.1 In Memory (VCR/MMS)

```
Object in GC Heap
├── MethodTable* (CLR type)
├── Fields (instance data)
└── Memantic Metadata* ──────────► Side Table Entry
                                   ├── UUID
                                   ├── Relations[]
                                   ├── Semantic[]
                                   └── etc.
```

**Implementation options:**
- **Side table:** Zero overhead for non-Memantic objects (RECOMMENDED)
- **Object header:** Overhead for every object (NOT recommended)
- **Lazy population:** Metadata created on first access

### 4.2 In Persistence (Memantic Datastores)

```
RavenDB Document:
{
    "_id": "order-uuid-here",
    "_memantic": {
        "uuid": "...",
        "names": ["Order", "ORD-123"],
        "type": "MyApp.Orders.Order@v1",
        "embedding": [0.23, -0.15, ...],
        "security": { "classification": "internal" },
        ...
    },
    "customer": "customer-uuid",
    "items": [...],
    ...
}

Neo4j Node:
(o:Order:MemanticObject {
    uuid: "...",
    embedding: [0.23, -0.15, ...],
    ...
})
-[:PLACED_BY {type: "ref"}]-> (c:Customer)
-[:SIMILAR_TO {score: 0.92}]-> (po:PurchaseOrder)
```

### 4.3 On The Wire (Transfer)

```
Engram Wire Format:
┌─────────────────────────────────────────────────────────────────┐
│  HEADER                                                          │
│  ├── Engram UUID                                                │
│  ├── Source node                                                │
│  ├── Timestamp                                                  │
│  └── Signature                                                  │
├─────────────────────────────────────────────────────────────────┤
│  OBJECT TABLE                                                    │
│  ├── Object 1: { memantic_metadata, field_data }                │
│  ├── Object 2: { memantic_metadata, field_data }                │
│  └── ...                                                        │
├─────────────────────────────────────────────────────────────────┤
│  RELATION TABLE                                                  │
│  ├── (obj1) -[REF]-> (obj2)                                     │
│  ├── (obj1) -[CONTAINS]-> (obj3)                                │
│  └── ...                                                        │
├─────────────────────────────────────────────────────────────────┤
│  TYPE TABLE                                                      │
│  ├── Type 1: VTS metadata                                       │
│  └── ...                                                        │
└─────────────────────────────────────────────────────────────────┘
```

### 4.4 In VNS

VNS uses Memantic Metadata for:
- Name resolution (names, namespaces → object)
- Semantic search (embeddings → objects)
- Relationship traversal (relations → related objects)

### 4.5 In VSS (Virtual Security System)

VSS uses Memantic Metadata for:
- Access decisions (classification, policy)
- Ownership verification (source, owners, signature)
- Audit (genealogy, lifecycle)

---

## 5. Relations vs References

**Critical distinction from Louis:**

> "Relations (which is the same as a 'ref' on our platform; all refs are relations but not all relations are refs)"

### 5.1 References (Subset)

References are **runtime pointers** to other objects:
- Correspond to object graph edges
- GC tracks these
- Have direction (source → target)
- Are typed (field type)

### 5.2 Relations (Superset)

Relations include references PLUS:
- **Semantic relations:** "similar-to", "opposite-of", "related-to"
- **Domain relations:** "customer-of", "manages", "depends-on"
- **Bidirectional:** A-related-to-B implies B-related-to-A (optionally)
- **Weighted:** Relation strength (similarity score, etc.)
- **Non-runtime:** Exist in metadata, not necessarily in memory

```
References ⊂ Relations

All refs are relations: obj.Customer (ref) → (Order)-[:CUSTOMER]->(Customer)
Not all relations are refs: (Order)-[:SIMILAR_TO]->(PurchaseOrder) (no runtime ptr)
```

---

## 6. Memantic Metadata in Platform Components

### 6.1 VCR (Virtual Core Runtime)

| Component | Uses Memantic Metadata For |
|-----------|---------------------------|
| **MMS** | Object identity, graph structure |
| **VEE** | Execution context metadata |
| **GC Integration** | Tracking Memantic objects |

### 6.2 VOS Services

| Service | Uses Memantic Metadata For |
|---------|---------------------------|
| **VNS** | Name resolution, discovery |
| **VSS** | Access control, audit |
| **Persistence** | Storing/retrieving objects |
| **Distribution** | Transfer, migration |

### 6.3 VTS (Virtual Type System)

VTS Types themselves have Memantic Metadata:
- Type UUID
- Type semantic embedding
- Type relations (inheritance, uses)
- Type security classification

### 6.4 VCOM

VCOM uses Memantic Metadata as the foundation:
- VObject UUID = Memantic UUID
- VObject relations = Memantic relations
- VObject persistence = via Memantic Metadata

---

## 7. The Memantic Object Lifecycle

### 7.1 Creation

```
1. Object instantiated (new Order())
2. Memantic Metadata created:
   - UUID assigned (UUIDv7)
   - VTS type linked
   - Initial relations established
   - Semantic embedding computed (lazy or eager)
3. Registered with MMS
4. Optionally registered with VNS
```

### 7.2 Modification

```
1. Object state changes
2. Memantic Metadata updated:
   - Modified timestamp
   - Version incremented
   - Relations updated if needed
   - Embedding recomputed if significant change
3. Persistence triggered if configured
```

### 7.3 Transfer

```
1. Engram extracted (object + metadata)
2. Wire format includes full Memantic Metadata
3. Received on target node
4. Memantic Metadata recreated
5. Object instantiated with preserved identity
```

### 7.4 Persistence

```
1. Object marked for persistence
2. Memantic Metadata serialized with object
3. Stored in Memantic Datastore:
   - Document store (RavenDB): object content + metadata
   - Graph store (Neo4j): relations + embeddings
4. On reload: object + metadata reconstituted together
```

---

## 8. Semantic Encoding

### 8.1 What Gets Embedded

| Aspect | Embedding Input |
|--------|-----------------|
| **Type** | Type name, structure, documentation |
| **Instance** | Field values, content |
| **Context** | Usage patterns, relations |
| **Behavior** | Method names, contracts |

### 8.2 Embedding Model

Options:
- **Domain-trained:** Custom model for DOTNExT platform
- **Pre-trained + fine-tuned:** Start with BERT/GPT embeddings, fine-tune
- **Multi-modal:** Combine code embeddings + natural language

### 8.3 Embedding Uses

| Use Case | How Embeddings Help |
|----------|---------------------|
| **VNS Semantic Search** | "Find orders like this" |
| **Type Compatibility** | "Is this type similar enough?" |
| **AI Operations** | Natural language → object |
| **Anomaly Detection** | "This object is unusual" |
| **Recommendations** | "Objects you might want" |

---

## 9. Implementation Phases

### Phase 1: Core Structure
- [ ] Define IMemanticMetadata interface
- [ ] Implement side-table storage in MMS
- [ ] UUID assignment mechanism (UUIDv7)

### Phase 2: Persistence Integration
- [ ] RavenDB metadata serialization
- [ ] Neo4j relation storage
- [ ] Cross-store consistency

### Phase 3: VNS Integration
- [ ] Name registration from metadata
- [ ] Semantic search using embeddings
- [ ] Relation-based discovery

### Phase 4: VSS Integration
- [ ] Security classification enforcement
- [ ] Access policy evaluation
- [ ] Audit trail from lifecycle

### Phase 5: Semantic Layer
- [ ] Embedding computation
- [ ] Embedding storage (vector index)
- [ ] Semantic similarity operations

---

## 10. Open Questions

### Design Questions
1. Mandatory vs optional metadata fields?
2. Metadata versioning and migration?
3. Embedding model selection and updates?
4. Performance of metadata operations?

### Implementation Questions
5. Side table vs object header (confirmed: side table)?
6. Lazy vs eager metadata population?
7. Metadata caching strategy?
8. Cross-node metadata sync?

---

## 11. Key Insights

1. **Memantic Metadata is the Universal Fabric** - It's not an afterthought; it's the foundation.

2. **Everything WITH Metadata is a Memantic Object** - The metadata makes objects first-class citizens.

3. **Relations ⊃ References** - All refs are relations, but relations include more.

4. **Semantic Encoding is Native** - Not bolted on; built in from the start.

5. **Pervasive, Not Optional** - Present in memory, persistence, wire, VNS, VSS.

6. **Engrams ARE Memantic Objects in Memantic Memories** - The metadata is what makes them Engrams.

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| WIP-01-MEMANTICS-MEMORY-SYSTEM.md | MMS manages Memantic Metadata |
| WIP-05-VIRTUAL-TYPE-SYSTEM.md | VTS types have Memantic Metadata |
| DOTNExT-Engrams-Revised.md | Engrams = Memantic Objects |
| 02-CONSOLIDATED-VISION.md | Platform architecture |

---

*This document defines Memantic Metadata - the universal fabric that makes objects into Memantic Objects and enables the full DOTNExT platform vision. Named during Louis's explanation of the pervasive metadata present throughout the platform.*

*Version 0.1 - 2025-12-15 - Initial conceptualization*
