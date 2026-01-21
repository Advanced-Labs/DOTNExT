// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronsidetable.cpp
//
// DOTNExT VAYRON Phase 3 Modification
// FCalls and QCalls for VAYRON side table runtime support.
//
// These implementations call back into managed code (VayronMetaTable) for
// actual data access. The native layer provides:
// - Fast path optimization for VAYRON handle detection
// - State machine validation
// - Statistics gathering
//
// Note: The actual side table data resides in managed memory (ConditionalWeakTable).
// Native code accesses it via managed callbacks or cached pointers.
//

#include "common.h"
#include "object.h"
#include "syncblk.h"
#include "vayronsidetable.h"
#include "vayronhandle.h"
#include "fcall.h"

//==========================================================================
// State Transition Tables (Phase 3)
//==========================================================================

// Valid transitions lookup table
// [fromState][toState] = valid (1) or invalid (0)
static const BYTE s_ValidTransitions[5][5] = {
    // To:    NM  MZ  MT  DY  ST
    /* NM */ {1,  1,  1,  1,  0},  // NotMaterialized -> Materializing, Materialized, Dirty
    /* MZ */ {0,  1,  1,  0,  1},  // Materializing -> Materialized, Stale
    /* MT */ {0,  0,  1,  1,  1},  // Materialized -> Dirty, Stale
    /* DY */ {0,  0,  1,  1,  1},  // Dirty -> Materialized, Stale
    /* ST */ {1,  1,  1,  0,  1},  // Stale -> NotMaterialized, Materializing, Materialized
};

//==========================================================================
// Helper Functions
//==========================================================================

// Quick check if object is a VAYRON handle (uses header bit)
FORCEINLINE BOOL IsVayronHandleQuick(Object* obj)
{
    if (obj == NULL)
        return FALSE;

    ObjHeader* header = obj->GetHeader();
    return header->IsVayronHandle();
}

//==========================================================================
// Metadata Access FCalls
//==========================================================================

// NOTE: These FCalls check the VAYRON header bit first, then call back to
// managed code for actual metadata retrieval. In a fully integrated runtime,
// we could cache metadata pointers in a native table for faster access.

FCIMPL2(FC_BOOL_RET, VayronSideTableNative::TryGetOid, Object* obj, INT64* pOid)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj) || pOid == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    // For Phase 3, we rely on managed code for actual metadata access.
    // This FCall serves as a fast-path check before managed call.
    // The managed caller will invoke VayronMetaTable.TryGetOid after this check.
    //
    // In a future optimization (Phase 5+), we could cache the OID in a native
    // side table for direct access.

    *pOid = 0; // Placeholder - actual value retrieved via managed callback
    FC_RETURN_BOOL(TRUE); // Indicates object IS a VAYRON handle
}
FCIMPLEND

FCIMPL2(FC_BOOL_RET, VayronSideTableNative::TryGetState, Object* obj, INT32* pState)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj) || pState == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    *pState = 0; // Placeholder - actual value retrieved via managed callback
    FC_RETURN_BOOL(TRUE);
}
FCIMPLEND

FCIMPL3(FC_BOOL_RET, VayronSideTableNative::TryGetCachedBodyPtr, Object* obj, void** ppBodyPtr, INT32* pBodySize)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj) || ppBodyPtr == NULL || pBodySize == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    *ppBodyPtr = NULL;
    *pBodySize = 0;
    FC_RETURN_BOOL(TRUE);
}
FCIMPLEND

FCIMPL2(FC_BOOL_RET, VayronSideTableNative::TryGetEpoch, Object* obj, INT64* pEpoch)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj) || pEpoch == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    *pEpoch = -1;
    FC_RETURN_BOOL(TRUE);
}
FCIMPLEND

FCIMPL2(FC_BOOL_RET, VayronSideTableNative::TryGetMetaInfo, Object* obj, VayronMetaInfo* pInfo)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj) || pInfo == NULL)
    {
        FC_RETURN_BOOL(FALSE);
    }

    // Zero-initialize the structure
    memset(pInfo, 0, sizeof(VayronMetaInfo));
    pInfo->Epoch = -1;
    pInfo->State = NotMaterialized;

    FC_RETURN_BOOL(TRUE);
}
FCIMPLEND

//==========================================================================
// State Management FCalls
//==========================================================================

