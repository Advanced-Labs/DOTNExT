# Voron Architecture Overview

> Engineering analysis of Voron's high-level architecture, design philosophy, and LMDB heritage.

---

## 1. What is Voron?

Voron is a **single-file, memory-mapped, transactional storage engine** developed by Hibernating Rhinos for RavenDB. It is heavily inspired by LMDB (Lightning Memory-Mapped Database) but extends and adapts the design for .NET and RavenDB's specific needs.

### Core Characteristics

| Characteristic | Description |
|----------------|-------------|
| **Storage Model** | Single data file + journal files |
| **I/O Model** | Memory-mapped (mmap/CreateFileMapping) |
| **Transaction Model** | ACID with MVCC (snapshot isolation) |
| **Concurrency** | Single writer, multiple readers |
| **Data Structure** | B+Trees as the fundamental primitive |
| **Language** | C# with unsafe code for performance |
| **Page Size** | Fixed 8KB pages |

---

## 2. LMDB Heritage

Voron borrows several key concepts from LMDB:

### Inherited from LMDB

1. **Memory-Mapped Architecture**: The entire database file is memory-mapped, making reads essentially pointer dereferences into mapped memory.

2. **Copy-on-Write (COW)**: Pages are never modified in place. Write transactions create new versions of pages.

3. **Single Writer Semantics**: Only one write transaction at a time, but unlimited concurrent readers.

4. **Fully Serialized Transactions**: The entire transaction is visible atomically to readers.

5. **No Buffer Cache**: The operating system's page cache IS the buffer cache.

### Divergences from LMDB

1. **Scratch Buffers**: Voron uses explicit scratch buffers for COW instead of LMDB's approach of writing directly to new pages in the data file.

2. **Write-Ahead Journal**: Voron adds WAL for durability, whereas LMDB relies solely on atomic page writes and careful ordering.

3. **Richer Data Structures**: Voron adds Tables, Containers, Posting Lists, HNSW graphs, etc.

4. **Compression**: Built-in page-level compression support.

5. **Encryption**: AEAD encryption support at the pager level.

6. **Schema Versioning**: Built-in schema upgrade infrastructure.

---

## 3. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                     StorageEnvironment                               │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    Transaction Layer                            ││
│  │  ┌──────────────────┐    ┌──────────────────────────────────┐  ││
│  │  │   Transaction    │───▶│    LowLevelTransaction           │  ││
│  │  │ (High-level API) │    │ (Page operations, COW, commit)   │  ││
│  │  └──────────────────┘    └──────────────────────────────────┘  ││
│  └─────────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    Data Structure Layer                         ││
│  │  ┌──────┐ ┌───────────┐ ┌───────┐ ┌────────────┐ ┌──────────┐ ││
│  │  │ Tree │ │FixedSize  │ │ Table │ │ Container  │ │ Posting  │ ││
│  │  │(B+T) │ │   Tree    │ │       │ │            │ │  List    │ ││
│  │  └──────┘ └───────────┘ └───────┘ └────────────┘ └──────────┘ ││
│  └─────────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    Storage Layer                                 ││
│  │  ┌─────────────────┐  ┌────────────────┐  ┌──────────────────┐ ││
│  │  │  ScratchBuffer  │  │   FreeSpace    │  │     Journal      │ ││
│  │  │     Pool        │  │   Handling     │  │ (Write-Ahead Log)│ ││
│  │  └─────────────────┘  └────────────────┘  └──────────────────┘ ││
│  └─────────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    Paging Layer                                  ││
│  │  ┌─────────────────────────────────────────────────────────────┐││
│  │  │                  AbstractPager                              │││
│  │  │  ┌──────────────────────┐  ┌──────────────────────────┐   │││
│  │  │  │ RvnMemoryMapPager    │  │ WindowsMemoryMapPager    │   │││
│  │  │  │ (Linux/macOS mmap)   │  │ (CreateFileMapping)      │   │││
│  │  │  └──────────────────────┘  └──────────────────────────┘   │││
│  │  │  ┌──────────────────────┐  ┌──────────────────────────┐   │││
│  │  │  │ CryptoPager          │  │ 32BitsPager variants     │   │││
│  │  │  │ (Encryption wrapper) │  │ (Address space limited)  │   │││
│  │  │  └──────────────────────┘  └──────────────────────────┘   │││
│  │  └─────────────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    File System Layer                                 │
│  ┌──────────────────────┐  ┌─────────────────────────────────────┐ │
│  │  Raven.voron         │  │  *.journal files                    │ │
│  │  (Data file)         │  │  (Write-ahead logs)                 │ │
│  └──────────────────────┘  └─────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 4. Core Components

### 4.1 StorageEnvironment

The main entry point. Responsibilities:
- Lifecycle management (create/open/dispose)
- Transaction creation
- Global state management (transaction counter, next page number)
- Coordination of flushing and syncing
- Schema upgrade orchestration

```csharp
// Key properties from StorageEnvironment.cs:138-191
public StorageEnvironment(StorageEnvironmentOptions options)
{
    _dataPager = options.DataPager;  // Main data file pager
    _freeSpaceHandling = new FreeSpaceHandling();
    _headerAccessor = new HeaderAccessor(this);
    _scratchBufferPool = new ScratchBufferPool(this);
    _journal = new WriteAheadJournal(this);

    if (IsNew)
        CreateNewDatabase();
    else
        LoadExistingDatabase();  // Includes recovery
}
```

