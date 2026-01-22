# Voron Page Architecture

> Engineering analysis of Voron's page structures, node layouts, and memory representations.

---

## 1. Page Fundamentals

### 1.1 Page as Unit of I/O

In Voron, the **page** (8KB) is the fundamental unit of:
- Storage allocation
- Memory mapping
- Transaction tracking
- Checksum validation

```csharp
// From Constants.cs
public const int PageSize = 8 * 1024;  // 8192 bytes
```

### 1.2 Page Wrapper (Page.cs)

```csharp
public readonly struct Page
{
    public readonly byte* Pointer;

    public Page(byte* pointer)
    {
        Pointer = pointer;
    }

    public long PageNumber
    {
        get => ((PageHeader*)Pointer)->PageNumber;
        set => ((PageHeader*)Pointer)->PageNumber = value;
    }

    public PageFlags Flags
    {
        get => ((PageHeader*)Pointer)->Flags;
        set => ((PageHeader*)Pointer)->Flags = value;
    }

    public bool IsValid => Pointer != null;
}
```

---

## 2. Page Header Structure

### 2.1 Common Header (PageHeader.cs)

Every page begins with:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct PageHeader
{
    [FieldOffset(0)]
    public long PageNumber;     // 8 bytes - unique identifier

    [FieldOffset(8)]
    public PageFlags Flags;     // 2 bytes - page type

    [FieldOffset(10)]
    public ushort Reserved;     // 2 bytes - alignment

    [FieldOffset(12)]
    public uint Checksum;       // 4 bytes - XXHash32
}
```

### 2.2 Page Flags

```csharp
[Flags]
public enum PageFlags : ushort
{
    Single = 0,                     // 0x0000 - Regular single page
    Overflow = 1,                   // 0x0001 - Overflow sequence
    RawData = 2,                    // 0x0002 - Raw data section
    Container = 4,                  // 0x0004 - Container/blob storage
    VariableSizeTreePage = 8,       // 0x0008 - B+Tree page
    Stream = 16,                    // 0x0010 - Stream data
    FixedSizeTreePage = 32,         // 0x0020 - Fixed-size tree
    ReservedValue1 = 64,            // 0x0040 - Reserved
    Compressed = 128,               // 0x0080 - Page compressed
}
```

---

## 3. Tree Page Structure

### 3.1 Tree Page Header

```csharp
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct TreePageHeader
{
    [FieldOffset(0)]
    public long PageNumber;

    [FieldOffset(8)]
    public TreePageFlags TreeFlags;  // Leaf/Branch/Overflow

    [FieldOffset(10)]
    public ushort Lower;             // End of offset array (grows down)

    [FieldOffset(12)]
    public ushort Upper;             // Start of node data (grows up)

    [FieldOffset(14)]
    public ushort Reserved;

    [FieldOffset(16)]
    public int OverflowSize;         // For overflow: total data size
}
```

### 3.2 Page Space Management

```
Page Memory Layout (8192 bytes)
┌────────────────────────────────────────────────────────────────────────┐
│ Header (24 bytes)                                                      │
│  Lower ──────────────────────┐                                         │
├───────────────────────────────┼────────────────────────────────────────┤
│ Offset Array                  │ (grows toward increasing addresses)    │
│ [off0][off1][off2]...         ▼                                        │
│                               │                                        │
│ ◄─────── Lower points here ───┘                                        │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│              <<< Usable Free Space = Upper - Lower >>>                 │
│                                                                        │
├────────────────────────────────────────────────────────────────────────┤
│                               ▲                                        │
│ ◄─────── Upper points here ───┘                                        │
│                                                                        │
│ Node Data (grows toward decreasing addresses)                          │
│ [Node N][Node N-1][Node N-2]...                                        │
└────────────────────────────────────────────────────────────────────────┘

Usable space: PageSize - HeaderSize - (NumberOfEntries * sizeof(ushort))
```

### 3.3 TreePage Class (Data/BTrees/TreePage.cs)

```csharp
public unsafe class TreePage
{
    public readonly int PageSize;
    public byte* Base;

    // Search state
    public int LastMatch;
    public int LastSearchPosition;
    public bool Dirty;

    // Properties mapping to header
    public long PageNumber { get => Header->PageNumber; set => ... }
    public TreePageFlags TreeFlags { get => Header->TreeFlags; set => ... }
    public ushort Lower { get => Header->Lower; set => ... }
    public ushort Upper { get => Header->Upper; set => ... }

    // Computed properties
    public int NumberOfEntries => (Lower - Constants.Tree.PageHeaderSize) / 2;
    public bool IsBranch => (TreeFlags & TreePageFlags.Branch) != 0;
    public bool IsLeaf => (TreeFlags & TreePageFlags.Leaf) != 0;
    public bool IsOverflow => (TreeFlags & TreePageFlags.Overflow) != 0;

