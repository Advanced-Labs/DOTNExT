# VAYRON Phase 1: Foundation Design

> Single VM, Persistence Only. Make `[Virtual, Persistent]` work.

---

## Goal

After Phase 1, this code works:

```csharp
[Virtual]
public class Counter
{
    public int Value { get; set; }
}

// First run
using (var tx = Vayron.BeginWrite())
{
    var counter = Vayron.Create<Counter>();
    counter.Value = 42;
    var id = Vayron.GetId(counter);  // e.g., returns OID 12345
    Console.WriteLine($"Created counter with ID: {id}");
    tx.Commit();
}

// After restart (different process)
using (var tx = Vayron.BeginRead())
{
    var counter = Vayron.Get<Counter>(12345);  // Load by OID
    Console.WriteLine($"Value: {counter.Value}");  // Prints 42
}
```

**Key constraints:**
- `Counter` is a normal class (no base class, no special property syntax)
- Only the `[Virtual]` attribute marks it as special
- Field access (`counter.Value`) is normal C# syntax
- Persistence is transparent

---

## Component Breakdown

### 1. Compile-Time: Roslyn Analyzer/Generator (Optional)

Could generate:
- Type registration metadata
- Field offset tables
- Validation of `[Virtual]` usage

**Decision:** Defer to Phase 1b. Start with runtime-only approach.

### 2. Runtime: Type Recognition

When a type is loaded, the runtime needs to know it's Virtual:

```cpp
// In class loader (classload.cpp or methodtable.cpp)
void ClassLoader::LoadVirtueMetadata(MethodTable* pMT)
{
    // Check for [Virtual] attribute
    if (HasCustomAttribute(pMT, "VirtualAttribute"))
    {
        VirtueMetadata* meta = AllocateVirtueMetadata();
        meta->Flags = VIRTUE_VIRTUAL;

        // Check for additional virtue attributes
        if (HasCustomAttribute(pMT, "PersistentAttribute"))
            meta->Flags |= VIRTUE_PERSISTENT;

        // Store in MethodTable or EEClass
        pMT->SetVirtueMetadata(meta);
    }
}
```

### 3. Runtime: Object Allocation Hook

When `new Counter()` is called on a Virtual type:

```cpp
// In object allocation (gchelpers.cpp or similar)
Object* AllocateObject(MethodTable* pMT, ...)
{
    Object* obj = /* normal allocation */;

    if (pMT->HasVirtueMetadata())
    {
        // Set VIRTUAL bit in header
        obj->GetHeader()->SetBit(BIT_SBLK_IS_VIRTUAL);

        // Initialize in Memory System
        VayronMemorySystem::OnObjectCreated(obj, pMT->GetVirtueMetadata());
    }

    return obj;
}
```

### 4. Memory System: Object Tracking

```cpp
// VayronMemorySystem (new runtime component)
class VayronMemorySystem
{
    // OID → Object mapping (for activated objects)
    static ConcurrentDictionary<VayronOid, ObjectHandle> s_activated;

    // Object → Metadata mapping
    static ConditionalWeakTable<Object, VayronObjectMeta> s_metadata;

    static void OnObjectCreated(Object* obj, VirtueMetadata* typeMeta)
    {
        VayronOid oid = GenerateOid();

        VayronObjectMeta* objMeta = new VayronObjectMeta();
        objMeta->Oid = oid;
        objMeta->State = ObjectState::New;  // Not yet persisted
        objMeta->TypeMeta = typeMeta;

        s_metadata.Add(obj, objMeta);
        s_activated.Add(oid, CreateWeakHandle(obj));
    }

    static VayronOid GenerateOid()
    {
        // Thread-safe OID generation
        // Could be: local counter, or distributed (Orleans) in Phase 3
        return Interlocked::Increment(&s_nextOid);
    }
};
```

### 5. Field Write Interception

**Option A: JIT Helper**

