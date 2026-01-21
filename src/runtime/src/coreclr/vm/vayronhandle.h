// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronhandle.h
//
// DOTNExT VAYRON Modification
// FCalls for VAYRON persistent handle runtime support.
//
// This file provides the native runtime interface for VAYRON handle classification.
// VAYRON handles use bit 31 (BIT_SBLK_IS_VAYRON_HANDLE) in the object header
// to enable fast O(1) classification without managed code overhead.
//

#ifndef __VAYRONHANDLE_H__
#define __VAYRONHANDLE_H__

#include "fcall.h"
#include "object.h"

// VAYRON Handle Runtime Support
//
// These FCalls expose object header bit manipulation for VAYRON persistent handles.
// The VAYRON bit (bit 31) is used to mark objects that represent handles to
// persistent storage in Voron.
//
// Benefits of runtime-level classification:
// - Single bit test (~1 instruction) vs managed type check
// - Enables future JIT helper interception for transparent field access
// - Supports SOS debugging extensions
//
class VayronHandleNative
{
public:
    // Checks if an object is a VAYRON handle by testing bit 31
    static FCDECL1(FC_BOOL_RET, IsVayronHandle, Object* obj);

    // Marks an object as a VAYRON handle by setting bit 31
    static FCDECL1(void, MarkAsVayronHandle, Object* obj);

    // Clears the VAYRON handle bit (for testing/cleanup)
    static FCDECL1(void, ClearVayronHandle, Object* obj);

    // Gets the raw sync block value (for debugging)
    static FCDECL1(UINT32, GetSyncBlockValue, Object* obj);
};

// QCall versions for use when GC transition is needed
extern "C" BOOL QCALLTYPE VayronHandle_IsVayronHandle(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE VayronHandle_MarkAsVayronHandle(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE VayronHandle_ClearVayronHandle(QCall::ObjectHandleOnStack obj);

#endif // __VAYRONHANDLE_H__