    // Key offsets array
    public ushort* KeysOffsets => (ushort*)(Base + Constants.Tree.PageHeaderSize);

    // Get node at position
    public TreeNodeHeader* GetNode(int index) => (TreeNodeHeader*)(Base + KeysOffsets[index]);
}
```

---

## 4. Node Structure

### 4.1 Node Header

```csharp
[StructLayout(LayoutKind.Explicit, Size = 6)]
public struct TreeNodeHeader
{
    [FieldOffset(0)]
    public ushort DataSize;      // Size of value (or overflow page count)

    [FieldOffset(2)]
    public ushort KeySize;       // Size of key

    [FieldOffset(4)]
    public TreeNodeFlags Flags;  // Node type flags
}

// Node memory layout:
// [TreeNodeHeader: 6 bytes][Key: KeySize bytes][Value: DataSize bytes]
// Total size must be even (aligned)
```

### 4.2 Node Flags

```csharp
[Flags]
public enum TreeNodeFlags : ushort
{
    None = 0,
    Data = 1,              // Value is inline after key
    PageRef = 2,           // Value is page number (8 bytes) - overflow
    MultiValuePageRef = 4, // Multi-value tree reference
    Duplicate = 8,         // Duplicate key handling
}
```

### 4.3 Node Layout Examples

**Inline Value Node (Leaf)**:
```
[NodeHeader:6][Key:N][Value:M]
└── Flags = Data
└── DataSize = M
└── KeySize = N
```

**Overflow Reference Node (Leaf)**:
```
[NodeHeader:6][Key:N][PageNumber:8]
└── Flags = PageRef
└── DataSize = number of overflow pages
└── KeySize = N
└── PageNumber = first overflow page
```

**Branch Node**:
```
[NodeHeader:6][Key:N][ChildPageNumber:8]
└── Flags = PageRef
└── KeySize = N
└── ChildPageNumber = pointer to child page
```

---

## 5. Page Operations

### 5.1 Binary Search

```csharp
// In TreePage.cs
public TreeNodeHeader* Search(LowLevelTransaction tx, Slice key)
{
    int numberOfEntries = NumberOfEntries;
    if (numberOfEntries == 0)
    {
        LastSearchPosition = 0;
        LastMatch = 1;
        return null;
    }

    int low = IsLeaf ? 0 : 1;  // Branch: first entry is leftmost pointer
    int high = numberOfEntries - 1;
    int position = 0;
    int lastMatch = -1;

    ushort* offsets = KeysOffsets;
    byte* @base = Base;

    while (low <= high)
    {
        position = (low + high) >> 1;
        var node = (TreeNodeHeader*)(@base + offsets[position]);

        var pageKey = TreeNodeHeader.GetKeyPtr(node, out var pageKeyLength);
        lastMatch = Memory.Compare(key.Content.Ptr, pageKey,
            Math.Min(key.Size, pageKeyLength));

        if (lastMatch == 0)
            lastMatch = key.Size - pageKeyLength;

        if (lastMatch == 0)
            break;

        if (lastMatch > 0)
            low = position + 1;
        else
            high = position - 1;
    }

    LastMatch = lastMatch;
    LastSearchPosition = position;
    return (TreeNodeHeader*)(@base + offsets[position]);
}
```

### 5.2 Node Allocation

```csharp
// Allocate space for new node in page
public TreeNodeHeader* AllocateNewNode(int keySize, int dataSize)
{
    int nodeSize = Constants.Tree.NodeHeaderSize + keySize + dataSize;
    nodeSize += nodeSize & 1;  // Ensure even alignment

    // Check if enough space
    int availableSpace = Upper - Lower;
    int requiredSpace = nodeSize + sizeof(ushort);  // Node + offset

    if (requiredSpace > availableSpace)
        return null;  // Need page split

    // Allocate node at Upper
    Upper -= (ushort)nodeSize;
    var node = (TreeNodeHeader*)(Base + Upper);

    // Add offset entry
    KeysOffsets[NumberOfEntries] = Upper;
    Lower += sizeof(ushort);

    return node;
}
```

### 5.3 Page Split

When a page is full:

```csharp
public (TreePage Left, TreePage Right, Slice SplitKey) Split(LowLevelTransaction tx)
{
    // Allocate new page
    var newPage = tx.AllocatePage(1);

    // Find split point (middle)
    int splitIndex = NumberOfEntries / 2;
    var splitKey = GetNodeKey(splitIndex);

    // Move right half to new page
    for (int i = splitIndex; i < NumberOfEntries; i++)
    {
        var node = GetNode(i);
        CopyNodeTo(node, newPage);
    }

    // Truncate left page
    NumberOfEntries = splitIndex;

    return (this, newPage, splitKey);
}
```

---

## 6. Specialized Page Types

### 6.1 Fixed-Size Tree Page

```csharp
// Header for fixed-size tree pages
[StructLayout(LayoutKind.Explicit)]
public struct FixedSizeTreePageHeader
{
    [FieldOffset(0)]
    public long PageNumber;

