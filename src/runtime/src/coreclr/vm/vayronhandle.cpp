// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronhandle.cpp
//
// DOTNExT VAYRON Modification
// FCalls and QCalls for VAYRON persistent handle runtime support.
//

#include "common.h"
#include "object.h"
#include "syncblk.h"
#include "vayronhandle.h"
#include "fcall.h"

//==========================================================================
// VAYRON Handle FCalls
//==========================================================================

// IsVayronHandle - Fast check if object is a VAYRON persistent handle
//
// Tests bit 31 (BIT_SBLK_IS_VAYRON_HANDLE) in the object header.
// This is an O(1) operation that compiles to a single bit test instruction.
//
FCIMPL1(FC_BOOL_RET, VayronHandleNative::IsVayronHandle, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    ObjHeader* header = obj->GetHeader();
    FC_RETURN_BOOL(header->IsVayronHandle());
}
FCIMPLEND

// MarkAsVayronHandle - Mark object as a VAYRON persistent handle
//
// Sets bit 31 (BIT_SBLK_IS_VAYRON_HANDLE) in the object header.
// This is called during VayronHandle construction to enable fast classification.
//
// Thread safety: Uses interlocked operations (via SetBit) to ensure
// atomic modification of the sync block value.
//
FCIMPL1(void, VayronHandleNative::MarkAsVayronHandle, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return;
    }

    ObjHeader* header = obj->GetHeader();
    header->MarkAsVayronHandle();
}
FCIMPLEND

// ClearVayronHandle - Clear the VAYRON handle bit
//
// Clears bit 31 in the object header. Used for testing and cleanup.
//
FCIMPL1(void, VayronHandleNative::ClearVayronHandle, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return;
    }

    ObjHeader* header = obj->GetHeader();
    header->ClearVayronHandle();
}
FCIMPLEND

// GetSyncBlockValue - Get the raw sync block value (for debugging)
//
// Returns the raw 32-bit sync block value from the object header.
// Useful for SOS debugging extensions and diagnostics.
//
FCIMPL1(UINT32, VayronHandleNative::GetSyncBlockValue, Object* obj)
{
    FCALL_CONTRACT;

    if (obj == NULL)
    {
        return 0;
    }

    ObjHeader* header = obj->GetHeader();
    return header->GetBits();
}
FCIMPLEND

//==========================================================================
// VAYRON Handle QCalls (for scenarios needing GC transition)
//==========================================================================

extern "C" BOOL QCALLTYPE VayronHandle_IsVayronHandle(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        ObjHeader* header = objRef->GetHeader();
        result = header->IsVayronHandle();
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE VayronHandle_MarkAsVayronHandle(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        ObjHeader* header = objRef->GetHeader();
        header->MarkAsVayronHandle();
    }

    END_QCALL;
}

extern "C" void QCALLTYPE VayronHandle_ClearVayronHandle(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        ObjHeader* header = objRef->GetHeader();
        header->ClearVayronHandle();
    }

    END_QCALL;
}
