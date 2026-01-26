# VAYRON R1 Project Documentation

> **VAYRON R1** - A novel runtime platform inspired by OS/kernel architecture: CLR/VM/compiler stack restructured as a **microkernel + device driver system** for software paradigms.

---

## Overview

VAYRON transforms the .NET CLR from a closed runtime into an extensible platform where core behaviors become pluggable "device drivers":

| Device Class | Responsibility | Gen-0 Engine |
|--------------|----------------|--------------|
| **ObjectModelDevice** | What an object IS (layout, scanning, identity) | CLR Default |
| **FieldAccessDevice** | Field read/write interception | CLR Default |
| **StorageDevice** | Persistence (layer I/O, transactions) | Voron |
| **CallDispatchDevice** | Method invocation/routing | NewOrleans |
| **RelationalDevice** | Edges and graph traversal | (Phase 3+) |
| **VersionDevice** | Time-travel / MVCC | (Phase 5+) |
| **SecurityDevice** | Kernel-enforced security | (Early wiring) |

Default behavior is preserved via "DefaultDrivers" that proxy standard CLR operations.

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [VAYRON-R1-Platform-Vision.md](./VAYRON-R1-Platform-Vision.md) | Complete platform vision, architecture, and design philosophy |
| [VAYRON-R1-Roadmap-and-Codebase-Map.md](./VAYRON-R1-Roadmap-and-Codebase-Map.md) | Implementation roadmap and codebase analysis |
| [Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md](./Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md) | Phase 1 detailed implementation plan |

### Archived Documentation

Original documentation versions are preserved in [Older/](./Older/).

---

## Phase Overview

### Phase 1: Open the CLR (DDS/SAL Skeleton) - *Current Focus*
- Implement DDS routing bit in object headers
- Create ops_root side table (SyncBlockIndex-keyed for GC safety)
- Define ObjectModelDevice + FieldAccessDevice interfaces
- Implement DefaultDrivers (proxy CLR behavior)
- Validate with test suite

> **Note:** Phase 1 delivers routing infrastructure + default driver scaffolding only. No persistence yet — that's Phase 2.

### Phase 2: Persistence Vertical Slice
- StorageDevice interface becomes real
- Voron-backed StorageDriver implementation
- VUID (UUID v7) identity system
- Create -> mutate -> restart -> materialize validation

### Phase 3: Relational Substrate
- RelationalDevice for edges and reverse-edges
- Graph traversal primitives
- Indexing integration with Voron

### Phase 4: Distribution
- CallDispatchDevice implementation
- NewOrleans-backed activation/placement
- Remote method invocation

### Phase 5+: Advanced Features
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
| **VType** | A CLR type marked as virtual and subject to virtualization rules |
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

1. Read [VAYRON-R1-Platform-Vision.md](./VAYRON-R1-Platform-Vision.md) to understand the architecture
2. Review [VAYRON-R1-Roadmap-and-Codebase-Map.md](./VAYRON-R1-Roadmap-and-Codebase-Map.md) for implementation roadmap and codebase details
3. Start with Phase 1 implementation plan in [Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md](./Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md)

---

*VAYRON R1 R&D Project - Advanced-Labs/DOTNExT*
