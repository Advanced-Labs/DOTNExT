# CoreCLR Object Layout & Type System - Technical Summary

> Source: BOTR type-system.md + CoreCLR source analysis
> Purpose: Foundation for Engram system design
> Generated: 2025-12-05

---

## 1. Object Header Structure

### Memory Layout

The object header **precedes** the object pointer in memory at a negative offset:

```
Memory Layout (64-bit):
┌─────────────────────────┐  ← ObjHeader - 8 bytes before object pointer
│  m_alignpad (4 bytes)   │     Alignment padding
├─────────────────────────┤
│  m_SyncBlockValue (4B)  │     Sync block index / thin lock / hash
├─────────────────────────┤  ← Object pointer (what references point to)
│  m_pMethTab (8 bytes)   │     MethodTable pointer (offset 0)
├─────────────────────────┤
│  Instance fields...     │
└─────────────────────────┘
```

**Header Sizes:**
- **64-bit:** 8 bytes (4-byte alignment pad + 4-byte SyncBlockValue)
- **32-bit:** 4 bytes (SyncBlockValue only)

### SyncBlockValue Bit Layout (32-bit DWORD)

```
Bit 31 (0x80000000): BIT_SBLK_UNUSED           ← AVAILABLE FOR USE!
Bit 30 (0x40000000): BIT_SBLK_FINALIZER_RUN
Bit 29 (0x20000000): BIT_SBLK_GC_RESERVE
Bit 28 (0x10000000): BIT_SBLK_SPIN_LOCK
Bit 27 (0x08000000): BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX
Bit 26 (0x04000000): BIT_SBLK_IS_HASHCODE
Bits 0-25:           Payload (see modes below)
```

**Mode 1: Thin Lock (Bit 27 = 0)**
- Bits 0-15: Thread ID
- Bits 16-21: Recursion level (max 64)
- **Bits 22-25: Available (4 bits)**

**Mode 2: Hash or SyncBlock Index (Bit 27 = 1)**
- Bits 0-25: Either hash code OR sync block index
- Bit 26 distinguishes: 1 = hash, 0 = sync block index

### CRITICAL: Available Bits

| Location | Bits Available | Notes |
|----------|---------------|-------|
| Bit 31 | 1 bit | Explicitly marked BIT_SBLK_UNUSED |
| Bits 22-25 (thin lock mode) | 4 bits | Only when not in hash/syncblock mode |
| MethodTable low bits | 2-3 bits | Used by GC - dangerous to touch |

---

## 2. MethodTable Structure

Every managed object points to its MethodTable as the first field.

### Key Fields

| Field | Purpose |
|-------|---------|
| `m_pEEClass` | Cold metadata pointer |
| `m_pParentMethodTable` | Parent type |
| `m_pModule` | Containing module |
| `m_dwFlags` | Status flags |
| `m_BaseSize` | Instance size |
| `m_wNumVirtuals` | Virtual method count |
| `m_wNumInterfaces` | Interface count |

### Size

~64 bytes on 64-bit release builds.

### Reserved Flags in MethodTableAuxiliaryData

```cpp
// unused enum = 0x4000  ← Available!
// unused enum = 0x8000  ← Available!
```

---

## 3. EEClass Structure

"Cold" metadata accessed during type loading, JIT, or reflection.

### Relationship to MethodTable

**Asymmetric:**
- Multiple generic instantiations share one EEClass but have distinct MethodTables
- `List<string>` and `List<object>` → same EEClass, different MethodTables

### Key Fields

| Field | Purpose |
|-------|---------|
| `m_pMethodTable` | Canonical MethodTable |
| `m_pFieldDescList` | Field descriptors |
| `m_NumInstanceFields` | Instance field count |

---

## 4. Reference Field Tracking (GC Descriptors)

### CGCDesc Structure

GC descriptors are stored at **negative offsets from the MethodTable**:

