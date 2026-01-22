# Voron Data Structures

> Engineering analysis of Voron's primary data structures: Trees, Fixed-Size Trees, Tables, and Containers.

---

## 1. Overview

Voron provides a hierarchy of data structures, all ultimately built on B+Trees:

```
                   ┌─────────────────────┐
                   │       Table         │ ← High-level: Schema, indexes
                   │  (Structured Data)  │
                   └──────────┬──────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   Tree (B+T)    │ │  FixedSizeTree  │ │   Container     │
│ Variable-size   │ │  Fixed entries  │ │  Blob storage   │
│   keys/values   │ │   optimized     │ │                 │
└─────────────────┘ └─────────────────┘ └─────────────────┘
          │
          ├── CompactTree (prefix-compressed)
          ├── Lookup (numeric/textual)
          └── PostingList (inverted index)
```

---

## 2. Tree (B+Tree) - Data/BTrees/Tree.cs

The foundational data structure. A self-balancing B+Tree supporting variable-size keys and values.

### 2.1 Structure

```csharp
public unsafe partial class Tree
{
    private readonly TreeMutableState _state;      // Tree metadata
    private readonly LowLevelTransaction _llt;      // Transaction context
    private readonly Transaction _tx;
    private RecentlyFoundTreePages _recentlyFoundPages;  // Page cache

    public Slice Name { get; private set; }         // Tree identifier
    public bool IsLeafCompressionSupported { get; }
}
```

### 2.2 Tree State

```csharp
// TreeRootHeader - persisted metadata
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TreeRootHeader
{
    [FieldOffset(0)]  public RootObjectType RootObjectType;
    [FieldOffset(1)]  public TreeFlags Flags;
    [FieldOffset(8)]  public long RootPageNumber;     // Root of tree
    [FieldOffset(16)] public long PageCount;          // Total pages
    [FieldOffset(24)] public long BranchPages;
    [FieldOffset(32)] public long LeafPages;
    [FieldOffset(40)] public long OverflowPages;
    [FieldOffset(48)] public long NumberOfEntries;    // Key count
    [FieldOffset(56)] public int Depth;               // Tree height
}
```

### 2.3 Key Operations

```csharp
// Adding a value
public DirectAddScope DirectAdd(Slice key, int len, out byte* ptr)
{
    // 1. Find leaf page for key
    var foundPage = FindPageFor(key, out node, out cursorConstructor);

    // 2. Modify page (COW)
    var page = ModifyPage(foundPage);

    // 3. Allocate space for entry
    var node = page.AllocateNewNode(key.Size, len);

    // 4. Return pointer to value area
    ptr = node + NodeHeaderSize + key.Size;
}

// Reading a value
public bool TryRead(Slice key, out ValueReader reader)
{
    // 1. Find leaf containing key
    var page = FindPageFor(key, out node, out _);

    // 2. Binary search within leaf
    var nodeHeader = page.Search(key);

    // 3. Return value reader if found
    if (nodeHeader != null && page.LastMatch == 0)
    {
        reader = new ValueReader(nodeHeader.Value, nodeHeader.DataSize);
        return true;
    }
    return false;
}
```

### 2.4 Tree Page Layout

```
TreePage (8KB default)
┌────────────────────────────────────────────────────────────────────┐
│ TreePageHeader (24 bytes)                                          │
│  ├── PageNumber (8 bytes)                                          │
│  ├── TreeFlags (2 bytes) - Leaf/Branch/Overflow                    │
│  ├── Lower (2 bytes) - End of offset array                         │
│  ├── Upper (2 bytes) - Start of node data                          │
│  └── OverflowSize (4 bytes)                                        │
├────────────────────────────────────────────────────────────────────┤
│ Key Offsets Array (grows down from header)                         │
│  [offset0][offset1][offset2]...                                    │
├────────────────────────────────────────────────────────────────────┤
│                    <<< Free Space >>>                              │
├────────────────────────────────────────────────────────────────────┤
│ Nodes (grow up from bottom)                                        │
│  ┌─────────────────────────────────────┐                           │
│  │ Node N: [Header][Key][Value/PageRef]│                           │
│  ├─────────────────────────────────────┤                           │
│  │ Node N-1: [Header][Key][Value]      │                           │
│  └─────────────────────────────────────┘                           │
└────────────────────────────────────────────────────────────────────┘
```

