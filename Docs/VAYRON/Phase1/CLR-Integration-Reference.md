# CLR Integration Reference for VAYRON Phase 1

> **Purpose:** Detailed CLR integration analysis extracted from previous research. Directly applicable to Phase 1 TDS (TypeDriver System) implementation.
>
> **Naming Convention:** C++ uses `TDS` prefix for brevity; C# uses `TypeDriver` for readability.
>
> **Source:** Consolidated from previous VAYRON research docs (Runtime-Integration-Analysis, VAYRON-Synthesis).

---

## 1. Object Header Bit Layout (Verified)

### 1.1 m_SyncBlockValue Structure

**Location:** `src/runtime/src/coreclr/vm/syncblk.h`

```
Bit Layout of m_SyncBlockValue (32-bit value):

┌────────────────────────────────────────────────────────────────┐
│ 31 │ 30 │ 29-28 │ 27 │ 26 │ 25-0                              │
├────┴────┴───────┴────┴────┴───────────────────────────────────┤
│  U │ R  │  GC   │ SP │ HS │ SyncBlock Index / Hash Code       │
└───────────────────────────────────────────────────────────────┘

Legend:
- Bit 31 (U):  BIT_SBLK_UNUSED (0x80000000) - EXPLICITLY UNUSED ← TDS routing bit
- Bit 30 (R):  Reserved
- Bits 29-28:  BIT_SBLK_GC_RESERVE - GC marking bits
- Bit 27 (SP): BIT_SBLK_SPIN_LOCK - Thin lock spin bit
- Bit 26 (HS): BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX - Distinguishes content
- Bits 25-0:   SyncBlock index (26 bits = 67M entries) OR hashcode
```

### 1.2 Bit Usage Analysis

| Bit | Constant | Usage | Availability for DDS |
|-----|----------|-------|---------------------|
| 31 | `BIT_SBLK_UNUSED` | Explicitly marked unused | **Available** (TDS routing) |
| 30 | (reserved) | Reserved, sometimes used in DEBUG | **Risky** |
| 29-28 | `BIT_SBLK_GC_RESERVE` | GC marking | **Do not use** |
| 27 | `BIT_SBLK_SPIN_LOCK` | Thin lock | **Do not use** |
| 26 | `BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX` | Content discriminator | **Do not use** |
| 25-0 | SyncBlock index/hash | Core functionality | **Do not use** |

### 1.3 Key Source Code

```cpp
// From syncblk.h - Bit definitions
#define BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX    0x04000000
#define BIT_SBLK_SPIN_LOCK                  0x08000000
#define BIT_SBLK_GC_RESERVE                 0x30000000
#define BIT_SBLK_UNUSED                     0x80000000  // ← Use this for TDS

// ObjHeader class methods (syncblk.h)
class ObjHeader
{
private:
    DWORD m_SyncBlockValue;

public:
    // Bit manipulation (thread-safe)
    void SetBit(DWORD dwBit);    // Interlocked OR
    void ClrBit(DWORD dwBit);    // Interlocked AND-NOT
    DWORD GetBits(DWORD dwBitMask) const;

    // SyncBlock access
    DWORD GetSyncBlockIndex() const;
    SyncBlock* GetSyncBlock();
    SyncBlock* GetSyncBlockSpecial();  // May allocate
};
```

**Recommendation:** Rename `BIT_SBLK_UNUSED` to `BIT_SBLK_TDS_NONDEFAULT` and add helper methods:

```cpp
// Proposed additions to ObjHeader class
inline bool IsTDSNonDefault() const {
    return (GetBits(BIT_SBLK_TDS_NONDEFAULT) != 0);
}

inline void SetTDSNonDefault() {
    SetBit(BIT_SBLK_TDS_NONDEFAULT);
}

inline void ClearTDSNonDefault() {
    ClrBit(BIT_SBLK_TDS_NONDEFAULT);
}
```

---

## 2. SyncBlock Integration

