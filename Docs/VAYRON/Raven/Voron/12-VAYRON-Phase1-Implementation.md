# VAYRON Phase 1 Implementation Documentation

> Implementation record for Phase 1 (Pure Managed Prototype) of the VAYRON synthesis.
> Based on the design in `11-VAYRON-Synthesis.md`.

---

## 1. Implementation Overview

**Phase**: 1 - Pure Managed Prototype
**Status**: Complete
**Location**: `/src/Vayron/`
**Commit**: `feat(vayron): Implement Phase 1 VAYRON synthesis - managed prototype`

### Goals Achieved

| Goal | Status | Notes |
|------|--------|-------|
| VayronHandle class with lazy materialization | ✅ | Full implementation |
| VayronEnvironment wrapping StorageEnvironment | ✅ | Full implementation |
| OID index as FixedSizeTree | ✅ | Using `Lookup<Int64LookupKey>` |
| Body storage via Container | ✅ | Using Voron `Container` |
| Basic CRUD operations | ✅ | Create, Read, Update, Delete |
| Performance baseline measurements | ⏳ | Tests created, awaiting benchmarks |

---

## 2. Architecture

### 2.1 Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        User Application                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│   ┌──────────────────┐    ┌──────────────────┐                      │
│   │  VayronEntity    │    │ VayronTypeRegistry│                      │
│   │  (User classes)  │    │  (Schema mgmt)   │                      │
│   └────────┬─────────┘    └──────────────────┘                      │
│            │                                                         │
│   ┌────────▼─────────┐                                              │
│   │   VayronHandle   │◄──────── VayronMetaTable (Side table)        │
│   │ (Lazy material.) │          ConditionalWeakTable<obj, Meta>     │
│   └────────┬─────────┘                                              │
│            │                                                         │
│   ┌────────▼─────────┐    ┌──────────────────┐                      │
│   │VayronTransaction │◄───│    AsyncLocal    │                      │
│   │ (Ambient scope)  │    │  (Flow context)  │                      │
│   └────────┬─────────┘    └──────────────────┘                      │
│            │                                                         │
│   ┌────────▼─────────┐                                              │
│   │VayronEnvironment │                                              │
│   │ (Main entry pt)  │                                              │
│   └────────┬─────────┘                                              │
│            │                                                         │
├────────────┼────────────────────────────────────────────────────────┤
│            │              VORON LAYER                                │
│   ┌────────▼─────────┐                                              │
│   │StorageEnvironment│                                              │
│   ├──────────────────┤                                              │
│   │                  │                                              │
│   │  ┌────────────┐  │  OID → StorageLocation                       │
│   │  │  Lookup    │  │  (vayron:oid-index)                          │
│   │  │Int64Lookup │  │                                              │
│   │  └────────────┘  │                                              │
│   │                  │                                              │
│   │  ┌────────────┐  │  Object Bodies                               │
│   │  │ Container  │  │  (vayron:bodies)                             │
│   │  │            │  │                                              │
│   │  └────────────┘  │                                              │
│   │                  │                                              │
│   │  ┌────────────┐  │  Metadata (next OID, etc.)                   │
│   │  │   Tree     │  │  (vayron:metadata)                           │
│   │  │            │  │                                              │
│   │  └────────────┘  │                                              │
│   │                  │                                              │
│   └──────────────────┘                                              │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Data Flow

```
CREATE FLOW:
1. User creates new VayronEntity subclass
2. VayronHandle generates new OID via Environment
3. Handle marked dirty, registered for persistence
4. On commit: body serialized to Container, OID mapping added to Lookup

READ FLOW:
1. User loads VayronEntity by OID
2. On first field access, EnsureMaterialized() called
3. Lookup queried for OID → StorageLocation
4. Container.GetReadOnly() retrieves body bytes
5. Body cached in handle, epoch recorded

UPDATE FLOW:
1. User sets field value via SetField<T>()
2. Cached body modified, handle marked dirty
3. On commit: body written back via Container.GetMutable() or reallocated

DELETE FLOW:
1. User calls Delete() on handle
2. Container entry deleted
3. OID mapping removed from Lookup
4. Handle invalidated
```

