# NXIA Memory Architecture Specification

> **Version:** 0.1 (Draft)  
> **Date:** 2025-12-22  
> **Status:** Design Specification  
> **Companion to:** NXIA Consolidated Platform Overview v1.4  
> **Scope:** Detailed memory architecture for MMS, including storage organization, content-addressing, sectors, relation indexing, and acceleration.

---

## Executive Summary

This document specifies the memory architecture that underlies NXIA's Memantic Memory System (MMS). It synthesizes concepts from:

- **mimalloc**: Segment/page/block hierarchical allocation with thread-local fast paths
- **LMDB**: Memory-mapped copy-on-write B+trees with crash consistency
- **Content-addressed storage**: Hash-identified immutable content (git, Unison, IPFS)
- **Graph databases**: Relation-aware indexing for first-class graph operations

The result is a unified memory substrate where:

- Objects have **universal identity** across all boundaries
- State is organized as **content-addressed layers** that can be independently versioned, faulted, and stored
- **Sectors** define authority and storage characteristics (RAM, persistent, GPU, remote)
- **Relations are indexed**, making graph traversal an O(log n) operation rather than pointer-chasing
- **GPU acceleration** is architecturally integrated for graph and semantic operations
- **Fault-in** is the universal access pattern for missing layers, objects, code, or capabilities

This architecture eliminates the traditional layered stack (heap → serialize → DB → cache → replicate) by making identity, relations, versioning, and persistence **substrate primitives**.

---

## Table of Contents

