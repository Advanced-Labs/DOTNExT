# Voron Memory Management

> Engineering analysis of Voron's memory-mapped I/O, pager abstraction, and address space management.

---

## 1. Overview

Voron's memory management is built on a fundamental principle: **the database IS the address space**. Through memory mapping, the entire data file becomes directly addressable memory. This eliminates traditional buffer management and enables efficient read access.

---

## 2. Core Abstractions

### 2.1 The Pager Hierarchy

```
                    AbstractPager
                         │
          ┌──────────────┼──────────────┐
          │              │              │
    RvnMemoryMapPager  WindowsMemoryMapPager  CryptoPager
    (Linux/macOS)       (Windows)              (Wrapper)
          │              │
          │         Windows32BitsMemoryMapPager
          │         (32-bit address space)
          │
    Posix32BitsMemoryMapPager
    (32-bit POSIX)
```

### 2.2 AbstractPager (Impl/Paging/AbstractPager.cs)

The base class defines the memory mapping contract:

```csharp
public abstract unsafe class AbstractPager : IDisposable
{
    // Core constants
    public const int PageMaxSpace = Constants.Storage.PageSize - Constants.Tree.PageHeaderSize;
    public int NodeMaxSize;      // PageMaxSpace / 2 - 1
    public int PageMinSpace;     // (int)(PageMaxSpace * 0.33)

    // State
    private PagerState _pagerState;
    public long NumberOfAllocatedPages { get; protected set; }

    // Key operations
    public abstract byte* AcquirePagePointer(
        IPagerLevelTransactionState tx,
        long pageNumber,
        PagerState pagerState = null);

    public abstract byte* AcquirePagePointerWithOverflowHandling(
        IPagerLevelTransactionState tx,
        long pageNumber,
        PagerState pagerState = null);

    public abstract void AllocateMorePages(long newLength);
    protected abstract void DisposeInternal();
}
```

### 2.3 PagerState

Represents a snapshot of the memory mapping state:

```csharp
public sealed unsafe class PagerState
{
    // Memory mappings for this state
    public AllocationInfo[] AllocationInfos;

    // Reference counting for MVCC
    private int _refs;
    public void AddRef() => Interlocked.Increment(ref _refs);
    public void Release() { ... }

    // Prefetching support
    public bool ShouldPrefetchSegment(long pageNumber, out long segmentNumber);
}

public struct AllocationInfo
{
    public byte* BaseAddress;    // Start of mapping
    public long Size;            // Size of mapping
}
```

---

## 3. Memory Mapping Implementation

### 3.1 Linux/macOS (RvnMemoryMapPager)

Uses POSIX `mmap()` via the PAL (Platform Abstraction Layer):

```csharp
// From Impl/Paging/RvnMemoryMapPager.cs
protected override byte* AcquirePagePointerInternal(
    IPagerLevelTransactionState tx,
    long pageNumber,
    PagerState pagerState)
{
    // Direct pointer into mapped region
    var state = pagerState ?? _pagerState;
    return state.MapBase + (pageNumber * Constants.Storage.PageSize);
}
```

Mapping creation:
```csharp
// Simplified from actual implementation
var mmapResult = Pal.rvn_mmap_file(
    fileHandle,
    offset: 0,
    size,
    flags: MmapFlags.Shared,  // Shared mapping
    out baseAddress);
```

### 3.2 Windows (WindowsMemoryMapPager)

Uses `CreateFileMapping` + `MapViewOfFile`:

```csharp
// Key Windows API calls (via P/Invoke)
// 1. Create file mapping object
var fileMapping = CreateFileMapping(
    fileHandle,
    securityAttributes: null,
    PAGE_READWRITE,
    sizeHigh,
    sizeLow,
    name: null);

// 2. Map view into process address space
var baseAddress = MapViewOfFile(
    fileMapping,
    FILE_MAP_ALL_ACCESS,
    offsetHigh: 0,
    offsetLow: 0,
    numberOfBytesToMap: size);
```