### 2.1 SyncBlock Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Global SyncBlock Table                            │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │ SyncTableEntry[0]    SyncTableEntry[1]    SyncTableEntry[n]... │ │
│  │ ┌────────────────┐  ┌────────────────┐  ┌────────────────┐    │ │
│  │ │ m_SyncBlock*   │  │ m_SyncBlock*   │  │ m_SyncBlock*   │    │ │
│  │ │ m_Object*      │  │ m_Object*      │  │ m_Object*      │    │ │
│  │ └────────────────┘  └────────────────┘  └────────────────┘    │ │
│  └────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                           │
                           │ SyncBlockIndex (bits 0-25)
                           ▼
              ┌────────────────────────────┐
              │         Object             │
              │  ┌──────────────────────┐  │
              │  │ ObjHeader            │  │
              │  │   m_SyncBlockValue   │──┼── Contains index to SyncTableEntry
              │  │   BIT_SBLK_TDS_...   │  │   OR hashcode (bit 26 discriminates)
              │  ├──────────────────────┤  │
              │  │ MethodTable*         │  │
              │  ├──────────────────────┤  │
              │  │ Instance fields...   │  │
              │  └──────────────────────┘  │
              └────────────────────────────┘
```

### 2.2 SyncBlock Class

```cpp
// From syncblk.h
class SyncBlock
{
private:
    // COM/Interop data
    InteropSyncBlockInfo* m_pInteropInfo;

    // Synchronization
    AwareLock  m_Monitor;

    // Hash code storage (when needed)
    DWORD      m_dwHashCode;

    // Associated AppDomain
    ADIndex    m_dwAppDomainIndex;

public:
    // Lifecycle
    void Init();
    void Recycle();  // Called when returning to free list

    // Access methods
    AwareLock* GetMonitor() { return &m_Monitor; }
    DWORD GetHashCode();
    void SetHashCode(DWORD hashCode);
};
```

### 2.3 SyncBlock Lifecycle and TDS Cleanup

**Critical Issue:** When an object is collected, its SyncBlock is recycled (returned to free list). The SyncBlockIndex may be reused for a different object.

**Required Hook Point:**

```cpp
// In syncblk.cpp - SyncBlock recycling
void SyncBlock::Recycle()
{
    // ... existing cleanup ...

    // TDS CLEANUP: Remove ops_root entry for this SyncBlock index
    // The SyncBlockIndex is about to be reused, so stale mapping must be removed
    DWORD index = GetSyncBlockIndex();  // Need to pass this somehow
    g_OpsRootTable.OnSyncBlockRecycled(index);
}
```

**Alternative Approaches:**
1. **SyncBlockCache hook** - Hook into `SyncBlockCache::GetNextFreeSyncBlock()`
2. **GCToEEInterface hook** - Hook into `SyncBlockCache::CleanupSyncBlocks()`
3. **WeakReference tracking** - Use WeakReference in ops_root table, check on access

**Recommendation:** Hook into SyncBlock recycling path. The exact location needs verification during implementation.

---

## 3. JIT Helper Functions

### 3.1 Field Access Helpers

**Location:** `src/runtime/src/coreclr/vm/jithelpers.cpp`

| Helper | Purpose | Lines (approx) |
|--------|---------|----------------|
| `JIT_GetFieldAddr` | Get address of field for byref | ~475 |
| `JIT_GetField` | Read object reference field | ~500 |
| `JIT_SetField` | Write object reference field | ~525 |
| `JIT_GetFieldAddr_Framed` | Same with explicit frame | ~480 |

```cpp
// JIT_GetFieldAddr signature
HCIMPL2(void*, JIT_GetFieldAddr, Object* obj, FieldDesc* pFD)
{
    FCALL_CONTRACT;

    _ASSERTE(obj != NULL);
    _ASSERTE(pFD != NULL);

    void* addr = pFD->GetAddressGuaranteedInHeap(obj);
    return addr;
}
HCIMPLEND
```

### 3.2 Proposed TDS Modification

```cpp
HCIMPL2(void*, JIT_GetFieldAddr, Object* obj, FieldDesc* pFD)
{
    FCALL_CONTRACT;

    _ASSERTE(obj != NULL);
    _ASSERTE(pFD != NULL);

    // TDS fast-path check
    if (UNLIKELY(obj->IsTDSNonDefault()))
    {
        return TDS_GetFieldAddrHelper(obj, pFD);  // NOINLINE
    }

    // Original fast path
    return pFD->GetAddressGuaranteedInHeap(obj);
}
HCIMPLEND

