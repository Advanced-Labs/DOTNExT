# VAYRON Project Documentation

> **VAYRON** - A novel runtime platform inspired by OS/kernel architecture: CLR/VM/compiler stack restructured as a microkernel + device driver system for software paradigms.

---

## Overview

VAYRON transforms the .NET CLR from a closed runtime into an extensible platform where core behaviors become pluggable "device drivers":

- **ObjectModelDevice** - What an object IS (layout, scanning, identity)
- **FieldAccessDevice** - Field read/write interception
- **StorageDevice** - Persistence (backed by Voron)
- **CallDispatchDevice** - Method invocation/routing (backed by NewOrleans)
- **RelationalDevice** - Edges and graph traversal
- **VersionDevice** - Time-travel / MVCC
- **SecurityDevice** - Kernel-enforced security

Default behavior is preserved via "DefaultDrivers" that proxy standard CLR operations.

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [VAYRON-Codebase-Analysis.md](./VAYRON-Codebase-Analysis.md) | Analysis of existing infrastructure in DOTNExT repo |
| [VAYRON-Phase0-Implementation-Plan.md](./VAYRON-Phase0-Implementation-Plan.md) | Detailed implementation plan for Phase 0 |

---

## Phase Overview

### Phase 0: Open the CLR (Current Focus)
- Implement DDS routing bit in object headers
- Create ops_root side table
- Define ObjectModelDevice + FieldAccessDevice interfaces
- Implement DefaultDrivers (proxy CLR behavior)
- Validate with test suite

### Phase 1: Persistence Vertical Slice
- StorageDevice interface becomes real
- Voron-backed StorageDriver implementation
- VUID (UUID v7) identity system
- Create → mutate → restart → materialize validation

### Phase 2: Relational Substrate
- RelationalDevice for edges and reverse-edges
- Graph traversal primitives
- Indexing integration with Voron

### Phase 3: Distribution
- CallDispatchDevice implementation
- NewOrleans-backed activation/placement
- Remote method invocation

### Phase 4+: Advanced Features
- Replication policies
- VersionDevice (time-travel)
- SemanticDevice (embeddings)
- Security hardening

---

## Key Concepts

| Term | Definition |
|------|------------|
| **DDS** | Device Driver System - the pluggability mechanism |
| **SAL** | Software Abstraction Layer - what DDS implements |
| **VObject** | Virtualized object instance (runtime view) |
| **Varia** | Whole object across space+time (conceptual) |
| **VUID** | Virtual Unique Identifier (UUID v7) |
| **ops_root** | Per-object driver dispatch table |
| **DefaultDriver** | Driver that proxies standard CLR behavior |

---

## Integration Engines

### Voron (from RavenDB)
Page-based, MVCC, ACID storage engine providing:
- B-Tree key-value storage
- Write-ahead journaling
- Transaction isolation
- Crash recovery

**Role:** Gen-0 StorageDevice driver

### NewOrleans (Orleans fork)
Distributed virtual actor framework providing:
- Grain activation/placement
- Single-threaded execution model
- Cross-silo messaging
- Filter-based interception

**Role:** Gen-0 CallDispatch/Placement driver family

---

## Repository Structure

```
DOTNExT/
├── src/
│   ├── runtime/           # CLR - extensibility target
│   ├── roslyn/            # Compiler (future C= support)
│   ├── NewOrleans/        # Orleans fork
│   └── Raven/             # RavenDB with Voron
│       └── src/Voron/     # Storage engine
├── Docs/
│   └── VAYRON/            # This folder
└── ...
```

---

## Getting Started

1. Read [VAYRON-Codebase-Analysis.md](./VAYRON-Codebase-Analysis.md) to understand existing infrastructure
2. Review [VAYRON-Phase0-Implementation-Plan.md](./VAYRON-Phase0-Implementation-Plan.md) for detailed implementation steps
3. Start with WP1 (Header Bit Infrastructure) as the foundation

---

*VAYRON R&D Project - Advanced-Labs/DOTNExT*