### 3.3 32-Bit Address Space Management

On 32-bit systems, the entire file can't be mapped at once. Voron uses a windowed approach:

```csharp
// From Windows32BitsMemoryMapPager.cs / Posix32BitsMemoryMapPager.cs
public class TransactionState
{
    // Maps page ranges to their current mappings
    public Dictionary<long, LoadedPage> LoadedPages;

    // Mapping windows (multiple smaller mappings)
    public List<MappedView> ActiveViews;
}

// Pages are mapped on-demand in chunks
protected override byte* AcquirePagePointerInternal(...)
{
    if (!transactionState.LoadedPages.TryGetValue(pageNumber, out var page))
    {
        // Map the page range containing this page
        page = MapPageRange(pageNumber);
        transactionState.LoadedPages[pageNumber] = page;
    }
    return page.Pointer;
}
```

---

## 4. Copy-on-Write with Scratch Buffers

Voron doesn't modify the data file directly. Instead, modifications go to scratch buffers.

### 4.1 Scratch Buffer Pool (Impl/Scratch/ScratchBufferPool.cs)

```csharp
public sealed class ScratchBufferPool
{
    // Current active scratch file
    internal ScratchBufferItem _current;

    // All scratch files
    private readonly ConcurrentDictionary<int, ScratchBufferItem> _scratchBuffers;

    // Recycle bin for old scratches
    private readonly LinkedList<ScratchBufferItem> _recycleArea;

    // Allocate a page in scratch
    public PageFromScratchBuffer Allocate(
        LowLevelTransaction tx,
        int numberOfPages)
    {
        // Find space in current scratch or create new one
        var current = _current;
        var result = current.File.Allocate(tx, numberOfPages);
        if (result != null)
            return result;

        // Need new scratch file
        _current = NextFile(...);
        return _current.File.Allocate(tx, numberOfPages);
    }
}
```

### 4.2 Scratch Buffer File (Impl/Scratch/ScratchBufferFile.cs)

```csharp
public sealed class ScratchBufferFile
{
    private readonly AbstractPager _scratchPager;  // Memory-mapped scratch file

    // Tracking allocations
    private readonly Dictionary<long, PagePosition> _allocatedPages;

    // Free after transaction completes
    private readonly Dictionary<long, List<long>> _freePagesByTransaction;

    public PageFromScratchBuffer Allocate(LowLevelTransaction tx, int numberOfPages)
    {
        // Find or allocate contiguous space
        long position = FindFreeSpace(numberOfPages);
        if (position == -1)
        {
            // Grow scratch file
            _scratchPager.AllocateMorePages(...);
            position = _lastUsedPosition;
        }

        return new PageFromScratchBuffer(
            _scratchNumber,
            position,
            numberOfPages);
    }
}
```

### 4.3 Page Resolution Flow

When reading a page during a transaction:

```
Page Requested
     │
     ▼
┌────────────────────────────────┐
│ Check Scratch Buffer Table     │◄── Current transaction's modified pages
│ (LowLevelTransaction.          │
│  _scratchPagesTable)           │
└────────────────┬───────────────┘
                 │ Not found
                 ▼
┌────────────────────────────────┐
│ Check Journal Snapshots        │◄── Recent transactions not yet flushed
│ (for read transactions)        │
└────────────────┬───────────────┘
                 │ Not found
                 ▼
┌────────────────────────────────┐
│ Read from Data Pager           │◄── Memory-mapped data file
│ (AbstractPager)                │
└────────────────────────────────┘
```

---

## 5. Page Lifecycle

### 5.1 Page Allocation

```csharp
// In LowLevelTransaction
public Page AllocatePage(int numberOfPages)
{
    // 1. Try free space first
    var pageNumber = _freeSpaceHandling.TryAllocateFromFreeSpace(this, numberOfPages);

    if (pageNumber == null)
    {
        // 2. Allocate at end of file
        pageNumber = _state.NextPageNumber;
        _state = _state.WithNextPageNumber(pageNumber.Value + numberOfPages);
    }

    // 3. Allocate in scratch for modifications
    var scratchBuffer = _env.ScratchBufferPool.Allocate(this, numberOfPages);
    _scratchPagesTable[pageNumber.Value] = scratchBuffer;
    _dirtyPages.Add(pageNumber.Value);

    // 4. Return page wrapper
    return new Page(scratchBuffer.GetPointer());
}
```

