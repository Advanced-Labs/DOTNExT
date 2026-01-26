# T03: Device Interfaces

> **Work Package:** WP3
> **Dependencies:** None (can run parallel to T01)
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Define C++ interface structures for device classes (`IObjectModelOps`, `IFieldAccessOps`) and the `OpsRoot` dispatch table.

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/runtime/src/coreclr/vm/dds/ddsinterfaces.h` | Device interface definitions |
| `src/runtime/src/coreclr/vm/dds/opsroot.h` | OpsRoot structure |

---

## Implementation Steps

### Step 1: Create ddsinterfaces.h

```cpp
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
// VContext - Execution context (Phase 1: placeholder)
//=============================================================================
struct VContext
{
    uint32_t version;           // Context structure version
    uint32_t flags;             // Context flags

    // Reserved for future phases
    // Phase 2: transaction handle
    // Phase 3: security principal
    // Phase 4: call dispatch context
    void* reserved[6];
};

// Global null context (Phase 1: used everywhere)
extern VContext g_NullContext;

//=============================================================================
// Reference enumeration callback
//=============================================================================
typedef void (*DDSRefEnumCallback)(Object** refLocation, ScanContext* sc, void* context);

//=============================================================================
// IObjectModelOps - What an object IS to the runtime
//=============================================================================
struct IObjectModelOps
{
    uint32_t version;

    // Get total object size in bytes
    size_t (STDMETHODCALLTYPE *GetSize)(VContext* ctx, Object* obj);

    // Enumerate reference fields for GC
    void (STDMETHODCALLTYPE *ScanRefs)(
        VContext* ctx,
        Object* obj,
        DDSRefEnumCallback callback,
        ScanContext* sc,
        void* context);

    // Get direct field address (null = use FieldAccessDevice)
    void* (STDMETHODCALLTYPE *GetFieldAddress)(VContext* ctx, Object* obj, FieldDesc* field);

    // Get MethodTable for type information
    MethodTable* (STDMETHODCALLTYPE *GetMethodTable)(VContext* ctx, Object* obj);

    // Check if object is valid/materialized
    bool (STDMETHODCALLTYPE *IsValid)(VContext* ctx, Object* obj);

    // Prepare object for access (lazy materialization hook)
    bool (STDMETHODCALLTYPE *EnsureMaterialized)(VContext* ctx, Object* obj);

    // Reserved for future expansion
    void* reserved[4];
};

//=============================================================================
// IFieldAccessOps - Field read/write interception
//=============================================================================
struct IFieldAccessOps
{
    uint32_t version;

    // Read field value
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

    // Post-access hook
    void (STDMETHODCALLTYPE *OnAfterAccess)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        bool isWrite);

    // Get effective field address after hooks
    void* (STDMETHODCALLTYPE *GetEffectiveAddress)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field);

    void* reserved[4];
};

//=============================================================================
// IStorageOps - Persistence (Phase 2, interface reserved)
//=============================================================================
struct IStorageOps
{
    uint32_t version;

    bool (STDMETHODCALLTYPE *Persist)(VContext* ctx, Object* obj, uint64_t* outVuid);
    Object* (STDMETHODCALLTYPE *Materialize)(VContext* ctx, uint64_t vuid, MethodTable* expectedType);
    bool (STDMETHODCALLTYPE *IsDirty)(VContext* ctx, Object* obj);
    void (STDMETHODCALLTYPE *MarkDirty)(VContext* ctx, Object* obj);

    void* (STDMETHODCALLTYPE *BeginTransaction)(VContext* ctx);
    bool (STDMETHODCALLTYPE *CommitTransaction)(VContext* ctx, void* txHandle);
    void (STDMETHODCALLTYPE *RollbackTransaction)(VContext* ctx, void* txHandle);

    void* reserved[8];
};

//=============================================================================
// ICallDispatchOps - Remote invocation (Phase 4, interface reserved)
//=============================================================================
struct ICallDispatchOps
{
    uint32_t version;

    void* (STDMETHODCALLTYPE *Invoke)(
        VContext* ctx,
        Object* obj,
        void* methodDesc,
        void* args,
        void* returnBuffer);

