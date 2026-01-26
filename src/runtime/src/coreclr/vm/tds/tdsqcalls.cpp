// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSQCALLS.CPP
//
// TypeDriver System (TDS) - QCall Implementations for Managed API
// Phase 1: Testing and diagnostics

#include "common.h"
#include "tds/tdsinterfaces.h"
#include "tds/opsroot.h"
#include "tds/opsroottable.h"
#include "tds/tdsintrinsics.h"
#include "qcall.h"
#include "object.h"
#include "field.h"

//=============================================================================
// TypeDriverHelper QCalls
//=============================================================================

extern "C" BOOL QCALLTYPE TDSNative_IsNonDefaultRouted(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = OBJECTREFToObject(objRef)->GetHeader()->IsTDSNonDefault() ? TRUE : FALSE;
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE TDSNative_EnableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        // Create default OpsRoot (all default drivers)
        OpsRoot* ops = TDS_CreateOpsRoot(nullptr, nullptr, nullptr, nullptr);
        TDS_SetOpsRoot(OBJECTREFToObject(objRef), ops);
    }

    END_QCALL;
}

extern "C" void QCALLTYPE TDSNative_DisableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        TDS_ClearOpsRoot(OBJECTREFToObject(objRef));
    }

    END_QCALL;
}

extern "C" UINT32 QCALLTYPE TDSNative_GetDriverFlags(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    UINT32 flags = 0;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        OpsRoot* ops = g_OpsRootTable.Get(OBJECTREFToObject(objRef));
        flags = ops->flags;
    }

    END_QCALL;

    return flags;
}

extern "C" INT32 QCALLTYPE TDSNative_GetRoutedObjectCount()
{
    QCALL_CONTRACT;

    INT32 count = 0;

    BEGIN_QCALL;

    count = (INT32)g_OpsRootTable.GetCount();

    END_QCALL;

    return count;
}

//=============================================================================
// VIntrinsics QCalls - Field Access
//=============================================================================

extern "C" INT32 QCALLTYPE TDSNative_ReadInt32Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset)
{
    QCALL_CONTRACT;

    INT32 result = 0;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        // Direct read from object at offset
        // Note: In Phase 1, this doesn't go through TDS routing for simplicity
        // Full TDS routing would require FieldDesc lookup
        void* addr = (BYTE*)pObj + fieldOffset;
        result = *(INT32*)addr;
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE TDSNative_WriteInt32Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, INT32 value)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        void* addr = (BYTE*)pObj + fieldOffset;
        *(INT32*)addr = value;
    }

    END_QCALL;
}

extern "C" INT64 QCALLTYPE TDSNative_ReadInt64Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset)
{
    QCALL_CONTRACT;

    INT64 result = 0;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        void* addr = (BYTE*)pObj + fieldOffset;
        result = *(INT64*)addr;
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE TDSNative_WriteInt64Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, INT64 value)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        void* addr = (BYTE*)pObj + fieldOffset;
        *(INT64*)addr = value;
    }

    END_QCALL;
}

extern "C" void QCALLTYPE TDSNative_ReadRefField(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, QCall::ObjectHandleOnStack result)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        OBJECTREF* addr = (OBJECTREF*)((BYTE*)pObj + fieldOffset);
        result.Set(*addr);
    }

    END_QCALL;
}

extern "C" void QCALLTYPE TDSNative_WriteRefField(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, QCall::ObjectHandleOnStack value)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        Object* pObj = OBJECTREFToObject(objRef);
        OBJECTREF* addr = (OBJECTREF*)((BYTE*)pObj + fieldOffset);
        SetObjectReference(addr, value.Get());
    }

    END_QCALL;
}
