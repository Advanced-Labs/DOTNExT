# Voron Write-Ahead Journal (WAL)

> Engineering analysis of Voron's durability layer: journal structure, recovery, and checkpointing.

---

## 1. Overview

Voron uses Write-Ahead Logging (WAL) to ensure durability without modifying the data file on every transaction. The flow is:

```
Transaction Commit
        │
        ▼
┌───────────────────┐
│ Write to Journal  │ ← Dirty pages written here first
│ + fsync           │
└─────────┬─────────┘
          │ (Transaction is durable after this)
          ▼
┌───────────────────┐
│ Background Flush  │ ← Later, apply to data file
│ Journal → Data    │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ Delete Old        │ ← Once data file has all changes
│ Journals          │
└───────────────────┘
```

---

## 2. Journal Structure

### 2.1 File Layout

```
journals/
├── 0000000000000000001.journal    # Oldest active journal
├── 0000000000000000002.journal
├── 0000000000000000003.journal    # Current journal
└── (older journals deleted after flush)
```

### 2.2 Journal File Format

```
Journal File
┌─────────────────────────────────────────────────────────────────────┐
│ Transaction 1                                                       │
│ ┌─────────────────────────────────────────────────────────────────┐│
│ │ TransactionHeader (96 bytes)                                    ││
│ │  ├── HeaderMarker (magic)                                       ││
│ │  ├── TransactionId                                              ││
│ │  ├── NextPageNumber                                             ││
│ │  ├── PageCount                                                  ││
│ │  ├── Hash (XXHash)                                              ││
│ │  ├── Root tree state                                            ││
│ │  └── Compression info                                           ││
│ ├─────────────────────────────────────────────────────────────────┤│
│ │ Page Data (compressed or raw)                                   ││
│ │  [Page1 data][Page2 data][Page3 data]...                        ││
│ └─────────────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────────────┤
│ Transaction 2                                                       │
│ ┌─────────────────────────────────────────────────────────────────┐│
│ │ TransactionHeader                                               ││
│ │ ┌───────────────────────────────────────────────────────────────┤│
│ │ │ Page Data                                                     ││
│ └─────────────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────────────┤
│ ...more transactions...                                             │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.3 Transaction Header

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TransactionHeader
{
    public const int SizeOf = 96;
    public const ulong HeaderMarker = 0x1A4C92AD90ABC123;

    [FieldOffset(0)]  public ulong HeaderMarker;
    [FieldOffset(8)]  public long TransactionId;
    [FieldOffset(16)] public long NextPageNumber;
    [FieldOffset(24)] public long LastPageNumber;
    [FieldOffset(32)] public int PageCount;
    [FieldOffset(36)] public uint Hash;              // XXHash32
    [FieldOffset(40)] public TreeRootHeader Root;    // 40 bytes
    [FieldOffset(80)] public TransactionMarker TxMarker;
    [FieldOffset(84)] public CompressedPagesInfo Compressed;
    // Padding to 96 bytes
}

public enum TransactionMarker : byte
{
    None = 0,
    Commit = 1,      // Normal commit
    Lazy = 2,        // Lazy commit (no fsync)
}
```

---

## 3. WriteAheadJournal Class

### 3.1 Structure (Impl/Journal/WriteAheadJournal.cs)

```csharp
public sealed class WriteAheadJournal : IDisposable
{
    private readonly StorageEnvironment _env;
    private readonly AbstractPager _dataPager;

    // Journal file management
    private ImmutableAppendOnlyList<JournalFile> _files;
    internal JournalFile CurrentFile;
    private long _journalIndex = -1;

    // Write coordination
    private readonly object _writeLock = new object();

    // Background flushing
    private readonly JournalApplicator _journalApplicator;

    // Compression
    private AbstractPager _compressionPager;
    private readonly DiffPages _diffPage;
}
```

### 3.2 Key Operations

```csharp
// Write transaction to journal
public void WriteToJournal(
    LowLevelTransaction tx,
    CompressedTransactionBuffer compressed)
{
    lock (_writeLock)
    {
        EnsureCurrentFile(compressed.Size4Kbs);

        CurrentFile.Write(tx, compressed);

        if (tx.Flags == TransactionFlags.ReadWrite)
        {
            CurrentFile.Sync();  // fsync for durability
        }
    }
}

// Create new journal file
private JournalFile NextFile(int numberOf4Kbs)
{
    _journalIndex++;
    var journalPager = _env.Options.CreateJournalWriter(_journalIndex, size);
    var journal = new JournalFile(_env, journalPager, _journalIndex);
    _files = _files.Append(journal);
    return journal;
}
```