```cpp
// In jithelpers.cpp
HCIMPL3(void, JIT_SetField32, Object* obj, FieldDesc* pFD, int32_t value)
{
    if (IsVirtualObject(obj))
    {
        VayronMemorySystem::OnFieldWrite(obj, pFD->GetOffset(), &value, sizeof(int32_t));
    }

    // Always write to object (Pattern B)
    *(int32_t*)pFD->GetAddress(obj) = value;
}
HCIMPLEND
```

**Option B: Write Barrier Extension**

```cpp
// In gchelpers.cpp - extend existing write barrier
void WriteBarrier_Vayron(Object* obj, void* fieldAddr, size_t size)
{
    if (IsVirtualObject(obj))
    {
        // Calculate field offset from object base
        ptrdiff_t offset = (byte*)fieldAddr - (byte*)obj;
        VayronMemorySystem::OnFieldWrite(obj, offset, fieldAddr, size);
    }
}
```

**Decision:** Start with JIT helpers (more explicit control), migrate to write barrier if perf requires.

### 6. Memory System: Field Change Tracking

```cpp
void VayronMemorySystem::OnFieldWrite(Object* obj, int offset, void* value, size_t size)
{
    VayronObjectMeta* meta = GetMetadata(obj);

    // Mark object as dirty
    meta->State = ObjectState::Dirty;

    // If in transaction, record the change
    if (VayronTransaction* tx = GetCurrentTransaction())
    {
        tx->RecordWrite(meta->Oid, offset, value, size);
    }
    else
    {
        // Auto-transaction mode? Or error?
        // Decision: Require explicit transaction for writes
        throw new InvalidOperationException("Write to Virtual object outside transaction");
    }
}
```

### 7. Transaction Management

```cpp
class VayronTransaction
{
    VoronTransaction* m_voronTx;      // Underlying Voron transaction
    List<WriteRecord> m_writes;        // Recorded field writes
    List<VayronOid> m_created;         // New objects in this tx
    bool m_isWrite;

public:
    static VayronTransaction* BeginRead()
    {
        auto tx = new VayronTransaction();
        tx->m_voronTx = VoronEngine::BeginReadTransaction();
        tx->m_isWrite = false;
        SetCurrentTransaction(tx);
        return tx;
    }

    static VayronTransaction* BeginWrite()
    {
        auto tx = new VayronTransaction();
        tx->m_voronTx = VoronEngine::BeginWriteTransaction();
        tx->m_isWrite = true;
        SetCurrentTransaction(tx);
        return tx;
    }

    void Commit()
    {
        if (!m_isWrite) {
            m_voronTx->Dispose();
            return;
        }

        // Persist all dirty objects
        for (WriteRecord& write : m_writes)
        {
            PersistFieldChange(write);
        }

        // Persist new objects
        for (VayronOid oid : m_created)
        {
            PersistNewObject(oid);
        }

        m_voronTx->Commit();
        ClearCurrentTransaction();
    }

    void Rollback()
    {
        // Discard changes
        // For Pattern B: need to restore object fields from Voron
        for (WriteRecord& write : m_writes)
        {
            RestoreFieldFromVoron(write);
        }

        m_voronTx->Rollback();
        ClearCurrentTransaction();
    }
};
```

### 8. Voron Integration

```cpp
class VoronEngine
{
    static StorageEnvironment* s_env;

    static void Initialize(const char* path)
    {
        // Initialize Voron embedded in runtime
        StorageEnvironmentOptions options;
        options.BasePath = path;
        s_env = new StorageEnvironment(options);
    }

    // Schema:
    //   Tree "objects/{TypeToken}" - stores object bodies keyed by OID
    //   Tree "oid-index" - maps OID to TypeToken (for loading by OID)

    static void PersistObject(VayronOid oid, MethodTable* pMT, void* fieldData, size_t size)
    {
        auto typeName = GetTypeName(pMT);
        auto tree = GetOrCreateTree("objects/" + typeName);

        tree->Add(oid, Slice(fieldData, size));

        auto index = GetTree("oid-index");
        index->Add(oid, pMT->GetTypeToken());
    }

    static bool TryLoadObject(VayronOid oid, /*out*/ void* fieldData, /*out*/ size_t* size)
    {
        // Lookup type from OID
        auto index = GetTree("oid-index");
        uint32_t typeToken;
        if (!index->TryRead(oid, &typeToken))
            return false;

        // Load body
        auto pMT = ResolveType(typeToken);
        auto typeName = GetTypeName(pMT);
        auto tree = GetTree("objects/" + typeName);

        return tree->TryRead(oid, fieldData, size);
    }
};
```

