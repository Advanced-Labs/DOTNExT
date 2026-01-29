// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSQCALLS.H
//
// TypeDriver System (TDS) - QCall Declarations for Managed API

#ifndef _TDS_QCALLS_H_
#define _TDS_QCALLS_H_

#include "qcall.h"

//=============================================================================
// TypeDriverHelper QCalls
//=============================================================================

extern "C" BOOL QCALLTYPE TDSNative_IsNonDefaultRouted(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE TDSNative_EnableNonDefaultRouting(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE TDSNative_DisableNonDefaultRouting(QCall::ObjectHandleOnStack obj);
extern "C" UINT32 QCALLTYPE TDSNative_GetDriverFlags(QCall::ObjectHandleOnStack obj);
extern "C" INT32 QCALLTYPE TDSNative_GetRoutedObjectCount();

//=============================================================================
// VIntrinsics QCalls - Field Access
//=============================================================================

extern "C" INT32 QCALLTYPE TDSNative_ReadInt32Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset);
extern "C" void QCALLTYPE TDSNative_WriteInt32Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, INT32 value);
extern "C" INT64 QCALLTYPE TDSNative_ReadInt64Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset);
extern "C" void QCALLTYPE TDSNative_WriteInt64Field(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, INT64 value);
extern "C" void QCALLTYPE TDSNative_ReadRefField(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, QCall::ObjectHandleOnStack result);
extern "C" void QCALLTYPE TDSNative_WriteRefField(QCall::ObjectHandleOnStack obj, INT32 fieldOffset, QCall::ObjectHandleOnStack value);

//=============================================================================
// VContext QCalls - Phase 2 Context Management
//=============================================================================

// Forward declaration
struct VContext;

extern "C" void* QCALLTYPE TDSContext_Create();
extern "C" void QCALLTYPE TDSContext_Destroy(VContext* ctx);
extern "C" BOOL QCALLTYPE TDSContext_HasTransaction(VContext* ctx);
extern "C" BOOL QCALLTYPE TDSContext_IsWriteTransaction(VContext* ctx);
extern "C" BOOL QCALLTYPE TDSContext_IsDirty(VContext* ctx);
extern "C" UINT32 QCALLTYPE TDSContext_GetFlags(VContext* ctx);
extern "C" void QCALLTYPE TDSContext_SetDirty(VContext* ctx);
extern "C" void QCALLTYPE TDSContext_ClearDirty(VContext* ctx);
extern "C" VContext* QCALLTYPE TDSContext_GetCurrent();
extern "C" void QCALLTYPE TDSContext_Push(VContext* ctx);
extern "C" VContext* QCALLTYPE TDSContext_Pop();

//=============================================================================
// VUID QCalls - Phase 2 Virtual Object Identity
//=============================================================================

extern "C" void QCALLTYPE TDSNative_GenerateVUID(UINT64* outHi, UINT64* outLo);
extern "C" void QCALLTYPE TDSNative_GetObjectVUID(QCall::ObjectHandleOnStack obj, UINT64* outHi, UINT64* outLo);
extern "C" void QCALLTYPE TDSNative_SetObjectVUID(QCall::ObjectHandleOnStack obj, UINT64 hi, UINT64 lo);

//=============================================================================
// Dirty Tracking QCalls - Phase 2 Object Modification Tracking
//=============================================================================

extern "C" void QCALLTYPE TDSNative_MarkDirty(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE TDSNative_ClearDirty(QCall::ObjectHandleOnStack obj);
extern "C" BOOL QCALLTYPE TDSNative_IsObjectDirty(QCall::ObjectHandleOnStack obj);
extern "C" INT32 QCALLTYPE TDSNative_GetDirtyCount();

#endif // _TDS_QCALLS_H_
