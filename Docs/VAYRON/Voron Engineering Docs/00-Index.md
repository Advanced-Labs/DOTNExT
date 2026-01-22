# Voron Storage Engine Research Documentation

> **Project:** VAYRON - Runtime-Integrated Persistent Storage
> **Status:** Research & Analysis Phase
> **Author:** Engineering Documentation by Claude
> **Source Analyzed:** RavenDB 6.x (src/Raven/src/Voron)

---

## Purpose

This documentation provides deep engineering analysis of Voron, RavenDB's LMDB-inspired storage engine. The analysis is conducted with a specific goal: understanding how Voron's architecture could inform and enable integration of persistent storage primitives directly into the .NET runtime for the VAYRON project.

---

## Document Index

### Core Architecture
| Document | Description |
|----------|-------------|
| [01-Architecture-Overview](./01-Architecture-Overview.md) | High-level architecture, design philosophy, LMDB heritage |
| [02-Memory-Management](./02-Memory-Management.md) | Memory mapping, pager abstraction, address space management |
| [03-Storage-Layout](./03-Storage-Layout.md) | Page formats, file structure, on-disk layout |

### Data Structures
| Document | Description |
|----------|-------------|
| [04-Data-Structures](./04-Data-Structures.md) | B+Trees, Fixed-Size Trees, Tables, Containers |
| [05-Page-Architecture](./05-Page-Architecture.md) | Page types, headers, node layouts |

### Transaction & Durability
| Document | Description |
|----------|-------------|
| [06-Transaction-Model](./06-Transaction-Model.md) | ACID guarantees, MVCC, transaction lifecycle |
| [07-Journal-WAL](./07-Journal-WAL.md) | Write-Ahead Logging, recovery, checkpointing |

### VAYRON Integration
| Document | Description |
|----------|-------------|
| [08-Integration-Analysis](./08-Integration-Analysis.md) | Key integration points for runtime embedding |
| [09-VAYRON-Considerations](./09-VAYRON-Considerations.md) | Architectural considerations for handle/body separation |
| [10-Runtime-Integration-Analysis](./10-Runtime-Integration-Analysis.md) | Deep CLR analysis: object header, GC, JIT, type system |
| [11-VAYRON-Synthesis](./11-VAYRON-Synthesis.md) | Final synthesis: integration map, proof path, risk ledger |

---

## Quick Reference

### Key Source Locations

```
src/Raven/src/Voron/
├── StorageEnvironment.cs         # Main entry point, environment lifecycle
├── StorageEnvironmentOptions.cs  # Configuration, pager creation
├── Page.cs, PageHeader.cs        # Core page structures
├── Constants.cs                  # Magic numbers, page sizes, versions
│
├── Data/                         # Data structure implementations
│   ├── BTrees/                   # Variable-size B+Tree (Tree.cs)
│   ├── Fixed/                    # Fixed-size trees
│   ├── Tables/                   # Structured table storage
│   ├── Containers/               # Blob containers
│   ├── CompactTrees/             # Prefix-compressed trees
│   ├── Lookups/                  # Numeric/textual lookups
│   ├── PostingLists/             # Inverted index structures
│   └── Compression/              # Page-level compression
│
├── Impl/                         # Core implementation
│   ├── Transaction.cs            # High-level transaction API
│   ├── LowLevelTransaction.cs    # Page-level transaction operations
│   ├── Paging/                   # Memory mapping abstraction
│   │   ├── AbstractPager.cs      # Base pager interface
│   │   ├── RvnMemoryMapPager.cs  # POSIX mmap implementation
│   │   └── Windows*.cs           # Windows memory map variants
│   ├── Journal/                  # Write-ahead logging
│   │   ├── WriteAheadJournal.cs  # Journal management
│   │   └── JournalApplicator.cs  # Recovery & application
│   ├── Scratch/                  # Copy-on-write scratch buffers
│   ├── FreeSpace/                # Free page tracking
│   └── FileHeaders/              # Database header management
│
└── Platform/                     # Platform-specific code
    ├── Posix/
    └── Win32/
```