### 5.2 Page Modification (COW)

```csharp
public Page ModifyPage(long pageNumber)
{
    // Already modified in this transaction?
    if (_scratchPagesTable.TryGetValue(pageNumber, out var existing))
    {
        return new Page(existing.GetPointer());
    }

    // Copy from source to scratch (COW)
    var scratchBuffer = _env.ScratchBufferPool.Allocate(this, 1);
    var sourcePointer = AcquirePagePointer(pageNumber);
    var destPointer = scratchBuffer.GetPointer();

    Memory.Copy(destPointer, sourcePointer, Constants.Storage.PageSize);

    _scratchPagesTable[pageNumber] = scratchBuffer;
    _dirtyPages.Add(pageNumber);

    return new Page(destPointer);
}
```

### 5.3 Page Freeing

```csharp
public void FreePage(long pageNumber)
{
    // Pages aren't immediately freed - deferred to avoid reader conflicts
    _pagesToFreeOnCommit.Push(pageNumber);

    // If we modified it this transaction, release scratch immediately
    if (_scratchPagesTable.TryGetValue(pageNumber, out var scratchPage))
    {
        _scratchPagesTable.Remove(pageNumber);
        _unusedScratchPages.Add(scratchPage);
    }

    _freedPages.Add(pageNumber);
}
```

---

## 6. Prefetching

Voron implements intelligent prefetching to reduce page fault latency:

```csharp
// In PagerState
public void Prefetch(long pageNumber)
{
    if (ShouldPrefetchSegment(pageNumber, out var segmentNumber))
    {
        // Prefetch entire segment (default 4MB)
        var offset = segmentNumber * _prefetchSegmentSize;
        var pointer = MapBase + offset;
        Pal.rvn_prefetch_virtual_memory(pointer, _prefetchSegmentSize);

        _prefetchedSegments.Set((int)segmentNumber);
    }
}
```

---

## 7. Memory Locking (Encryption)

When encryption is enabled, Voron locks pages in memory to prevent secrets from being paged to disk:

```csharp
// In AbstractPager
protected void Lock(byte* address, long sizeToLock, TransactionState state)
{
    if (Sodium.Lock(address, (UIntPtr)sizeToLock) == 0)
        return;  // Success

    // Handle failure - may need to increase working set
    TryHandleFailureToLockMemory(address, sizeToLock);
}
```

---

## 8. VAYRON Integration Considerations

### 8.1 Key Interfaces for Runtime Integration

1. **AbstractPager**: Could be extended/wrapped for runtime-aware allocation
2. **PagerState**: Reference counting model compatible with handle lifetime
3. **AcquirePagePointer**: Core operation for "fault-in" behavior

### 8.2 Potential Hook Points

```csharp
// Hypothetical VAYRON extension
public interface IVayronPager : AbstractPager
{
    // Runtime can register for page access events
    event Action<long, byte*> OnPageMaterialized;

    // Runtime can hint about expected access patterns
    void HintPageAccess(long pageNumber, AccessPattern pattern);

    // Support for handle registration
    void RegisterHandle(long pageNumber, VayronHandle handle);
}
```

### 8.3 Memory Model Alignment

Voron's model aligns well with VAYRON goals:

| Voron Concept | VAYRON Analog |
|---------------|---------------|
| Page | Object body storage unit |
| PageNumber | Stable OID (with offset) |
| AcquirePagePointer | Fault-in / materialization |
| COW scratch | Handle updates while preserving versions |
| Transaction ID | Epoch/version for MVCC |

See [08-Integration-Analysis](./08-Integration-Analysis.md) for detailed integration strategies.
