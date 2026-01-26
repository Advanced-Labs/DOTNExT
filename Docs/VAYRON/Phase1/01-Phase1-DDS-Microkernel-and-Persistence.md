# VAYRON Phase 1: Opening the CLR - Implementation Plan

> **Document Version:** 1.1
> **Status:** Initial Analysis & Planning
> **Phase:** 1 (DDS/SAL Skeleton + Microkernel Bring-up)

---

## Executive Summary

This document provides a detailed implementation plan for **Phase 1** of the VAYRON project: establishing the Device Driver System (DDS) and Software Abstraction Layer (SAL) skeleton within the forked .NET CLR.

**Phase 1 Goal:** Make the CLR extensible without breaking existing behavior. All objects continue to work exactly as before, but the infrastructure exists to route specific objects through non-default drivers.

**Key Deliverables:**
1. DDS routing bit (`BIT_SBLK_DDS_NONDEFAULT`) implemented in object headers
2. `ops_root*` resolution infrastructure (side-table initially)
3. `ObjectModelDevice` interface + DefaultDriver
4. `FieldAccessDevice` interface + DefaultDriver
5. Intrinsic helpers for prototype testing (`VFieldRead`, `VFieldWrite`)
6. Test suite validating no regression + basic non-default routing

---

## Phase 1 Non-Goals (Explicitly Out of Scope)

> **Important:** The following items are intentionally deferred to later phases. Do not attempt to implement them in Phase 1.

| Non-Goal | Deferred To | Rationale |
|----------|-------------|-----------|
| **Persistence / StorageDevice real implementation** | Phase 2 | Phase 1 establishes routing; persistence requires stable infrastructure first |
| **VUID generation and mapping** | Phase 2 | Depends on StorageDevice being functional |
| **Voron integration** | Phase 2 | Storage engine integration requires Phase 1 foundation |
| **Remote dispatch / CallDispatchDevice** | Phase 4 | Requires persistence (Phase 2) and relations (Phase 3) first |
| **NewOrleans integration** | Phase 4 | Distribution builds on persistence layer |
| **JIT codegen modifications** | Phase 2+ | Use JIT helpers for Phase 1; optimize later |
| **Alternate object layouts (Mode B)** | Phase 2+ | Start with stub+external body (Mode A) |
| **Transaction coordination across drivers** | Phase 2+ | Requires StorageDevice to be functional |

Phase 1 focuses solely on **opening the CLR** — establishing the DDS routing mechanism and proving that default drivers correctly proxy existing CLR behavior. All feature work builds on this foundation.

---

## Part I: Architecture Analysis

### 1.1 Current CLR Object Model

Every managed object in the CLR has this layout:

```
+------------------------------------------+
|           Object Header (8 bytes)        |  <- Contains SyncBlock index + flags
+------------------------------------------+
|         MethodTable* (8 bytes)           |  <- Points to type metadata
+------------------------------------------+
|              Object Body                 |  <- Instance fields
|                 ...                      |
+------------------------------------------+
```

#### Object Header Structure

The object header is a 32-bit value (padded to 8 bytes on 64-bit) containing:

```cpp
// From src/runtime/src/coreclr/vm/syncblk.h

// Bit layout of m_SyncBlockValue:
// Bits 0-25:  SyncBlock index (26 bits = 67M possible sync blocks)
// Bit 26:     BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX - distinguishes hashcode vs index
// Bit 27:     BIT_SBLK_SPIN_LOCK - thin lock spin
// Bits 28-29: BIT_SBLK_GC_RESERVE (2 bits) - GC marking
// Bit 30:     Reserved
// Bit 31:     BIT_SBLK_UNUSED (0x80000000) - EXPLICITLY UNUSED

#define BIT_SBLK_UNUSED                 0x80000000  // <- THIS IS OUR ENTRY POINT
```

**Critical Finding:** `BIT_SBLK_UNUSED` at bit 31 is explicitly marked as unused by the CLR. This is the ideal location for the DDS routing bit.

#### SyncBlock Table

When an object needs synchronization, hashcode storage, or COM interop, it gets a SyncBlock entry:

```cpp
// SyncBlock provides per-object extended storage
class SyncBlock {
    // Interop info, lock state, dependent handles, etc.
    // We can add: ops_root* pointer as a new slot
};

// SyncTableEntry in the global table
struct SyncTableEntry {
    SyncBlock* m_SyncBlock;
    Object*    m_Object;      // Weak reference back to object
    // We can add: ops_root* for non-default objects without full SyncBlock
};
```

### 1.2 Existing Infrastructure We Can Leverage

| Component | Location | How We Use It |
|-----------|----------|---------------|
| `BIT_SBLK_UNUSED` | `syncblk.h:87` | Repurpose as `BIT_SBLK_DDS_NONDEFAULT` |
| SyncBlock allocation | `syncblk.cpp` | Extend to store `ops_root*` |
| `FieldDesc::GetAddressGuaranteedInHeap` | `field.cpp` | DefaultDriver wraps this |
| JIT helper `JIT_GetFieldAddr` | `jithelpers.cpp:475` | Interception point for field access |
| `GCScanRoots` / `GCHeap::Promote` | `gcenv.h`, `gc.cpp` | ObjectModel integration |
| `MethodTable::GetNumInstanceFieldBytes` | `methodtable.h` | DefaultDriver uses this |
| Write barrier `JIT_WriteBarrier` | `jithelpers.cpp:550` | Non-default can hook this |

### 1.3 Field Access Code Path (Current)

When JIT compiles field access:

```
C#: obj.Field = value;
     |
CIL: stfld <field token>
     |
JIT: (for common cases) inline offset calculation
     mov [obj + fieldOffset], value

JIT: (for complex cases) helper call
     call JIT_SetField

JIT: (for reference fields) write barrier
     call JIT_WriteBarrier
```

**Interception Points:**
1. **JIT compilation** - emit different code for DDS objects (later phases)
2. **JIT helpers** - modify `JIT_GetFieldAddr`, `JIT_SetField` (Phase 1)
3. **Write barriers** - critical for GC correctness (Phase 1 must preserve)

### 1.4 GC Integration Points

The GC needs to know:
1. **Object size** - for allocation and copying
2. **Reference locations** - for tracing
3. **Write barrier requirements** - for generational/concurrent GC

Current implementation:
```cpp
// CGCDesc attached to MethodTable describes reference layout
class CGCDesc {
    // Series of (offset, count) pairs describing ref fields
    // Used by GC to enumerate references
};

// GC calls this during scanning
void GCHeap::Promote(Object** ppObject, ScanContext* sc, uint32_t flags);
```