// Separate function to keep fast path small
NOINLINE void* TDS_GetFieldAddrHelper(Object* obj, FieldDesc* pFD)
{
    OpsRoot* ops = TDS_GetOpsRoot(obj);
    VContext* ctx = &g_NullContext;

    // Try ObjectModel first
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, pFD);
    if (addr != nullptr) {
        return addr;
    }

    // Fall back to FieldAccess driver's effective address
    return ops->fieldAccessOps->GetEffectiveAddress(ctx, obj, pFD);
}
```

### 3.3 Write Barrier Helpers

**Location:** `src/runtime/src/coreclr/vm/jithelpers.cpp` (~550)

```cpp
// Standard write barrier
HCIMPL2(void, JIT_WriteBarrier, Object** dst, Object* ref)
{
    *dst = ref;
    // Card table update for generational GC
    g_card_table[card_byte(*dst)] = 0xFF;
}
```

**TDS Consideration:** FieldAccessDevice.WriteBarrier() must either:
1. Call the standard write barrier internally
2. Provide equivalent GC notification

---

## 4. GC Integration Points

### 4.1 Object Scanning

**Location:** `src/runtime/src/coreclr/vm/gcenv.h`, `gc.cpp`

```cpp
// GC callback for promoting references
typedef void (*promote_func)(Object** ppObject, ScanContext* sc, uint32_t flags);

// GC-EE interface for root scanning
class GCToEEInterface
{
public:
    static void GcScanRoots(promote_func* fn, int condemned, int max_gen, ScanContext* sc);
    static void GcStartWork(int condemned, int max_gen);
    static void GcDone(int condemned);
};
```

### 4.2 CGCDesc - Reference Layout

**Location:** `src/runtime/src/coreclr/vm/gcdesc.h`

```cpp
class CGCDesc
{
public:
    // Get descriptor from MethodTable
    static CGCDesc* GetCGCDescFromMT(MethodTable* pMT);

    // Series of (offset, size) pairs for reference fields
    CGCDescSeries* GetHighestSeries();
    size_t GetNumSeries();
};

class CGCDescSeries
{
public:
    size_t GetSeriesOffset();
    size_t GetSeriesSize();
};
```

### 4.3 TDS ObjectModelDevice GC Integration

For Phase 1, DefaultObjectModelDriver should use existing CGCDesc:

```cpp
static void DefaultOM_ScanRefs(
    VContext* ctx,
    Object* obj,
    TDSRefEnumCallback callback,
    ScanContext* sc,
    void* context)
{
    MethodTable* mt = obj->GetMethodTable();
    if (!mt->ContainsPointers()) return;

    CGCDesc* map = CGCDesc::GetCGCDescFromMT(mt);
    CGCDescSeries* series = map->GetHighestSeries();
    size_t numSeries = map->GetNumSeries();
    size_t objSize = obj->GetSize();

    for (size_t i = 0; i < numSeries; i++)
    {
        Object** start = (Object**)((uint8_t*)obj + series->GetSeriesOffset());
        Object** end = (Object**)((uint8_t*)start + series->GetSeriesSize() + objSize);

        while (start < end)
        {
            if (*start != nullptr)
                callback(start, sc, context);
            start++;
        }
        series--;
    }
}
```

---

## 5. Thread Safety Considerations

### 5.1 Object Header Modifications

The `ObjHeader::SetBit()` and `ClrBit()` methods use interlocked operations:

```cpp
void ObjHeader::SetBit(DWORD dwBit)
{
    DWORD oldVal, newVal;
    do {
        oldVal = m_SyncBlockValue;
        newVal = oldVal | dwBit;
    } while (InterlockedCompareExchange(&m_SyncBlockValue, newVal, oldVal) != oldVal);
}
```

**Safe for concurrent access** - TDS routing bit can be set from any thread.

### 5.2 OpsRootTable Thread Safety

The side table must be thread-safe:
- **Readers:** Multiple threads reading concurrently (common case)
- **Writers:** Single writer at a time (less common)

Recommended: Use `CrstExplicitInit` (CLR's critical section) or reader-writer lock.

```cpp
class OpsRootTable
{
private:
    CrstExplicitInit m_lock;
    SHash<OpsRootTableTraits> m_table;

public:
    OpsRoot* Get(DWORD syncBlockIndex)
    {
        CrstHolder lock(&m_lock);  // Or use reader lock
        return m_table.Lookup(syncBlockIndex);
    }