```
Memory Layout:
┌─────────────────────────┐
│  CGCDescSeries[n-1]     │  ← Negative offset from MethodTable
├─────────────────────────┤
│  NumSeries (size_t)     │  ← *((size_t*)MethodTable - 1)
├─────────────────────────┤
│  MethodTable            │  ← Object's m_pMethTab points here
└─────────────────────────┘
```

### CGCDescSeries

Each series describes a contiguous run of reference fields:

| Field | Purpose |
|-------|---------|
| `seriessize` | Length in bytes |
| `startoffset` | Offset from object start |

**KEY INSIGHT:** The runtime already has complete information about which fields are references and where they are located. This is exactly what Engram needs for relationship tracking!

---

## 5. Object Identity (Current State)

### No UUID - Just Address

Objects are currently identified by **memory address only**. No persistent unique ID.

### Hash Code Storage

- **In-header:** 26 bits (bits 0-25 when bit 26=1, bit 27=1)
- **In SyncBlock:** Full 32 bits
- Generated lazily on first request
- Based on address at generation time
- Stable for object lifetime (survives GC compaction)

### Weak References

| Type | Purpose |
|------|---------|
| `HNDTYPE_WEAK_SHORT` | Tracks until first unreachable detection |
| `HNDTYPE_WEAK_LONG` | Survives finalization |

---

## 6. Object Size Constants

| Constant | 64-bit Value |
|----------|--------------|
| OBJECT_SIZE | 8 bytes |
| OBJHEADER_SIZE | 8 bytes |
| MIN_OBJECT_SIZE | 24 bytes |
| Large Object Threshold | 85,000 bytes |

---

## 7. Extension Points for Engram

### Option A: Leverage SyncBlock

**Pro:** Sanctioned extension point, already sparse
**Con:** Allocated per-object when needed, adds memory pressure

Could add to SyncBlock:
```cpp
struct SyncBlock {
    // ... existing fields ...
    GUID m_EngramId;        // 16 bytes
    void* m_RelationTable;  // Pointer to relationship data
};
```

### Option B: Side Table Indexed by Object Address

**Pro:** Zero overhead for non-engram objects
**Con:** Lookup overhead, must handle GC compaction

```cpp
ConcurrentDictionary<IntPtr, EngramData> g_EngramTable;
```

### Option C: MethodTable Extension (Per-Type Flag)

**Pro:** Type-level opt-in, efficient
**Con:** Still need per-instance UUID storage

Could use reserved bits in MethodTableAuxiliaryData to flag "engram-enabled" types.

### Option D: Bit 31 as Engram Marker

**Pro:** Zero space cost for non-engram objects
**Con:** Only 1 bit - just a marker, not UUID storage

Use BIT_SBLK_UNUSED (bit 31) to indicate "this object has engram data" then look up in side table.

### Recommended Hybrid Approach

1. **BIT_SBLK_UNUSED (bit 31)** = "Has Engram Data" marker
2. **Side table** indexed by current address for hot path
3. **SyncBlock extension** for full engram metadata when needed
4. **Type-level flag** to auto-enable for marked types

---

## 8. CGCDesc - Our Secret Weapon

The GC descriptor already tells us:
- Which fields are references
- Their exact offsets
- Contiguous runs

This means:
- We don't need to discover relationships at runtime
- We can statically know the relationship structure from the type
- Engram extraction can walk the CGCDesc to find all references

**Key API:**
```cpp
CGCDesc* CGCDesc::GetCGCDescFromMT(MethodTable* pMT);
size_t CGCDesc::GetNumSeries();
CGCDescSeries* CGCDesc::GetLowestSeries();
```

---

## 9. Files to Read in Source

| Purpose | Path |
|---------|------|
| Object header | `src/coreclr/vm/object.h` |
| SyncBlock | `src/coreclr/vm/syncblk.h` |
| MethodTable | `src/coreclr/vm/methodtable.h` |
| EEClass | `src/coreclr/vm/class.h` |
| GC Descriptors | `src/coreclr/gc/gcdesc.h` |
| Handle tables | `src/coreclr/gc/objecthandle.h` |

---

*Last updated: 2025-12-05*
