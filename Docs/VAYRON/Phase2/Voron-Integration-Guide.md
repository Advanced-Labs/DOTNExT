# Voron Integration Guide for StorageDevice

> **Purpose:** Practical Voron API patterns and usage guide for implementing StorageDevice driver in Phase 2.
>
> **Source:** Consolidated from previous VAYRON research docs (Integration-Analysis, Phase1-Implementation).

---

## 1. Voron API Overview

### 1.1 Core Components

| Component | Namespace | Purpose |
|-----------|-----------|---------|
| `StorageEnvironment` | `Voron` | Main entry point, manages database |
| `Transaction` | `Voron.Impl` | High-level read/write transaction |
| `Tree` | `Voron.Data.BTrees` | B+Tree for key-value storage |
| `Lookup<T>` | `Voron.Data.Lookups` | Specialized lookup tables |
| `Container` | `Voron.Data.Containers` | Variable-size blob storage |
| `Slice` | `Voron` | Efficient key/value wrapper |

### 1.2 Environment Setup

```csharp
// Create/open storage environment
var options = StorageEnvironmentOptions.ForPath("/path/to/data");
options.InitialFileSize = 64 * 1024 * 1024;  // 64MB initial
options.MaxLogFileSize = 256 * 1024 * 1024;  // 256MB journal

using var env = new StorageEnvironment(options);
```

---

## 2. Transaction Patterns

### 2.1 Read Transaction

```csharp
using (var tx = env.ReadTransaction())
{
    var tree = tx.ReadTree("vobjects");
    var result = tree.Read(vuidSlice);

    if (result != null)
    {
        // result.Reader provides access to value bytes
        var reader = result.Reader;
        // Process data...
    }
}  // Transaction auto-disposed
```

### 2.2 Write Transaction

```csharp
using (var tx = env.WriteTransaction())
{
    var tree = tx.CreateTree("vobjects");

    // Write operation
    tree.Add(vuidSlice, serializedBytes);

    // MUST commit explicitly
    tx.Commit();
}  // Rollback if Commit() not called
```

### 2.3 Transaction Characteristics

| Aspect | Read Transaction | Write Transaction |
|--------|------------------|-------------------|
| Concurrency | Multiple concurrent | Single writer |
| Duration | Can be long-lived | Should be short |
| Isolation | Snapshot at start | Sees own writes |
| Commit | Implicit on dispose | Explicit required |

---

## 3. Data Structures for VAYRON

### 3.1 Tree (B+Tree) - Primary VObject Storage

```csharp
// Creating/accessing a tree
var vobjectTree = tx.CreateTree("vobjects");

// Key-value operations
vobjectTree.Add(vuidSlice, bodyBytes);           // Insert/update
var result = vobjectTree.Read(vuidSlice);        // Read
vobjectTree.Delete(vuidSlice);                   // Delete

// Check existence
bool exists = vobjectTree.Read(vuidSlice) != null;
```

### 3.2 Lookup Tables - For Indexes

```csharp
// Int64 lookup (e.g., for type-based index)
using Voron.Data.Lookups;

// Create lookup
var lookup = tx.LookupFor<Int64LookupKey>("typeIndex");

// Add entry: typeId -> list of VUIDs
lookup.Add(typeId, vuidValue);

// Query by typeId
var iterator = lookup.CreateIterator();
while (iterator.MoveNext())
{
    var vuid = iterator.CurrentKey;
    // Process...
}
```

### 3.3 Container - For Large Bodies

For VObject bodies that exceed typical B+Tree value sizes:

```csharp
using Voron.Data.Containers;

// Create container
var container = tx.OpenContainer("largeBodies");

// Store large body, get back containerId
long containerId = container.Allocate(bodyBytes.Length);
container.GetMutableDirectAdd(tx.LowLevelTransaction, containerId, out var ptr);
bodyBytes.CopyTo(new Span<byte>(ptr, bodyBytes.Length));

// Read back
var (data, size) = container.GetDirect(tx.LowLevelTransaction, containerId);
```

---

## 4. VUID Storage Strategy

### 4.1 VUID as Slice