1. [Design Principles](#1-design-principles)
2. [Storage Hierarchy](#2-storage-hierarchy)
3. [Universal Envelope](#3-universal-envelope)
4. [Content-Addressed Layers](#4-content-addressed-layers)
5. [Sector Authority Model](#5-sector-authority-model)
6. [Relation Indexing](#6-relation-indexing)
7. [Copy-on-Write and Snapshots](#7-copy-on-write-and-snapshots)
8. [Fault-In Architecture](#8-fault-in-architecture)
9. [GPU Acceleration](#9-gpu-acceleration)
10. [Allocation and Reclamation](#10-allocation-and-reclamation)
11. [Integration Points](#11-integration-points)
12. [Appendix: Data Structures](#appendix-data-structures)

---

## 1. Design Principles

### 1.1 Core Tenets

1. **Identity Over Address**  
   Objects are identified by OID, not memory location. Addresses are caching optimizations; identity is truth.

2. **Layers Over Monoliths**  
   Object state is decomposed into independently addressable layers. Each layer can be present, absent, cached, or authoritative in different sectors.

3. **Content Over Location**  
   Layer content is identified by hash. Same content = same hash, regardless of where or when it was created.

4. **Authority Over Ownership**  
   Sectors define where authoritative state lives. Caches are explicitly non-authoritative.

5. **Indexes Over Pointers**  
   Relations are stored in B+tree indexes, not as embedded pointers. Graph structure is queryable, not just traversable.

6. **Fault-In Over Preload**  
   Missing data triggers structured acquisition. The system assumes incompleteness and handles it gracefully.

7. **Selective Cost Over Uniform Tax**  
   Memory Classes determine which capabilities (and costs) apply to each object. Pay for what you use.

### 1.2 What This Architecture Eliminates

| Traditional Pattern | NXIA Equivalent |
|---------------------|-----------------|
| Serialize to bytes for storage | Persist layer to authoritative sector |
| Different IDs per layer (DB ID, cache key, object ref) | Single OID everywhere |
| ORM mapping between objects and tables | Objects ARE the storage |
| Separate graph DB for relationships | Relations indexed in MMS |
| Cache invalidation protocols | Sector authority + fault-in |
| Schema migration scripts | Content-addressed versioning + layer evolution |
| Pointer chasing for graph traversal | B+tree index lookup |

---

## 2. Storage Hierarchy

### 2.1 Overview

The storage hierarchy is inspired by mimalloc's segment/page/block organization, adapted for NXIA's requirements.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              MMS Fabric                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                         Segment (4 MB)                              │ │
│  │  - Unit of allocation from OS (mmap)                                │ │
│  │  - Thread-local ownership for allocation fast path                  │ │
│  │  - Unit of COW snapshot                                             │ │
│  │  - Memory Class homogeneous (all objects same class)                │ │
│  ├────────────────────────────────────────────────────────────────────┤ │
│  │                                                                      │ │
│  │  ┌──────────────────────────────────────────────────────────────┐  │ │
│  │  │                      Page (64 KB)                             │  │ │
│  │  │  - Unit of mapping/protection                                 │  │ │
│  │  │  - Unit of fault-in                                           │  │ │
│  │  │  - Unit of sector binding                                     │  │ │
│  │  │  - Contains page header + object slots                        │  │ │
│  │  ├──────────────────────────────────────────────────────────────┤  │ │
│  │  │                                                                │  │ │
│  │  │  ┌────────────────────────────────────────────────────────┐  │  │ │
│  │  │  │              Object Slot (variable)                    │  │  │ │
│  │  │  │  - Universal Envelope (fixed header)                   │  │  │ │
│  │  │  │  - Layer presence bitmap                               │  │  │ │
│  │  │  │  - Inline layers (if present and small)                │  │  │ │
│  │  │  │  - Layer references (if external)                      │  │  │ │
│  │  │  └────────────────────────────────────────────────────────┘  │  │ │
│  │  │                                                                │  │ │
│  │  └──────────────────────────────────────────────────────────────┘  │ │
│  │                                                                      │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Segment

A **Segment** is a large, aligned memory region (typically 4 MB).

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            Segment Header                                │
├─────────────────────────────────────────────────────────────────────────┤
│  segment_id        : SegmentId (64-bit)                                  │
│  memory_class      : MemoryClass                                         │
│  sector_binding    : SectorId                                            │
│  owner_thread      : ThreadId (for allocation fast path)                 │
│  page_count        : u16                                                 │
│  free_page_bitmap  : [u64; N]                                            │
│  cow_generation    : u64 (for snapshot tracking)                         │
│  cow_parent        : Option<SegmentRef>                                  │
│  flags             : SegmentFlags                                        │
├─────────────────────────────────────────────────────────────────────────┤
│  Page 0  │  Page 1  │  Page 2  │  ...  │  Page 63                        │
│  (64 KB) │  (64 KB) │  (64 KB) │       │  (64 KB)                        │
└─────────────────────────────────────────────────────────────────────────┘
```

**Key properties:**

- **Thread-local ownership**: Each segment is owned by a thread for allocation. Other threads can read, but allocation is lock-free for the owner.
- **Memory Class homogeneous**: All objects in a segment share the same Memory Class. This simplifies layout, capability checks, and GC.
- **COW unit**: When a snapshot is taken, segments are marked COW. Writes trigger page-level copy.
- **Sector binding**: The segment knows which sector is authoritative for its contents.

### 2.3 Page

A **Page** is the unit of mapping, protection, and fault-in (typically 64 KB).

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Page Header                                 │
├─────────────────────────────────────────────────────────────────────────┤
│  page_id           : PageId (segment_id + offset)                        │
│  state             : PageState (Mapped | Unmapped | Faulting | COW)      │
│  object_count      : u16                                                 │
│  free_slot_head    : u16 (offset to first free slot)                     │
│  largest_free      : u16 (bytes, for allocation decisions)               │
│  cow_generation    : u64                                                 │
│  checksum          : u32 (optional, for integrity)                       │
├─────────────────────────────────────────────────────────────────────────┤
│  Slot Table                                                              │
│  ┌───────┬───────┬───────┬───────┬─────────────────────────────────────┐│
│  │Slot 0 │Slot 1 │Slot 2 │  ...  │ Free Space                          ││
│  │(Obj A)│(Obj B)│(Free) │       │                                     ││
│  └───────┴───────┴───────┴───────┴─────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────┘
```

**Key properties:**

- **Unit of fault-in**: When a page is not present, accessing it triggers fault-in from the authoritative sector.
- **Unit of protection**: Pages can have different protection (RO, RW, RX via dual-mapping).
- **COW granularity**: Copy-on-write operates at page level, not object level.

### 2.4 Object Slot

An **Object Slot** contains an object's envelope and layer data.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                             Object Slot                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                     Universal Envelope (64 bytes)                  │  │
│  │  (See Section 3 for details)                                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                     Layer Presence Header (16 bytes)               │  │
│  │  presence_bitmap   : u32 (which layers are inline)                 │  │
│  │  external_bitmap   : u32 (which layers are in external storage)    │  │
│  │  layer_count       : u8                                            │  │
│  │  reserved          : [u8; 7]                                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                     Inline Layers (variable)                       │  │
│  │                                                                     │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │ Layer 1: Base State                                          │  │  │
│  │  │ hash: 0xD4E5F6...                                            │  │  │
│  │  │ data: { name: "Alice", balance: 1500, tier: "Gold" }         │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │ Layer 2: Relations (inline edge table)                       │  │  │
│  │  │ hash: 0x112233...                                            │  │  │
│  │  │ edges: [(orders, 0x5678), (orders, 0x5679), (mgr, 0x9ABC)]   │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                     External Layer References (variable)           │  │
│  │                                                                     │  │
│  │  Layer 4 (Semantic): hash=0x778899..., sector=gpu-accel            │  │
│  │  Layer 5 (Code):     hash=0xAABBCC..., sector=persistent           │  │
│  │                                                                     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Universal Envelope

### 3.1 Purpose

The Universal Envelope is the **fixed-size header** present on every NXIA object. It provides:

- Stable identity (OID)
- Type information (VTS reference)
- Memory Class and capabilities
- Security attachment
- Provenance tracking
- Relation metadata

### 3.2 Layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      Universal Envelope (64 bytes)                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Bytes 0-7:    OID (64-bit object identifier)                            │
│                - Globally unique                                         │
│                - Stable across moves/migrations                          │
│                                                                          │
│  Bytes 8-15:   TypeRef (64-bit VTS type reference)                       │
│                - Points to type in Virtual Type System                   │
│                - Includes version component                              │
│                                                                          │
│  Bytes 16-17:  MemoryClass (16-bit)                                      │
│                - Native (0x01)                                           │
│                - Managed (0x02)                                          │
│                - Capability (0x03)                                       │
│                - Memantic (0x04)                                         │
│                - Extended classes (0x10+)                                │
│                                                                          │
│  Bytes 18-19:  Capabilities (16-bit bitmap)                              │
│                - Bit 0: Relations present                                │
│                - Bit 1: Mailbox enabled                                  │
│                - Bit 2: Persistence enabled                              │
│                - Bit 3: Semantic layer present                           │
│                - Bit 4: Provenance tracking                              │
│                - Bit 5: COW snapshots enabled                            │
│                - Bit 6: Execution capture enabled                        │
│                - Bit 7: Audit logging enabled                            │
│                - Bits 8-15: Reserved / class-specific                    │
│                                                                          │
│  Bytes 20-23:  SecurityLabel (32-bit VSS reference)                      │
│                - Points to security policy                               │
│                - Used for access checks                                  │
│                                                                          │
│  Bytes 24-31:  ProvenanceRef (64-bit)                                    │
│                - Creator OID or external ID                              │
│                - Used for lineage tracking                               │
│                                                                          │
│  Bytes 32-39:  VersionStamp (64-bit)                                     │
│                - Epoch (32-bit) + Sequence (32-bit)                      │
│                - Monotonic within object lifetime                        │
│                                                                          │
│  Bytes 40-43:  RelationCount (32-bit)                                    │
│                - Number of outgoing edges                                │
│                - 0 if relations not enabled                              │
│                                                                          │
│  Bytes 44-47:  SlotSize (32-bit)                                         │
│                - Total bytes for this object slot                        │
│                - Used for iteration and allocation                       │
│                                                                          │
│  Bytes 48-55:  Reserved (64-bit)                                         │
│                - Future use                                              │
│                - Alignment padding                                       │
│                                                                          │
│  Bytes 56-63:  EnvelopeChecksum (64-bit)                                 │
│                - Integrity check for envelope                            │
│                - Optional (can be 0 if disabled)                         │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.3 OID Structure

The OID (Object Identifier) is a 64-bit value with internal structure:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           OID (64 bits)                                  │
├──────────────────────┬──────────────────────┬───────────────────────────┤
│  Domain (16 bits)    │  Node (16 bits)      │  Local ID (32 bits)       │
├──────────────────────┼──────────────────────┼───────────────────────────┤
│  Federation/cluster  │  Machine within      │  Unique within node       │
│  identifier          │  domain              │  (monotonic counter)      │
└──────────────────────┴──────────────────────┴───────────────────────────┘
```

This structure enables:
- **Local allocation without coordination**: Nodes allocate Local IDs independently
- **Global uniqueness**: Domain + Node prefix ensures no collisions
- **Efficient routing**: Domain/Node can be used for federation routing
- **Compact representation**: 64 bits fits in a register

---

## 4. Content-Addressed Layers

### 4.1 Layer Model

An NXIA object's state is decomposed into **layers**. Each layer:

- Has a **Layer ID** (small integer, 0-31)
- Contains **content** (bytes)
- Is identified by the **hash of its content** (content-addressed)
- Can be **present** (inline in slot), **external** (in another sector), or **absent** (not yet loaded)

### 4.2 Standard Layer Types

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Standard Layer Types                             │
├──────────┬──────────────────────────────────────────────────────────────┤
│ Layer 0  │ ENVELOPE (implicit, always present)                           │
│          │ The Universal Envelope itself. Not separately hashed.         │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 1  │ BASE_STATE                                                    │
│          │ The object's field values. Typed per VTS.                     │
│          │ Example: { name: "Alice", balance: 1500, tier: "Gold" }       │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 2  │ RELATIONS                                                     │
│          │ Outgoing edges. Compact edge table or relation objects.       │
│          │ Example: [(orders, 0x5678), (manager, 0x9ABC)]                │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 3  │ PROVENANCE                                                    │
│          │ Creation/modification history. Audit trail.                   │
│          │ Example: { created_by, created_at, modified_by, modified_at } │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 4  │ SEMANTIC                                                      │
│          │ Embeddings, vector representations, semantic tags.            │
│          │ Example: { embedding: [0.12, -0.34, ...], tags: ["vip"] }     │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 5  │ CODE                                                          │
│          │ Associated behavior: source hash, IL hash, native cache.      │
│          │ Example: { source: 0xFFF, il: 0xEEE, native: 0xDDD }          │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 6  │ SECURITY                                                      │
│          │ Extended security metadata beyond envelope label.             │
│          │ Example: { acl: [...], audit_policy: ... }                    │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Layer 7+ │ EXTENSION (class-specific or application-defined)             │
│          │ Custom layers per Memory Class or application need.           │
└──────────┴──────────────────────────────────────────────────────────────┘
```

### 4.3 Content Addressing

Each layer's content is hashed to produce a **Layer Hash**:

```
Layer Hash = SHA-256(layer_id || layer_content_bytes)
           = 32 bytes (256 bits)
           (truncated to 20 bytes for storage efficiency, collision-safe)
```

**Properties of content addressing:**

1. **Immutability**: A hash identifies exact content. Content never changes for a given hash.

2. **Deduplication**: If two objects have identical Layer 1 content, they share the same hash. Storage can deduplicate.

3. **Integrity**: Hash mismatch = corruption or tampering detected.

4. **Efficient comparison**: To check if two objects have the same state, compare hashes (20 bytes) not content (potentially kilobytes).

5. **Caching**: Content can be cached by hash. Cache never needs invalidation (content is immutable).

### 4.4 Layer Storage: Inline vs. External

Layers can be stored in two ways:

**Inline**: Layer data is stored directly in the object slot.
- Fast access (no indirection)
- Appropriate for small, frequently-accessed layers
- Limited by slot size

**External**: Layer data is stored elsewhere, referenced by hash.
- Appropriate for large layers (embeddings, code)
- Appropriate for layers in different sectors (GPU, remote)
- Requires fault-in to access

```
Object Slot for Customer 0x1234:

  Layers 1-2: INLINE (small, hot)
  ┌────────────────────────────────────────┐
  │ Layer 1 (Base State): 48 bytes inline  │
  │ Layer 2 (Relations): 36 bytes inline   │
  └────────────────────────────────────────┘

  Layers 4-5: EXTERNAL (large, cold)
  ┌────────────────────────────────────────┐
  │ Layer 4: hash=0x778899..., sector=gpu  │ → 3KB embedding in GPU VRAM
  │ Layer 5: hash=0xAABBCC..., sector=disk │ → 12KB compiled code on SSD
  └────────────────────────────────────────┘
```

### 4.5 Layer Versioning (Delta Chains)

Layers can be versioned using **delta chains** (similar to git pack files):

```
Version History for Object 0x1234, Layer 1 (Base State):

  ┌─────────────────────────────────────────────────────────────────┐
  │ Version 1 (BASE)                                                 │
  │ Hash: 0xAAA...                                                   │
  │ Type: Full                                                       │
  │ Content: { name: "Alice", balance: 1000, tier: "Silver" }        │
  │ Size: 48 bytes                                                   │
  └─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │ Version 2 (DELTA)                                                │
  │ Hash: 0xBBB...                                                   │
  │ Type: Delta                                                      │
  │ Base: 0xAAA...                                                   │
  │ Delta: { balance: 1000 → 1200 }                                  │
  │ Size: 12 bytes                                                   │
  └─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │ Version 3 (DELTA)                                                │
  │ Hash: 0xCCC...                                                   │
  │ Type: Delta                                                      │
  │ Base: 0xBBB...                                                   │
  │ Delta: { tier: "Silver" → "Gold" }                               │
  │ Size: 10 bytes                                                   │
  └─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │ Version 4 (CURRENT)                                              │
  │ Hash: 0xDDD...                                                   │
  │ Type: Delta                                                      │
  │ Base: 0xCCC...                                                   │
  │ Delta: { balance: 1200 → 1500 }                                  │
  │ Size: 12 bytes                                                   │
  └─────────────────────────────────────────────────────────────────┘
```

**Reconstruction**: To get Version 4 content:
```
Apply chain: V1 (base) → V2 delta → V3 delta → V4 delta → result
```

**Optimization**: Periodically "rebase" to create new full versions, limiting chain length.

### 4.6 Engrams: Portable Layer Bundles

An **Engram** is a portable bundle of layers for one or more objects. Used for:
- Snapshots
- Migration
- Replication
- Persistence
- Inspection/debugging

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Engram                                      │
├─────────────────────────────────────────────────────────────────────────┤
│  Header:                                                                 │
│    engram_id     : EngramId                                              │
│    created_at    : Timestamp                                             │
│    epoch         : EpochId (coherence boundary)                          │
│    object_count  : u32                                                   │
│    layer_mask    : u32 (which layers included)                           │
│    total_size    : u64                                                   │
├─────────────────────────────────────────────────────────────────────────┤
│  Object Manifests:                                                       │
│    ┌─────────────────────────────────────────────────────────────────┐  │
│    │ Object 0x1234:                                                   │  │
│    │   Layer 1: hash=0xDDD..., size=48, inline=true                   │  │
│    │   Layer 2: hash=0x112..., size=36, inline=true                   │  │
│    │   Layer 4: hash=0x778..., size=3072, inline=false, ref=@1024     │  │
│    └─────────────────────────────────────────────────────────────────┘  │
│    ┌─────────────────────────────────────────────────────────────────┐  │
│    │ Object 0x1235:                                                   │  │
│    │   Layer 1: hash=0xEEE..., size=52, inline=true                   │  │
│    │   Layer 2: hash=0x223..., size=24, inline=true                   │  │
│    └─────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer Data Pool:                                                        │
│    @0:    [Layer 1 of 0x1234: 48 bytes]                                  │
│    @48:   [Layer 2 of 0x1234: 36 bytes]                                  │
│    @84:   [Layer 1 of 0x1235: 52 bytes]                                  │
│    @136:  [Layer 2 of 0x1235: 24 bytes]                                  │
│    @1024: [Layer 4 of 0x1234: 3072 bytes]                                │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Sector Authority Model

### 5.1 What is a Sector?

A **Sector** is a storage authority region. It defines:
- **Where** data physically resides (RAM, SSD, GPU, remote node)
- **What** consistency/durability guarantees apply
- **How** access is mediated (mapped, RPC, DMA)
- **Who** is authoritative for mutations

### 5.2 Sector Types

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Sector Types                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  RAM SECTOR                                                              │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Backing: Memory Host Process page pools                          │  │
│  │  Durability: None (volatile)                                       │  │
│  │  Latency: ~100ns                                                   │  │
│  │  Access: Memory-mapped views                                       │  │
│  │  Use: Hot working set, caches                                      │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  PERSISTENT SECTOR                                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Backing: mmap'd files (SSD/NVMe), managed by MDD                  │  │
│  │  Durability: Crash-safe (COW + careful ordering)                   │  │
│  │  Latency: ~10μs                                                    │  │
│  │  Access: Memory-mapped (read), MDD ops (write)                     │  │
│  │  Use: Authoritative state, Engram storage                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  GPU SECTOR                                                              │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Backing: GPU VRAM, managed by GPU driver                          │  │
│  │  Durability: None (volatile, reconstructible)                      │  │
│  │  Latency: ~1μs + transfer time                                     │  │
│  │  Access: GPU kernels, DMA transfer                                 │  │
│  │  Use: Embeddings, vector indexes, graph adjacency for acceleration │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  REMOTE SECTOR                                                           │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Backing: Another NXIA node's MMS                                  │  │
│  │  Durability: Depends on remote node's configuration                │  │
│  │  Latency: ~1ms+ (network)                                          │  │
│  │  Access: Federation protocol (Engram transfer)                     │  │
│  │  Use: Distributed objects, cross-node relations                    │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Authority vs. Cache

Critical distinction:

| Aspect | Authoritative Sector | Cache Sector |
|--------|---------------------|--------------|
| Source of truth | Yes | No |
| Writes go here | Yes | No (or write-through) |
| Recovery source | Yes | No |
| Can be evicted | No (until policy allows) | Yes |
| Consistency | Defines it | Derived from authority |

**Example configuration:**

```
Object 0x1234 (Customer):
  Layer 1 (Base State):
    AUTHORITATIVE: Persistent Sector "local-db"
    CACHED:        RAM Sector "hot-cache"
  
  Layer 4 (Semantic):
    AUTHORITATIVE: GPU Sector "embeddings"
    CACHED:        (none - accessed in place on GPU)

Object 0x9999 (Remote Customer):
  Layer 1 (Base State):
    AUTHORITATIVE: Remote Sector "peer-node-3"
    CACHED:        RAM Sector "hot-cache"
```

### 5.4 Sector Binding

Each object/layer has a **sector binding** that specifies authority:

```rust
struct SectorBinding {
    object_id: OID,
    layer_id: LayerId,
    
    // Where is truth?
    authoritative_sector: SectorId,
    
    // Where are copies allowed?
    cache_sectors: Vec<SectorId>,
    
    // How to handle consistency?
    consistency_policy: ConsistencyPolicy,
    
    // How to handle conflicts?
    conflict_resolution: ConflictResolution,
}

enum ConsistencyPolicy {
    Strong,           // Reads always see latest write
    Eventual {        // Reads may be stale
        max_staleness: Duration,
    },
    Snapshot,         // Reads see consistent point-in-time
    Optimistic,       // Detect conflicts at commit
}

enum ConflictResolution {
    LastWriteWins,
    FirstWriteWins,
    Merge { handler: MergeHandlerId },
    Fail,
}
```

### 5.5 Sector Resolution Chain

When accessing an object/layer, MMS resolves through a sector chain:

```
Access OID=0x1234, Layer=1:

┌─────────────────────────────────────────────────────────────────────────┐
│ Step 1: Check RAM Sector (hot-cache)                                     │
│         Present? ──YES──► Return mapped view                             │
│              │                                                           │
│              NO                                                          │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 2: Check Persistent Sector (local-db)                               │
│         Present? ──YES──► Hydrate to RAM sector, return view             │
│              │                                                           │
│              NO                                                          │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 3: Check Remote Sector (if configured)                              │
│         Present? ──YES──► Fetch Engram, hydrate, return view             │
│              │                                                           │
│              NO                                                          │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 4: Object not found                                                 │
│         Return structured error (ObjectNotFound, LayerNotFound, etc.)    │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.6 Write Flow with Authority

```
Write to OID=0x1234, Layer=1, new_value:

┌─────────────────────────────────────────────────────────────────────────┐
│ Step 1: Check VSS permissions                                            │
│         Has write capability? ──NO──► Return AccessDenied                │
│              │                                                           │
│             YES                                                          │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 2: Determine authoritative sector                                   │
│         Lookup sector binding for (0x1234, Layer 1)                      │
│         → Authoritative: Persistent Sector "local-db"                    │
│              │                                                           │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 3: Compute new layer hash                                           │
│         new_hash = SHA-256(1 || serialize(new_value))                    │
│              │                                                           │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 4: Write to authoritative sector (COW)                              │
│         - Create new page if COW active                                  │
│         - Write layer content                                            │
│         - Update layer hash in envelope                                  │
│         - Update version stamp                                           │
│         - Persist (fsync if durable)                                     │
│              │                                                           │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 5: Invalidate caches                                                │
│         - Mark cached copies stale                                       │
│         - Or: update inline (write-through)                              │
│         - Or: evict from cache (lazy refresh)                            │
│              │                                                           │
│              ▼                                                           │
├─────────────────────────────────────────────────────────────────────────┤
│ Step 6: At epoch boundary                                                │
│         - Publish new version                                            │
│         - Notify subscribers                                             │
│         - Record in provenance (if enabled)                              │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Relation Indexing

### 6.1 The Problem with Pointer-Based Graphs

Traditional object graphs use embedded references:

```csharp
class Customer {
    List<Order> Orders;      // Pointer to list, list contains pointers
    Employee Manager;        // Pointer
}
```

**Problems:**
- Finding all orders for a customer: O(1) (follow pointer)
- Finding all customers for a manager: O(N) scan of all customers
- Finding all manager relationships: O(N) scan
- Graph traversal: Pointer chasing, poor cache locality
- Persistence: Must serialize/deserialize entire structure

### 6.2 Relation-Indexed Architecture

NXIA stores relations in **B+tree indexes**, making all query patterns efficient:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     Relation Index Architecture                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  PRIMARY INDEX: (Source, RelationType) → [Targets]                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Key: (OID, RelationType)                                          │  │
│  │  Value: [TargetOID, TargetOID, ...]                                │  │
│  │                                                                     │  │
│  │  Examples:                                                          │  │
│  │    (0x1234, "orders")  → [0x5678, 0x5679]                          │  │
│  │    (0x1234, "manager") → [0x9ABC]                                  │  │
│  │    (0x1235, "orders")  → [0x5680, 0x5681, 0x5682]                  │  │
│  │                                                                     │  │
│  │  Query: "Get all orders for customer 0x1234"                       │  │
│  │  → B+tree lookup: O(log N)                                         │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  REVERSE INDEX: (Target, RelationType) → [Sources]                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Key: (OID, RelationType)                                          │  │
│  │  Value: [SourceOID, SourceOID, ...]                                │  │
│  │                                                                     │  │
│  │  Examples:                                                          │  │
│  │    (0x9ABC, "manager") → [0x1234, 0x1235, 0x1236]                  │  │
│  │    (0x5678, "orders")  → [0x1234]                                  │  │
│  │                                                                     │  │
│  │  Query: "Which customers does manager 0x9ABC manage?"              │  │
│  │  → B+tree lookup: O(log N)                                         │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  TYPE INDEX: RelationType → [(Source, Target), ...]                      │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Key: RelationType                                                 │  │
│  │  Value: [(SourceOID, TargetOID), ...]                              │  │
│  │                                                                     │  │
│  │  Examples:                                                          │  │
│  │    "orders"  → [(0x1234,0x5678), (0x1234,0x5679), (0x1235,0x5680)] │  │
│  │    "manager" → [(0x1234,0x9ABC), (0x1235,0x9ABC)]                  │  │
│  │                                                                     │  │
│  │  Query: "Find all manager relationships"                           │  │
│  │  → B+tree scan: O(K) for K matching edges                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.3 B+Tree Structure for Relations

```
                            ┌─────────────────────┐
                            │    Root Node        │
                            │  Keys: [0x5000]     │
                            └──────────┬──────────┘
                                       │
                    ┌──────────────────┴──────────────────┐
                    │                                     │
          ┌─────────▼─────────┐             ┌─────────────▼─────────┐
          │  Internal Node    │             │    Internal Node      │
          │  Keys: [0x2000,   │             │    Keys: [0x7000,     │
          │         0x3000]   │             │           0x8000]     │
          └─────────┬─────────┘             └───────────┬───────────┘
                    │                                   │
     ┌──────────────┼──────────────┐                   ...
     │              │              │
┌────▼────┐   ┌─────▼────┐   ┌────▼────┐
│Leaf Node│   │Leaf Node │   │Leaf Node│
├─────────┤   ├──────────┤   ├─────────┤
│(0x1234, │   │(0x2500,  │   │(0x3100, │
│ orders) │   │ orders)  │   │ manager)│
│→[5678,  │   │→[5690]   │   │→[9ABC]  │
│  5679]  │   │          │   │         │
│         │   │(0x2501,  │   │(0x3200, │
│(0x1234, │   │ manager) │   │ orders) │
│ manager)│   │→[9ABD]   │   │→[5700]  │
│→[9ABC]  │   │          │   │         │
│         │   │          │   │         │
│ Next ───┼──►│ Next ────┼──►│ Next ───┼──► ...
└─────────┘   └──────────┘   └─────────┘
```

**Properties:**
- Leaf nodes are linked for efficient range scans
- Keys are (Source OID, Relation Type) tuples
- Values are arrays of Target OIDs
- Standard B+tree operations: O(log N) lookup, O(log N) insert/delete

### 6.4 Relation Objects (Rich Edges)

For edges that need metadata beyond just (source, type, target), NXIA supports **Relation Objects**:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Relation Object                                  │
├─────────────────────────────────────────────────────────────────────────┤
│  relation_id   : OID (this relation is itself an object!)                │
│  source        : OID                                                     │
│  target        : OID                                                     │
│  relation_type : RelationType                                            │
│  created_at    : Timestamp                                               │
│  created_by    : OID                                                     │
│  weight        : f32 (optional, for weighted graphs)                     │
│  properties    : Map<String, Value> (arbitrary metadata)                 │
│  security_label: SecurityLabel                                           │
└─────────────────────────────────────────────────────────────────────────┘
```

**Example: Weighted order relationship with metadata**

```
Relation Object 0xR001:
  source: 0x1234 (Customer "Alice")
  target: 0x5678 (Order #1001)
  relation_type: "orders"
  created_at: 2025-01-15T10:30:00Z
  properties: {
    priority: "high",
    shipping_method: "express"
  }
```

Relation Objects are stored in MMS like any object, with their own envelope and layers.

### 6.5 Multi-Hop Query Execution

**Query: "Find all orders placed by customers managed by manager 0x9ABC"**

```
Traditional (pointer chasing):
  for customer in ALL_CUSTOMERS:          # O(N) scan
    if customer.manager == 0x9ABC:        # check each
      for order in customer.orders:       # follow pointers
        results.add(order)
  
  Total: O(N × M) where N = customers, M = avg orders per customer
  Memory access: Random, cache-hostile


NXIA (index-based):
  Step 1: Reverse index lookup
          Key: (0x9ABC, "manager" as target)
          Result: [0x1234, 0x1235, 0x1236]
          Cost: O(log N)

  Step 2: Primary index lookups (batchable)
          Keys: [(0x1234, "orders"), (0x1235, "orders"), (0x1236, "orders")]
          Results: [[0x5678, 0x5679], [0x5680], [0x5681, 0x5682]]
          Cost: O(K × log N) where K = number of customers (3 in this case)

  Step 3: Flatten results
          [0x5678, 0x5679, 0x5680, 0x5681, 0x5682]

  Total: O(K × log N + result_size)
  Memory access: Sequential B+tree traversal, cache-friendly
```

### 6.6 Index Storage and Persistence

The relation indexes are themselves NXIA objects stored in MMS:

```
Index Segment (dedicated segment for relation indexes):
┌─────────────────────────────────────────────────────────────────────────┐
│  Segment Header                                                          │
│    memory_class: System (special class for indexes)                      │
│    sector_binding: Persistent Sector "index-store"                       │
├─────────────────────────────────────────────────────────────────────────┤
│  Page 0: Primary Index B+tree root + internal nodes                      │
│  Page 1: Primary Index internal nodes                                    │
│  Pages 2-10: Primary Index leaf nodes                                    │
│  Page 11: Reverse Index B+tree root + internal nodes                     │
│  Pages 12-20: Reverse Index leaf nodes                                   │
│  Page 21: Type Index B+tree root                                         │
│  ...                                                                     │
└─────────────────────────────────────────────────────────────────────────┘
```

**Persistence:** Indexes use the same COW mechanism as data. Updates to indexes:
1. Create new B+tree pages (COW)
2. Update parent pointers
3. Commit at epoch boundary
4. Old pages become reclaimable

**Crash recovery:** Because indexes use COW, they're always consistent. After crash, the last committed epoch's index state is valid.

---

## 7. Copy-on-Write and Snapshots

### 7.1 COW Mechanism

Copy-on-Write enables efficient snapshots and isolation:

```
Before snapshot:
┌─────────────────────────────────────────────────────────────────────────┐
│  Segment S1 (generation=5)                                               │
│  ┌─────────┬─────────┬─────────┬─────────┐                              │
│  │ Page 0  │ Page 1  │ Page 2  │ Page 3  │                              │
│  │ (data)  │ (data)  │ (data)  │ (data)  │                              │
│  └─────────┴─────────┴─────────┴─────────┘                              │
└─────────────────────────────────────────────────────────────────────────┘

Take snapshot at epoch 100:
  - Record: Snapshot(epoch=100, segment=S1, generation=5)
  - Set: S1.cow_generation = 6
  - Pages are NOT copied yet

After snapshot, before any writes:
┌─────────────────────────────────────────────────────────────────────────┐
│  Segment S1 (generation=6, cow_active=true)                              │
│  ┌─────────┬─────────┬─────────┬─────────┐                              │
│  │ Page 0  │ Page 1  │ Page 2  │ Page 3  │  ← Still shared with        │
│  │ gen=5   │ gen=5   │ gen=5   │ gen=5   │    snapshot                  │
│  └─────────┴─────────┴─────────┴─────────┘                              │
└─────────────────────────────────────────────────────────────────────────┘

Write to Page 1:
  1. Page 1 has gen=5, current gen=6 → COW triggered
  2. Allocate new Page 1' 
  3. Copy Page 1 content to Page 1'
  4. Apply write to Page 1'
  5. Update segment's page table to point to Page 1'
  6. Set Page 1'.generation = 6

After write:
┌─────────────────────────────────────────────────────────────────────────┐
│  Segment S1 (generation=6)                                               │
│  ┌─────────┬─────────┬─────────┬─────────┐                              │
│  │ Page 0  │ Page 1' │ Page 2  │ Page 3  │                              │
│  │ gen=5   │ gen=6   │ gen=5   │ gen=5   │                              │
│  └─────────┴─────────┴─────────┴─────────┘                              │
│              (new)                                                       │
│                                                                          │
│  Snapshot (epoch=100) still sees:                                        │
│  ┌─────────┬─────────┬─────────┬─────────┐                              │
│  │ Page 0  │ Page 1  │ Page 2  │ Page 3  │                              │
│  │ gen=5   │ gen=5   │ gen=5   │ gen=5   │                              │
│  └─────────┴─────────┴─────────┴─────────┘                              │
│              (original, preserved)                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7.2 Snapshot Creation

```rust
fn create_snapshot(scope: SnapshotScope) -> Snapshot {
    // 1. Wait for current epoch to complete
    let epoch = wait_for_epoch_boundary();
    
    // 2. For each segment in scope
    let segment_refs = Vec::new();
    for segment in scope.segments() {
        // Increment generation (marks COW boundary)
        segment.increment_generation();
        segment_refs.push(SegmentSnapshot {
            segment_id: segment.id,
            generation: segment.generation - 1,  // Previous gen is snapshot
            page_map: segment.page_table.clone(),
        });
    }
    
    // 3. Record snapshot metadata
    let snapshot = Snapshot {
        id: generate_snapshot_id(),
        epoch,
        created_at: now(),
        segments: segment_refs,
    };
    
    // 4. Persist snapshot record (if durable snapshot)
    if scope.durable {
        persist_snapshot_record(&snapshot);
    }
    
    snapshot
}
```

### 7.3 Snapshot Restoration

```rust
fn restore_snapshot(snapshot: Snapshot, target_scope: RestoreScope) -> Result<()> {
    // 1. For each segment in snapshot
    for seg_snap in snapshot.segments {
        let segment = get_or_create_segment(seg_snap.segment_id);
        
        // 2. Restore page table
        for (page_idx, page_ref) in seg_snap.page_map.iter() {
            // Pages from snapshot are read-only
            let page = resolve_page(page_ref)?;
            segment.page_table[page_idx] = page_ref;
        }
        
        // 3. Mark segment as COW (future writes will copy)
        segment.cow_generation = seg_snap.generation + 1;
        segment.cow_active = true;
    }
    
    // 4. Rebuild indexes if needed
    if target_scope.rebuild_indexes {
        rebuild_relation_indexes(snapshot.segments)?;
    }
    
    Ok(())
}
```

### 7.4 Diff Between Snapshots

Because layers are content-addressed, diffing snapshots is efficient:

```rust
fn diff_snapshots(old: &Snapshot, new: &Snapshot) -> SnapshotDiff {
    let mut diff = SnapshotDiff::new();
    
    for segment_id in union(old.segment_ids(), new.segment_ids()) {
        let old_seg = old.get_segment(segment_id);
        let new_seg = new.get_segment(segment_id);
        
        match (old_seg, new_seg) {
            (None, Some(s)) => diff.add_segment(s),
            (Some(s), None) => diff.remove_segment(s),
            (Some(old_s), Some(new_s)) => {
                // Compare pages by generation
                for page_idx in 0..PAGE_COUNT {
                    let old_page = old_s.page_table[page_idx];
                    let new_page = new_s.page_table[page_idx];
                    
                    if old_page != new_page {
                        // Pages differ - compare objects within
                        let page_diff = diff_pages(old_page, new_page);
                        diff.add_page_diff(segment_id, page_idx, page_diff);
                    }
                }
            }
        }
    }
    
    diff
}

fn diff_pages(old_page: &Page, new_page: &Page) -> PageDiff {
    let mut page_diff = PageDiff::new();
    
    for slot in union(old_page.slots(), new_page.slots()) {
        let old_obj = old_page.get_object(slot);
        let new_obj = new_page.get_object(slot);
        
        match (old_obj, new_obj) {
            (None, Some(o)) => page_diff.add_object(o),
            (Some(o), None) => page_diff.remove_object(o),
            (Some(old_o), Some(new_o)) => {
                // Compare layer hashes (fast!)
                for layer_id in 0..MAX_LAYERS {
                    let old_hash = old_o.layer_hash(layer_id);
                    let new_hash = new_o.layer_hash(layer_id);
                    
                    if old_hash != new_hash {
                        page_diff.modify_layer(
                            old_o.oid, 
                            layer_id, 
                            old_hash, 
                            new_hash
                        );
                    }
                }
            }
        }
    }
    
    page_diff
}
```

**Key insight:** Layer hashes make comparison O(1) per layer. We only examine actual content when hashes differ.

---

## 8. Fault-In Architecture

### 8.1 Unified Fault-In Pattern

Fault-in is NXIA's universal mechanism for handling missing data:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Fault-In Flow                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────┐                                                         │
│  │ Access(X)   │  Pathway attempts to access object/layer                │
│  └──────┬──────┘                                                         │
│         │                                                                │
│         ▼                                                                │
│  ┌─────────────┐     YES    ┌─────────────┐                              │
│  │ Present?    │───────────►│ Return      │  Fast path: data in RAM     │
│  └──────┬──────┘            └─────────────┘                              │
│         │ NO                                                             │
│         ▼                                                                │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ Create Fault-In Request                                          │    │
│  │   object_id: X                                                   │    │
│  │   layers_needed: [1, 2]                                          │    │
│  │   rights_required: Read                                          │    │
│  │   requesting_pathway: P                                          │    │
│  └──────────────────────────┬──────────────────────────────────────┘    │
│                             │                                            │
│                             ▼                                            │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ Yield Pathway P                                                  │    │
│  │   - Save pathway state                                           │    │
│  │   - Mark as "waiting on fault-in"                                │    │
│  │   - Scheduler can run other pathways                             │    │
│  └──────────────────────────┬──────────────────────────────────────┘    │
│                             │                                            │
│                             ▼                                            │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ MMS Server Processes Request                                     │    │
│  │   1. Determine authoritative sector for X                        │    │
│  │   2. Request data from sector (may involve I/O, network)         │    │
│  │   3. Hydrate into RAM sector                                     │    │
│  │   4. Update presence bitmaps                                     │    │
│  │   5. VSS check (does pathway have rights?)                       │    │
│  └──────────────────────────┬──────────────────────────────────────┘    │
│                             │                                            │
│                             ▼                                            │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ Resume Pathway P                                                 │    │
│  │   - Success: data now present, continue execution                │    │
│  │   - Failure: return structured error to pathway                  │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 8.2 Fault-In Request Types

```rust
enum FaultInRequest {
    // Object layer(s) not present in RAM
    ObjectLayer {
        oid: OID,
        layers: LayerMask,
        rights: AccessRights,
    },
    
    // Object exists somewhere but we don't know where
    ObjectResolve {
        oid: OID,
    },
    
    // Type information needed
    TypeInfo {
        type_ref: TypeRef,
    },
    
    // Code artifacts needed
    CodeArtifact {
        code_hash: Hash,
        artifact_type: ArtifactType,  // Source, IL, Native
    },
    
    // Relation index portion needed
    RelationIndex {
        index_type: IndexType,
        key_range: KeyRange,
    },
    
    // Semantic layer (embeddings) needed
    SemanticLayer {
        oid: OID,
        target_sector: SectorId,  // Often GPU
    },
    
    // Security policy needed
    SecurityPolicy {
        policy_ref: PolicyRef,
    },
}
```

### 8.3 Fault-In Response

```rust
enum FaultInResponse {
    Success {
        // Data is now present
        location: DataLocation,
        // How long until we should refresh (for cached data)
        cache_ttl: Option<Duration>,
    },
    
    PartialSuccess {
        // Some layers present, others still missing
        present: LayerMask,
        missing: LayerMask,
        // Missing layers might need different handling
        missing_reason: Vec<(LayerId, FaultInFailure)>,
    },
    
    Failure(FaultInFailure),
}

enum FaultInFailure {
    ObjectNotFound,
    LayerNotFound { layer_id: LayerId },
    SectorUnavailable { sector: SectorId },
    AccessDenied { required: AccessRights },
    NetworkError { details: String },
    Timeout,
    VersionConflict { expected: Version, found: Version },
}
```

### 8.4 Batch Fault-In

For efficiency, NXIA supports batch fault-in:

```rust
// Instead of:
for oid in oids {
    let obj = access(oid);  // Potentially N separate fault-ins
}

// Use:
let batch = FaultInBatch::new()
    .add(oid1, layers![BASE_STATE, RELATIONS])
    .add(oid2, layers![BASE_STATE])
    .add(oid3, layers![BASE_STATE, SEMANTIC]);

let results = mms.fault_in_batch(batch).await;

// All requests are coalesced:
// - Same sector requests are batched
// - Network requests are pipelined
// - Results are hydrated together
```

---

## 9. GPU Acceleration

### 9.1 GPU Sector Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         GPU Sector                                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                        GPU Memory Layout                            │ │
│  ├────────────────────────────────────────────────────────────────────┤ │
│  │                                                                      │ │
│  │  REGION 1: Graph Structure (CSR format)                             │ │
│  │  ┌─────────────────────────────────────────────────────────────┐   │ │
│  │  │  row_ptr:  [0, 2, 5, 8, 12, ...]        (vertex offsets)    │   │ │
│  │  │  col_idx:  [1, 3, 0, 2, 4, 1, 3, ...]   (edge targets)      │   │ │
│  │  │  edge_type:[0, 1, 0, 0, 1, 2, ...]      (relation types)    │   │ │
│  │  │  edge_weight:[1.0, 0.5, 0.8, ...]       (optional weights)  │   │ │
│  │  └─────────────────────────────────────────────────────────────┘   │ │
│  │                                                                      │ │
│  │  REGION 2: Embeddings                                                │ │
│  │  ┌─────────────────────────────────────────────────────────────┐   │ │
│  │  │  Object embeddings: N × D matrix (N objects, D dimensions)  │   │ │
│  │  │  [0.12, -0.34, 0.56, ...],  // Object 0                     │   │ │
│  │  │  [0.23, 0.45, -0.67, ...],  // Object 1                     │   │ │
│  │  │  ...                                                         │   │ │
│  │  └─────────────────────────────────────────────────────────────┘   │ │
│  │                                                                      │ │
│  │  REGION 3: Working Memory                                            │ │
│  │  ┌─────────────────────────────────────────────────────────────┐   │ │
│  │  │  Frontier buffers (for BFS/traversal)                        │   │ │
│  │  │  Result buffers                                              │   │ │
│  │  │  Intermediate computation                                    │   │ │
│  │  └─────────────────────────────────────────────────────────────┘   │ │
│  │                                                                      │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 9.2 CSR (Compressed Sparse Row) Format

The graph structure is exported to GPU in CSR format for efficient parallel access:

```
Example Graph:
  0 ──orders──► 1
  0 ──orders──► 3  
  1 ──manager─► 2
  1 ──orders──► 4
  2 ──orders──► 4
  3 ──manager─► 2

CSR Representation:
  row_ptr:   [0, 2, 4, 5, 6, 6]
              │  │  │  │  │  └─ vertex 4: no outgoing edges (empty)
              │  │  │  │  └──── vertex 3: 1 edge (indices 5..6)
              │  │  │  └─────── vertex 2: 1 edge (indices 4..5)
              │  │  └────────── vertex 1: 2 edges (indices 2..4)
              │  └───────────── vertex 0: 2 edges (indices 0..2)
              └──────────────── start

  col_idx:   [1, 3, 2, 4, 4, 2]
              │  │  │  │  │  └─ edge 5: 3→2
              │  │  │  │  └──── edge 4: 2→4
              │  │  │  └─────── edge 3: 1→4
              │  │  └────────── edge 2: 1→2
              │  └───────────── edge 1: 0→3
              └──────────────── edge 0: 0→1

  edge_type: [0, 0, 1, 0, 0, 1]  (0=orders, 1=manager)
```

**Access pattern:**
```
Edges from vertex V:
  start = row_ptr[V]
  end = row_ptr[V + 1]
  for i in start..end:
    target = col_idx[i]
    rel_type = edge_type[i]
```

### 9.3 GPU Kernels for Graph Operations

**BFS Level Expansion:**

```cuda
__global__ void bfs_expand_kernel(
    const int* row_ptr,
    const int* col_idx,
    const int* frontier,
    const int frontier_size,
    int* next_frontier,
    int* next_size,
    int* visited
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (tid < frontier_size) {
        int vertex = frontier[tid];
        int start = row_ptr[vertex];
        int end = row_ptr[vertex + 1];
        
        for (int i = start; i < end; i++) {
            int neighbor = col_idx[i];
            
            // Atomic check-and-set for visited
            if (atomicCAS(&visited[neighbor], 0, 1) == 0) {
                // We're first to visit this neighbor
                int pos = atomicAdd(next_size, 1);
                next_frontier[pos] = neighbor;
            }
        }
    }
}
```

**Semantic Similarity Search:**

```cuda
__global__ void cosine_similarity_kernel(
    const float* embeddings,    // N × D matrix
    const float* query,         // D-dim query vector
    const int N,
    const int D,
    float* similarities         // Output: N similarities
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (tid < N) {
        float dot = 0.0f;
        float norm_emb = 0.0f;
        float norm_query = 0.0f;
        
        for (int d = 0; d < D; d++) {
            float e = embeddings[tid * D + d];
            float q = query[d];
            dot += e * q;
            norm_emb += e * e;
            norm_query += q * q;
        }
        
        similarities[tid] = dot / (sqrtf(norm_emb) * sqrtf(norm_query));
    }
}
```

### 9.4 CPU/GPU Query Router

```rust
struct QueryRouter {
    gpu_sector: GpuSector,
    cpu_indexes: RelationIndexes,
}

impl QueryRouter {
    fn execute(&self, query: GraphQuery) -> QueryResult {
        let plan = self.plan_query(&query);
        
        match plan.execution_target {
            ExecutionTarget::Cpu => {
                self.execute_cpu(query)
            }
            ExecutionTarget::Gpu => {
                self.execute_gpu(query)
            }
            ExecutionTarget::Hybrid { gpu_parts, cpu_parts } => {
                // Execute GPU parts in parallel with CPU parts
                let gpu_future = self.execute_gpu_async(gpu_parts);
                let cpu_results = self.execute_cpu(cpu_parts);
                let gpu_results = gpu_future.await;
                self.merge_results(cpu_results, gpu_results)
            }
        }
    }
    
    fn plan_query(&self, query: &GraphQuery) -> QueryPlan {
        // Heuristics for CPU vs GPU
        let estimated_edges = self.estimate_edge_count(query);
        let has_semantic = query.involves_embeddings();
        
        if estimated_edges > 10_000 || has_semantic {
            // Large traversal or semantic query → GPU
            QueryPlan::gpu(query)
        } else if estimated_edges < 100 {
            // Small query → CPU (avoid GPU transfer overhead)
            QueryPlan::cpu(query)
        } else {
            // Medium → depends on query type
            QueryPlan::auto(query)
        }
    }
}
```

### 9.5 Synchronization: CPU ↔ GPU

The GPU sector maintains a **derived representation** of the graph. Synchronization:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CPU ↔ GPU Synchronization                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  At Epoch Boundary:                                                      │
│                                                                          │
│  1. Collect relation index changes since last sync                       │
│     - New edges added                                                    │
│     - Edges removed                                                      │
│     - Edge weights/types modified                                        │
│                                                                          │
│  2. Compute delta for GPU update                                         │
│     - Incremental CSR update (if small changes)                          │
│     - Full CSR rebuild (if large changes)                                │
│                                                                          │
│  3. Transfer to GPU                                                      │
│     - Async DMA transfer                                                 │
│     - Double-buffering for zero-downtime                                 │
│                                                                          │
│  4. Update GPU sector metadata                                           │
│     - New sync epoch                                                     │
│     - Vertex/edge counts                                                 │
│                                                                          │
│  Embedding updates:                                                      │
│  - Individual embedding changes: direct GPU memory write                 │
│  - Bulk re-embedding: batch transfer                                     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 10. Allocation and Reclamation

### 10.1 Allocation Fast Path (Thread-Local)

Inspired by mimalloc, allocation uses thread-local segments:

```rust
fn allocate(size: usize, class: MemoryClass) -> ObjectSlot {
    // 1. Get thread-local segment for this class
    let segment = THREAD_LOCAL.get_segment(class);
    
    // 2. Try to allocate from current page
    if let Some(slot) = segment.current_page.try_allocate(size) {
        return slot;
    }
    
    // 3. Try another page in this segment
    for page in segment.pages_with_space(size) {
        if let Some(slot) = page.try_allocate(size) {
            segment.current_page = page;
            return slot;
        }
    }
    
    // 4. Allocate new page in segment
    if segment.has_free_pages() {
        let page = segment.allocate_page();
        let slot = page.allocate(size).unwrap();
        segment.current_page = page;
        return slot;
    }
    
    // 5. Need new segment (slow path)
    allocate_slow(size, class)
}

fn allocate_slow(size: usize, class: MemoryClass) -> ObjectSlot {
    // Request new segment from MMS Server
    let segment = MMS_SERVER.allocate_segment(class);
    
    // Assign to this thread
    THREAD_LOCAL.add_segment(segment);
    
    // Allocate from new segment
    let page = segment.allocate_page();
    let slot = page.allocate(size).unwrap();
    segment.current_page = page;
    slot
}
```

### 10.2 Reclamation: Liveness and GC

NXIA separates **liveness determination** from **reclamation**:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      Liveness Service                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Sources of Liveness:                                                    │
│                                                                          │
│  1. Explicit roots                                                       │
│     - Named objects in VNS                                               │
│     - Pinned objects (API call)                                          │
│     - System objects                                                     │
│                                                                          │
│  2. Relation graph reachability                                          │
│     - Objects reachable from roots via relations                         │
│     - Computed using relation indexes (efficient!)                       │
│                                                                          │
│  3. Leases                                                               │
│     - Runtime hosts mapping views hold implicit leases                   │
│     - Pathways accessing objects hold leases                             │
│     - Leases expire when view unmapped / pathway completes               │
│                                                                          │
│  4. External references                                                  │
│     - Objects referenced by remote sectors                               │
│     - Objects with pending fault-in requests                             │
│                                                                          │
│  Liveness is computed at epoch boundaries (consistent snapshot)          │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                      Reclamation Service                                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Reclamation Tiers:                                                      │
│                                                                          │
│  Tier 0: Evict cached layers from RAM                                    │
│          - Object still exists (in persistent sector)                    │
│          - RAM pages returned to pool                                    │
│          - Triggered by memory pressure                                  │
│                                                                          │
│  Tier 1: Evict derived layers (embeddings, indexes)                      │
│          - Can be recomputed                                             │
│          - GPU memory freed                                              │
│                                                                          │
│  Tier 2: Archive old versions                                            │
│          - Compact delta chains                                          │
│          - Move cold versions to cold storage                            │
│                                                                          │
│  Tier 3: Delete objects                                                  │
│          - Only for objects with no liveness                             │
│          - Requires policy approval                                      │
│          - Provenance record retained (tombstone)                        │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 10.3 Graph-Aware GC

Because relations are indexed, GC can use the graph directly:

```rust
fn compute_live_set(roots: &[OID]) -> HashSet<OID> {
    let mut live = HashSet::new();
    let mut frontier = roots.to_vec();
    
    while let Some(oid) = frontier.pop() {
        if live.insert(oid) {
            // Use relation index for efficient edge lookup
            let edges = RELATION_INDEX.get_outgoing_edges(oid);
            for (_, target) in edges {
                if !live.contains(&target) {
                    frontier.push(target);
                }
            }
        }
    }
    
    live
}
```

**Comparison to traditional GC:**
- Traditional: Trace heap pointers (requires scanning object contents)
- NXIA: Query relation index (B+tree lookups, much faster)

---

## 11. Integration Points

### 11.1 VEE Integration (Execution Engine)

```rust
// VEE calls MMS for object access
impl Pathway {
    fn access_object(&mut self, oid: OID, layers: LayerMask) -> AccessResult {
        match MMS.try_access(oid, layers, self.rights()) {
            Ok(view) => AccessResult::Present(view),
            Err(NotPresent { .. }) => {
                // Request fault-in and yield
                MMS.request_fault_in(oid, layers, self.id());
                self.yield_for_fault_in()
            }
            Err(AccessDenied { .. }) => {
                AccessResult::Error(AccessError::PermissionDenied)
            }
        }
    }
}
```

### 11.2 RS Integration (Relational System)

```rust
// RS uses MMS relation indexes
impl RelationalSystem {
    fn traverse(&self, start: OID, relation: RelationType) -> Vec<OID> {
        // Direct index lookup - no object access needed!
        MMS.relation_index().lookup_forward(start, relation)
    }
    
    fn query(&self, query: GraphQuery) -> QueryResult {
        // Route to CPU or GPU based on query characteristics
        MMS.query_router().execute(query)
    }
}
```

### 11.3 VSS Integration (Security System)

```rust
// VSS enforces at MMS access points
impl MmsServer {
    fn try_access(&self, oid: OID, layers: LayerMask, subject: Subject) -> Result<View> {
        let object = self.resolve(oid)?;
        
        // Check VSS policy
        let policy = VSS.get_policy(object.security_label());
        let rights = policy.evaluate(subject, &object, layers)?;
        
        if rights.contains(AccessRights::READ) {
            Ok(self.create_view(object, layers, rights))
        } else {
            Err(AccessDenied { required: AccessRights::READ, granted: rights })
        }
    }
}
```

### 11.4 Driver Integration

```rust
// Memory Device Driver interface
trait MemoryDeviceDriver {
    /// Read layer content by hash
    fn read_layer(&self, hash: LayerHash) -> Result<LayerContent>;
    
    /// Write layer content, returns hash
    fn write_layer(&self, content: &LayerContent) -> Result<LayerHash>;
    
    /// Read Engram for object
    fn read_engram(&self, oid: OID, layers: LayerMask) -> Result<Engram>;
    
    /// Write Engram
    fn write_engram(&self, engram: &Engram) -> Result<()>;
    
    /// Sector capabilities
    fn capabilities(&self) -> SectorCapabilities;
}

// Memory Class Driver interface  
trait MemoryClassDriver {
    /// Create object slot
    fn allocate(&self, type_ref: TypeRef, size: usize) -> Result<ObjectSlot>;
    
    /// Initialize envelope
    fn init_envelope(&self, slot: &mut ObjectSlot, type_ref: TypeRef);
    
    /// Serialize layer to bytes
    fn serialize_layer(&self, object: &Object, layer: LayerId) -> Result<Vec<u8>>;
    
    /// Deserialize layer from bytes
    fn deserialize_layer(&self, slot: &mut ObjectSlot, layer: LayerId, bytes: &[u8]) -> Result<()>;
    
    /// Class-specific capabilities
    fn capabilities(&self) -> ClassCapabilities;
}
```

---

## Appendix: Data Structures

### A.1 Core Types

```rust
// Object identifier (64-bit)
#[repr(transparent)]
struct OID(u64);

impl OID {
    fn domain(&self) -> u16 { (self.0 >> 48) as u16 }
    fn node(&self) -> u16 { (self.0 >> 32) as u16 }
    fn local_id(&self) -> u32 { self.0 as u32 }
    
    fn new(domain: u16, node: u16, local_id: u32) -> Self {
        OID(((domain as u64) << 48) | ((node as u64) << 32) | (local_id as u64))
    }
}

// Layer hash (truncated SHA-256)
#[repr(transparent)]
struct LayerHash([u8; 20]);

// Layer identifier (small integer)
#[repr(transparent)]
struct LayerId(u8);

// Layer mask (bitmap)
#[repr(transparent)]
struct LayerMask(u32);

impl LayerMask {
    const BASE_STATE: Self = LayerMask(1 << 1);
    const RELATIONS: Self = LayerMask(1 << 2);
    const PROVENANCE: Self = LayerMask(1 << 3);
    const SEMANTIC: Self = LayerMask(1 << 4);
    const CODE: Self = LayerMask(1 << 5);
    
    fn contains(&self, layer: LayerId) -> bool {
        (self.0 & (1 << layer.0)) != 0
    }
}

// Version stamp
#[repr(C)]
struct VersionStamp {
    epoch: u32,
    sequence: u32,
}

// Memory class
#[repr(u16)]
enum MemoryClass {
    Native = 0x01,
    Managed = 0x02,
    Capability = 0x03,
    Memantic = 0x04,
    // Extension classes: 0x10+
}

// Capabilities bitmap
bitflags! {
    struct Capabilities: u16 {
        const RELATIONS      = 1 << 0;
        const MAILBOX        = 1 << 1;
        const PERSISTENCE    = 1 << 2;
        const SEMANTIC       = 1 << 3;
        const PROVENANCE     = 1 << 4;
        const COW_SNAPSHOTS  = 1 << 5;
        const EXEC_CAPTURE   = 1 << 6;
        const AUDIT          = 1 << 7;
    }
}
```

### A.2 Universal Envelope

```rust
#[repr(C)]
struct UniversalEnvelope {
    oid: OID,                          // 8 bytes
    type_ref: u64,                     // 8 bytes
    memory_class: MemoryClass,         // 2 bytes
    capabilities: Capabilities,        // 2 bytes
    security_label: u32,               // 4 bytes
    provenance_ref: u64,               // 8 bytes
    version_stamp: VersionStamp,       // 8 bytes
    relation_count: u32,               // 4 bytes
    slot_size: u32,                    // 4 bytes
    reserved: u64,                     // 8 bytes
    checksum: u64,                     // 8 bytes
}                                      // Total: 64 bytes

static_assert!(size_of::<UniversalEnvelope>() == 64);
```

### A.3 Page Header

```rust
#[repr(C)]
struct PageHeader {
    page_id: u64,                      // Segment ID + offset
    state: PageState,                  // 1 byte
    _pad1: [u8; 3],                    // 3 bytes padding
    object_count: u16,                 // 2 bytes
    free_slot_head: u16,               // 2 bytes
    largest_free: u16,                 // 2 bytes
    _pad2: [u8; 2],                    // 2 bytes padding
    cow_generation: u64,               // 8 bytes
    checksum: u32,                     // 4 bytes
    _pad3: [u8; 4],                    // 4 bytes padding
}                                      // Total: 32 bytes

#[repr(u8)]
enum PageState {
    Unmapped = 0,
    Mapped = 1,
    Faulting = 2,
    Cow = 3,
}
```

### A.4 Segment Header

```rust
#[repr(C)]
struct SegmentHeader {
    segment_id: u64,                   // 8 bytes
    memory_class: MemoryClass,         // 2 bytes
    flags: SegmentFlags,               // 2 bytes
    sector_id: u32,                    // 4 bytes
    owner_thread: u64,                 // 8 bytes (ThreadId)
    page_count: u16,                   // 2 bytes
    _pad: [u8; 2],                     // 2 bytes padding
    cow_generation: u64,               // 8 bytes
    cow_parent: u64,                   // 8 bytes (SegmentId or 0)
    free_page_bitmap: [u64; 1],        // 8 bytes (for 64 pages)
}                                      // Total: 52 bytes (+ padding to 64)

bitflags! {
    struct SegmentFlags: u16 {
        const COW_ACTIVE     = 1 << 0;
        const PINNED         = 1 << 1;
        const SYSTEM         = 1 << 2;
    }
}
```

### A.5 Relation Index Entry

```rust
// Primary index: (Source, RelationType) → [Targets]
#[repr(C)]
struct RelationIndexKey {
    source_oid: OID,
    relation_type: u32,
    _pad: u32,
}

#[repr(C)]
struct RelationIndexValue {
    count: u32,
    // Followed by `count` OIDs inline, or reference to overflow page
    targets_inline: [OID; 7],  // Up to 7 inline, then overflow
    overflow_page: u64,        // 0 if no overflow
}

// Reverse index: (Target, RelationType) → [Sources]
// Same structure, different index
```

---

## Summary

This memory architecture specification defines:

1. **Storage Hierarchy**: Segments (4MB) → Pages (64KB) → Object Slots, with thread-local allocation for fast path

2. **Universal Envelope**: 64-byte fixed header on every object providing identity, type, class, capabilities, security, provenance

3. **Content-Addressed Layers**: Object state decomposed into hash-identified layers that can be independently stored, versioned, and faulted

4. **Sector Authority**: RAM, Persistent, GPU, and Remote sectors with explicit authority model and cache semantics

5. **Relation Indexing**: B+tree indexes over edges, making graph queries O(log N) instead of pointer-chasing

6. **Copy-on-Write**: Page-level COW enabling efficient snapshots without copying

7. **Fault-In**: Unified mechanism for acquiring missing layers, objects, code, or capabilities

8. **GPU Acceleration**: CSR graph representation + embeddings in GPU sector for parallel graph and semantic queries

9. **Graph-Aware GC**: Liveness computed via relation indexes, reclamation in tiers

This architecture forms the foundation for all other NXIA subsystems (VEE, RS, VSS, VTS, VNS).

---

*End of NXIA Memory Architecture Specification v0.1*
