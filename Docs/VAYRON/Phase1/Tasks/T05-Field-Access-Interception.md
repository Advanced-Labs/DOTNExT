# T05: Field Access Interception

> **Work Package:** WP5
> **Dependencies:** T01, T02, T03, T04
> **Estimated Complexity:** High
> **Status:** Pending

---

## Objective

Create intrinsic helpers for field access that route through DDS drivers when the routing bit is set. **Phase 1 does NOT modify JIT helpers** - it uses explicit intrinsics only.

---

## Scope Clarification

### What Phase 1 Does
- Adds **new intrinsic functions** (`VFieldRead`, `VFieldWrite`)
- These check the DDS bit and dispatch through drivers
- Used for prototype testing of driver infrastructure

### What Phase 1 Does NOT Do
- Does NOT modify `JIT_GetFieldAddr`, `JIT_SetField`, etc.
- Does NOT add transparent field access interception
- JIT surgery is deferred to Phase 2.5 (see IMP-002)

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/runtime/src/coreclr/vm/dds/ddsintrinsics.h` | Intrinsic declarations |
| `src/runtime/src/coreclr/vm/dds/ddsintrinsics.cpp` | Intrinsic implementations |

---

## Implementation Steps

### Step 1: Create ddsintrinsics.h

```cpp
#ifndef _DDS_INTRINSICS_H_
#define _DDS_INTRINSICS_H_

#include "common.h"
#include "dds/opsroot.h"

//=============================================================================
// DDS Field Access Intrinsics (Phase 1: explicit calls)
//=============================================================================

// Read a field value through DDS routing
// Returns: bytes read, or -1 on error
intptr_t DDS_ReadField(Object* obj, FieldDesc* field, void* buffer, size_t bufferSize);

// Write a field value through DDS routing
void DDS_WriteField(Object* obj, FieldDesc* field, const void* value, size_t valueSize);

// Write a reference field with barrier through DDS routing
void DDS_WriteRefField(Object* obj, FieldDesc* field, Object* newRef);

// Get effective field address through DDS routing
void* DDS_GetFieldAddress(Object* obj, FieldDesc* field);

//=============================================================================
// Convenience templates (for managed interop)
//=============================================================================

template<typename T>
T DDS_ReadFieldValue(Object* obj, int fieldOffset)
{
    T result;
    FieldDesc* field = FindFieldByOffset(obj, fieldOffset);
    DDS_ReadField(obj, field, &result, sizeof(T));
    return result;
}

template<typename T>
void DDS_WriteFieldValue(Object* obj, int fieldOffset, T value)
{
    FieldDesc* field = FindFieldByOffset(obj, fieldOffset);
    DDS_WriteField(obj, field, &value, sizeof(T));
}

#endif // _DDS_INTRINSICS_H_
```

### Step 2: Create ddsintrinsics.cpp

```cpp
#include "common.h"
#include "dds/ddsintrinsics.h"
#include "dds/opsroottable.h"
#include "object.h"
#include "field.h"

//=============================================================================
// Field Read - Routes through driver if DDS bit set
//=============================================================================