---

## 3. File Inventory

### 3.1 Core Library (`src/Vayron/Vayron/`)

| File | Lines | Purpose |
|------|-------|---------|
| `VayronOid.cs` | 58 | 64-bit stable object identifier |
| `MaterializationState.cs` | 36 | Enum: NotMaterialized, Materializing, Materialized, Dirty, Stale |
| `VayronMeta.cs` | 108 | Metadata: OID, epoch, cached ptr, state, storage location |
| `VayronMetaTable.cs` | 58 | Static ConditionalWeakTable wrapper |
| `VayronTransaction.cs` | 148 | Ambient transaction via AsyncLocal |
| `VayronEnvironment.cs` | 355 | Main entry point, wraps StorageEnvironment |
| `VayronHandle.cs` | 282 | Base handle with lazy materialization |
| `VayronEntity.cs` | 69 | Base class for user entities |
| `VayronTypeRegistry.cs` | 188 | Schema management, attribute-based |
| `VoronExtensions.cs` | 33 | Helper methods for Voron API |
| `Vayron.csproj` | 20 | Project file targeting net9.0 |

**Total**: ~1,355 lines

### 3.2 Test Project (`src/Vayron/Vayron.Tests/`)

| File | Lines | Purpose |
|------|-------|---------|
| `TestEntities.cs` | 78 | Example Person and Product entities |
| `VayronBasicTests.cs` | 190 | Unit tests for CRUD operations |
| `Vayron.Tests.csproj` | 25 | Test project file |

**Total**: ~293 lines

---

## 4. API Reference

### 4.1 VayronOid

```csharp
// Stable 64-bit object identifier
public readonly struct VayronOid : IEquatable<VayronOid>, IComparable<VayronOid>
{
    public static readonly VayronOid Invalid;
    public long Value { get; }
    public bool IsValid { get; }
}
```

### 4.2 VayronEnvironment

```csharp
public sealed class VayronEnvironment : IDisposable
{
    // Construction
    public VayronEnvironment(VayronEnvironmentOptions options);

    // Properties
    public StorageEnvironment VoronEnvironment { get; }
    public bool IsNew { get; }

    // Transactions
    public VayronTransactionScope ReadTransaction();
    public VayronTransactionScope WriteTransaction();

    // OID generation
    public VayronOid GenerateOid();
}
```

### 4.3 VayronTransactionScope

```csharp
public sealed class VayronTransactionScope : IDisposable
{
    public Transaction VoronTransaction { get; }
    public bool IsWriteTransaction { get; }
    public long Epoch { get; }

    public void Commit();  // Write transactions only
    public void Dispose(); // Rolls back if not committed
}
```

### 4.4 VayronHandle

```csharp
public class VayronHandle : IVayronHandle, IDisposable
{
    // Properties
    public VayronOid Oid { get; }
    public bool IsDirty { get; }
    public bool IsMaterialized { get; }

    // Field access (protected, for derived classes)
    protected T GetField<T>(int offset) where T : unmanaged;
    protected void SetField<T>(int offset, T value) where T : unmanaged;

    // Operations
    public void Delete();
    public void Invalidate();
    public void Dispose();
}
```

### 4.5 VayronEntity (Base for User Types)

```csharp
public abstract class VayronEntity : VayronHandle
{
    protected VayronTypeSchema Schema { get; }

    protected VayronEntity(VayronEnvironment environment);          // New entity
    protected VayronEntity(VayronEnvironment environment, VayronOid oid); // Load existing
}
```

### 4.6 Attributes

```csharp
[VayronPersistent(SchemaVersion = 1)]
public class MyEntity : VayronEntity { ... }

[VayronField(Order = 0)]
public int MyProperty { get => GetField<int>(0); set => SetField(0, value); }
```