### Core Constants

```csharp
// From Constants.cs
PageSize = 8KB (8192 bytes)
MagicMarker = 0xB16BAADC0DEF0015
TransactionHeaderMarker = 0x1A4C92AD90ABC123
CurrentVersion = 23
MaxCompressedPageSize = 64KB
MinKeysInPage = 2
```

### Architecture Pillars

1. **Memory-Mapped I/O**: All data access through mmap
2. **Copy-on-Write**: Modifications go to scratch buffers
3. **Write-Ahead Logging**: Durability before data file modification
4. **B+Tree Foundation**: Primary data structure for all indexing
5. **MVCC**: Snapshot isolation via transaction IDs

---

## Reading Order

For engineers new to Voron:

1. Start with **01-Architecture-Overview** for the big picture
2. Read **06-Transaction-Model** to understand the isolation model
3. Dive into **02-Memory-Management** for the core abstraction
4. Study **04-Data-Structures** for the tree implementations
5. Review **07-Journal-WAL** for durability mechanisms

For VAYRON integration work:

1. Start with **08-Integration-Analysis** for Voron-side integration points
2. Study **10-Runtime-Integration-Analysis** for CLR integration points
3. Review **06-Transaction-Model** (impacts object materialization)
4. Read **09-VAYRON-Considerations** for architectural decisions
5. Review **11-VAYRON-Synthesis** for final integration map and proof path

---

## Key Terminology

| Term | Meaning in Voron |
|------|------------------|
| **Page** | 8KB unit of storage, the atomic I/O unit |
| **Pager** | Abstraction over memory-mapped files |
| **Tree** | B+Tree for variable-size keys/values |
| **FixedSizeTree** | Optimized B+Tree for fixed-size entries |
| **Table** | Higher-level abstraction with schema, indexes |
| **Scratch Buffer** | COW buffer for uncommitted page modifications |
| **Journal** | Write-ahead log for durability |
| **Slice** | Efficient byte buffer abstraction (key/value) |
| **LowLevelTransaction** | Page-level transaction operations |
| **Transaction** | High-level API over LowLevelTransaction |

---

## Version Information

- **Voron Version Analyzed:** Schema version 23
- **RavenDB Source:** Approximately 6.x branch
- **CoreCLR Analyzed:** DOTNExT VMR runtime branch
- **Documentation Date:** 2026-01-21

---

## Runtime Source Locations (for documents 10-11)

```
src/runtime/src/coreclr/
├── vm/                          # Virtual Machine implementation
│   ├── syncblk.h                # Object header, BIT_SBLK_* definitions
│   ├── object.h                 # Object structure, GetHeader()
│   ├── methodtable.h            # Type flags, category masks
│   ├── class.h                  # EEClass, VMFlags
│   ├── jithelpers.cpp           # JIT_GetFieldAddr, JIT_WriteBarrier
│   ├── jitinterface.h           # Helper declarations
│   ├── wellknownattributes.h    # Known attribute enum
│   └── typehandle.h             # TypeHandle class
│
├── gc/                          # Garbage Collector
│   ├── gc.cpp                   # Mark phase, go_through_object
│   ├── gcpriv.h                 # CFinalize, mark_queue_t
│   ├── gcdesc.h                 # CGCDesc, CGCDescSeries
│   └── gcinterface.h            # promote_func typedef
│
├── jit/                         # Just-In-Time Compiler
│   ├── gentree.h                # GenTreeFieldAddr
│   ├── importer.cpp             # CEE_LDFLD handling
│   ├── codegencommon.cpp        # Write barrier emission
│   └── namedintrinsiclist.h     # Intrinsic definitions
│
└── inc/                         # Headers
    ├── jithelpers.h             # JITHELPER macro definitions
    └── corinfo.h                # CORINFO_HELP_* enum
```