### 9. Object Loading (Materialization)

```cpp
// Managed API
public static class Vayron
{
    public static T Get<T>(long oid) where T : class
    {
        // Check if already activated
        if (TryGetActivated(oid, out T existing))
            return existing;

        // Allocate new CLR object
        T obj = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        // Load fields from Voron
        VoronEngine.LoadIntoObject(oid, obj);

        // Register as activated
        RegisterActivated(oid, obj);

        return obj;
    }
}

// Native implementation
void VoronEngine::LoadIntoObject(VayronOid oid, Object* obj)
{
    byte buffer[MAX_OBJECT_SIZE];
    size_t size;

    if (!TryLoadObject(oid, buffer, &size))
        throw ObjectNotFoundException(oid);

    // Copy field data into object
    // Skip object header, copy to field area
    memcpy(GetFieldBase(obj), buffer, size);

    // Set up metadata
    VayronMemorySystem::OnObjectLoaded(obj, oid);
}
```

### 10. GC Integration

```cpp
// When a Virtual object is about to be collected
void VayronMemorySystem::OnObjectCollected(Object* obj)
{
    VayronObjectMeta* meta = GetMetadata(obj);
    if (meta == nullptr) return;

    // Remove from activated map
    s_activated.Remove(meta->Oid);

    // Object body stays in Voron - persistence survives GC
    // This is the key difference from normal objects!

    // Cleanup metadata
    delete meta;
}
```

---

## Pattern B Data Flow (Synchronized Copy)

### Write Flow

```
1. User code: counter.Value = 42;
                    │
2. JIT_SetField32(obj, &Value, 42)
                    │
3. IsVirtualObject(obj)? YES
                    │
4. VayronMemorySystem::OnFieldWrite(obj, offset=8, value=42, size=4)
   │
   ├── Get metadata, mark Dirty
   ├── Get current transaction
   └── Record: {oid=12345, offset=8, value=42, size=4}
                    │
5. Write to CLR object: obj->Value = 42
   (Object now has value, but not yet in Voron)
                    │
6. Later: tx.Commit()
   │
   ├── For each recorded write:
   │     VoronEngine::PersistFieldChange(oid, offset, value, size)
   │     → Voron tree update
   │
   └── Voron commit (WAL, fsync)
```

### Read Flow (After Materialization)

```
1. User code: x = counter.Value;
                    │
2. JIT_GetField32(obj, &Value)
                    │
3. Read directly from CLR object: return obj->Value
   (No interception needed for reads in Pattern B!)
```

### Read Flow (Materialization)

```
1. User code: var counter = Vayron.Get<Counter>(12345);
                    │
2. Check s_activated - not found
                    │
3. Allocate Counter (GetUninitializedObject)
   Object header gets VIRTUAL bit set
                    │
4. VoronEngine::TryLoadObject(12345, buffer, &size)
   │
   ├── Lookup oid-index: 12345 → TypeToken
   ├── Resolve type: Counter
   └── Read from objects/Counter tree → field bytes
                    │
5. Copy field bytes into CLR object
                    │
6. Register in s_activated: 12345 → WeakRef(obj)
                    │
7. Return obj (fully materialized, reads are fast)
```

---

## File Changes Required

### New Files

| File | Purpose |
|------|---------|
| `src/coreclr/vm/vayron.h` | VayronMemorySystem, VayronObjectMeta |
| `src/coreclr/vm/vayron.cpp` | Implementation |
| `src/coreclr/vm/vayrontx.h` | VayronTransaction |
| `src/coreclr/vm/vayrontx.cpp` | Transaction implementation |
| `src/coreclr/vm/voronengine.h` | VoronEngine wrapper |
| `src/coreclr/vm/voronengine.cpp` | Voron integration |

