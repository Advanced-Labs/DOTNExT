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

#endif // _TDS_QCALLS_H_
