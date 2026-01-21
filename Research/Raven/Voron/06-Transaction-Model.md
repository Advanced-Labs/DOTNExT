# Voron Transaction Model

> Engineering analysis of Voron's ACID guarantees, MVCC implementation, and transaction lifecycle.

---

## 1. Overview

Voron implements a strict transaction model with:

| Property | Implementation |
|----------|----------------|
| **Atomicity** | All-or-nothing commit via journal |
| **Consistency** | Schema validation, invariants in commit |
| **Isolation** | Snapshot isolation via MVCC |
| **Durability** | WAL fsync before commit completion |

---

## 2. Transaction Types

### 2.1 Read Transactions

```csharp
TransactionFlags.Read
```

Characteristics:
- Never block other transactions
- See a consistent snapshot at start time
- No scratch buffer allocation needed
- Can be cloned for long-running operations

### 2.2 Write Transactions

```csharp
TransactionFlags.ReadWrite
```

Characteristics:
- Only one active at a time (global write lock)
- Allocate pages in scratch buffers (COW)
- Commit via journal with fsync
- Can be async-committed for performance

---

## 3. Two-Level Transaction Architecture

Voron uses two transaction layers:

### 3.1 LowLevelTransaction (Impl/LowLevelTransaction.cs)

Handles page-level operations:

```csharp
public sealed unsafe class LowLevelTransaction : IPagerLevelTransactionState
{
    // Identity
    private readonly long _id;
    private readonly StorageEnvironment _env;
    private readonly TransactionFlags Flags;

    // Page tracking
    private readonly HashSet<long> _dirtyPages;
    private readonly Stack<long> _pagesToFreeOnCommit;
    private readonly Dictionary<long, PageFromScratchBuffer> _scratchPagesTable;
    private readonly HashSet<long> _freedPages;

    // State snapshot (for read isolation)
    private readonly StorageEnvironmentState _state;

    // Journal integration
    private readonly WriteAheadJournal _journal;
    private TransactionHeader _txHeader;
}
```

Key operations:
```csharp
// Page operations
public Page GetPage(long pageNumber);
public Page AllocatePage(int numberOfPages);
public void FreePage(long pageNumber);
public Page ModifyPage(long pageNumber);

// Transaction lifecycle
public void Commit();
public void Rollback();
```

### 3.2 Transaction (Impl/Transaction.cs)

Handles high-level data structure operations:

```csharp
public sealed class Transaction : IDisposable
{
    private LowLevelTransaction _lowLevelTransaction;

    // Data structure caches
    private Dictionary<Slice, Tree> _trees;
    private Dictionary<TableKey, Table> _tables;
    private Dictionary<ContainerId, Container.TransactionState> _containers;
    private Dictionary<Slice, PostingList> _postingLists;

    // Lifecycle
    public void Commit();      // PrepareForCommit + LLT.Commit
    public void Dispose();     // Cleanup
}
```

---

## 4. MVCC Implementation

### 4.1 Transaction IDs

Each transaction gets a monotonically increasing ID:

```csharp
// In StorageEnvironment
private long _transactionId;

public long NextTransactionId => Interlocked.Increment(ref _transactionId);
```

### 4.2 Snapshot Isolation

Read transactions capture the environment state at start:

```csharp
public LowLevelTransaction(StorageEnvironment env, ...)
{
    _id = env.CurrentReadTransactionId;

    // Capture state snapshot
    _state = env.State;  // Immutable reference

    // Capture journal snapshots for page resolution
    JournalSnapshots = _journal.GetSnapshots();
}
```

### 4.3 Page Version Resolution

When reading, find the correct version of a page:

```csharp
public byte* AcquirePagePointer(long pageNumber)
{
    // 1. Check current transaction's scratch (our modifications)
    if (_scratchPagesTable.TryGetValue(pageNumber, out var scratch))
    {
        return _env.ScratchBufferPool.AcquirePagePointer(scratch);
    }

    // 2. For reads: check journal for newer (but not our) versions
    foreach (var journalSnapshot in JournalSnapshots)
    {
        if (journalSnapshot.TryGetPageFromJournal(pageNumber, _id, out var ptr))
        {
            return ptr;
        }
    }

    // 3. Read from data file
    return DataPager.AcquirePagePointer(this, pageNumber);
}
```