### Modified Files

| File | Change |
|------|--------|
| `src/coreclr/vm/syncblk.h` | Add `BIT_SBLK_IS_VIRTUAL` |
| `src/coreclr/vm/methodtable.h` | Add `VirtueMetadata*` member |
| `src/coreclr/vm/classload.cpp` | Load virtue metadata on type load |
| `src/coreclr/vm/gchelpers.cpp` | Hook object allocation |
| `src/coreclr/vm/jithelpers.cpp` | Intercept field writes |
| `src/coreclr/vm/gcheap.cpp` | Hook object finalization |

### Build Integration

- Voron needs to be compiled as native code (C++ port or C++/CLI wrapper)
- Link Voron into coreclr.dll/libcoreclr.so

---

## Managed API Surface

```csharp
namespace System.Runtime.Vayron
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class VirtualAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class PersistentAttribute : Attribute { }

    public static class Vayron
    {
        // Initialize (typically at app startup)
        public static void Initialize(string storagePath);

        // Object creation and loading
        public static T Create<T>() where T : class, new();
        public static T? Get<T>(long oid) where T : class;
        public static bool TryGet<T>(long oid, out T? obj) where T : class;

        // Identity
        public static long GetId(object obj);
        public static bool IsVirtual(object obj);

        // Transactions
        public static VayronTransaction BeginRead();
        public static VayronTransaction BeginWrite();
    }

    public class VayronTransaction : IDisposable
    {
        public void Commit();
        public void Rollback();
        public void Dispose();  // Rollback if not committed
    }
}
```

---

## Testing Strategy

### Unit Tests

1. **Attribute recognition**: Type with `[Virtual]` has VirtueMetadata
2. **Header bit**: New Virtual object has VIRTUAL bit set
3. **Field write tracking**: Writes are recorded in transaction
4. **Persist/load roundtrip**: Write, commit, restart, load, verify

### Integration Tests

1. **Multiple objects**: Create several, commit, reload all
2. **Object graph**: Objects referencing each other (prep for Phase 2)
3. **Transaction rollback**: Changes discarded, Voron unchanged
4. **Concurrent reads**: Multiple threads reading
5. **GC behavior**: Object collected, reloaded from Voron

### Benchmark Tests

1. **Field write overhead**: Virtual vs. normal object
2. **Field read overhead**: Should be zero for Pattern B
3. **Materialization cost**: Cold load from Voron
4. **Transaction throughput**: Commits per second

---

## Open Questions for Phase 1

1. **Voron compilation**: Keep as C# with P/Invoke? Rewrite critical paths in C++?

2. **Transaction scope**: Thread-local? AsyncLocal? Explicit parameter?

3. **Auto-properties**: `{ get; set; }` generates backing field - do we intercept the backing field write?

4. **Struct fields**: Virtual object with struct field - how does that work?

5. **Reference fields**: Virtual object with `string` or `object` field - serialize how?

6. **Initialization**: Constructor runs, sets fields - are those writes tracked?

---

## Implementation Order

1. **Week 1**: Runtime foundation
   - Add `BIT_SBLK_IS_VIRTUAL` to syncblk.h
   - Add `VirtueMetadata` structure
   - Hook type loading to detect `[Virtual]` attribute
   - Hook object allocation to set header bit

2. **Week 2**: Memory System skeleton
   - VayronMemorySystem with OID generation
   - Object metadata tracking (ConditionalWeakTable)
   - Field write interception (JIT helpers)

3. **Week 3**: Voron integration
   - Build Voron as native library
   - VoronEngine wrapper
   - Basic persist/load operations

4. **Week 4**: Transactions
   - VayronTransaction implementation
   - Change tracking
   - Commit/rollback logic

5. **Week 5**: Polish and testing
   - Managed API
   - Test suite
   - Performance baseline