### 4.2 Transaction / LowLevelTransaction

Two-tier transaction system:

**LowLevelTransaction** (Impl/LowLevelTransaction.cs):
- Page-level operations
- Manages dirty pages, freed pages
- Handles scratch buffer allocation
- Commits to journal
- MVCC snapshot isolation

**Transaction** (Impl/Transaction.cs):
- High-level API
- Tree/Table/Container management
- Prepares structures for commit
- User-facing operations

```csharp
// Transaction types
[Flags]
public enum TransactionFlags { Read = 0, ReadWrite = 1 }

// Key transaction state
public sealed class LowLevelTransaction {
    private readonly HashSet<long> _dirtyPages;           // Modified pages
    private readonly Stack<long> _pagesToFreeOnCommit;     // Deferred frees
    private readonly Dictionary<long, PageFromScratchBuffer> _scratchPagesTable;
    private readonly StorageEnvironmentState _state;       // Snapshot
}
```

### 4.3 AbstractPager

Memory mapping abstraction (Impl/Paging/AbstractPager.cs):

```csharp
public abstract unsafe class AbstractPager
{
    // Core page acquisition
    public abstract byte* AcquirePagePointer(
        IPagerLevelTransactionState tx,
        long pageNumber);

    // Allocation
    public abstract void AllocateMorePages(long newLength);

    // Platform-specific implementations:
    // - RvnMemoryMapPager (Linux/macOS via mmap)
    // - WindowsMemoryMapPager (Windows via CreateFileMapping)
    // - Windows32BitsMemoryMapPager (32-bit address space management)
    // - CryptoPager (encryption wrapper)
}
```

### 4.4 Write-Ahead Journal

Durability layer (Impl/Journal/WriteAheadJournal.cs):

```csharp
public sealed class WriteAheadJournal
{
    // Journal files (immutable list, append-only)
    private ImmutableAppendOnlyList<JournalFile> _files;

    // Current file being written to
    internal JournalFile CurrentFile;

    // Applies journal transactions to data file
    private readonly JournalApplicator _journalApplicator;
}
```

---

## 5. Data Flow: Write Transaction

```
1. Begin Write Transaction
   └── LowLevelTransaction created with next transaction ID
   └── Snapshot of current state captured

2. Modifications
   ├── Page read needed?
   │   └── Check scratch buffers → Check journal → Check data file
   │
   └── Page modification
       └── Allocate page in scratch buffer (COW)
       └── Mark page as dirty
       └── Update in-memory structures

3. Commit
   ├── Transaction.PrepareForCommit()
   │   └── Write tree states to root objects
   │   └── Finalize data structures
   │
   ├── LowLevelTransaction.Commit()
   │   └── Write transaction header + pages to journal
   │   └── fsync journal
   │   └── Update transaction counter
   │
   └── Background: Journal → Data File
       └── JournalApplicator.ApplyLogsToDataFile()
       └── Eventually fsync data file
       └── Delete old journals
```

---

## 6. Data Flow: Read Transaction

```
1. Begin Read Transaction
   └── Capture current transaction ID (snapshot)
   └── No locks needed (readers never block)

2. Page Access
   ├── Page in scratch buffer for newer transaction?
   │   └── Use older version from journal/data file
   │
   └── Acquire page pointer
       └── Memory-mapped read (pointer dereference)
       └── Validate checksum if needed

3. Dispose
   └── Release pager state references
   └── Allow scratch buffer cleanup for completed transactions
```

---

## 7. Key Design Decisions

### 7.1 Copy-on-Write via Scratch Buffers

Instead of LMDB's approach of allocating new pages directly in the data file, Voron uses temporary "scratch" files:

**Rationale**:
- Better control over memory lifetime
- Enables encryption without modifying data file structure
- Cleaner separation of concerns
- Works better with .NET's memory model

### 7.2 Write-Ahead Logging

Voron adds explicit WAL where LMDB doesn't need it:

**Rationale**:
- Crash recovery without corrupting the data file
- Enables incremental backup
- More predictable durability guarantees
- Compression of journal entries

### 7.3 Single Writer

Like LMDB, only one write transaction at a time:

**Rationale**:
- Massively simplifies concurrency
- No lock contention on data structures
- Readers never blocked by writers
- Writers never blocked by readers

---

## 8. File Layout

```
Database Directory/
├── Raven.voron              # Main data file
│   ├── Header pages (2x for redundancy)
│   └── Data pages (Trees, Tables, etc.)
│
├── Journals/
│   ├── 0000000000000000001.journal
│   ├── 0000000000000000002.journal
│   └── ...
│
└── Temp/
    └── scratch.*.buffers    # Scratch buffer files (temporary)
```

---

## 9. VAYRON Relevance

For VAYRON integration, the key architectural insights are:

1. **Memory Mapping Is Central**: Everything flows through mmap. This is the surface VAYRON would hook into.

2. **Page-Centric Model**: 8KB pages are the fundamental unit. Object bodies could be stored as page contents.

3. **Transaction IDs Enable MVCC**: VAYRON handles could cache which transaction version they're viewing.

4. **Pager Abstraction Is Clean**: The AbstractPager interface could be extended or wrapped for runtime integration.

5. **Scratch Buffers Manage COW**: Understand this for implementing materialization/fault-in behavior.

See [08-Integration-Analysis](./08-Integration-Analysis.md) for detailed integration points.