### 4.4 Writer Isolation

Writers always see their own modifications:

```csharp
// In LowLevelTransaction.ModifyPage
public Page ModifyPage(long pageNumber)
{
    // Already modified?
    if (_scratchPagesTable.TryGetValue(pageNumber, out var existing))
    {
        return new Page(existing.GetPointer());
    }

    // COW: copy to scratch
    var scratchBuffer = ScratchBufferPool.Allocate(this, 1);
    var srcPtr = AcquirePagePointer(pageNumber);  // Current version
    var dstPtr = scratchBuffer.GetPointer();

    Memory.Copy(dstPtr, srcPtr, PageSize);

    _scratchPagesTable[pageNumber] = scratchBuffer;
    _dirtyPages.Add(pageNumber);

    return new Page(dstPtr);
}
```

---

## 5. Transaction Lifecycle

### 5.1 Begin Transaction

```csharp
public Transaction ReadTransaction(...)
{
    // Read lock not needed - MVCC allows concurrent reads
    var txId = CurrentReadTransactionId;
    var llt = new LowLevelTransaction(this, txId, TransactionFlags.Read, ...);
    return new Transaction(llt);
}

public Transaction WriteTransaction(...)
{
    // Acquire exclusive write lock
    _transactionWriter.Wait();  // Semaphore(1)

    try
    {
        var txId = NextTransactionId;
        var llt = new LowLevelTransaction(this, txId, TransactionFlags.ReadWrite, ...);
        return new Transaction(llt);
    }
    catch
    {
        _transactionWriter.Release();
        throw;
    }
}
```

### 5.2 Commit (Write Transaction)

```
Transaction.Commit()
│
├── 1. PrepareForCommit()
│   ├── Write tree states to root objects
│   ├── Finalize table statistics
│   └── Prepare posting lists
│
├── 2. LowLevelTransaction.Commit()
│   │
│   ├── 2a. PrepareForCommit()
│   │   ├── Process free space
│   │   ├── Finalize root objects tree
│   │   └── Calculate transaction header
│   │
│   ├── 2b. WriteToJournalIsRequired? (usually yes)
│   │   │
│   │   ├── Compress dirty pages
│   │   ├── Build transaction buffer
│   │   └── Write to journal file
│   │
│   ├── 2c. fsync journal
│   │
│   ├── 2d. Update environment state
│   │   ├── NextPageNumber
│   │   ├── RootTreeState
│   │   └── TransactionId
│   │
│   └── 2e. Release scratch buffers for completed readers
│
└── 3. Release write lock
```

### 5.3 Transaction Header

Each committed transaction has a header in the journal:

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TransactionHeader
{
    [FieldOffset(0)]  public ulong HeaderMarker;      // Magic: 0x1A4C92AD90ABC123
    [FieldOffset(8)]  public long TransactionId;
    [FieldOffset(16)] public long NextPageNumber;
    [FieldOffset(24)] public long LastPageNumber;
    [FieldOffset(32)] public int PageCount;           // Pages in this tx
    [FieldOffset(36)] public uint Hash;               // XXHash of data
    [FieldOffset(40)] public TreeRootHeader Root;     // Root tree state
    [FieldOffset(88)] public TransactionMarker TxMarker;
    [FieldOffset(92)] public CompressedInfo Compressed;
    // ...
}
```

---

## 6. Rollback Handling

### 6.1 Explicit Rollback

```csharp
public void Rollback()
{
    if (Committed || RolledBack)
        return;

    // Notify listeners
    OnRollBack?.Invoke(this);

    // Return scratch pages to pool (no journal write)
    foreach (var page in _transactionPages)
    {
        _env.ScratchBufferPool.Free(page);
    }

    // Mark pages NOT freed (they weren't actually freed)
    // The free space tree wasn't modified

    RolledBack = true;
}
```

### 6.2 Implicit Rollback (Dispose without Commit)

```csharp
public void Dispose()
{
    if (!Committed)
    {
        Rollback();  // Implicit rollback
    }

    // Release resources
    ReleasePagerStates();
    _allocator?.Dispose();

    OnDispose?.Invoke(this);
}
```

---

## 7. Async Commit

For higher throughput, Voron supports async commit:

```csharp
// Start async commit and begin new transaction immediately
public Transaction BeginAsyncCommitAndStartNewTransaction(
    TransactionPersistentContext persistentContext)
{
    // Prepare current transaction
    PrepareForCommit();

    // Start writing to journal in background
    // But DON'T wait for fsync

    // Immediately create new transaction that can see our changes
    var newLlt = new LowLevelTransaction(previous: _lowLevelTransaction, ...);
    return new Transaction(newLlt);
}