---

## 5. Usage Examples

### 5.1 Define a Persistent Entity

```csharp
[VayronPersistent(SchemaVersion = 1)]
public class Person : VayronEntity
{
    // Field layout (manual offset calculation for now):
    // Offset 0:  Age (int, 4 bytes) -> aligned to 8 = offset 0
    // Offset 8:  Salary (long, 8 bytes)
    // Offset 16: IsActive (bool, 1 byte)

    [VayronField(Order = 0)]
    public int Age
    {
        get => GetField<int>(0);
        set => SetField(0, value);
    }

    [VayronField(Order = 1)]
    public long Salary
    {
        get => GetField<long>(8);
        set => SetField(8, value);
    }

    [VayronField(Order = 2)]
    public bool IsActive
    {
        get => GetField<bool>(16);
        set => SetField(16, value);
    }

    public Person(VayronEnvironment env) : base(env) { }
    public Person(VayronEnvironment env, VayronOid oid) : base(env, oid) { }
}
```

### 5.2 CRUD Operations

```csharp
// Initialize environment
using var env = new VayronEnvironment(new VayronEnvironmentOptions
{
    Path = "/path/to/storage"
});

// CREATE
VayronOid personOid;
using (var tx = env.WriteTransaction())
{
    var person = new Person(env)
    {
        Age = 30,
        Salary = 75000,
        IsActive = true
    };
    personOid = person.Oid;
    tx.Commit();
}

// READ
using (var tx = env.ReadTransaction())
{
    var person = new Person(env, personOid);
    Console.WriteLine($"Age: {person.Age}");      // 30
    Console.WriteLine($"Salary: {person.Salary}"); // 75000
}

// UPDATE
using (var tx = env.WriteTransaction())
{
    var person = new Person(env, personOid)
    {
        Age = 31,
        Salary = 80000
    };
    tx.Commit();
}

// DELETE
using (var tx = env.WriteTransaction())
{
    var person = new Person(env, personOid);
    person.Delete();
    tx.Commit();
}
```

### 5.3 Transaction Rollback

```csharp
using (var tx = env.WriteTransaction())
{
    var person = new Person(env, savedOid)
    {
        Age = 99  // This change...
    };
    // No commit - dispose triggers rollback
}
// ...will NOT be persisted
```

---

## 6. Storage Format

### 6.1 Body Header (8 bytes)

```
┌──────────────┬───────────────┬───────────────┐
│ TypeToken(4) │SchemaVer(2)   │ Flags(2)      │
└──────────────┴───────────────┴───────────────┘
```

### 6.2 Body Layout

```
┌──────────────────────────────────────────────┐
│              BodyHeader (8 bytes)            │
├──────────────────────────────────────────────┤
│              Field Data (variable)           │
│  - Fields stored at computed offsets         │
│  - 8-byte alignment between fields           │
└──────────────────────────────────────────────┘
```

### 6.3 Voron Trees/Containers

| Name | Type | Key | Value |
|------|------|-----|-------|
| `vayron:oid-index` | Lookup<Int64LookupKey> | OID (long) | StorageLocation (long) |
| `vayron:bodies` | Container | - | Body bytes |
| `vayron:metadata` | Tree | "next-oid" | Next OID value (long) |

---

## 7. Design Decisions

### 7.1 Why ConditionalWeakTable for Metadata?

- **GC-friendly**: Metadata automatically cleaned when handle collected
- **Proven pattern**: Same approach used in CLR's DependentHandle
- **No header pressure**: Keeps handle objects minimal
- **Thread-safe**: Built-in synchronization

### 7.2 Why AsyncLocal for Transactions?

- **Async/await compatible**: Flows across async boundaries
- **No explicit passing**: Ambient access from any handle
- **Nested support**: Reference counting for nested transactions

### 7.3 Why Lookup<Int64LookupKey> Instead of FixedSizeTree?

