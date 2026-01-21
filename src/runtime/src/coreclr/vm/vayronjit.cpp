// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronjit.cpp
//
// DOTNExT VAYRON Phase 5 Implementation
// JIT helper interception for transparent field access to persistent handles.
//

#include "common.h"
#include "object.h"
#include "syncblk.h"
#include "vayronjit.h"
#include "vayronhandle.h"
#include "fcall.h"
#include "field.h"

//==========================================================================
// Static Member Initialization
//==========================================================================

VayronFieldAccessStats VayronJitSupport::s_Stats = { 0 };
void* VayronJitSupport::s_ManagedMaterializeCallback = NULL;

//==========================================================================
// VayronJitSupport Implementation
//==========================================================================

// Main entry point for VAYRON field access interception
// This is called from the modified JIT_GetFieldAddr helper
void* VayronJitSupport::GetFieldAddr(Object* obj, FieldDesc* pFD)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    InterlockedIncrement64(&s_Stats.TotalFieldAccesses);

    // Null check
    if (obj == NULL)
    {
        InterlockedIncrement64(&s_Stats.NullObjectAccesses);
        return NULL;
    }

    // Check if this is a VAYRON handle
    if (!IsVayronHandle_Fast(obj))
    {
        InterlockedIncrement64(&s_Stats.NonVayronFallbacks);
        // Not a VAYRON handle - return standard field address
        return pFD->GetAddressGuaranteedInHeap(obj);
    }

    // Get field offset
    DWORD fieldOffset = pFD->GetOffset();

    return GetFieldAddrFast(obj, fieldOffset);
}

// Optimized version when we already know it's a VAYRON handle
void* VayronJitSupport::GetFieldAddrFast(Object* obj, DWORD fieldOffset)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // Try to get cached body pointer first (fast path)
    void* cachedPtr = GetCachedBodyPtr(obj);

    if (cachedPtr != NULL && !NeedsMaterialization(obj))
    {
        // Fast path - body is cached and valid
        InterlockedIncrement64(&s_Stats.FastPathHits);
        return GetVayronFieldPtr(cachedPtr, fieldOffset);
    }

    // Slow path - need to materialize
    InterlockedIncrement64(&s_Stats.SlowPathMaterializations);

    void* bodyPtr = RequestMaterialization(obj);
    if (bodyPtr == NULL)
    {
        // Materialization failed - likely no transaction
        InterlockedIncrement64(&s_Stats.TransactionMisses);
        return NULL;
    }

    return GetVayronFieldPtr(bodyPtr, fieldOffset);
}

// Check if body needs materialization (stale check)
BOOL VayronJitSupport::NeedsMaterialization(Object* obj)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // This check requires accessing managed metadata
    // For now, we always return FALSE for simplicity
    // The managed code handles staleness detection
    return FALSE;
}

// Get cached body pointer if available
void* VayronJitSupport::GetCachedBodyPtr(Object* obj)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // This needs to access the managed side table
    // For Phase 5, we implement a basic version that stores
    // the pointer in a reserved field or uses the managed interop

    // The managed VayronMetaTable stores this information
    // We need to call back to managed code to retrieve it
    // For performance, the managed code can update native state

    // Placeholder - in full implementation, this would access
    // a native shadow table or call managed code
    return NULL;
}

// Called after field write to mark handle as dirty
void VayronJitSupport::OnFieldWrite(Object* obj, DWORD fieldOffset)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // Mark the handle as dirty in managed code
    // This triggers dirty tracking for commit
}

// Get current transaction epoch
INT64 VayronJitSupport::GetCurrentTransactionEpoch()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // This needs to access the managed AsyncLocal<VayronTransactionScope>
    // Returns -1 if no transaction
    return -1;
}

// Check if there's an active transaction
BOOL VayronJitSupport::HasActiveTransaction()
{
    return GetCurrentTransactionEpoch() >= 0;
}

// Request managed code to materialize the body
void* VayronJitSupport::RequestMaterialization(Object* obj)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    return CallManagedMaterialize(obj);
}

// Update cached body info after materialization
void VayronJitSupport::UpdateCachedBodyInfo(Object* obj, void* bodyPtr, INT32 bodySize, INT64 epoch)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    // Store in native shadow table for fast access
    // For Phase 5, this is managed by the managed side table
}