// Wait for the async commit to complete
public void EndAsyncCommit()
{
    // Wait for journal fsync to complete
    _lowLevelTransaction.EndAsyncCommit();
}
```

This allows pipelining:
1. Transaction A prepares commit
2. Transaction B starts immediately (sees A's changes in scratch)
3. Transaction A's journal write completes
4. Transaction B can use A's results

---

## 8. Transaction Merging

Multiple write transactions can be merged for efficiency:

```csharp
// In WriteAheadJournal
public void WriteToJournal(LowLevelTransaction tx, ...)
{
    // Acquire write lock
    lock (_writeLock)
    {
        // Multiple transactions can be batched into one journal write
        // if they're queued during a slow fsync

        FlushPages(tx);

        if (ShouldFlushToDisk())
        {
            FlushToFile();  // One fsync for multiple transactions
        }
    }
}
```

---

## 9. Concurrency Model

### 9.1 Single Writer Guarantee

```csharp
// In StorageEnvironment
private readonly SemaphoreSlim _transactionWriter = new SemaphoreSlim(1, 1);

public Transaction WriteTransaction(...)
{
    // Only one writer at a time
    _transactionWriter.Wait();  // Blocks other writers
    try { ... }
    catch { _transactionWriter.Release(); throw; }
}
```

### 9.2 Reader-Writer Interaction

```
Timeline:
─────────────────────────────────────────────────────────────────────────►

Reader 1: ├───────── sees state T1 ─────────────────────────────────────┤
                                                                         │
Writer:       ├─── modifies pages (scratch) ──┤ commit T2               │
                                                    │                     │
Reader 2:                                           ├─── sees state T2 ──┤
                                                                         │
Reader 1 still sees T1 (snapshot isolation) ─────────────────────────────┘
```

### 9.3 Thread Safety

| Operation | Thread Safety |
|-----------|---------------|
| Create read transaction | Thread-safe |
| Create write transaction | Serialized (semaphore) |
| Read within transaction | Thread-safe for that transaction |
| Write within transaction | NOT thread-safe (single writer anyway) |
| Transaction dispose | Must be done by owner |

---

## 10. VAYRON Relevance

### 10.1 Handle Epoch/Version

VAYRON handles could store transaction ID to track version:

```csharp
struct VayronHandle
{
    ulong OID;              // Stable identity
    long LastKnownTxId;     // Version this handle last saw
    byte* CachedPointer;    // Stale if tx advanced
}
```

### 10.2 Materialization Timing

The MVCC model suggests when to rematerialize:

```csharp
void AccessObject(VayronHandle handle)
{
    if (handle.LastKnownTxId < currentTxId)
    {
        // Object may have changed - rematerialize
        handle.CachedPointer = ResolveFromVoron(handle.OID);
        handle.LastKnownTxId = currentTxId;
    }
    return handle.CachedPointer;
}
```

### 10.3 Transaction Scope for Object Access

VAYRON objects would need transaction context:

```csharp
using (var tx = env.ReadTransaction())
{
    // All object accesses within this scope see consistent snapshot
    var obj1 = VayronHandle.Load(tx, oid1);
    var obj2 = VayronHandle.Load(tx, oid2);
    // obj1 and obj2 guaranteed consistent
}
```

See [08-Integration-Analysis](./08-Integration-Analysis.md) for integration strategies.