**For Phase 1:** DefaultDrivers proxy to existing CGCDesc/MethodTable logic. Non-default ObjectModelDrivers must provide equivalent information through the driver interface.

---

## Part II: DDS/SAL Core Design

### 2.1 Device Class Hierarchy

```
+----------------------------------------------------------------------+
|                        DDS Core (Phase 1)                            |
+----------------------------------------------------------------------+
|                                                                      |
|  +------------------+     +------------------+                       |
|  | ObjectModelDevice|     | FieldAccessDevice|                       |
|  |                  |     |                  |                       |
|  | - GetSize()      |     | - Read()         |                       |
|  | - ScanRefs()     |     | - Write()        |                       |
|  | - GetFieldAddr() |     | - WriteBarrier() |                       |
|  | - GetLayout()    |     | - OnAccess()     |                       |
|  +--------+---------+     +--------+---------+                       |
|           |                        |                                 |
|           v                        v                                 |
|  +------------------+     +------------------+                       |
|  | DefaultObjectMod |     | DefaultFieldAcc  |                       |
|  | (wraps CLR)      |     | (wraps CLR)      |                       |
|  +------------------+     +------------------+                       |
|                                                                      |
+----------------------------------------------------------------------+
|                     Reserved (Phase 2+)                              |
+----------------------------------------------------------------------+
|  StorageDevice | CallDispatchDevice | RelationalDevice | Security    |
+----------------------------------------------------------------------+
```

### 2.2 ops_root Structure

```cpp
// The per-object driver dispatch table root
struct OpsRoot {
    uint32_t version;           // For ABI compatibility checking
    uint32_t flags;             // Driver combination flags

    IObjectModelOps*  objectModelOps;   // Required, never null
    IFieldAccessOps*  fieldAccessOps;   // Required, never null

    // Reserved for Phase 2+
    IStorageOps*      storageOps;       // Optional, null = not persistent
    ICallDispatchOps* callDispatchOps;  // Optional, null = local only
    IRelationalOps*   relationalOps;    // Optional, null = no edges
    ISecurityOps*     securityOps;      // Optional, null = default security

    void* reserved[8];          // Future expansion
};

// Singleton for default behavior
extern OpsRoot g_DefaultOpsRoot;
```

### 2.3 Device Interface Definitions

```cpp
//=============================================================================
// VContext - Minimal execution context (Phase 1: placeholder for future use)
//=============================================================================

// Forward-compatible context struct. Phase 1 passes this through but doesn't use it.
// Later phases will populate fields as needed (transactions, security, etc.)
struct VContext {
    uint32_t version;           // Context structure version
    uint32_t flags;             // Context flags (reserved)
    void*    reserved[6];       // Reserved for: transaction, security, thread state, etc.
};

// Global null context for Phase 1 (all drivers receive this, ignore it)
extern VContext g_NullContext;

//=============================================================================
// ObjectModelDevice - What an object IS to the runtime
//=============================================================================
struct IObjectModelOps {
    // Version for ABI compatibility
    uint32_t version;

    // Get the total size of the object in bytes
    size_t (*GetSize)(VContext* ctx, Object* obj);

    // Enumerate all reference fields for GC
    // Callback signature: void(Object** refLocation, ScanContext* sc)
    void (*ScanRefs)(VContext* ctx, Object* obj, void* callback, void* context);

    // Get the address of a field within the object
    // Returns null if field should be accessed through FieldAccessDevice
    void* (*GetFieldAddress)(VContext* ctx, Object* obj, FieldDesc* field);

    // Get layout information for tooling/debugging
    // Returns serializable layout descriptor
    void* (*GetLayoutDescriptor)(VContext* ctx, Object* obj);

    // Called when object is being collected
    void (*OnFinalize)(VContext* ctx, Object* obj);

    // Reserved
    void* reserved[4];
};

//=============================================================================
// FieldAccessDevice - Field read/write interception
//=============================================================================
struct IFieldAccessOps {
    uint32_t version;

    // Read a field value
    // For value types: copies into buffer, returns bytes written
    // For references: returns Object* cast to intptr_t
    intptr_t (*Read)(VContext* ctx, Object* obj, FieldDesc* field, void* buffer, size_t bufferSize);

    // Write a field value
    // For value types: copies from buffer
    // For references: value is Object*
    void (*Write)(VContext* ctx, Object* obj, FieldDesc* field, void* value, size_t valueSize);

    // Write barrier for reference fields (GC integration)
    void (*WriteBarrier)(VContext* ctx, Object* obj, FieldDesc* field, Object* newRef, Object* oldRef);

    // Called before any field access (for lazy materialization, etc.)
    // Return false to proceed with access, true if driver handled it
    bool (*OnBeforeAccess)(VContext* ctx, Object* obj, FieldDesc* field, bool isWrite);

    // Called after field access (for dirty tracking, etc.)
    void (*OnAfterAccess)(VContext* ctx, Object* obj, FieldDesc* field, bool isWrite);

    void* reserved[4];
};

//=============================================================================
// Default implementations (proxy to existing CLR behavior)
//=============================================================================
extern IObjectModelOps  g_DefaultObjectModelOps;
extern IFieldAccessOps  g_DefaultFieldAccessOps;
```

#### Design Note: VContext in Phase 1