---

## 4. Compression

Voron compresses journal entries for space efficiency.

### 4.1 Diff-Based Compression

```csharp
// DiffPages class
public class DiffPages
{
    public void ComputeDiff(
        byte* originalPage,
        byte* modifiedPage,
        int pageSize,
        out byte* diffBuffer,
        out int diffSize)
    {
        // XOR the pages, then compress the result
        // Many pages have small changes → high compression
    }
}
```

### 4.2 LZ4 Compression

```csharp
// In WriteAheadJournal
private void CompressPages(
    LowLevelTransaction tx,
    out CompressedTransactionBuffer result)
{
    // 1. Compute diffs for each dirty page
    foreach (var pageNumber in tx.DirtyPages)
    {
        var original = GetOriginalPage(pageNumber);
        var modified = tx.GetModifiedPage(pageNumber);
        _diffPage.ComputeDiff(original, modified, ...);
    }

    // 2. LZ4 compress the diffs
    var compressed = LZ4.Compress(diffBuffer);

    result = new CompressedTransactionBuffer(compressed);
}
```

---

## 5. JournalApplicator

Background process that applies journal entries to the data file.

### 5.1 Structure (Impl/Journal/JournalApplicator.cs)

```csharp
public sealed class JournalApplicator : IDisposable
{
    private readonly WriteAheadJournal _journal;
    private long _lastSyncedTransactionId;
    private long _lastSyncedJournal;

    // Apply journals to data file
    public void ApplyLogsToDataFile(CancellationToken token)
    {
        // 1. Find transactions to apply
        var transactionsToApply = GetPendingTransactions();

        // 2. For each transaction, copy pages to data file
        foreach (var tx in transactionsToApply)
        {
            foreach (var page in tx.Pages)
            {
                CopyPageToDataFile(page);
            }
        }

        // 3. fsync data file
        _dataPager.Sync();

        // 4. Update last synced position
        _lastSyncedTransactionId = transactionsToApply.Last().TransactionId;

        // 5. Delete old journals
        DeleteOldJournals();
    }
}
```

### 5.2 Flush Trigger Conditions

```csharp
// Flush conditions:
// 1. Journal file size exceeds threshold
// 2. Number of unflushed transactions exceeds limit
// 3. Explicit flush request
// 4. Shutdown

public bool ShouldFlush()
{
    return _totalJournalSize > Options.MaxScratchBufferSize ||
           _unflushedTransactions > Options.MaxTransactionsBeforeFlush;
}
```

---

## 6. Recovery

### 6.1 Recovery Process (on database open)

```csharp
// In WriteAheadJournal.RecoverDatabase()
public bool RecoverDatabase(TransactionHeader* txHeader)
{
    // 1. Read file header to find last synced position
    var logInfo = _headerAccessor.Get(ptr => ptr->Journal);

    // 2. Replay journals from last synced position
    for (var journalNum = logInfo.LastSyncedJournal;
         journalNum <= logInfo.CurrentJournal;
         journalNum++)
    {
        using (var pager = OpenJournalPager(journalNum))
        using (var reader = new JournalReader(pager, ...))
        {
            // Validate and apply each transaction
            var transactions = reader.RecoverAndValidate();

            foreach (var tx in transactions)
            {
                if (tx.TransactionId > logInfo.LastSyncedTransactionId)
                {
                    // Apply this transaction's pages to data file
                    ApplyTransaction(tx);
                }
            }
        }
    }

    // 3. Sync data file
    _dataPager.Sync();

    // 4. Update header
    return true;
}
```

### 6.2 JournalReader

```csharp
public sealed class JournalReader : IDisposable
{
    public List<TransactionHeader> RecoverAndValidate(StorageEnvironmentOptions options)
    {
        var transactions = new List<TransactionHeader>();

        while (ReadNextTransaction(out var header, out var pages))
        {
            // Validate hash
            var computedHash = XXHash32.Calculate(pages);
            if (computedHash != header.Hash)
            {
                // Corruption detected - stop here
                break;
            }

            // Decompress pages
            DecompressPages(pages);

            // Apply to data file
            foreach (var page in pages)
            {
                _dataPager.Write(page.PageNumber, page.Data);
            }

            transactions.Add(header);
        }

        return transactions;
    }
}
```