FCIMPL2(FC_BOOL_RET, VayronSideTableNative::IsValidTransition, INT32 fromState, INT32 toState)
{
    FCALL_CONTRACT;

    // Validate state values
    if (fromState < 0 || fromState >= 5 || toState < 0 || toState >= 5)
    {
        FC_RETURN_BOOL(FALSE);
    }

    // Same state is always valid
    if (fromState == toState)
    {
        FC_RETURN_BOOL(TRUE);
    }

    FC_RETURN_BOOL(s_ValidTransitions[fromState][toState] != 0);
}
FCIMPLEND

FCIMPL1(FC_BOOL_RET, VayronSideTableNative::IsBodyAvailable, Object* obj)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj))
    {
        FC_RETURN_BOOL(FALSE);
    }

    // Materialized (2) or Dirty (3) means body is available
    // This is a placeholder - actual state comes from managed code
    FC_RETURN_BOOL(FALSE);
}
FCIMPLEND

FCIMPL1(FC_BOOL_RET, VayronSideTableNative::NeedsLoad, Object* obj)
{
    FCALL_CONTRACT;

    if (!IsVayronHandleQuick(obj))
    {
        FC_RETURN_BOOL(FALSE);
    }

    // NotMaterialized (0) or Stale (4) means needs load
    FC_RETURN_BOOL(TRUE);
}
FCIMPLEND

//==========================================================================
// Statistics FCalls
//==========================================================================

// Global statistics (updated via managed callbacks or interop)
static volatile INT32 s_ActiveCount = 0;
static volatile INT64 s_TotalBytesTracked = 0;
static volatile INT64 s_GetCount = 0;
static volatile INT64 s_MissCount = 0;

FCIMPL0(INT32, VayronSideTableNative::GetActiveCount)
{
    FCALL_CONTRACT;
    return s_ActiveCount;
}
FCIMPLEND

FCIMPL0(INT64, VayronSideTableNative::GetTotalBytesTracked)
{
    FCALL_CONTRACT;
    return s_TotalBytesTracked;
}
FCIMPLEND

FCIMPL0(INT32, VayronSideTableNative::GetHitRateScaled)
{
    FCALL_CONTRACT;

    INT64 gets = s_GetCount;
    INT64 misses = s_MissCount;

    if (gets == 0)
        return 10000; // 100% if no gets

    return (INT32)(((gets - misses) * 10000) / gets);
}
FCIMPLEND

//==========================================================================
// QCalls (GC-Safe Operations)
//==========================================================================

extern "C" BOOL QCALLTYPE VayronSideTable_TryGetOid(QCall::ObjectHandleOnStack obj, INT64* pOid)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL && IsVayronHandleQuick(OBJECTREFToObject(objRef)))
    {
        // The managed caller will retrieve the actual OID
        if (pOid != NULL)
        {
            *pOid = 0;
        }
        result = TRUE;
    }

    END_QCALL;

    return result;
}

extern "C" BOOL QCALLTYPE VayronSideTable_TryGetState(QCall::ObjectHandleOnStack obj, INT32* pState)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL && IsVayronHandleQuick(OBJECTREFToObject(objRef)))
    {
        if (pState != NULL)
        {
            *pState = NotMaterialized;
        }
        result = TRUE;
    }

    END_QCALL;

    return result;
}

extern "C" BOOL QCALLTYPE VayronSideTable_TryGetCachedBodyPtr(QCall::ObjectHandleOnStack obj, void** ppBodyPtr, INT32* pBodySize)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL && IsVayronHandleQuick(OBJECTREFToObject(objRef)))
    {
        if (ppBodyPtr != NULL)
        {
            *ppBodyPtr = NULL;
        }
        if (pBodySize != NULL)
        {
            *pBodySize = 0;
        }
        result = TRUE;
    }

    END_QCALL;

    return result;
}

extern "C" BOOL QCALLTYPE VayronSideTable_TryGetMetaInfo(QCall::ObjectHandleOnStack obj, VayronMetaInfo* pInfo)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL && IsVayronHandleQuick(OBJECTREFToObject(objRef)) && pInfo != NULL)
    {
        memset(pInfo, 0, sizeof(VayronMetaInfo));
        pInfo->Epoch = -1;
        pInfo->State = NotMaterialized;
        result = TRUE;
    }

    END_QCALL;

    return result;
}

//==========================================================================
// Statistics Update Functions (Called from managed code)
//==========================================================================

// These can be called via P/Invoke from managed code to update native statistics
extern "C" void QCALLTYPE VayronSideTable_UpdateStatistics(INT32 activeCount, INT64 totalBytes, INT64 getCount, INT64 missCount)
{
    QCALL_CONTRACT_NO_GC_TRANSITION;

    s_ActiveCount = activeCount;
    s_TotalBytesTracked = totalBytes;
    s_GetCount = getCount;
    s_MissCount = missCount;
}