// Call managed materialization
void* VayronJitSupport::CallManagedMaterialize(Object* obj)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    if (s_ManagedMaterializeCallback == NULL)
    {
        // Callback not registered - fall back to managed code
        return NULL;
    }

    // Call the registered managed callback
    // The callback signature is: IntPtr MaterializeCallback(object handle)
    typedef void* (*MaterializeFunc)(Object*);
    MaterializeFunc callback = (MaterializeFunc)s_ManagedMaterializeCallback;

    return callback(obj);
}

//==========================================================================
// FCalls Implementation
//==========================================================================

FCIMPL2(void*, VayronJitNative::GetFieldAddr, Object* obj, FieldDesc* pFD)
{
    FCALL_CONTRACT;

    // Handle null
    if (obj == NULL)
    {
        return NULL;
    }

    // Check if VAYRON handle using fast inline check
    if (!IsVayronHandle_Fast(obj))
    {
        // Not a VAYRON handle - standard path
        if (pFD->IsEnCNew())
        {
            HELPER_METHOD_FRAME_BEGIN_RET_1(obj);
            void* result = pFD->GetAddress(obj);
            HELPER_METHOD_FRAME_END();
            return result;
        }
        return pFD->GetAddressGuaranteedInHeap(obj);
    }

    // VAYRON handle - use JIT support
    HELPER_METHOD_FRAME_BEGIN_RET_1(obj);
    void* result = VayronJitSupport::GetFieldAddr(obj, pFD);
    HELPER_METHOD_FRAME_END();

    return result;
}
FCIMPLEND

FCIMPL1(void*, VayronJitNative::GetCachedBodyPtr, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return NULL;
    }

    return VayronJitSupport::GetCachedBodyPtr(obj);
}
FCIMPLEND

FCIMPL1(FC_BOOL_RET, VayronJitNative::NeedsMaterialization, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    FC_RETURN_BOOL(VayronJitSupport::NeedsMaterialization(obj));
}
FCIMPLEND

FCIMPL4(void, VayronJitNative::UpdateCachedBodyInfo, Object* obj, void* bodyPtr, INT32 bodySize, INT64 epoch)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return;
    }

    VayronJitSupport::UpdateCachedBodyInfo(obj, bodyPtr, bodySize, epoch);
}
FCIMPLEND

FCIMPL1(void, VayronJitNative::MarkDirty, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return;
    }

    VayronJitSupport::OnFieldWrite(obj, 0);
}
FCIMPLEND

FCIMPL0(void*, VayronJitNative::GetStats)
{
    FCALL_CONTRACT;
    return VayronJitSupport::GetStats();
}
FCIMPLEND

FCIMPL0(void, VayronJitNative::ResetStats)
{
    FCALL_CONTRACT;
    VayronJitSupport::ResetStats();
}
FCIMPLEND

FCIMPL1(void, VayronJitNative::RegisterMaterializeCallback, void* callback)
{
    FCALL_CONTRACT;
    VayronJitSupport::s_ManagedMaterializeCallback = callback;
}
FCIMPLEND

//==========================================================================
// QCalls Implementation
//==========================================================================

extern "C" void* QCALLTYPE VayronJit_GetFieldAddr(QCall::ObjectHandleOnStack obj, FieldDesc* pFD)
{
    QCALL_CONTRACT;

    void* result = NULL;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = VayronJitSupport::GetFieldAddr(OBJECTREFToObject(objRef), pFD);
    }

    END_QCALL;

    return result;
}

extern "C" void* QCALLTYPE VayronJit_GetCachedBodyPtr(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    void* result = NULL;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = VayronJitSupport::GetCachedBodyPtr(OBJECTREFToObject(objRef));
    }

    END_QCALL;

    return result;
}

extern "C" BOOL QCALLTYPE VayronJit_NeedsMaterialization(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = VayronJitSupport::NeedsMaterialization(OBJECTREFToObject(objRef));
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE VayronJit_UpdateCachedBodyInfo(QCall::ObjectHandleOnStack obj, void* bodyPtr, INT32 bodySize, INT64 epoch)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        VayronJitSupport::UpdateCachedBodyInfo(OBJECTREFToObject(objRef), bodyPtr, bodySize, epoch);
    }

    END_QCALL;
}

extern "C" void QCALLTYPE VayronJit_MarkDirty(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        VayronJitSupport::OnFieldWrite(OBJECTREFToObject(objRef), 0);
    }

    END_QCALL;
}