    [FieldOffset(8)]
    public FixedSizeTreePageFlags Flags;

    [FieldOffset(10)]
    public ushort NumberOfEntries;

    [FieldOffset(12)]
    public ushort ValueSize;        // Size of each value

    [FieldOffset(14)]
    public ushort StartPosition;    // For embedded trees
}

// Entry layout: [Key:8][Value:ValueSize] - no per-entry header
// Very dense packing
```

### 6.2 Container Page

```csharp
// Header for container pages
public struct ContainerPageHeader
{
    public long PageNumber;
    public PageFlags Flags;          // = Container
    public ushort NumberOfEntries;
    public ushort Lower;             // End of offset array
    public ushort Upper;             // Start of entries
    public long NextContainerPage;   // Link to next page
}

// Entries: [Size:4][Data:variable]
```

### 6.3 Raw Data Section Page

```csharp
// For table row storage
public struct RawDataSectionPageHeader
{
    public long PageNumber;
    public PageFlags Flags;          // = RawData
    public long SectionOwner;        // Table that owns this
    public int NumberOfPages;        // Pages in this section
    public short AllocatedSize;      // Fixed allocation size
    public short NumberOfEntries;
    public int UsedBytes;
}

// Followed by allocation bitmap and entries
```

---

## 7. Overflow Page Handling

### 7.1 When Overflow Occurs

Value size threshold:
```csharp
// If value won't fit in page, use overflow
int maxInlineSize = (PageSize - PageHeaderSize) / 2 - NodeHeaderSize - KeySize;
// Approximately 4000 bytes for typical key
```

### 7.2 Overflow Page Chain

```
Leaf Page (contains overflow reference)
┌────────────────────────────────────────┐
│ [NodeHeader][Key][PageRef=100][Count=3]│
└────────────────────┬───────────────────┘
                     │
     ┌───────────────▼───────────────┐
     │ Page 100 (Overflow, first)    │
     │ [PageHeader][Data 0-8167]     │
     └───────────────┬───────────────┘
                     │ (implicit continuation)
     ┌───────────────▼───────────────┐
     │ Page 101 (Overflow)           │
     │ [PageHeader][Data 8168-16335] │
     └───────────────┬───────────────┘
                     │
     ┌───────────────▼───────────────┐
     │ Page 102 (Overflow, last)     │
     │ [PageHeader][Data 16336-end]  │
     └───────────────────────────────┘
```

---

## 8. Page Compression

### 8.1 Compressed Page Layout

```
Compressed Page
┌─────────────────────────────────────────────────────────────────────┐
│ PageHeader (16 bytes)                                               │
│  └── Flags |= Compressed                                            │
├─────────────────────────────────────────────────────────────────────┤
│ CompressionHeader                                                   │
│  ├── UncompressedSize (4 bytes)                                     │
│  ├── CompressedSize (4 bytes)                                       │
│  └── DictionaryId (4 bytes)                                         │
├─────────────────────────────────────────────────────────────────────┤
│ Compressed Data (LZ4 or Zstd)                                       │
│  [compressed page content...]                                       │
└─────────────────────────────────────────────────────────────────────┘
```

### 8.2 Decompression on Access

```csharp
// In LowLevelTransaction
public byte* AcquirePagePointer(long pageNumber)
{
    var ptr = GetRawPagePointer(pageNumber);
    var header = (PageHeader*)ptr;

    if ((header->Flags & PageFlags.Compressed) != 0)
    {
        // Decompress to temporary buffer
        var decompressed = DecompressPage(ptr);
        _decompressedPages[pageNumber] = decompressed;
        return decompressed;
    }

    return ptr;
}
```

---

## 9. VAYRON Implications

### 9.1 Object Body as Page Content

VAYRON object bodies could be stored as:

1. **Small objects**: Inline in Container pages
2. **Medium objects**: Dedicated pages
3. **Large objects**: Overflow page chains

### 9.2 Page-Level Caching

VayronHandle's `CachedBodyPtr` would point into:
- Container page (inline)
- First page of body (dedicated)
- Memory buffer (if decompressed)

### 9.3 Modification Granularity

COW operates at page level:
- Modifying any byte in a page copies the entire page
- For large objects spanning multiple pages, only modified pages are copied
- This aligns well with field-level modification patterns

### 9.4 Prefetching Considerations

For hot path optimization:
```csharp
// If we know related objects, prefetch their pages
void PrefetchRelated(VayronHandle handle)
{
    var relatedPageNumbers = GetRelatedPages(handle.OID);
    foreach (var pageNum in relatedPageNumbers)
    {
        _pager.Prefetch(pageNum);
    }
}
```
