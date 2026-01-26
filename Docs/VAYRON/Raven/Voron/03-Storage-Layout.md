# Voron Storage Layout

> Engineering analysis of Voron's on-disk format: file structure, page types, and header layouts.

---

## 1. File Organization

### 1.1 Database Files

```
Database Directory/
├── Raven.voron                  # Primary data file
│   └── All data pages (trees, containers, free space)
│
├── Journals/
│   ├── 0000000000000000001.journal
│   ├── 0000000000000000002.journal
│   └── ... (numbered sequentially)
│
└── Temp/
    └── scratch.{n}.buffers      # Temporary scratch files
```

### 1.2 Data File Structure

```
Raven.voron
┌─────────────────────────────────────────────────────────────────────┐
│ Header Page 0 (8KB) - Primary file header                           │
├─────────────────────────────────────────────────────────────────────┤
│ Header Page 1 (8KB) - Backup file header (redundancy)               │
├─────────────────────────────────────────────────────────────────────┤
│ Page 2: Root Objects Tree root                                      │
├─────────────────────────────────────────────────────────────────────┤
│ Page 3: Free Space Tree root                                        │
├─────────────────────────────────────────────────────────────────────┤
│ Pages 4+: User data (trees, tables, containers, etc.)               │
│  ├── Tree pages (branch and leaf)                                   │
│  ├── Container pages (blob storage)                                 │
│  ├── Fixed-size tree pages                                          │
│  ├── Overflow pages (large values)                                  │
│  └── Free pages (marked in free space tree)                         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. Page Size and Constants

### 2.1 Core Constants (Constants.cs)

```csharp
namespace Voron.Global
{
    public static class Constants
    {
        public static class Storage
        {
            public const int PageSize = 8 * 1024;  // 8KB
        }

        public static class Tree
        {
            public const int PageHeaderSize = 24;
            public const int MinKeysInPage = 2;
            public const int NodeHeaderSize = 6;
            public const int NodeOffsetSize = 2;  // ushort
        }

        public static class Size
        {
            public const int Kilobyte = 1024;
            public const int Megabyte = 1024 * Kilobyte;
            public const int Gigabyte = 1024 * Megabyte;
        }

        public static class Compression
        {
            public const int MaxPageSize = 64 * 1024;  // 64KB max compressed
        }
    }
}
```

### 2.2 Magic Numbers

```csharp
// File header magic
public const ulong MagicMarker = 0xB16BAADC0DEF0015;

// Transaction header magic
public const ulong TransactionHeaderMarker = 0x1A4C92AD90ABC123;

// Schema version
public const int CurrentVersion = 23;
```

---

## 3. File Header

### 3.1 Structure (Impl/FileHeaders/FileHeader.cs)

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct FileHeader
{
    public const int SizeOf = 256;  // Fixed size

    [FieldOffset(0)]
    public ulong MagicMarker;       // 0xB16BAADC0DEF0015

    [FieldOffset(8)]
    public int Version;             // Schema version (23)

    [FieldOffset(12)]
    public int PageSize;            // 8192

    [FieldOffset(16)]
    public TransactionHeader TransactionHeader;  // Last committed tx

    [FieldOffset(112)]
    public JournalInfo Journal;     // Journal state

    [FieldOffset(144)]
    public IncrementalBackupInfo IncrementalBackup;

    [FieldOffset(176)]
    public Guid DatabaseGuid;       // Unique DB identifier

    [FieldOffset(192)]
    public TreeRootHeader FreeSpaceRoot;  // Free space tree

    [FieldOffset(232)]
    public TreeRootHeader Root;     // Main root objects tree
}
```

### 3.2 Dual Header Redundancy

Two headers for crash safety:

```
Page 0: Header Copy A
Page 1: Header Copy B

On update:
1. Modify the non-current header
2. fsync
3. Switch current pointer

On recovery:
- Read both headers
- Use the one with higher valid transaction ID
```

---

## 4. Page Types and Headers

### 4.1 Common Page Header (PageHeader.cs)

Every page starts with:

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct PageHeader
{
    public const int SizeOf = 16;

    [FieldOffset(0)]
    public long PageNumber;         // Page number in file

    [FieldOffset(8)]
    public PageFlags Flags;         // Page type flags

    [FieldOffset(10)]
    public ushort Reserved;

    [FieldOffset(12)]
    public uint Checksum;           // XXHash32 (for validation)
}

