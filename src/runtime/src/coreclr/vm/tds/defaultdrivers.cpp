// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// DEFAULTDRIVERS.CPP
//
// TypeDriver System (TDS) - Default Driver Implementations
// These drivers proxy to standard CLR behavior, maintaining backward compatibility.

#include "common.h"
#include "tds/tdsinterfaces.h"
#include "tds/opsroot.h"
#include "tds/opsroottable.h"
#include "tds/dirtyset.h"
#include "object.h"
#include "field.h"
#include "methodtable.h"
#include "gcdesc.h"
#include "gchelpers.h"

//=============================================================================
// Global Context (Phase 2: with transaction fields)
//=============================================================================

VContext g_NullContext = {
    VCONTEXT_VERSION,     // version
    VCONTEXT_FLAG_NONE,   // flags
    nullptr,              // transaction
    nullptr,              // transactionScope
    nullptr,              // securityCtx
    nullptr,              // activationCtx
    { nullptr, nullptr }  // reserved
};

//=============================================================================
// Default ObjectModel Driver - Proxies to standard CLR behavior
//=============================================================================

static size_t STDMETHODCALLTYPE DefaultOM_GetSize(VContext* ctx, Object* obj)
{
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    return obj->GetSize();
}

static void STDMETHODCALLTYPE DefaultOM_ScanRefs(
    VContext* ctx,
    Object* obj,
    TDSRefEnumCallback callback,
    ScanContext* sc,
    void* context)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    UNREFERENCED_PARAMETER(ctx);
    _ASSERTE(obj != nullptr);
    _ASSERTE(callback != nullptr);

    MethodTable* mt = obj->GetMethodTable();
    if (!mt->ContainsGCPointers())
        return;

    // For default objects, the GC already knows how to scan via CGCDesc.
    // This callback interface exists for custom object models that have
    // different layouts. In Phase 1, we don't intercept GC scanning -
    // the standard CLR scanning path handles default objects.
    //
    // If this callback is invoked, it means a custom driver wants to
    // enumerate refs. For default objects, we use the standard mechanism.
    //
    // Note: Full implementation would iterate CGCDesc series here.
    // For Phase 1, we assume GC uses its native scanning for default objects.
    UNREFERENCED_PARAMETER(callback);
    UNREFERENCED_PARAMETER(sc);
    UNREFERENCED_PARAMETER(context);
}

static void* STDMETHODCALLTYPE DefaultOM_GetFieldAddress(
    VContext* ctx, Object* obj, FieldDesc* field)
{
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    return field->GetAddressGuaranteedInHeap(obj);
}

static MethodTable* STDMETHODCALLTYPE DefaultOM_GetMethodTable(
    VContext* ctx, Object* obj)
{
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    return obj->GetMethodTable();
}

static bool STDMETHODCALLTYPE DefaultOM_IsValid(VContext* ctx, Object* obj)
{
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    return obj != nullptr && obj->GetMethodTable() != nullptr;
}

static bool STDMETHODCALLTYPE DefaultOM_EnsureMaterialized(VContext* ctx, Object* obj)
{
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);
    UNREFERENCED_PARAMETER(obj);

    return true;  // Default objects are always materialized
}

// Global default ObjectModel driver
IObjectModelOps g_DefaultObjectModelOps = {
    TDS_OBJECTMODEL_VERSION,
    DefaultOM_GetSize,
    DefaultOM_ScanRefs,
    DefaultOM_GetFieldAddress,
    DefaultOM_GetMethodTable,
    DefaultOM_IsValid,
    DefaultOM_EnsureMaterialized,
    { nullptr, nullptr, nullptr, nullptr }  // reserved
};

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
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(buffer != nullptr);

    void* addr = field->GetAddressGuaranteedInHeap(obj);
    UINT fieldSize = field->GetSize();

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
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    _ASSERTE(value != nullptr);

    void* addr = field->GetAddressGuaranteedInHeap(obj);
    UINT fieldSize = field->GetSize();

    _ASSERTE(valueSize == fieldSize);
    memcpy(addr, value, fieldSize);
}