```csharp
// VUID is UUID v7 (16 bytes, time-sortable)
public readonly struct VUID
{
    private readonly Guid _value;

    public Slice ToSlice()
    {
        Span<byte> buffer = stackalloc byte[16];
        _value.TryWriteBytes(buffer);
        return Slice.From(buffer);
    }

    public static VUID FromSlice(Slice slice)
    {
        return new VUID(new Guid(slice.AsSpan()));
    }
}
```

### 4.2 Recommended Tree Layout

```
Trees in Voron for VAYRON Phase 2:

1. "vobjects"
   Key: VUID (16 bytes)
   Value: Serialized body bytes (or Container reference)

2. "typeIndex"
   Key: (TypeId:int64, VUID:16 bytes) compound
   Value: empty (presence = membership)

3. "dirtySet"
   Key: VUID
   Value: Timestamp (for flush ordering)

4. "metadata"
   Key: String (various config keys)
   Value: Varies
```

---

## 5. Body Serialization Patterns

### 5.1 Tagged Field Map (Recommended for Phase 2)

```
Body Layout:
┌──────────────────────────────────────────────────────────────┐
│ Header (fixed)                                                │
│ ├─ Version: uint8                                            │
│ ├─ FieldCount: uint16                                        │
│ └─ Flags: uint8                                              │
├──────────────────────────────────────────────────────────────┤
│ Field Directory (variable)                                    │
│ For each field:                                              │
│ ├─ FieldToken: uint32 (metadata token for field)             │
│ ├─ TypeCode: uint8 (primitive type or complex marker)        │
│ └─ Offset: uint32 (offset into data section)                 │
├──────────────────────────────────────────────────────────────┤
│ Data Section (variable)                                       │
│ └─ Field values in serialized form                           │
└──────────────────────────────────────────────────────────────┘
```

### 5.2 Serialization Code Pattern

```csharp
public class BodySerializer
{
    public byte[] Serialize(object obj, FieldDesc[] fields)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header
        writer.Write((byte)1);  // Version
        writer.Write((ushort)fields.Length);
        writer.Write((byte)0);  // Flags

        // Build field directory and data
        var dataStream = new MemoryStream();
        var dataWriter = new BinaryWriter(dataStream);

        foreach (var field in fields)
        {
            writer.Write(field.MetadataToken);
            writer.Write(GetTypeCode(field.FieldType));
            writer.Write((uint)dataStream.Position);

            WriteFieldValue(dataWriter, obj, field);
        }

        // Append data section
        writer.Write(dataStream.ToArray());

        return ms.ToArray();
    }
}
```

---

## 6. Transaction Integration with VContext

### 6.1 VContext Transaction Wrapper

```csharp
// Phase 2: VContext carries Voron transaction
public class VoronVContext : VContext
{
    public Transaction VoronTransaction { get; }
    public bool IsWriteTransaction { get; }

    internal VoronVContext(Transaction tx, bool isWrite)
    {
        VoronTransaction = tx;
        IsWriteTransaction = isWrite;
    }
}
```

### 6.2 StorageDevice Transaction Flow

```
BeginTransaction() → VoronVContext created
       │
       ▼
IStorageOps.Persist(ctx, obj, vuid)
       │
       ▼
Uses ctx.VoronTransaction to write
       │
       ▼
CommitTransaction() → tx.Commit()
```

---

## 7. Performance Considerations

### 7.1 Measured Baselines (from previous research)

| Operation | Latency | Notes |
|-----------|---------|-------|
| Read (cached page) | ~10-50ns | Memory-mapped |
| Read (cold page) | ~100-500μs | Disk I/O |
| Write (buffered) | ~1-5μs | In-memory |
| Commit (small tx) | ~1-10ms | Journal sync |
| Commit (large tx) | 10-100ms | Depends on data size |

### 7.2 Optimization Strategies

1. **Batch Writes** - Group multiple VObject persists in one transaction
2. **Read-Only Transactions** - Use read transactions when possible (no commit overhead)
3. **Memory-Mapped Access** - Voron memory-maps data files; repeated reads are fast
4. **Lazy Materialization** - Don't read bodies until field access

### 7.3 Memory-Mapping Pattern

```csharp
// Voron pages are memory-mapped
// Direct pointer access for zero-copy reads
public unsafe byte* GetBodyPointer(Transaction tx, VUID vuid)
{
    var tree = tx.ReadTree("vobjects");
    var result = tree.Read(vuid.ToSlice());

    if (result == null) return null;

    // Direct pointer to memory-mapped data
    return result.Reader.Base;
}
```