intptr_t DDS_ReadField(Object* obj, FieldDesc* field, void* buffer, size_t bufferSize)
{
    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(buffer != nullptr);

    // Get OpsRoot (returns default if not routed)
    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Check pre-access hook
    if (ops->fieldAccessOps->OnBeforeAccess(ctx, obj, field, false))
    {
        // Driver handled access - read through driver
        intptr_t result = ops->fieldAccessOps->Read(ctx, obj, field, buffer, bufferSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
        return result;
    }

    // Get field address (may be redirected by ObjectModel)
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr == nullptr)
    {
        // Must use FieldAccess driver
        intptr_t result = ops->fieldAccessOps->Read(ctx, obj, field, buffer, bufferSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
        return result;
    }

    // Direct read
    size_t fieldSize = field->GetSize();
    if (bufferSize < fieldSize) return -1;

    memcpy(buffer, addr, fieldSize);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
    return (intptr_t)fieldSize;
}

//=============================================================================
// Field Write - Routes through driver if DDS bit set
//=============================================================================

void DDS_WriteField(Object* obj, FieldDesc* field, const void* value, size_t valueSize)
{
    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(value != nullptr);

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Check pre-access hook
    if (ops->fieldAccessOps->OnBeforeAccess(ctx, obj, field, true))
    {
        // Driver handles write
        ops->fieldAccessOps->Write(ctx, obj, field, value, valueSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
        return;
    }

    // Get field address
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr == nullptr)
    {
        ops->fieldAccessOps->Write(ctx, obj, field, value, valueSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
        return;
    }

    // Direct write
    size_t fieldSize = field->GetSize();
    _ASSERTE(valueSize == fieldSize);
    memcpy(addr, value, fieldSize);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
}

//=============================================================================
// Reference Field Write - With GC barrier
//=============================================================================

void DDS_WriteRefField(Object* obj, FieldDesc* field, Object* newRef)
{
    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(field->IsObjRef());

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Always go through WriteBarrier for reference fields
    ops->fieldAccessOps->WriteBarrier(ctx, obj, field, newRef);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
}

//=============================================================================
// Get Field Address - For scenarios needing direct pointer
//=============================================================================

void* DDS_GetFieldAddress(Object* obj, FieldDesc* field)
{
    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Ensure object is materialized
    ops->objectModelOps->EnsureMaterialized(ctx, obj);

    // Try ObjectModel first
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr != nullptr) return addr;

    // Fall back to FieldAccess
    return ops->fieldAccessOps->GetEffectiveAddress(ctx, obj, field);
}

//=============================================================================
// Helper: Find FieldDesc by offset (for template convenience)
//=============================================================================

FieldDesc* FindFieldByOffset(Object* obj, int offset)
{
    MethodTable* mt = obj->GetMethodTable();
    // Implementation depends on CLR internals
    // This is a simplified version
    return mt->GetFieldDescListRaw()->GetFieldDescByOffset(offset);
}
```

---

## Usage Pattern (Managed Side)

```csharp
// Managed wrapper (Phase 1 testing)
namespace System.Runtime.DDS
{
    internal static class DDSIntrinsics
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern T ReadField<T>(object obj, int fieldOffset) where T : unmanaged;

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteField<T>(object obj, int fieldOffset, T value) where T : unmanaged;

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteRefField(object obj, int fieldOffset, object value);
    }
}
```

---

## Key Design Points

### No JIT Modification
- Phase 1 uses explicit intrinsic calls
- Transparent field access comes in Phase 2.5 (IMP-002)

### Fast Path for Default Objects
- `g_OpsRootTable.Get()` returns `&g_DefaultOpsRoot` for non-routed objects
- Default drivers just proxy to CLR behavior
- Overhead is minimal (~10-20ns) for prototype testing

### Write Barrier Preservation
- `DDS_WriteRefField` always goes through driver's `WriteBarrier`
- Default driver uses `SetObjectReference()` (CLR write barrier)
- GC correctness is preserved

---

## Acceptance Criteria

- [ ] `DDS_ReadField` reads through driver for routed objects
- [ ] `DDS_ReadField` works for default objects (proxy behavior)
- [ ] `DDS_WriteField` writes through driver for routed objects
- [ ] `DDS_WriteRefField` uses write barrier
- [ ] `DDS_GetFieldAddress` returns correct address
- [ ] Templates compile and work
- [ ] Managed wrappers work (QCalls/FCalls)
- [ ] No GC corruption under stress
- [ ] Performance acceptable for testing (~50-100ns per access)

---

## Testing

### Basic Functionality

```cpp
void TestDDSIntrinsics()
{
    Object* obj = AllocateTestObject();
    FieldDesc* intField = GetIntField(obj);

    // Test with default object
    int value = 42;
    DDS_WriteField(obj, intField, &value, sizeof(value));

    int readBack = 0;
    DDS_ReadField(obj, intField, &readBack, sizeof(readBack));
    assert(readBack == 42);

    // Test with routed object
    OpsRoot* custom = CreateTracingOpsRoot();  // Logs all accesses
    DDS_SetOpsRoot(obj, custom);

    DDS_WriteField(obj, intField, &value, sizeof(value));
    // Verify tracing driver logged the write
}
```

### Reference Field Test

```cpp
void TestDDSRefField()
{
    Object* parent = AllocateTestObject();
    Object* child = AllocateTestObject();
    FieldDesc* refField = GetRefField(parent);

    DDS_WriteRefField(parent, refField, child);

    Object* readBack = nullptr;
    DDS_ReadField(parent, refField, &readBack, sizeof(readBack));
    assert(readBack == child);

    // Force GC and verify reference is still valid
    GC_Collect();
    DDS_ReadField(parent, refField, &readBack, sizeof(readBack));
    assert(readBack == child);
}
```

---

## References

- Main Doc: Part III §3.2 WP5
- Main Doc: Part VI §6.1 (JIT = None decision)
- Backlog: IMP-002 (JIT Helper Interception)
- CLR Integration Reference: §3 (JIT Helper Functions)
