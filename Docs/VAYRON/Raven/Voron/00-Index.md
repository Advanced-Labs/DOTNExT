# Voron Storage Engine Reference Documentation

> **Purpose:** Reference documentation for Voron storage engine fundamentals.
> **Source Analyzed:** RavenDB 6.x (src/Raven/src/Voron)
>
> **Note:** For VAYRON R1 integration guidance, see:
> - Phase 1 CLR integration: `../../Phase1/CLR-Integration-Reference.md`
> - Phase 2 Voron usage: `../../Phase2/Voron-Integration-Guide.md`

---

## Document Index

### Core Architecture (01-07)

| Document | Description |
|----------|-------------|
| [01-Architecture-Overview](./01-Architecture-Overview.md) | High-level architecture, design philosophy, LMDB heritage |
| [02-Memory-Management](./02-Memory-Management.md) | Memory mapping, pager abstraction, address space management |
| [03-Storage-Layout](./03-Storage-Layout.md) | Page formats, file structure, on-disk layout |
| [04-Data-Structures](./04-Data-Structures.md) | B+Trees, Fixed-Size Trees, Tables, Containers |
| [05-Page-Architecture](./05-Page-Architecture.md) | Page types, headers, node layouts |
| [06-Transaction-Model](./06-Transaction-Model.md) | ACID guarantees, MVCC, transaction lifecycle |
| [07-Journal-WAL](./07-Journal-WAL.md) | Write-Ahead Logging, recovery, checkpointing |

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

For VAYRON integration:

1. Read this folder's docs (01-07) for Voron fundamentals
2. See `../../Phase1/CLR-Integration-Reference.md` for CLR integration points
3. See `../../Phase2/Voron-Integration-Guide.md` for StorageDevice patterns

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
- **Documentation Date:** 2026-01-21 (original), 2026-01-26 (reorganized)

---

*Voron fundamentals documentation. For VAYRON-specific integration, see Phase1/ and Phase2/ folders.*
