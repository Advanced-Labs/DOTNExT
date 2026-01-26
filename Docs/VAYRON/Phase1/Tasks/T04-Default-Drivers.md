# T04: Default Drivers

> **Work Package:** WP4
> **Dependencies:** T03 (Device Interfaces)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement default drivers that proxy to existing CLR behavior. These are the "pass-through" drivers that maintain backward compatibility.

---

## Naming Convention

| Context | Convention | Example |
|---------|------------|---------|
| C++ directory | `tds/` | `src/runtime/src/coreclr/vm/tds/` |
| C++ functions | `TDS_*` | `TDS_Initialize()`, `TDS_CreateOpsRoot()` |
| C++ version constants | `TDS_*_VERSION` | `TDS_OBJECTMODEL_VERSION` |

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/runtime/src/coreclr/vm/tds/defaultdrivers.cpp` | Default driver implementations |

---

## Implementation Steps

### Step 1: Implement g_NullContext

```cpp
#include "tds/tdsinterfaces.h"

VContext g_NullContext = {
    1,      // version
    0,      // flags
    { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr }  // reserved
};
```

### Step 2: Implement DefaultObjectModelOps

```cpp
#include "common.h"
#include "tds/opsroot.h"
#include "object.h"
#include "field.h"
#include "methodtable.h"
#include "gcdesc.h"

//=============================================================================
// Default ObjectModel Driver - Proxies to standard CLR behavior
//=============================================================================

static size_t STDMETHODCALLTYPE DefaultOM_GetSize(VContext* ctx, Object* obj)
{
    UNREFERENCED_PARAMETER(ctx);
    return obj->GetSize();
}