- **Better API fit**: Voron's Lookup provides exact key→value semantics
- **Efficient for 64→64 bit mappings**: Optimized encoding
- **State serialization**: LookupState can be persisted to root objects

### 7.4 Why Container for Bodies?

- **Variable-size handling**: Bodies can grow/shrink
- **Overflow support**: Large bodies automatically span pages
- **Allocation/deallocation**: Built-in space management
- **Stable IDs**: ContainerEntryId encodes location

---

## 8. Known Limitations (Phase 1)

1. **Manual offset calculation**: Field offsets must be computed manually
2. **No reference tracking**: Handles to other handles not automatically followed
3. **No schema migration**: SchemaVersion stored but not acted upon
4. **No query support**: Must know OID to load object
5. **No indexing**: No secondary indexes on fields
6. **String fields not supported**: Only unmanaged types via GetField<T>

---

## 9. Performance Characteristics

### 9.1 Expected Costs (Phase 1 - Managed)

| Operation | Estimated Cost | Notes |
|-----------|---------------|-------|
| OID generation | ~5ns | Interlocked.Increment |
| IsVayronHandle check | N/A | Phase 2 feature |
| Metadata lookup | ~50ns | ConditionalWeakTable.TryGetValue |
| Field access (cold) | ~500ns+ | Voron read + copy |
| Field access (hot) | ~10ns | Cached byte[] access |
| Transaction start (read) | ~200ns | Voron ReadTransaction |
| Transaction start (write) | ~500ns | Voron WriteTransaction |
| Commit | ~1-10ms | Voron commit + journal |

### 9.2 Memory Overhead

| Component | Per-Object Cost |
|-----------|-----------------|
| VayronHandle fields | 32 bytes (OID + epoch + array ref + flags) |
| VayronMeta | 64 bytes (all metadata) |
| Cached body | Body size + array overhead |
| WeakReference | 16 bytes (in dirty handles bag) |

---

## 10. Testing

### 10.1 Test Coverage

| Test | Status | Description |
|------|--------|-------------|
| CanCreateEnvironment | ✅ | Environment initialization |
| CanGenerateOids | ✅ | OID uniqueness and ordering |
| CanCreateAndReadPerson | ✅ | Basic create/read cycle |
| CanUpdatePerson | ✅ | Update and verify |
| CanDeletePerson | ✅ | Delete and verify |
| CanCreateProduct | ✅ | Complex types (decimal, Guid, DateTime) |
| TransactionRollbackDoesNotPersist | ✅ | Rollback semantics |
| MultipleEntitiesInOneTransaction | ✅ | Batch operations |
| TypeRegistryCreatesCorrectSchema | ✅ | Schema generation |

### 10.2 Running Tests

```bash
cd src/Vayron/Vayron.Tests
dotnet test
```

---

## 11. Future Work (Phases 2-5)

### Phase 2: Object Header Tagging
- Repurpose `BIT_SBLK_UNUSED` (bit 31) for VAYRON classification
- ~50 lines of runtime C++ code
- Fast `IsVayronHandle()` check via bit test

### Phase 3: Side Table Integration
- Native side table access from runtime
- Faster metadata lookup than ConditionalWeakTable
- Lifecycle management hooks

### Phase 4: Transaction Integration
- Deeper ambient transaction support
- Automatic transaction detection in JIT helpers
- Write barrier awareness

### Phase 5: JIT Helper Interception
- Intercept `JIT_GetFieldAddr` for VAYRON types
- Transparent field access without property overhead
- ~200 lines of runtime modification

---

## 12. References

- `/Research/Raven/Voron/11-VAYRON-Synthesis.md` - Design synthesis
- `/Research/Raven/Voron/10-Runtime-Integration-Analysis.md` - CLR integration points
- `/Research/Raven/Voron/04-Data-Structures.md` - Voron primitives
- `/src/Raven/src/Voron/` - Voron source code