    bool (STDMETHODCALLTYPE *IsLocal)(VContext* ctx, Object* obj);
    uint64_t (STDMETHODCALLTYPE *GetLocationId)(VContext* ctx, Object* obj);

    void* reserved[8];
};

#endif // _DDS_INTERFACES_H_
```

### Step 2: Create opsroot.h

```cpp
#ifndef _OPSROOT_H_
#define _OPSROOT_H_

#include "dds/ddsinterfaces.h"

//=============================================================================
// OpsRoot - Per-object driver dispatch table
//=============================================================================
struct OpsRoot
{
    uint32_t version;       // Structure version
    uint32_t flags;         // Driver combination flags

    // Core devices (never null)
    IObjectModelOps*  objectModelOps;
    IFieldAccessOps*  fieldAccessOps;

    // Optional devices (null = not applicable)
    IStorageOps*      storageOps;
    ICallDispatchOps* callDispatchOps;

    void* reserved[8];

    // Convenience
    inline bool HasStorage() const { return storageOps != nullptr; }
    inline bool HasRemoteDispatch() const { return callDispatchOps != nullptr; }
};

// OpsRoot flags
#define OPSROOT_FLAG_PERSISTENT     0x0001
#define OPSROOT_FLAG_DISTRIBUTED    0x0002
#define OPSROOT_FLAG_VERSIONED      0x0004

//=============================================================================
// Global instances
//=============================================================================
extern OpsRoot g_DefaultOpsRoot;
extern IObjectModelOps g_DefaultObjectModelOps;
extern IFieldAccessOps g_DefaultFieldAccessOps;

//=============================================================================
// DDS management functions
//=============================================================================
void DDS_Initialize();
void DDS_Shutdown();

OpsRoot* DDS_CreateOpsRoot(
    IObjectModelOps* objectModel,
    IFieldAccessOps* fieldAccess,
    IStorageOps* storage,
    ICallDispatchOps* dispatch);

void DDS_FreeOpsRoot(OpsRoot* ops);

// Get OpsRoot for object (inline for performance)
inline OpsRoot* DDS_GetOpsRoot(Object* obj);

// Set OpsRoot for object
void DDS_SetOpsRoot(Object* obj, OpsRoot* ops);

#endif // _OPSROOT_H_
```

---

## Design Notes

### VContext
- Phase 1: All calls receive `&g_NullContext`
- Drivers accept it but ignore it
- Phase 2+: Populated with transaction, security, etc.

### Reserved Slots
- Each interface has `reserved` array for future methods
- Adding to reserved is ABI-compatible
- Changing existing methods is ABI-breaking

### STDMETHODCALLTYPE
- Use for consistent calling convention
- Required for potential cross-module calls (Phase 4+)

---

## Acceptance Criteria

- [ ] `VContext` struct defined with reserved slots
- [ ] `g_NullContext` declared
- [ ] `IObjectModelOps` interface complete with all methods
- [ ] `IFieldAccessOps` interface complete with all methods
- [ ] `IStorageOps` interface reserved (Phase 2)
- [ ] `ICallDispatchOps` interface reserved (Phase 4)
- [ ] `OpsRoot` struct complete
- [ ] Convenience methods (`HasStorage`, etc.) work
- [ ] Flag constants defined
- [ ] Management function prototypes declared
- [ ] Headers compile without errors

---

## Testing

Compile verification:
```cpp
#include "dds/ddsinterfaces.h"
#include "dds/opsroot.h"

void TestInterfaceSizes()
{
    // Verify structure sizes are reasonable
    static_assert(sizeof(VContext) >= 32, "VContext too small");
    static_assert(sizeof(IObjectModelOps) >= 64, "IObjectModelOps too small");
    static_assert(sizeof(OpsRoot) >= 80, "OpsRoot too small");
}
```

---

## References

- Main Doc: Part II §2.1-2.3 (Device Class Hierarchy, OpsRoot, Interfaces)
- Main Doc: Part III §3.2 WP3
