// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronsidetable.h
//
// DOTNExT VAYRON Phase 3 Modification
// FCalls/QCalls for VAYRON side table runtime support.
//
// This file provides the native runtime interface for VAYRON side table access.
// The side table stores metadata for VAYRON handles, enabling fast runtime-level
// access to OID, epoch, cached body pointer, and materialization state.
//
// Key operations:
// - GetMetadata: Retrieve metadata pointer for a handle
// - GetCachedBodyPtr: Get native pointer to cached body
// - GetOid: Get the OID for a handle
// - GetState: Get materialization state
//

#ifndef __VAYRONSIDETABLE_H__
#define __VAYRONSIDETABLE_H__

#include "fcall.h"
#include "object.h"

// VAYRON Side Table Runtime Support
//
// These FCalls expose side table access for VAYRON handles.
// The side table uses ConditionalWeakTable on the managed side,
// but these native helpers provide fast paths for common operations.
//
// Phase 3 integration enables:
// - Fast metadata lookup from native code
// - Direct cached body pointer access
// - State machine operations
// - Lifecycle management hooks
//

// Materialization states (must match managed enum)
enum VayronMaterializationState
{
    NotMaterialized = 0,
    Materializing = 1,
    Materialized = 2,
    Dirty = 3,
    Stale = 4
};

// Native representation of VAYRON metadata (for fast access)
struct VayronMetaInfo
{
    INT64  Oid;
    INT64  Epoch;
    void*  CachedBodyPtr;
    INT32  CachedBodySize;
    INT32  State;
    UINT32 TypeToken;
    UINT16 SchemaVersion;
    UINT16 Flags;
};

class VayronSideTableNative
{
public:
    // =========================================================================
    // Metadata Access
    // =========================================================================

    // Gets the OID for a VAYRON handle
    static FCDECL2(FC_BOOL_RET, TryGetOid, Object* obj, INT64* pOid);

    // Gets the materialization state for a handle
    static FCDECL2(FC_BOOL_RET, TryGetState, Object* obj, INT32* pState);

    // Gets the cached body pointer and size
    static FCDECL3(FC_BOOL_RET, TryGetCachedBodyPtr, Object* obj, void** ppBodyPtr, INT32* pBodySize);

    // Gets the epoch for a handle
    static FCDECL2(FC_BOOL_RET, TryGetEpoch, Object* obj, INT64* pEpoch);

    // Gets full metadata info in one call (optimized for bulk access)
    static FCDECL2(FC_BOOL_RET, TryGetMetaInfo, Object* obj, VayronMetaInfo* pInfo);

    // =========================================================================
    // State Management
    // =========================================================================

    // Checks if a state transition is valid
    static FCDECL2(FC_BOOL_RET, IsValidTransition, INT32 fromState, INT32 toState);

    // Checks if body is available (Materialized or Dirty state)
    static FCDECL1(FC_BOOL_RET, IsBodyAvailable, Object* obj);

    // Checks if body needs loading
    static FCDECL1(FC_BOOL_RET, NeedsLoad, Object* obj);

    // =========================================================================
    // Statistics (for monitoring)
    // =========================================================================

    // Gets the total number of active handles
    static FCDECL0(INT32, GetActiveCount);

    // Gets the total bytes tracked
    static FCDECL0(INT64, GetTotalBytesTracked);

    // Gets the cache hit rate (scaled by 10000 for precision)
    static FCDECL0(INT32, GetHitRateScaled);
};

// QCall versions for complex operations requiring GC safety
extern "C" BOOL QCALLTYPE VayronSideTable_TryGetOid(QCall::ObjectHandleOnStack obj, INT64* pOid);
extern "C" BOOL QCALLTYPE VayronSideTable_TryGetState(QCall::ObjectHandleOnStack obj, INT32* pState);
extern "C" BOOL QCALLTYPE VayronSideTable_TryGetCachedBodyPtr(QCall::ObjectHandleOnStack obj, void** ppBodyPtr, INT32* pBodySize);
extern "C" BOOL QCALLTYPE VayronSideTable_TryGetMetaInfo(QCall::ObjectHandleOnStack obj, VayronMetaInfo* pInfo);

#endif // __VAYRONSIDETABLE_H__