### 2.5 Node Header

```csharp
[StructLayout(LayoutKind.Explicit, Size = SizeOf)]
public struct TreeNodeHeader
{
    public const int SizeOf = 6;

    [FieldOffset(0)]
    public ushort DataSize;        // Value size (or page count for overflow)

    [FieldOffset(2)]
    public ushort KeySize;         // Key size

    [FieldOffset(4)]
    public TreeNodeFlags Flags;    // Data, PageRef, MultiValue, etc.
}

[Flags]
public enum TreeNodeFlags : ushort
{
    None = 0,
    Data = 1,              // Inline value
    PageRef = 2,           // Reference to another page
    MultiValuePageRef = 4,  // Reference to multi-value tree
}
```

---

## 3. FixedSizeTree - Data/Fixed/FixedSizeTree.cs

Optimized B+Tree for fixed-size entries (e.g., posting lists, indexes).

### 3.1 When to Use

- All values have the same size
- Keys are 64-bit integers
- High entry count (millions+)

### 3.2 Advantages

- Denser packing (no per-entry size metadata)
- Simpler page splits
- More entries per page

### 3.3 Structure

```csharp
public sealed class FixedSizeTree
{
    private readonly Tree _parent;          // Container tree
    private readonly Slice _treeName;
    private readonly byte _valSize;         // Fixed value size
    private long _lastPageNumber;           // Cached root

    // Header stored in parent tree
    public struct FixedSizeTreeHeader
    {
        public RootObjectType RootObjectType;  // Embedded or Large
        public ushort ValueSize;
        public long NumberOfEntries;
        public long RootPageNumber;           // For Large type
        // For Embedded: data follows inline
    }
}
```

### 3.4 Page Layout

```
FixedSizeTreePage (8KB)
┌────────────────────────────────────────────────────────────────────┐
│ FixedSizeTreePageHeader                                            │
│  ├── PageNumber, Flags                                             │
│  ├── NumberOfEntries                                               │
│  ├── ValueSize (stored once per page)                              │
│  └── StartPosition                                                 │
├────────────────────────────────────────────────────────────────────┤
│ Dense Array of Entries                                             │
│  [Key0:8][Val0:N] [Key1:8][Val1:N] [Key2:8][Val2:N] ...           │
│  (N = fixed value size)                                            │
└────────────────────────────────────────────────────────────────────┘
```

---

## 4. Table - Data/Tables/Table.cs

High-level structured storage with schema enforcement and secondary indexes.

### 4.1 Structure

```csharp
public sealed class Table
{
    private readonly TableSchema _schema;       // Column definitions, indexes
    private readonly Tree _tableTree;           // Underlying storage
    private ActiveRawDataSmallSection _activeDataSmallSection;  // Row storage

    // Index structures
    private Dictionary<Slice, Tree> _treesBySliceCache;
    private Dictionary<Slice, FixedSizeTree> _fixedSizeTreeCache;

    public long NumberOfEntries => _stats.NumberOfEntries;
}
```

### 4.2 TableSchema

```csharp
public class TableSchema
{
    // Primary key definition
    public KeyDefinition Key;

    // Secondary indexes (by slice key)
    public Dictionary<Slice, IndexDefinition> Indexes;

    // Fixed-size indexes (by long key)
    public Dictionary<Slice, FixedSizeKeyIndexDef> FixedSizeIndexes;

    // Compression support
    public bool Compressed;
    public CompressionDictionaryInfo CompressionDictionary;
}
```

### 4.3 Table Storage Model

Tables use "raw data sections" for actual row storage:

```
Table Storage
┌─────────────────────────────────────────────────────────────────────┐
│ TableTree (manages metadata)                                        │
│  ├── Schema definition                                              │
│  ├── Statistics                                                     │
│  ├── Primary key tree → StorageId mappings                          │
│  └── Secondary index trees                                          │
├─────────────────────────────────────────────────────────────────────┤
│ RawDataSections (actual row data)                                   │
│  ├── ActiveRawDataSmallSection (current writes)                     │
│  ├── InactiveSections (full, read-only)                             │
│  └── ActiveCandidateSections (nearly full)                          │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.4 TableValueBuilder/Reader

```csharp
// Building a row
var builder = new TableValueBuilder();
builder.Add(keySlice);
builder.Add(valueSlice);
builder.Add(timestampLong);

table.Insert(ref builder);