    void Set(DWORD syncBlockIndex, OpsRoot* ops)
    {
        CrstHolder lock(&m_lock);  // Writer lock
        m_table.AddOrReplace(syncBlockIndex, ops);
    }
};
```

---

## 6. Risk Assessment

### 6.1 Known Risks from Previous Research

| Risk | Severity | Mitigation |
|------|----------|------------|
| Header bit conflict with future CLR versions | Low | Bit 31 explicitly marked unused; monitor .NET updates |
| JIT helper modification breaks optimization | Medium | Use NOINLINE for slow path; benchmark extensively |
| SyncBlock index reuse causes stale mapping | High | Hook into SyncBlock recycling (required) |
| Write barrier bypass causes GC corruption | Critical | Default driver always calls real barrier |
| Performance regression on default path | Medium | Single bit test; measure overhead |

### 6.2 Validation Requirements

1. **No Regression Tests**
   - All existing CLR tests must pass
   - Performance benchmarks for field access (default objects)

2. **TDS Functionality Tests**
   - Routing bit set/clear works
   - OpsRoot correctly associated and retrieved
   - Survives GC compaction
   - Survives GC collection

3. **Stress Tests**
   - Many TDS objects created/destroyed
   - Concurrent access patterns
   - SyncBlock recycling under load

---

## 7. Performance Targets

### 7.1 Overhead Budgets

| Operation | Current CLR | Target with TDS Check |
|-----------|-------------|----------------------|
| Field read (default obj) | ~1ns | <2ns (+bit test) |
| Field read (TDS obj, cached) | N/A | <50ns |
| Field read (TDS obj, cold) | N/A | <500ns |
| TDS bit test | N/A | <1ns |

### 7.2 Memory Overhead

| Component | Per-Object Cost |
|-----------|-----------------|
| TDS routing bit | 0 bytes (uses existing header bit) |
| OpsRoot pointer | Only for non-default objects |
| SyncBlock (if needed) | ~40 bytes (existing cost if allocated) |
| OpsRoot structure | ~64 bytes per unique driver combination |

---

## Appendix: Source File Quick Reference

```
CLR Object Model:
  vm/object.h              - Object class definition
  vm/object.cpp            - Object implementation
  vm/syncblk.h             - SyncBlock, ObjHeader, bit definitions
  vm/syncblk.cpp           - SyncBlock implementation
  vm/methodtable.h         - MethodTable (type metadata)

Field Access:
  vm/field.h               - FieldDesc class
  vm/field.cpp             - Field access implementation
  vm/jithelpers.cpp        - JIT helper functions (interception points)
  vm/jitinterface.cpp      - JIT-VM interface

GC Integration:
  vm/gcenv.h               - GC environment definitions
  vm/gchelpers.cpp         - GC helper functions
  vm/gcdesc.h              - CGCDesc (reference layout)
  gc/gc.cpp                - GC core implementation
  gc/gcenv.ee.cpp          - GC-EE interface
```

---

*Extracted from previous VAYRON research for VAYRON R1 Phase 1 implementation.*