[Flags]
public enum PageFlags : ushort
{
    Single = 0,                     // Regular page
    Overflow = 1,                   // Large value continues
    RawData = 2,                    // Raw data section
    Container = 4,                  // Container page
    VariableSizeTreePage = 8,       // B+Tree page
    Stream = 16,                    // Stream data
    FixedSizeTreePage = 32,         // Fixed-size tree
    ReservedValue1 = 64,
    Compressed = 128,               // Page is compressed
}
```

### 4.2 Tree Page Header (TreePageHeader)

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TreePageHeader
{
    public const int SizeOf = 24;

    // First 16 bytes: PageHeader
    [FieldOffset(0)]
    public long PageNumber;

    [FieldOffset(8)]
    public TreePageFlags TreeFlags;  // Leaf/Branch/Overflow

    [FieldOffset(10)]
    public ushort Lower;             // End of key offsets array

    [FieldOffset(12)]
    public ushort Upper;             // Start of node data

    [FieldOffset(14)]
    public ushort Reserved;

    [FieldOffset(16)]
    public int OverflowSize;         // Size for overflow pages
}

[Flags]
public enum TreePageFlags : ushort
{
    None = 0,
    Leaf = 1,                        // Leaf node
    Branch = 2,                      // Branch node
    Overflow = 4,                    // Overflow page
    Compressed = 8,                  // Compressed content
}
```

---

## 5. Tree Page Layout

### 5.1 Leaf Page

```
TreePage (Leaf)
┌─────────────────────────────────────────────────────────────────────┐
│ TreePageHeader (24 bytes)                                           │
│  ├── PageNumber (8)                                                 │
│  ├── TreeFlags = Leaf (2)                                           │
│  ├── Lower = 28 (end of offsets) (2)                                │
│  ├── Upper = 8100 (start of nodes) (2)                              │
│  └── OverflowSize (4)                                               │
├─────────────────────────────────────────────────────────────────────┤
│ Key Offsets (grows downward from header)                            │
│  [8100][8050][8000][7950] ← Each ushort points to a node           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│                     <<< Free Space >>>                              │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│ Nodes (grow upward from bottom)                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ @ offset 8100: [NodeHeader][Key][Value]                         ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ @ offset 8050: [NodeHeader][Key][Value]                         ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ @ offset 8000: [NodeHeader][Key][Value]                         ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

### 5.2 Branch Page

```
TreePage (Branch)
┌─────────────────────────────────────────────────────────────────────┐
│ TreePageHeader (24 bytes)                                           │
│  └── TreeFlags = Branch                                             │
├─────────────────────────────────────────────────────────────────────┤
│ Key Offsets [off0][off1][off2]...                                   │
├─────────────────────────────────────────────────────────────────────┤
│                     <<< Free Space >>>                              │
├─────────────────────────────────────────────────────────────────────┤
│ Nodes (each contains key + child page number)                       │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ [NodeHeader][Key][PageRef: 8 bytes]                             ││
│  │  └── PageRef points to child page                               ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

### 5.3 Node Header

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TreeNodeHeader
{
    public const int SizeOf = 6;

    [FieldOffset(0)]
    public ushort DataSize;         // Value size (or page count if overflow)

    [FieldOffset(2)]
    public ushort KeySize;          // Key size

    [FieldOffset(4)]
    public TreeNodeFlags Flags;     // Node type

    // Followed by: Key bytes, then Value bytes
}

[Flags]
public enum TreeNodeFlags : ushort
{
    None = 0,
    Data = 1,                       // Inline value data
    PageRef = 2,                    // Reference to overflow page
    MultiValuePageRef = 4,          // Reference to multi-value tree
    Duplicate = 8,                  // Duplicate key handling
}
```

---

## 6. Fixed-Size Tree Page Layout

Optimized for entries where all values have the same size.

```
FixedSizeTreePage
┌─────────────────────────────────────────────────────────────────────┐
│ PageHeader (16 bytes)                                               │
├─────────────────────────────────────────────────────────────────────┤
│ FixedSizeTreePageHeader                                             │
│  ├── NumberOfEntries (4 bytes)                                      │
│  ├── ValueSize (2 bytes)                                            │
│  ├── Flags (2 bytes) - Leaf/Branch                                  │
│  └── StartPosition (4 bytes)                                        │
├─────────────────────────────────────────────────────────────────────┤
│ Dense Array of Entries                                              │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Entry 0: [Key: 8 bytes][Value: N bytes]                      │  │
│  │ Entry 1: [Key: 8 bytes][Value: N bytes]                      │  │
│  │ Entry 2: [Key: 8 bytes][Value: N bytes]                      │  │
│  │ ...                                                          │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 7. Container Page Layout

For blob storage (documents, attachments).

