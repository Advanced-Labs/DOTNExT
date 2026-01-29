// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSINTRINSICS.CPP
//
// TypeDriver System (TDS) - Field Access Intrinsics Implementation
// Phase 1: Explicit intrinsic calls that route through TDS drivers.

#include "common.h"
#include "tds/tdsintrinsics.h"
#include "tds/tdsinterfaces.h"
#include "tds/opsroot.h"
#include "tds/opsroottable.h"
#include "object.h"
#include "field.h"

// External globals from defaultdrivers.cpp
extern VContext g_NullContext;

//=============================================================================
// TDS_ReadField - Read a field value through TDS routing
//=============================================================================

intptr_t TDS_ReadField(Object* obj, FieldDesc* field, void* buffer, size_t bufferSize)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(buffer != nullptr);

    // Get OpsRoot (returns default if not routed)
    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Check if driver wants to intercept this access
    if (ops->fieldAccessOps->OnBeforeAccess(ctx, obj, field, false /* isWrite */))
    {
        // Driver handles the read
        intptr_t result = ops->fieldAccessOps->Read(ctx, obj, field, buffer, bufferSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
        return result;
    }

    // Try to get direct field address from ObjectModel
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr == nullptr)
    {
        // No direct address - must use FieldAccess driver
        intptr_t result = ops->fieldAccessOps->Read(ctx, obj, field, buffer, bufferSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
        return result;
    }

    // Direct read from memory
    size_t fieldSize = field->GetSize();
    if (bufferSize < fieldSize)
    {
        return -1;  // Buffer too small
    }

    memcpy(buffer, addr, fieldSize);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, false);
    return (intptr_t)fieldSize;
}

//=============================================================================
// TDS_WriteField - Write a field value through TDS routing
//=============================================================================

void TDS_WriteField(Object* obj, FieldDesc* field, const void* value, size_t valueSize)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(value != nullptr);

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Check if driver wants to intercept this access
    if (ops->fieldAccessOps->OnBeforeAccess(ctx, obj, field, true /* isWrite */))
    {
        // Driver handles the write
        ops->fieldAccessOps->Write(ctx, obj, field, value, valueSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
        return;
    }

    // Try to get direct field address from ObjectModel
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr == nullptr)
    {
        // No direct address - must use FieldAccess driver
        ops->fieldAccessOps->Write(ctx, obj, field, value, valueSize);
        ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
        return;
    }

    // Direct write to memory
    size_t fieldSize = field->GetSize();
    _ASSERTE(valueSize == fieldSize);
    memcpy(addr, value, fieldSize);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
}

//=============================================================================
// TDS_WriteRefField - Write a reference field with GC barrier
//=============================================================================

void TDS_WriteRefField(Object* obj, FieldDesc* field, Object* newRef)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(field->IsObjRef());

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Reference field writes ALWAYS go through the WriteBarrier function
    // to ensure GC correctness. The driver is responsible for calling
    // SetObjectReference or equivalent.
    ops->fieldAccessOps->WriteBarrier(ctx, obj, field, newRef);
    ops->fieldAccessOps->OnAfterAccess(ctx, obj, field, true);
}

//=============================================================================
// TDS_GetFieldAddress - Get effective field address
//=============================================================================

void* TDS_GetFieldAddress(Object* obj, FieldDesc* field)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);

    OpsRoot* ops = g_OpsRootTable.Get(obj);
    VContext* ctx = &g_NullContext;

    // Ensure the object is materialized (for lazy/remote objects)
    ops->objectModelOps->EnsureMaterialized(ctx, obj);

    // Try ObjectModel driver first
    void* addr = ops->objectModelOps->GetFieldAddress(ctx, obj, field);
    if (addr != nullptr)
    {
        return addr;
    }

    // Fall back to FieldAccess driver's effective address
    return ops->fieldAccessOps->GetEffectiveAddress(ctx, obj, field);
}