> **Why VContext exists now:** Adding a context parameter to all driver ops in Phase 1 (even though it's unused) avoids breaking interface changes later. The cost is minimal — one extra pointer parameter that default drivers ignore.
>
> **Phase 1 usage:** All calls pass `&g_NullContext`. Default drivers receive and ignore it.
>
> **Phase 2+ usage:** Will populate VContext with transaction handles, security principals, etc. No signature changes required.

### 2.4 Routing Logic

```cpp
//=============================================================================
// Fast path routing check
//=============================================================================

// Bit 31 of SyncBlockValue: 0 = default, 1 = non-default routing
#define BIT_SBLK_DDS_NONDEFAULT  0x80000000

inline bool IsNonDefaultRouted(Object* obj) {
    return (obj->GetHeader()->GetBits() & BIT_SBLK_DDS_NONDEFAULT) != 0;
}

inline void SetNonDefaultRouted(Object* obj, bool enabled) {
    ObjHeader* header = obj->GetHeader();
    if (enabled) {
        header->SetBit(BIT_SBLK_DDS_NONDEFAULT);
    } else {
        header->ClrBit(BIT_SBLK_DDS_NONDEFAULT);
    }
}

//=============================================================================
// ops_root resolution (Stage 0: SyncBlock-based)
//=============================================================================

// IMPORTANT: We use SyncBlockIndex (not Object*) as the stable key.
// Object addresses change during GC compaction, but SyncBlockIndex is stable.
// This avoids unreliable GC relocation callbacks.

// Strategy: DDS objects get a SyncBlock entry, and we store ops_root* there.
// The SyncBlockIndex (bits 0-25 of header when BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX is set)
// provides a stable, GC-safe key.

class OpsRootTable {
private:
    // Hash table keyed by SyncBlockIndex (stable across GC)
    ConcurrentHashMap<DWORD, OpsRoot*> m_table;

public:
    OpsRoot* Get(Object* obj) {
        // Get SyncBlockIndex from object header
        DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
        if (syncBlockIndex == 0) {
            return &g_DefaultOpsRoot;  // No SyncBlock = default
        }

        OpsRoot* result;
        if (m_table.TryGet(syncBlockIndex, &result)) {
            return result;
        }
        return &g_DefaultOpsRoot;
    }

    void Set(Object* obj, OpsRoot* ops) {
        // Ensure object has a SyncBlock (allocates if needed)
        SyncBlock* syncBlock = obj->GetSyncBlock();
        DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();

        m_table.Set(syncBlockIndex, ops);
        SetNonDefaultRouted(obj, true);
    }

    void Remove(Object* obj) {
        DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
        if (syncBlockIndex != 0) {
            m_table.Remove(syncBlockIndex);
        }
        SetNonDefaultRouted(obj, false);
    }

    // Note: No OnObjectMoved needed! SyncBlockIndex is stable across GC.
    // When SyncBlock is freed (object collected), CLR recycles the index.
    // We clean up via weak reference or SyncBlock destructor hook.
};

extern OpsRootTable g_OpsRootTable;

//=============================================================================
// Unified routing function
//=============================================================================

inline OpsRoot* GetOpsRoot(Object* obj) {
    if (!IsNonDefaultRouted(obj)) {
        return &g_DefaultOpsRoot;
    }
    return g_OpsRootTable.Get(obj);
}
```

---

## Part III: Implementation Plan

### 3.1 Work Packages

| WP# | Name | Description | Dependencies | Estimated Complexity |
|-----|------|-------------|--------------|---------------------|
| WP1 | Header Bit Infrastructure | Repurpose BIT_SBLK_UNUSED | None | Low |
| WP2 | OpsRoot Side Table | Concurrent hash map + GC hooks | WP1 | Medium |
| WP3 | Device Interfaces | C++ interface definitions | None | Low |
| WP4 | Default Drivers | Proxy implementations | WP3 | Medium |
| WP5 | Field Access Interception | JIT helper modifications | WP1-4 | High |
| WP6 | GC Integration | SyncBlock recycling hooks | WP2 | Low |
| WP7 | Managed API Surface | C# APIs for testing | WP1-6 | Low |
| WP8 | Test Suite | Validation tests | WP7 | Medium |

### 3.2 Detailed Work Packages

---

#### WP1: Header Bit Infrastructure

**Objective:** Repurpose `BIT_SBLK_UNUSED` as `BIT_SBLK_DDS_NONDEFAULT`

**Files to Modify:**

```
src/runtime/src/coreclr/vm/syncblk.h
src/runtime/src/coreclr/vm/syncblk.cpp
src/runtime/src/coreclr/vm/object.h
```

**Changes:**

1. **syncblk.h** - Rename and document the bit
```cpp
// BEFORE:
#define BIT_SBLK_UNUSED  0x80000000

// AFTER:
// DDS (Device Driver System) routing bit
// When set, this object uses non-default drivers for runtime operations
// When clear (default), standard CLR behavior applies
#define BIT_SBLK_DDS_NONDEFAULT  0x80000000

// Legacy alias for compatibility
#define BIT_SBLK_UNUSED  BIT_SBLK_DDS_NONDEFAULT
```

2. **syncblk.h** - Add inline accessors
```cpp
class ObjHeader {
public:
    // ... existing methods ...

    // DDS routing support
    inline bool IsDDSNonDefault() const {
        return (GetBits() & BIT_SBLK_DDS_NONDEFAULT) != 0;
    }

    inline void SetDDSNonDefault() {
        SetBit(BIT_SBLK_DDS_NONDEFAULT);
    }

    inline void ClearDDSNonDefault() {
        ClrBit(BIT_SBLK_DDS_NONDEFAULT);
    }
};
```

3. **object.h** - Add convenience methods on Object
```cpp
class Object {
public:
    // ... existing methods ...

    // DDS routing
    inline bool IsDDSNonDefault() const {
        return GetHeader()->IsDDSNonDefault();
    }
};
```

**Validation:**
- Compile runtime successfully
- Run existing test suite - no regressions
- Verify bit is not used elsewhere in codebase

---

#### WP2: OpsRoot Side Table

**Objective:** Implement GC-safe mapping from objects to OpsRoot* using SyncBlockIndex as stable key

**New Files:**

```
src/runtime/src/coreclr/vm/dds/opsroottable.h
src/runtime/src/coreclr/vm/dds/opsroottable.cpp
```

**Files to Modify:**

```
src/runtime/src/coreclr/vm/CMakeLists.txt (add new files)
src/runtime/src/coreclr/vm/ceemain.cpp (initialization)
```

**Implementation:**

```cpp
// opsroottable.h

#ifndef _OPSROOTTABLE_H_
#define _OPSROOTTABLE_H_

#include "common.h"
#include "shash.h"  // Use existing SHash infrastructure

// Forward declarations
struct OpsRoot;
class Object;

//-----------------------------------------------------------------------------
// Hash traits for SyncBlockIndex keys (DWORD, GC-stable)
//-----------------------------------------------------------------------------
class OpsRootTableTraits : public DefaultSHashTraits<DWORD, OpsRoot*>
{
public:
    typedef DWORD key_t;  // SyncBlockIndex is stable across GC

    static key_t GetKey(element_t e) { return e.first; }
    static BOOL Equals(key_t k1, key_t k2) { return k1 == k2; }
    static count_t Hash(key_t k) {
        // SyncBlockIndex is already well-distributed
        return (count_t)k;
    }

    static const element_t Null() { return element_t(0, nullptr); }
    static const element_t Deleted() { return element_t((DWORD)-1, nullptr); }
    static bool IsNull(const element_t &e) { return e.first == 0; }
    static bool IsDeleted(const element_t &e) { return e.first == (DWORD)-1; }
};

//-----------------------------------------------------------------------------
// Thread-safe table mapping SyncBlockIndex -> OpsRoot* (GC-safe)
//-----------------------------------------------------------------------------
class OpsRootTable
{
private:
    // Hash table using SyncBlockIndex as key (GC-stable)
    typedef SHash<OpsRootTableTraits> TableType;

    TableType m_table;
    CrstExplicitInit m_lock;  // Reader-writer lock for modifications

public:
    void Initialize();
    void Destroy();

    // Get OpsRoot for object via its SyncBlockIndex
    OpsRoot* Get(DWORD syncBlockIndex);

    // Set OpsRoot for SyncBlockIndex (caller ensures object has SyncBlock)
    void Set(DWORD syncBlockIndex, OpsRoot* ops);

    // Remove OpsRoot for SyncBlockIndex
    void Remove(DWORD syncBlockIndex);

    // Called when SyncBlock is being recycled (cleanup hook)
    void OnSyncBlockRecycled(DWORD syncBlockIndex);

    // Enumerate all entries (for debugging/diagnostics)
    void EnumerateEntries(void (*callback)(DWORD, OpsRoot*, void*), void* context);
};

// Global instance
extern OpsRootTable g_OpsRootTable;

// The default OpsRoot (all default drivers)
extern OpsRoot g_DefaultOpsRoot;

#endif // _OPSROOTTABLE_H_
```

**Why SyncBlockIndex is GC-Safe:**

Using SyncBlockIndex as the key avoids GC relocation callbacks entirely:

1. **Object addresses change** during compacting GC, making `Object*` keys unsafe
2. **SyncBlockIndex is stable** - it's an index into the global SyncTableEntry array
3. **No relocation tracking needed** - the mapping remains valid as objects move
4. **Cleanup via SyncBlock lifecycle** - when object is collected, SyncBlock is recycled

> ⚠️ **MUST-DO: SyncBlockIndex Reuse Safety**
>
> **Problem:** When a SyncBlock is recycled (after object collection), its index may be reused for a different object. If the OpsRootTable isn't cleaned up, the new object could inherit stale driver bindings.
>
> **Required Implementation:**
> 1. **Hook into SyncBlock recycling** - `SyncBlock::OnRecycle()` must call `g_OpsRootTable.OnSyncBlockRecycled(index)`
> 2. **Clear DDS bit on recycling** - The new object won't have the bit set (freshly allocated), but defensive clearing in `OnSyncBlockRecycled` prevents edge cases
> 3. **Verify implementation** - Add tests that allocate/deallocate many DDS objects to stress the recycling path
>
> This is **critical for correctness** — stale mappings would cause undefined behavior.

```cpp
// In syncblk.cpp, when SyncBlock is being recycled:
void SyncBlock::OnRecycle()
{
    // ... existing cleanup ...

    // Clean up DDS entry
    DWORD index = GetSyncBlockIndex();
    g_OpsRootTable.OnSyncBlockRecycled(index);
}
```

**Note:** We hook into SyncBlock recycling (not GC relocation) for cleanup.

---

#### WP3: Device Interfaces

**Objective:** Define C++ interface structures for device classes

**New Files:**

```
src/runtime/src/coreclr/vm/dds/ddsinterfaces.h
src/runtime/src/coreclr/vm/dds/opsroot.h
```

**Implementation:**

```cpp
// ddsinterfaces.h

#ifndef _DDS_INTERFACES_H_
#define _DDS_INTERFACES_H_

#include "common.h"

// Forward declarations
class Object;
class FieldDesc;
class MethodTable;
struct ScanContext;

//=============================================================================
// Version constants for ABI compatibility
//=============================================================================
#define DDS_OBJECTMODEL_VERSION   1
#define DDS_FIELDACCESS_VERSION   1
#define DDS_STORAGE_VERSION       1
#define DDS_CALLDISPATCH_VERSION  1

//=============================================================================
// VContext - Execution context passed to all driver operations
//=============================================================================

// Minimal context struct for Phase 1. Later phases populate fields.
struct VContext
{
    uint32_t version;           // Context structure version
    uint32_t flags;             // Context flags (reserved)
    void*    reserved[6];       // Reserved for: transaction, security, thread state, etc.
};

// Global null context (Phase 1: passed but ignored by default drivers)
extern VContext g_NullContext;

//=============================================================================
// ObjectModelDevice Interface
//=============================================================================

// Reference enumeration callback
typedef void (*DDSRefEnumCallback)(Object** refLocation, ScanContext* sc, void* context);

struct IObjectModelOps
{
    uint32_t version;

    // Get total object size in bytes (including header)
    size_t (STDMETHODCALLTYPE *GetSize)(VContext* ctx, Object* obj);

    // Enumerate reference fields for GC
    void (STDMETHODCALLTYPE *ScanRefs)(
        VContext* ctx,
        Object* obj,
        DDSRefEnumCallback callback,
        ScanContext* sc,
        void* context);

    // Get direct field address (null if must use FieldAccessDevice)
    void* (STDMETHODCALLTYPE *GetFieldAddress)(VContext* ctx, Object* obj, FieldDesc* field);

    // Get MethodTable for type information
    MethodTable* (STDMETHODCALLTYPE *GetMethodTable)(VContext* ctx, Object* obj);

    // Check if object is valid/materialized
    bool (STDMETHODCALLTYPE *IsValid)(VContext* ctx, Object* obj);

    // Prepare object for access (lazy materialization hook)
    bool (STDMETHODCALLTYPE *EnsureMaterialized)(VContext* ctx, Object* obj);

    void* reserved[4];
};

//=============================================================================
// FieldAccessDevice Interface
//=============================================================================

struct IFieldAccessOps
{
    uint32_t version;

    // Read field value
    // Returns: number of bytes read, or -1 on error
    // For reference fields: buffer receives Object*, returns sizeof(Object*)
    intptr_t (STDMETHODCALLTYPE *Read)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        void* buffer,
        size_t bufferSize);

    // Write field value
    void (STDMETHODCALLTYPE *Write)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        const void* value,
        size_t valueSize);

    // Write barrier for reference fields
    void (STDMETHODCALLTYPE *WriteBarrier)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        Object* newRef);

    // Pre-access hook (return true to skip default access)
    bool (STDMETHODCALLTYPE *OnBeforeAccess)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        bool isWrite);

    // Post-access hook (for dirty tracking, logging, etc.)
    void (STDMETHODCALLTYPE *OnAfterAccess)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        bool isWrite);

    // Get effective field address after all hooks
    void* (STDMETHODCALLTYPE *GetEffectiveAddress)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field);

    void* reserved[4];
};

//=============================================================================
// StorageDevice Interface (Phase 2 - interface reserved now)
//=============================================================================

struct IStorageOps
{
    uint32_t version;

    // Persist object state to durable storage
    bool (STDMETHODCALLTYPE *Persist)(VContext* ctx, Object* obj, uint64_t* outVuid);

    // Materialize object from storage by VUID
    Object* (STDMETHODCALLTYPE *Materialize)(VContext* ctx, uint64_t vuid, MethodTable* expectedType);

    // Check if object has pending changes
    bool (STDMETHODCALLTYPE *IsDirty)(VContext* ctx, Object* obj);

    // Mark object as dirty (needs persistence)
    void (STDMETHODCALLTYPE *MarkDirty)(VContext* ctx, Object* obj);

    // Transaction support
    void* (STDMETHODCALLTYPE *BeginTransaction)(VContext* ctx);
    bool (STDMETHODCALLTYPE *CommitTransaction)(VContext* ctx, void* txHandle);
    void (STDMETHODCALLTYPE *RollbackTransaction)(VContext* ctx, void* txHandle);

    void* reserved[8];
};

//=============================================================================
// CallDispatchDevice Interface (Phase 4 - interface reserved now)
//=============================================================================

struct ICallDispatchOps
{
    uint32_t version;

    // Invoke method on object (may be remote)
    void* (STDMETHODCALLTYPE *Invoke)(
        VContext* ctx,
        Object* obj,
        void* methodDesc,
        void* args,
        void* returnBuffer);

    // Check if object is local or remote
    bool (STDMETHODCALLTYPE *IsLocal)(VContext* ctx, Object* obj);

    // Get location hint
    uint64_t (STDMETHODCALLTYPE *GetLocationId)(VContext* ctx, Object* obj);

    void* reserved[8];
};

#endif // _DDS_INTERFACES_H_
```

```cpp
// opsroot.h

#ifndef _OPSROOT_H_
#define _OPSROOT_H_

#include "ddsinterfaces.h"

//=============================================================================
// OpsRoot - Per-object driver dispatch table
//=============================================================================

struct OpsRoot
{
    uint32_t version;       // OpsRoot structure version
    uint32_t flags;         // Driver combination flags

    // Phase 1: Core devices (never null after initialization)
    IObjectModelOps*  objectModelOps;
    IFieldAccessOps*  fieldAccessOps;

    // Phase 2+: Optional devices (null = not applicable)
    IStorageOps*      storageOps;
    ICallDispatchOps* callDispatchOps;

    // Future expansion
    void* reserved[8];

    // Convenience accessors
    inline bool HasStorage() const { return storageOps != nullptr; }
    inline bool HasRemoteDispatch() const { return callDispatchOps != nullptr; }
};

// OpsRoot flags
#define OPSROOT_FLAG_PERSISTENT     0x0001
#define OPSROOT_FLAG_DISTRIBUTED    0x0002
#define OPSROOT_FLAG_VERSIONED      0x0004
#define OPSROOT_FLAG_RELATIONAL     0x0008

//=============================================================================
// Global singletons
//=============================================================================

// Default OpsRoot instance (all default drivers)
extern OpsRoot g_DefaultOpsRoot;

// Default driver implementations
extern IObjectModelOps  g_DefaultObjectModelOps;
extern IFieldAccessOps  g_DefaultFieldAccessOps;

//=============================================================================
// OpsRoot management
//=============================================================================

// Initialize DDS subsystem (called during CLR startup)
void DDS_Initialize();

// Shutdown DDS subsystem
void DDS_Shutdown();

// Create a new OpsRoot with specified drivers
OpsRoot* DDS_CreateOpsRoot(
    IObjectModelOps* objectModel,   // null = use default
    IFieldAccessOps* fieldAccess,   // null = use default
    IStorageOps* storage,           // null = no persistence
    ICallDispatchOps* dispatch);    // null = local only

// Free an OpsRoot
void DDS_FreeOpsRoot(OpsRoot* ops);

// Get OpsRoot for an object (via SyncBlockIndex)
inline OpsRoot* DDS_GetOpsRoot(Object* obj)
{
    if (!obj->IsDDSNonDefault()) {
        return &g_DefaultOpsRoot;
    }
    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
    return g_OpsRootTable.Get(syncBlockIndex);
}

// Set OpsRoot for an object (ensures SyncBlock exists, uses stable index)
inline void DDS_SetOpsRoot(Object* obj, OpsRoot* ops)
{
    // Ensure object has a SyncBlock (allocates if needed)
    SyncBlock* syncBlock = obj->GetSyncBlock();
    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();

    g_OpsRootTable.Set(syncBlockIndex, ops);
    obj->GetHeader()->SetDDSNonDefault();
}

#endif // _OPSROOT_H_
```

---

#### WP4: Default Drivers

**Objective:** Implement default drivers that proxy to existing CLR behavior

**New Files:**

```
src/runtime/src/coreclr/vm/dds/defaultdrivers.cpp
```

**Implementation:**

```cpp
// defaultdrivers.cpp

#include "common.h"
#include "dds/opsroot.h"
#include "dds/ddsinterfaces.h"
#include "object.h"
#include "field.h"
#include "methodtable.h"
#include "gcenv.h"

//=============================================================================
// Global null context (Phase 1: passed but ignored)
//=============================================================================
VContext g_NullContext = { 1, 0, { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr } };

//=============================================================================
// Default ObjectModel Driver
//=============================================================================

static size_t STDMETHODCALLTYPE DefaultOM_GetSize(VContext* ctx, Object* obj)
{
    (void)ctx;  // Unused in Phase 1
    return obj->GetSize();
}

static void STDMETHODCALLTYPE DefaultOM_ScanRefs(
    VContext* ctx,
    Object* obj,
    DDSRefEnumCallback callback,
    ScanContext* sc,
    void* context)
{
    (void)ctx;  // Unused in Phase 1
    MethodTable* mt = obj->GetMethodTable();

    if (!mt->ContainsPointers())
        return;

    // Use existing CGCDesc-based scanning
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
            {
                callback(start, sc, context);
            }
            start++;
        }
        series--;
    }
}

static void* STDMETHODCALLTYPE DefaultOM_GetFieldAddress(VContext* ctx, Object* obj, FieldDesc* field)
{
    (void)ctx;  // Unused in Phase 1
    return field->GetAddressGuaranteedInHeap(obj);
}

static MethodTable* STDMETHODCALLTYPE DefaultOM_GetMethodTable(VContext* ctx, Object* obj)
{
    (void)ctx;  // Unused in Phase 1
    return obj->GetMethodTable();
}

static bool STDMETHODCALLTYPE DefaultOM_IsValid(VContext* ctx, Object* obj)
{
    (void)ctx;  // Unused in Phase 1
    return obj != nullptr && obj->GetMethodTable() != nullptr;
}

static bool STDMETHODCALLTYPE DefaultOM_EnsureMaterialized(VContext* ctx, Object* obj)
{
    (void)ctx;  // Unused in Phase 1
    // Default objects are always materialized
    return true;
}

IObjectModelOps g_DefaultObjectModelOps = {
    DDS_OBJECTMODEL_VERSION,
    DefaultOM_GetSize,
    DefaultOM_ScanRefs,
    DefaultOM_GetFieldAddress,
    DefaultOM_GetMethodTable,
    DefaultOM_IsValid,
    DefaultOM_EnsureMaterialized,
    { nullptr, nullptr, nullptr, nullptr }  // reserved
};

//=============================================================================
// Default FieldAccess Driver
//=============================================================================

static intptr_t STDMETHODCALLTYPE DefaultFA_Read(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    void* buffer,
    size_t bufferSize)
{
    (void)ctx;  // Unused in Phase 1
    void* addr = field->GetAddressGuaranteedInHeap(obj);
    size_t fieldSize = field->GetSize();

    if (bufferSize < fieldSize)
        return -1;

    memcpy(buffer, addr, fieldSize);
    return (intptr_t)fieldSize;
}

static void STDMETHODCALLTYPE DefaultFA_Write(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    const void* value,
    size_t valueSize)
{
    (void)ctx;  // Unused in Phase 1
    void* addr = field->GetAddressGuaranteedInHeap(obj);
    size_t fieldSize = field->GetSize();

    _ASSERTE(valueSize == fieldSize);
    memcpy(addr, value, fieldSize);
}

static void STDMETHODCALLTYPE DefaultFA_WriteBarrier(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    Object* newRef)
{
    (void)ctx;  // Unused in Phase 1
    Object** addr = (Object**)field->GetAddressGuaranteedInHeap(obj);
    SetObjectReference(addr, newRef);  // Uses existing write barrier
}

static bool STDMETHODCALLTYPE DefaultFA_OnBeforeAccess(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    bool isWrite)
{
    (void)ctx;  // Unused in Phase 1
    // No special handling for default objects
    return false;
}

static void STDMETHODCALLTYPE DefaultFA_OnAfterAccess(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    bool isWrite)
{
    (void)ctx;  // Unused in Phase 1
    // No special handling for default objects
}

static void* STDMETHODCALLTYPE DefaultFA_GetEffectiveAddress(
    Object* obj,
    FieldDesc* field)
{
    return field->GetAddressGuaranteedInHeap(obj);
}

IFieldAccessOps g_DefaultFieldAccessOps = {
    DDS_FIELDACCESS_VERSION,
    DefaultFA_Read,
    DefaultFA_Write,
    DefaultFA_WriteBarrier,
    DefaultFA_OnBeforeAccess,
    DefaultFA_OnAfterAccess,
    DefaultFA_GetEffectiveAddress,
    { nullptr, nullptr, nullptr, nullptr }  // reserved
};

//=============================================================================
// Default OpsRoot
//=============================================================================

OpsRoot g_DefaultOpsRoot = {
    1,  // version
    0,  // flags
    &g_DefaultObjectModelOps,
    &g_DefaultFieldAccessOps,
    nullptr,  // storageOps
    nullptr,  // callDispatchOps
    { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr }
};
```

---

#### WP5: Field Access Interception

**Objective:** Modify JIT helpers to check DDS routing bit and dispatch through drivers

**Files to Modify:**

```
src/runtime/src/coreclr/vm/jithelpers.cpp
src/runtime/src/coreclr/vm/jitinterface.cpp
```

**Strategy:**

For Phase 1, we modify the JIT helpers (slow path) rather than JIT codegen (fast path). This is simpler and allows testing the infrastructure before committing to JIT changes.

**Implementation:**

```cpp
// In jithelpers.cpp

// Modified JIT_GetField helper
HCIMPL2(Object*, JIT_GetField, Object* obj, FieldDesc* pFD)
{
    FCALL_CONTRACT;

    // Fast path: check DDS bit
    if (UNLIKELY(obj->IsDDSNonDefault()))
    {
        // Slow path: dispatch through driver
        return DDS_GetFieldHelper(obj, pFD);
    }

    // Original fast path
    // ... existing implementation ...
}
HCIMPLEND

// New DDS dispatch helper (NOINLINE to keep fast path small)
NOINLINE Object* DDS_GetFieldHelper(Object* obj, FieldDesc* pFD)
{
    OpsRoot* ops = DDS_GetOpsRoot(obj);
    VContext* ctx = &g_NullContext;  // Phase 1: use null context

    // Check if driver wants to intercept
    if (ops->fieldAccessOps->OnBeforeAccess(ctx, obj, pFD, false))
    {
        // Driver handled the access
        // For reads, need to get value from driver
        Object* result = nullptr;
        ops->fieldAccessOps->Read(ctx, obj, pFD, &result, sizeof(result));
        return result;
    }

    // Get field address (may be redirected by ObjectModel driver)
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, pFD);
    if (addr == nullptr)
    {
        // Must use FieldAccess driver
        Object* result = nullptr;
        ops->fieldAccessOps->Read(ctx, obj, pFD, &result, sizeof(result));
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, pFD, false);
        return result;
    }

    // Direct read
    Object* result = *(Object**)addr;
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, pFD, false);
    return result;
}

// Similarly for JIT_SetField, JIT_GetFieldAddr, etc.
```

**Write Barrier Integration:**

```cpp
// Modified write barrier path
void STDCALL JIT_WriteBarrier_DDS(Object* obj, Object** dst, Object* ref)
{
    OpsRoot* ops = DDS_GetOpsRoot(obj);
    VContext* ctx = &g_NullContext;  // Phase 1: use null context

    // Find the FieldDesc for this location (may need reverse mapping)
    FieldDesc* pFD = FindFieldDescFromAddress(obj, dst);

    if (pFD != nullptr)
    {
        ops->fieldAccessOps->WriteBarrier(ctx, obj, pFD, ref);
    }
    else
    {
        // Fall back to direct write with standard barrier
        SetObjectReference(dst, ref);
    }
}
```

---

#### WP6: GC Integration

**Objective:** Ensure GC correctly handles DDS objects

**Files to Modify:**

```
src/runtime/src/coreclr/vm/syncblk.cpp  (SyncBlock recycling hook)
src/runtime/src/coreclr/gc/gcenv.ee.cpp (optional root scanning)
```

**Key Integration Points:**

1. **SyncBlock Recycling** - Clean up OpsRootTable entries when SyncBlock is recycled
2. **Reference Scanning** - Use ObjectModelDevice for non-default objects (if needed)

**Note:** Because we use SyncBlockIndex as the stable key, we do NOT need object relocation callbacks. The mapping remains valid as objects move during compaction.

**Implementation:**

```cpp
// In syncblk.cpp - hook into SyncBlock recycling

void SyncBlock::OnRecycle()
{
    // ... existing cleanup ...

    // Clean up DDS entry when SyncBlock is recycled (object collected)
    DWORD index = GetSyncBlockIndex();
    if (g_OpsRootTable.Contains(index))
    {
        g_OpsRootTable.OnSyncBlockRecycled(index);
    }
}

// Optional: In gcenv.ee.cpp for root scanning if drivers hold managed refs
void GCToEEInterface::GcScanRoots(
    promote_func* fn,
    int condemned,
    int max_gen,
    ScanContext* sc)
{
    // ... existing root scanning ...

    // Scan any managed references held by DDS drivers (if applicable)
    // Note: Most drivers use unmanaged storage, but this is the hook point
}
```

**Custom Scanning for Non-Default Objects:**

```cpp
// When GC scans an object with non-default ObjectModel
void ScanNonDefaultObject(Object* obj, promote_func* fn, ScanContext* sc)
{
    OpsRoot* ops = DDS_GetOpsRoot(obj);
    VContext* ctx = &g_NullContext;  // Phase 1: use null context

    // Use driver's scanning function
    ops->objectModelOps->ScanRefs(
        ctx,
        obj,
        [](Object** ref, ScanContext* sc, void* callbackCtx) {
            promote_func* fn = (promote_func*)callbackCtx;
            fn(*ref, sc, 0);  // Standard promotion
        },
        sc,
        (void*)fn);
}
```

---

#### WP7: Managed API Surface

**Objective:** Expose minimal C# API for testing DDS infrastructure

**New Files:**

```
src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/DDS/VirtualObject.cs
src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/DDS/DDSRuntime.cs
```

**Implementation:**

```csharp
// VirtualObject.cs
namespace System.Runtime.DDS
{
    /// <summary>
    /// Marker attribute indicating a type participates in DDS routing.
    /// Phase 1: Used for testing infrastructure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class VirtualAttribute : Attribute
    {
    }

    /// <summary>
    /// Marker attribute indicating a type should use persistent storage.
    /// Phase 2: Will activate StorageDevice driver.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class PersistentAttribute : Attribute
    {
    }
}

// DDSRuntime.cs
namespace System.Runtime.DDS
{
    /// <summary>
    /// Runtime services for DDS (internal, for testing).
    /// </summary>
    internal static class DDSRuntime
    {
        // Check if object is using non-default drivers
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern bool IsNonDefaultRouted(object obj);

        // Enable non-default routing for object (testing only)
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void EnableNonDefaultRouting(object obj);

        // Get driver flags for object
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern uint GetDriverFlags(object obj);
    }
}
```

**QCalls for Native Implementation:**

```cpp
// In qcall.cpp or new dds_qcall.cpp

BOOL QCALLTYPE DDSNative_IsNonDefaultRouted(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    GCX_COOP();

    OBJECTREF objRef = obj.Get();
    return objRef->IsDDSNonDefault() ? TRUE : FALSE;
}

void QCALLTYPE DDSNative_EnableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    GCX_COOP();

    OBJECTREF objRef = obj.Get();

    // Create default OpsRoot (uses all default drivers)
    OpsRoot* ops = DDS_CreateOpsRoot(nullptr, nullptr, nullptr, nullptr);

    // Associate with object
    DDS_SetOpsRoot(OBJECTREFToObject(objRef), ops);
}
```

---

#### WP8: Test Suite

**Objective:** Validate Phase 1 infrastructure

**New Files:**

```
src/runtime/src/tests/dds/Phase1Tests.cs
```

**Test Categories:**

```csharp
namespace DDS.Tests
{
    public class Phase1Tests
    {
        // Category 1: No regression tests
        [Fact]
        public void DefaultObjects_BehaviorUnchanged()
        {
            var obj = new TestClass { Value = 42 };
            Assert.False(DDSRuntime.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.Value);

            obj.Value = 100;
            Assert.Equal(100, obj.Value);
        }

        [Fact]
        public void DefaultObjects_GCWorks()
        {
            WeakReference wr;
            {
                var obj = new TestClass { Value = 42 };
                wr = new WeakReference(obj);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(wr.IsAlive);
        }

        // Category 2: Basic DDS routing tests
        [Fact]
        public void CanEnableNonDefaultRouting()
        {
            var obj = new TestClass();
            Assert.False(DDSRuntime.IsNonDefaultRouted(obj));

            DDSRuntime.EnableNonDefaultRouting(obj);
            Assert.True(DDSRuntime.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void NonDefaultRouted_FieldAccessWorks()
        {
            var obj = new TestClass { Value = 42 };
            DDSRuntime.EnableNonDefaultRouting(obj);

            // Field access should still work (using default drivers)
            Assert.Equal(42, obj.Value);

            obj.Value = 100;
            Assert.Equal(100, obj.Value);
        }

        [Fact]
        public void NonDefaultRouted_SurvivesGC()
        {
            var obj = new TestClass { Value = 42 };
            DDSRuntime.EnableNonDefaultRouting(obj);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.True(DDSRuntime.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.Value);
        }

        [Fact]
        public void NonDefaultRouted_SurvivesCompaction()
        {
            // Allocate many objects to trigger compaction
            var obj = new TestClass { Value = 42 };
            DDSRuntime.EnableNonDefaultRouting(obj);

            var holder = new List<byte[]>();
            for (int i = 0; i < 1000; i++)
            {
                holder.Add(new byte[1024]);
            }
            holder.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Object may have moved, but routing should still work
            Assert.True(DDSRuntime.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.Value);
        }

        // Category 3: Performance baseline
        [Fact]
        public void FieldAccess_DefaultVsNonDefault_Performance()
        {
            var defaultObj = new TestClass();
            var nonDefaultObj = new TestClass();
            DDSRuntime.EnableNonDefaultRouting(nonDefaultObj);

            const int iterations = 1_000_000;

            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                defaultObj.Value = i;
                _ = defaultObj.Value;
            }
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                nonDefaultObj.Value = i;
                _ = nonDefaultObj.Value;
            }
            sw2.Stop();

            // Non-default should be slower but not catastrophically so
            // Accept up to 10x overhead for Phase 1 (will optimize later)
            Assert.True(sw2.ElapsedMilliseconds < sw1.ElapsedMilliseconds * 10,
                $"Non-default too slow: {sw2.ElapsedMilliseconds}ms vs {sw1.ElapsedMilliseconds}ms");
        }
    }

    public class TestClass
    {
        public int Value { get; set; }
        public string Name { get; set; }
        public object Reference { get; set; }
    }
}
```

---

## Part IV: File Summary

### New Files to Create

| Path | Description |
|------|-------------|
| `src/runtime/src/coreclr/vm/dds/ddsinterfaces.h` | Device interface definitions |
| `src/runtime/src/coreclr/vm/dds/opsroot.h` | OpsRoot structure and management |
| `src/runtime/src/coreclr/vm/dds/opsroottable.h` | Side table declaration |
| `src/runtime/src/coreclr/vm/dds/opsroottable.cpp` | Side table implementation |
| `src/runtime/src/coreclr/vm/dds/defaultdrivers.cpp` | Default driver implementations |
| `src/runtime/src/coreclr/vm/dds/dds.cpp` | DDS initialization and utilities |
| `src/libraries/.../System/Runtime/DDS/VirtualAttribute.cs` | Managed attribute |
| `src/libraries/.../System/Runtime/DDS/DDSRuntime.cs` | Managed runtime API |
| `src/tests/dds/Phase1Tests.cs` | Test suite |

### Files to Modify

| Path | Changes |
|------|---------|
| `src/runtime/src/coreclr/vm/syncblk.h` | Rename `BIT_SBLK_UNUSED`, add accessors |
| `src/runtime/src/coreclr/vm/object.h` | Add `IsDDSNonDefault()` convenience method |
| `src/runtime/src/coreclr/vm/jithelpers.cpp` | Add DDS dispatch to field access helpers |
| `src/runtime/src/coreclr/vm/ceemain.cpp` | Initialize DDS subsystem |
| `src/runtime/src/coreclr/gc/gcenv.ee.cpp` | Add GC integration hooks |
| `src/runtime/src/coreclr/vm/CMakeLists.txt` | Add new DDS source files |

---

## Part V: Risk Assessment

### Technical Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| SyncBlock allocation overhead | Low | DDS objects already need extended metadata; minimal impact |
| Performance regression on default path | Medium | Careful branch placement, benchmarking |
| SyncBlock table capacity | Low | Monitor usage, can expand if needed |
| Thread safety in OpsRootTable | Medium | Use proven concurrent data structures |

**Note:** Using SyncBlockIndex as the stable key **eliminates** GC relocation tracking risks. The original concern about "GC corruption from incorrect relocation handling" is mitigated by design.

### Kill Switch

If DDS causes problems, we need an easy way to disable it:

```cpp
// Global kill switch
extern bool g_DDSEnabled;

inline bool IsNonDefaultRouted(Object* obj) {
    if (!g_DDSEnabled) return false;
    return (obj->GetHeader()->GetBits() & BIT_SBLK_DDS_NONDEFAULT) != 0;
}
```

### Rollback Plan

All changes are additive:
1. DDS files are new (can be excluded from build)
2. Header bit was unused (can be re-unused)
3. JIT helper changes are guarded by bit check (no effect if bit never set)

---

## Part VI: Success Criteria

Phase 1 is complete when:

1. **No Regressions**
   - All existing runtime tests pass
   - All existing library tests pass
   - Performance benchmarks show no degradation for default objects

2. **DDS Infrastructure Works**
   - Can mark object as non-default routed
   - OpsRoot is correctly associated and retrieved
   - Default drivers correctly proxy CLR behavior
   - GC correctly handles DDS objects (relocation, collection)

3. **Extensibility Validated**
   - Can create custom OpsRoot with modified drivers
   - Custom drivers are correctly invoked for field access
   - Infrastructure supports Phase 2 requirements

---

## Appendix A: Related CLR Source Locations

```
Runtime Core:
  src/runtime/src/coreclr/vm/object.h           - Object class definition
  src/runtime/src/coreclr/vm/object.cpp         - Object implementation
  src/runtime/src/coreclr/vm/syncblk.h          - SyncBlock and header bits
  src/runtime/src/coreclr/vm/syncblk.cpp        - SyncBlock implementation
  src/runtime/src/coreclr/vm/methodtable.h      - MethodTable (type metadata)
  src/runtime/src/coreclr/vm/field.h            - FieldDesc class
  src/runtime/src/coreclr/vm/field.cpp          - Field access implementation

JIT Integration:
  src/runtime/src/coreclr/vm/jithelpers.cpp     - JIT helper functions
  src/runtime/src/coreclr/vm/jitinterface.cpp   - JIT-VM interface

GC Integration:
  src/runtime/src/coreclr/gc/gc.cpp             - GC core
  src/runtime/src/coreclr/gc/gcenv.ee.cpp       - GC-EE interface
  src/runtime/src/coreclr/gc/gcenv.h            - GC environment
  src/runtime/src/coreclr/vm/gchelpers.cpp      - GC helper functions
```

---

## Appendix B: Glossary

| Term | Definition |
|------|------------|
| DDS | Device Driver System - the pluggability mechanism |
| SAL | Software Abstraction Layer - conceptual layer DDS implements |
| OpsRoot | Per-object driver dispatch table root |
| VObject | Virtualized object instance (runtime view) |
| Varia | Whole object across space+time (conceptual) |
| VUID | Virtual Unique Identifier (UUID v7) |
| DefaultDriver | Driver that proxies standard CLR behavior |
| Device Class | Interface contract for a runtime concern |

---

*VAYRON R1 Phase 1 Implementation Plan - Advanced-Labs/DOTNExT*