```
ContainerPage
┌─────────────────────────────────────────────────────────────────────┐
│ PageHeader + ContainerHeader                                        │
│  ├── PageNumber                                                     │
│  ├── Flags = Container                                              │
│  ├── NumberOfEntries                                                │
│  ├── NextContainerPage                                              │
│  └── UsedBytes                                                      │
├─────────────────────────────────────────────────────────────────────┤
│ Entry Offsets [off0][off1][off2]...                                 │
├─────────────────────────────────────────────────────────────────────┤
│                     <<< Free Space >>>                              │
├─────────────────────────────────────────────────────────────────────┤
│ Entries (variable size blobs)                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Entry 0: [Size: 4 bytes][Data: variable]                     │  │
│  │ Entry 1: [Size: 4 bytes][Data: variable]                     │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 8. Overflow Pages

For values larger than ~4KB (half a page).

```
Overflow Sequence
┌───────────────────────────┐
│ Leaf Node (references     │
│  overflow via PageRef)    │
│  └── PageNumber: 42       │
└───────────────┬───────────┘
                │
                ▼
┌───────────────────────────┐     ┌───────────────────────────┐
│ Page 42: First Overflow   │ ──► │ Page 43: Continuation     │
│  ├── Flags = Overflow     │     │  ├── Flags = Overflow     │
│  ├── OverflowSize = 20KB  │     │  └── Data continues...    │
│  └── Data (first 8KB)     │     └───────────────────────────┘
└───────────────────────────┘

Total: OverflowSize / PageSize pages (rounded up)
```

---

## 9. Raw Data Section

Used by Tables for row storage.

```
RawDataSection (small values)
┌─────────────────────────────────────────────────────────────────────┐
│ Section Header                                                      │
│  ├── PageNumber                                                     │
│  ├── SectionOwner (table identifier)                                │
│  ├── NumberOfPages                                                  │
│  ├── AllocatedSize                                                  │
│  └── NextSection                                                    │
├─────────────────────────────────────────────────────────────────────┤
│ Allocation Bitmap                                                   │
│  [bit0][bit1][bit2]... - tracks allocated entries                   │
├─────────────────────────────────────────────────────────────────────┤
│ Entries                                                             │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Entry 0: [AllocSize:2][Size:2][Data:variable]               │  │
│  │ Entry 1: [AllocSize:2][Size:2][Data:variable]               │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 10. Journal File Format

```
Journal File
┌─────────────────────────────────────────────────────────────────────┐
│ Transaction 1                                                       │
│  ├── TransactionHeader (96 bytes)                                   │
│  │    ├── HeaderMarker = 0x1A4C92AD90ABC123                        │
│  │    ├── TransactionId                                             │
│  │    ├── NextPageNumber                                            │
│  │    ├── PageCount                                                 │
│  │    ├── Hash (XXHash32)                                           │
│  │    ├── Root tree state                                           │
│  │    └── Compression info                                          │
│  └── Page Data (compressed or raw)                                  │
│       ├── [PageNumber][PageData]                                    │
│       ├── [PageNumber][PageData]                                    │
│       └── ...                                                       │
├─────────────────────────────────────────────────────────────────────┤
│ Transaction 2                                                       │
│  ├── TransactionHeader                                              │
│  └── Page Data                                                      │
├─────────────────────────────────────────────────────────────────────┤
│ ... more transactions ...                                           │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 11. Checksum and Validation

### 11.1 Page Checksum

```csharp
// Checksum stored in PageHeader
public uint Checksum;

// Calculated using XXHash32
public static uint CalculateChecksum(byte* page, int size)
{
    // Skip the checksum field itself
    return XXHash32.Calculate(page + ChecksumOffset, size - ChecksumOffset);
}
```

### 11.2 Transaction Hash

```csharp
// In TransactionHeader
public uint Hash;  // XXHash32 of all page data in transaction
```

---

## 12. VAYRON Storage Layout

### 12.1 Proposed OID Index Tree

```
Tree: "vayron:oid-index" (FixedSizeTree)
┌─────────────────────────────────────────────────────────────────────┐
│ Key: OID (8 bytes)                                                  │
│ Value: StorageLocation (8 bytes)                                    │
│  ├── Upper 40 bits: Page number                                     │
│  └── Lower 24 bits: Offset within page                              │
└─────────────────────────────────────────────────────────────────────┘
```

### 12.2 Proposed Body Storage

```
Container: "vayron:bodies"
┌─────────────────────────────────────────────────────────────────────┐
│ Each entry is an object body:                                       │
│  ├── TypeToken (4 bytes)                                            │
│  ├── FieldCount (2 bytes)                                           │
│  ├── Flags (2 bytes)                                                │
│  └── FieldData (variable)                                           │
└─────────────────────────────────────────────────────────────────────┘
```

This layout provides a foundation for VAYRON object storage while leveraging Voron's existing page management and durability infrastructure.