---

## 7. File Header Management

### 7.1 Database Header (Impl/FileHeaders/FileHeader.cs)

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct FileHeader
{
    [FieldOffset(0)]  public ulong MagicMarker;        // 0xB16BAADC0DEF0015
    [FieldOffset(8)]  public int Version;              // Schema version (23)
    [FieldOffset(12)] public int PageSize;             // 8192

    [FieldOffset(16)] public TransactionHeader LastTx; // Last committed tx
    [FieldOffset(112)] public JournalInfo Journal;     // Journal state
    [FieldOffset(144)] public IncrementalBackupInfo Backup;

    [FieldOffset(200)] public TreeRootHeader FreeSpaceRoot;
    [FieldOffset(240)] public TreeRootHeader Root;     // Main root tree
}
```

### 7.2 Journal Info

```csharp
[StructLayout(LayoutKind.Explicit)]
public struct JournalInfo
{
    [FieldOffset(0)]  public long CurrentJournal;          // Current journal number
    [FieldOffset(8)]  public long LastSyncedJournal;       // Last fully synced
    [FieldOffset(16)] public long LastSyncedTransactionId; // Last synced tx
    [FieldOffset(24)] public JournalInfoFlags Flags;
}
```

### 7.3 Header Redundancy

Two headers are maintained for crash safety:

```csharp
// In HeaderAccessor
public void Modify(Action<FileHeader*> modifier)
{
    // Alternate between two header locations
    var nextHeader = (_currentHeader + 1) % 2;

    // Copy current header to next location
    CopyHeader(_currentHeader, nextHeader);

    // Modify in next location
    modifier(GetHeader(nextHeader));

    // fsync
    Sync();

    // Switch to new header
    _currentHeader = nextHeader;
}
```

---

## 8. Durability Guarantees

### 8.1 Commit Durability

```csharp
// In LowLevelTransaction.Commit()
public void Commit()
{
    // 1. Write to journal
    _journal.WriteToJournal(this, compressedPages);

    // 2. fsync journal (makes transaction durable)
    CurrentJournalFile.Sync();

    // After this point, transaction is guaranteed durable
    // Even if process crashes, recovery will replay from journal

    // 3. Background: eventually apply to data file
    // (Handled by JournalApplicator)
}
```

### 8.2 Recovery Guarantee

After crash:
1. Open database
2. Read last synced position from header
3. Replay journals from that point
4. All committed transactions are recovered
5. Uncommitted transactions are discarded

---

## 9. Performance Considerations

### 9.1 Journal Size

```csharp
// Default options
InitialLogFileSize = 64 * 1024 * 1024;  // 64MB initial
MaxLogFileSize = 256 * 1024 * 1024;     // 256MB max per file
```

### 9.2 Lazy Commits

For non-critical transactions:

```csharp
// Skip fsync, accept potential loss on crash
tx.Commit(commitMode: CommitMode.Lazy);
```

### 9.3 Group Commit

Multiple transactions waiting can be committed together:

```csharp
// If multiple transactions are pending,
// they can share one fsync
```

---

## 10. VAYRON Relevance

### 10.1 Durability for Persistent Objects

VAYRON objects stored in Voron inherit its durability:

```
Object Modification
        │
        ▼
Handle marks body as dirty
        │
        ▼
Transaction.Commit()
        │
        ▼
Journal write + fsync ← Object change is now durable
        │
        ▼
Object visible to new readers
```

### 10.2 Recovery Semantics

After crash, VAYRON handles would:
1. Find their OID in recovered Voron storage
2. Rematerialize from recovered state
3. Epoch/version updated to post-recovery transaction

### 10.3 Transaction Boundaries

VAYRON operations would need clear transaction boundaries:

```csharp
using (var tx = vayron.WriteTransaction())
{
    handle1.Modify(data);  // Goes to journal
    handle2.Modify(data);  // Goes to journal
    tx.Commit();           // All-or-nothing durability
}
```

See [08-Integration-Analysis](./08-Integration-Analysis.md) for integration strategies.