---

## 8. Error Handling

### 8.1 Common Exceptions

| Exception | Cause | Handling |
|-----------|-------|----------|
| `VoronUnrecoverableErrorException` | Corruption/fatal | Restart required |
| `InvalidOperationException` | Wrong transaction type | Check tx.IsWriteTransaction |
| `ObjectDisposedException` | Transaction disposed | Lifecycle management |

### 8.2 Transaction Lifecycle

```csharp
Transaction tx = null;
try
{
    tx = env.WriteTransaction();

    // Operations...

    tx.Commit();
}
catch (Exception ex)
{
    // Transaction auto-rolls back on dispose without commit
    throw;
}
finally
{
    tx?.Dispose();
}
```

---

## 9. Testing Patterns

### 9.1 Test Environment Setup

```csharp
public class VoronTestBase : IDisposable
{
    protected StorageEnvironment Env { get; private set; }
    private string _tempPath;

    public VoronTestBase()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        var options = StorageEnvironmentOptions.ForPath(_tempPath);
        Env = new StorageEnvironment(options);
    }

    public void Dispose()
    {
        Env?.Dispose();
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }
}
```

### 9.2 Persistence Round-Trip Test

```csharp
[Fact]
public void VObject_PersistAndMaterialize_RoundTrips()
{
    var vuid = VUID.New();
    var originalData = new byte[] { 1, 2, 3, 4, 5 };

    // Persist
    using (var tx = Env.WriteTransaction())
    {
        var tree = tx.CreateTree("vobjects");
        tree.Add(vuid.ToSlice(), originalData);
        tx.Commit();
    }

    // Materialize
    using (var tx = Env.ReadTransaction())
    {
        var tree = tx.ReadTree("vobjects");
        var result = tree.Read(vuid.ToSlice());

        Assert.NotNull(result);
        Assert.Equal(originalData, result.Reader.AsSpan().ToArray());
    }
}
```

---

## 10. Integration with Phase 2 StorageDevice

### 10.1 IStorageOps Implementation Sketch

```csharp
public class VoronStorageOps : IStorageOps
{
    private readonly StorageEnvironment _env;

    public bool Persist(VContext ctx, Object obj, out ulong vuid)
    {
        var vctx = (VoronVContext)ctx;
        var tree = vctx.VoronTransaction.CreateTree("vobjects");

        vuid = VUID.New().ToUInt64();
        var body = SerializeBody(obj);

        tree.Add(VUID.FromUInt64(vuid).ToSlice(), body);
        return true;
    }

    public Object Materialize(VContext ctx, ulong vuid, MethodTable* expectedType)
    {
        var vctx = (VoronVContext)ctx;
        var tree = vctx.VoronTransaction.ReadTree("vobjects");

        var result = tree.Read(VUID.FromUInt64(vuid).ToSlice());
        if (result == null) return null;

        return DeserializeBody(result.Reader.AsSpan(), expectedType);
    }

    public void* BeginTransaction(VContext ctx)
    {
        var tx = _env.WriteTransaction();
        return GCHandle.ToIntPtr(GCHandle.Alloc(tx)).ToPointer();
    }

    // ... etc
}
```

---

## Appendix: Voron Source File Reference

```
src/Raven/src/Voron/
├── StorageEnvironment.cs              # Main API
├── StorageEnvironmentOptions.cs       # Configuration
├── Slice.cs                           # Key/value wrapper
├── Impl/
│   ├── Transaction.cs                 # High-level transaction
│   ├── LowLevelTransaction.cs         # Page-level operations
│   ├── Paging/
│   │   ├── AbstractPager.cs           # Storage abstraction
│   │   └── PagerState.cs              # Pager state
│   └── Journal/
│       └── WriteAheadJournal.cs       # Durability
├── Data/
│   ├── BTrees/
│   │   └── Tree.cs                    # B+Tree implementation
│   ├── Lookups/
│   │   └── Lookup.cs                  # Lookup tables
│   └── Containers/
│       └── Container.cs               # Blob storage
└── Global/
    └── Constants.cs                   # Page sizes, etc.
```

---

*Extracted from previous VAYRON research for VAYRON R1 Phase 2 implementation.*