// Reading a row
if (table.ReadByKey(key, out TableValueReader reader))
{
    var col0 = reader.Read(0, out int size0);  // Get column 0
    var col1 = reader.Read(1, out int size1);  // Get column 1
}
```

---

## 5. Container - Data/Containers/Container.cs

Blob storage for large values that don't fit in tree pages.

### 5.1 Purpose

- Store large blobs (documents, attachments)
- Reference by stable ID
- Support for overflow pages

### 5.2 Structure

```csharp
public static class Container
{
    // Create a new container
    public static ContainerId Create(LowLevelTransaction tx)
    {
        var page = tx.AllocatePage(1);
        InitializeContainerPage(page);
        return new ContainerId(page.PageNumber);
    }

    // Allocate space in container
    public static long Allocate(
        LowLevelTransaction tx,
        ContainerId container,
        int size,
        out byte* ptr)
    {
        // Find page with enough space, or allocate new
        // Returns storage ID (page number + offset encoding)
    }
}
```

### 5.3 Storage ID Encoding

```csharp
// ContainerId encodes page number and offset within page
public readonly struct ContainerId
{
    private readonly long _value;

    // Lower bits: offset within page
    // Higher bits: page number
    public long PageNumber => _value >> OffsetBits;
    public int Offset => (int)(_value & OffsetMask);
}
```

---

## 6. PostingList - Data/PostingLists/PostingList.cs

Optimized structure for inverted indexes (full-text search).

### 6.1 Design

- Stores sorted lists of document IDs
- Compressed using PFor (Packed Frame of Reference)
- Supports union/intersection operations

### 6.2 Structure

```csharp
public class PostingList
{
    private readonly PostingListState _state;
    private readonly LowLevelTransaction _tx;

    // State persisted in tree
    public struct PostingListState
    {
        public long RootPage;
        public long Count;
        public long LeafPages;
        public long BranchPages;
    }

    // Add document ID
    public void Add(long value);

    // Remove document ID
    public void Remove(long value);

    // Iterate all IDs
    public Iterator Iterate();
}
```

---

## 7. CompactTree - Data/CompactTrees/CompactTree.cs

Prefix-compressed tree for string keys with common prefixes.

### 7.1 Use Cases

- Full-text search terms
- File paths
- URLs

### 7.2 Key Feature

Keys are delta-encoded against a persistent dictionary, dramatically reducing storage for keys with common prefixes.

```csharp
public class CompactTree
{
    private readonly PersistentDictionary _dictionary;

    // Keys are encoded as:
    // [dictionary reference] + [suffix bytes]
    // Instead of full key storage
}
```

---

## 8. Lookup - Data/Lookups/Lookup.cs

Specialized trees for numeric or textual key lookups.

```csharp
// Numeric lookup (Int64 or Double keys)
public class Lookup<TKey> where TKey : struct, ILookupKey
{
    // Optimized for dense numeric key ranges
    // Stores values in leaf pages with numeric ordering
}

// Textual lookup (string keys with dictionary compression)
public class Lookup<CompactTree.CompactKeyLookup>
{
    // Uses CompactTree's dictionary for key compression
}
```

---

## 9. VAYRON Relevance

### 9.1 Object Storage Strategy

| Voron Structure | VAYRON Use |
|-----------------|------------|
| Container | Primary object body storage |
| Table | Typed entities with schema |
| Tree | Index structures, handle lookup |
| FixedSizeTree | OID → StorageLocation mapping |
| PostingList | Relationship/query indexes |

### 9.2 Handle-to-Body Mapping

A VAYRON handle could reference objects stored in Voron:

```
VayronHandle
├── OID (stable identity) ────────────┐
├── Cached hot pointer                │
└── Epoch/version                     │
                                      │
                           ┌──────────▼──────────┐
                           │   FixedSizeTree     │
                           │   OID → Location    │
                           └──────────┬──────────┘
                                      │
                           ┌──────────▼──────────┐
                           │    Container or     │
                           │   RawDataSection    │
                           └─────────────────────┘
```

### 9.3 Key Integration Points

1. **Container.Allocate**: Object body allocation
2. **FixedSizeTree**: OID lookup structure
3. **Table**: Typed object storage with schema
4. **PostingList**: Relationship indexes

See [08-Integration-Analysis](./08-Integration-Analysis.md) for detailed strategies.