static void STDMETHODCALLTYPE DefaultFA_WriteBarrier(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    Object* newRef)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    UNREFERENCED_PARAMETER(ctx);
    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);

    OBJECTREF* addr = (OBJECTREF*)field->GetAddressGuaranteedInHeap(obj);

    // Use standard CLR write barrier to maintain GC correctness
    SetObjectReference(addr, ObjectToOBJECTREF(newRef));
}

static bool STDMETHODCALLTYPE DefaultFA_OnBeforeAccess(
    VContext* ctx,
    Object* obj,
    FieldDesc* field,
    bool isWrite)
{
    LIMITED_METHOD_CONTRACT;
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
    LIMITED_METHOD_CONTRACT;
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
    LIMITED_METHOD_CONTRACT;
    UNREFERENCED_PARAMETER(ctx);

    _ASSERTE(obj != nullptr);
    _ASSERTE(field != nullptr);
    return field->GetAddressGuaranteedInHeap(obj);
}

// Global default FieldAccess driver
IFieldAccessOps g_DefaultFieldAccessOps = {
    TDS_FIELDACCESS_VERSION,
    DefaultFA_Read,
    DefaultFA_Write,
    DefaultFA_WriteBarrier,
    DefaultFA_OnBeforeAccess,
    DefaultFA_OnAfterAccess,
    DefaultFA_GetEffectiveAddress,
    { nullptr, nullptr, nullptr, nullptr }  // reserved
};

//=============================================================================
// Default OpsRoot - Used for all non-routed objects
//=============================================================================

OpsRoot g_DefaultOpsRoot = {
    OPSROOT_VERSION,            // version
    OPSROOT_FLAG_NONE,          // flags
    &g_DefaultObjectModelOps,   // objectModelOps
    &g_DefaultFieldAccessOps,   // fieldAccessOps
    nullptr,                    // storageOps (Phase 2)
    nullptr,                    // callDispatchOps (Phase 4)
    { nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr }  // reserved
};

//=============================================================================
// TDS Management Functions
//=============================================================================

void TDS_Initialize()
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    g_OpsRootTable.Initialize();
    TDS::g_DirtySet.Initialize();
}

void TDS_Shutdown()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    TDS::g_DirtySet.Destroy();
    g_OpsRootTable.Destroy();
}

OpsRoot* TDS_CreateOpsRoot(
    IObjectModelOps* objectModel,
    IFieldAccessOps* fieldAccess,
    IStorageOps* storage,
    ICallDispatchOps* dispatch)
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    OpsRoot* ops = new (nothrow) OpsRoot();
    if (ops == nullptr)
        return nullptr;

    ops->version = OPSROOT_VERSION;
    ops->flags = OPSROOT_FLAG_NONE;
    ops->objectModelOps = objectModel ? objectModel : &g_DefaultObjectModelOps;
    ops->fieldAccessOps = fieldAccess ? fieldAccess : &g_DefaultFieldAccessOps;
    ops->storageOps = storage;
    ops->callDispatchOps = dispatch;

    // Initialize reserved slots
    for (int i = 0; i < 8; i++)
    {
        ops->reserved[i] = nullptr;
    }

    // Set flags based on capabilities
    if (storage != nullptr)
        ops->flags |= OPSROOT_FLAG_PERSISTENT;
    if (dispatch != nullptr)
        ops->flags |= OPSROOT_FLAG_DISTRIBUTED;

    return ops;
}

void TDS_FreeOpsRoot(OpsRoot* ops)
{
    LIMITED_METHOD_CONTRACT;

    // Don't free the global default
    if (ops != nullptr && ops != &g_DefaultOpsRoot)
    {
        delete ops;
    }
}

//=============================================================================
// TDS Object Operations (convenience wrappers)
//=============================================================================

void TDS_SetOpsRoot(Object* obj, OpsRoot* ops)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(ops != nullptr);

    g_OpsRootTable.Set(obj, ops);
}

void TDS_ClearOpsRoot(Object* obj)
{
    CONTRACTL
    {
        THROWS;  // g_OpsRootTable.Remove() can throw
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);

    g_OpsRootTable.Remove(obj);
}