static void STDMETHODCALLTYPE DefaultOM_ScanRefs(
    VContext* ctx,
    Object* obj,
    TDSRefEnumCallback callback,
    ScanContext* sc,
    void* context)
{
    UNREFERENCED_PARAMETER(ctx);

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

static void* STDMETHODCALLTYPE DefaultOM_GetFieldAddress(
    VContext* ctx, Object* obj, FieldDesc* field)
{
    UNREFERENCED_PARAMETER(ctx);
    return field->GetAddressGuaranteedInHeap(obj);
}

static MethodTable* STDMETHODCALLTYPE DefaultOM_GetMethodTable(
    VContext* ctx, Object* obj)
{
    UNREFERENCED_PARAMETER(ctx);
    return obj->GetMethodTable();
}

static bool STDMETHODCALLTYPE DefaultOM_IsValid(VContext* ctx, Object* obj)
{
    UNREFERENCED_PARAMETER(ctx);
    return obj != nullptr && obj->GetMethodTable() != nullptr;
}

static bool STDMETHODCALLTYPE DefaultOM_EnsureMaterialized(VContext* ctx, Object* obj)
{
    UNREFERENCED_PARAMETER(ctx);
    UNREFERENCED_PARAMETER(obj);
    return true;  // Default objects are always materialized
}

IObjectModelOps g_DefaultObjectModelOps = {
    TDS_OBJECTMODEL_VERSION,
    DefaultOM_GetSize,
    DefaultOM_ScanRefs,
    DefaultOM_GetFieldAddress,
    DefaultOM_GetMethodTable,
    DefaultOM_IsValid,
    DefaultOM_EnsureMaterialized,
    { nullptr, nullptr, nullptr, nullptr }
};
```

### Step 3: Implement DefaultFieldAccessOps

```cpp
//=============================================================================
// Default FieldAccess Driver - Proxies to standard CLR behavior
//=============================================================================

static intptr_t STDMETHODCALLTYPE DefaultFA_Read(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    void* buffer,
    size_t bufferSize)
{
    UNREFERENCED_PARAMETER(ctx);

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
    UNREFERENCED_PARAMETER(ctx);

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
    UNREFERENCED_PARAMETER(ctx);

    Object** addr = (Object**)field->GetAddressGuaranteedInHeap(obj);
    SetObjectReference(addr, newRef);  // Standard write barrier
}

static bool STDMETHODCALLTYPE DefaultFA_OnBeforeAccess(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    bool isWrite)
{
    UNREFERENCED_PARAMETER(ctx);
    UNREFERENCED_PARAMETER(obj);
    UNREFERENCED_PARAMETER(field);
    UNREFERENCED_PARAMETER(isWrite);
    return false;  // Don't intercept, proceed with standard access
}

static void STDMETHODCALLTYPE DefaultFA_OnAfterAccess(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    bool isWrite)
{
    UNREFERENCED_PARAMETER(ctx);
    UNREFERENCED_PARAMETER(obj);
    UNREFERENCED_PARAMETER(field);
    UNREFERENCED_PARAMETER(isWrite);
    // No-op for default objects
}

static void* STDMETHODCALLTYPE DefaultFA_GetEffectiveAddress(
    VContext* ctx,
    Object* obj,
    FieldDesc* field)
{
    UNREFERENCED_PARAMETER(ctx);
    return field->GetAddressGuaranteedInHeap(obj);
}

IFieldAccessOps g_DefaultFieldAccessOps = {
    TDS_FIELDACCESS_VERSION,
    DefaultFA_Read,
    DefaultFA_Write,
    DefaultFA_WriteBarrier,
    DefaultFA_OnBeforeAccess,
    DefaultFA_OnAfterAccess,
    DefaultFA_GetEffectiveAddress,
    { nullptr, nullptr, nullptr, nullptr }
};
```

### Step 4: Implement g_DefaultOpsRoot

```cpp
//=============================================================================
// Default OpsRoot - Used for all non-routed objects
//=============================================================================

OpsRoot g_DefaultOpsRoot = {
    1,                          // version
    0,                          // flags
    &g_DefaultObjectModelOps,   // objectModelOps
    &g_DefaultFieldAccessOps,   // fieldAccessOps
    nullptr,                    // storageOps
    nullptr,                    // callDispatchOps
    { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr }
};
```

### Step 5: Implement TDS Management Functions

```cpp
void TDS_Initialize()
{
    g_OpsRootTable.Initialize();
}

void TDS_Shutdown()
{
    g_OpsRootTable.Destroy();
}

OpsRoot* TDS_CreateOpsRoot(
    IObjectModelOps* objectModel,
    IFieldAccessOps* fieldAccess,
    IStorageOps* storage,
    ICallDispatchOps* dispatch)
{
    OpsRoot* ops = new OpsRoot();
    ops->version = 1;
    ops->flags = 0;
    ops->objectModelOps = objectModel ? objectModel : &g_DefaultObjectModelOps;
    ops->fieldAccessOps = fieldAccess ? fieldAccess : &g_DefaultFieldAccessOps;
    ops->storageOps = storage;
    ops->callDispatchOps = dispatch;

    if (storage) ops->flags |= OPSROOT_FLAG_PERSISTENT;
    if (dispatch) ops->flags |= OPSROOT_FLAG_DISTRIBUTED;

    return ops;
}

void TDS_FreeOpsRoot(OpsRoot* ops)
{
    if (ops != &g_DefaultOpsRoot) {
        delete ops;
    }
}
```

---

## Key Design Decisions

### Write Barrier
- `DefaultFA_WriteBarrier` uses `SetObjectReference()` which is the standard CLR write barrier
- This ensures GC correctness is preserved

### CGCDesc Scanning
- `DefaultOM_ScanRefs` uses the existing `CGCDesc` mechanism
- This is the standard CLR way to enumerate references
- No custom scanning logic in Phase 1

### UNREFERENCED_PARAMETER
- Used to suppress warnings for `ctx` parameter
- Phase 1 ignores context; Phase 2+ will use it

---

## Acceptance Criteria

- [ ] `g_NullContext` initialized correctly
- [ ] `DefaultOM_GetSize` returns correct size
- [ ] `DefaultOM_ScanRefs` enumerates all references
- [ ] `DefaultOM_GetFieldAddress` returns correct address
- [ ] `DefaultOM_GetMethodTable` returns correct type
- [ ] `DefaultOM_IsValid` works for valid/invalid objects
- [ ] `DefaultOM_EnsureMaterialized` returns true
- [ ] `DefaultFA_Read` reads field correctly
- [ ] `DefaultFA_Write` writes field correctly
- [ ] `DefaultFA_WriteBarrier` uses CLR write barrier
- [ ] `DefaultFA_OnBeforeAccess` returns false (no intercept)
- [ ] `g_DefaultOpsRoot` wired to default drivers
- [ ] `TDS_Initialize` and `TDS_Shutdown` work
- [ ] `TDS_CreateOpsRoot` creates valid OpsRoot
- [ ] Runtime compiles and all tests pass

---

## Testing

### Unit Test

```cpp
void TestDefaultDrivers()
{
    Object* obj = AllocateTestObject();

    // Test ObjectModel
    size_t size = g_DefaultObjectModelOps.GetSize(&g_NullContext, obj);
    assert(size == obj->GetSize());

    MethodTable* mt = g_DefaultObjectModelOps.GetMethodTable(&g_NullContext, obj);
    assert(mt == obj->GetMethodTable());

    assert(g_DefaultObjectModelOps.IsValid(&g_NullContext, obj));
    assert(g_DefaultObjectModelOps.EnsureMaterialized(&g_NullContext, obj));

    // Test FieldAccess
    FieldDesc* field = GetTestField(obj);
    int value = 42;
    g_DefaultFieldAccessOps.Write(&g_NullContext, obj, field, &value, sizeof(value));

    int readBack = 0;
    g_DefaultFieldAccessOps.Read(&g_NullContext, obj, field, &readBack, sizeof(readBack));
    assert(readBack == 42);
}
```

---

## References

- Main Doc: Part III SS3.2 WP4
- Main Doc: Part II SS2.5 (Default implementations)
- CLR Integration Reference: SS4 (GC Integration Points)
