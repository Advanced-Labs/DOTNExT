// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: vayronjit.h
//
// DOTNExT VAYRON Phase 5 Implementation
// JIT helper interception for transparent field access to persistent handles.
//
// This file provides the native runtime interface for JIT-optimized field access
// to VAYRON persistent handles. By intercepting JIT helpers, we can transparently
// materialize object bodies from Voron storage without explicit property overhead.
//
// Architecture:
// ┌─────────────────────────────────────────────────────────────────────────┐
// │                       Field Access Flow                                  │
// ├─────────────────────────────────────────────────────────────────────────┤
// │  JIT-compiled code                                                       │
// │      │                                                                   │
// │      ▼                                                                   │
// │  JIT_GetFieldAddr_Vayron (interceptor)                                  │
// │      │                                                                   │
// │      ├─── IsVayronHandle? ───► NO ───► Standard path                    │
// │      │                                                                   │
// │      ▼ YES                                                               │
// │  VayronJitSupport::GetFieldAddr                                         │
// │      │                                                                   │
// │      ├─── Already materialized? ───► Return cached pointer              │
// │      │                                                                   │
// │      ▼ NO                                                                │
// │  Call managed Materialize()                                             │
// │      │                                                                   │
// │      ▼                                                                   │
// │  Return field address in materialized body                              │
// └─────────────────────────────────────────────────────────────────────────┘
//

#ifndef __VAYRONJIT_H__
#define __VAYRONJIT_H__

#include "fcall.h"
#include "object.h"
#include "syncblk.h"
#include "vayronhandle.h"

// Forward declarations
class FieldDesc;

//==========================================================================
// VayronFieldAccessStats
//
// Performance statistics for VAYRON field access operations.
// Used for monitoring and optimization.
//==========================================================================
struct VayronFieldAccessStats
{
    volatile LONG64 TotalFieldAccesses;       // Total field access interceptions
    volatile LONG64 FastPathHits;             // Cache hit - body already materialized
    volatile LONG64 SlowPathMaterializations; // Cache miss - needed to materialize
    volatile LONG64 TransactionMisses;        // No transaction - fallback to managed
    volatile LONG64 NullObjectAccesses;       // Null object handled
    volatile LONG64 NonVayronFallbacks;       // Object not VAYRON - standard path
    volatile LONG64 CacheInvalidations;       // Body cache invalidated (stale)
    volatile LONG64 TotalNanoseconds;         // Total time in VAYRON path

    void Reset()
    {
        InterlockedExchange64(&TotalFieldAccesses, 0);
        InterlockedExchange64(&FastPathHits, 0);
        InterlockedExchange64(&SlowPathMaterializations, 0);
        InterlockedExchange64(&TransactionMisses, 0);
        InterlockedExchange64(&NullObjectAccesses, 0);
        InterlockedExchange64(&NonVayronFallbacks, 0);
        InterlockedExchange64(&CacheInvalidations, 0);
        InterlockedExchange64(&TotalNanoseconds, 0);
    }
};

//==========================================================================
// VayronCachedBodyInfo
//
// Cached body information retrieved from managed side table.
// This struct mirrors the managed VayronMeta layout for interop.
//==========================================================================
struct VayronCachedBodyInfo
{
    UINT64 Oid;              // Object identifier
    INT64 Epoch;             // Transaction epoch when cached
    void* CachedBodyPtr;     // Pointer to cached body data
    INT32 CachedBodySize;    // Size of cached body
    INT32 State;             // Materialization state enum
    BOOL IsPinned;           // Whether body is pinned in memory
};

//==========================================================================
// VayronJitSupport
//
// Native support for JIT-optimized VAYRON field access.
// Provides the bridge between JIT helpers and VAYRON managed code.
//==========================================================================
class VayronJitSupport
{
public:
    //----------------------------------------------------------------------
    // Field Access Interception
    //----------------------------------------------------------------------

    // Main entry point for VAYRON field access
    // Returns pointer to field within materialized body
    static void* GetFieldAddr(Object* obj, FieldDesc* pFD);

    // Optimized version for when we already know it's a VAYRON handle
    static void* GetFieldAddrFast(Object* obj, DWORD fieldOffset);

    // Check if body needs materialization (stale check)
    static BOOL NeedsMaterialization(Object* obj);

    // Get cached body pointer if available
    static void* GetCachedBodyPtr(Object* obj);

    //----------------------------------------------------------------------
    // Write Interception (for dirty tracking)
    //----------------------------------------------------------------------

    // Called after field write to mark handle as dirty
    static void OnFieldWrite(Object* obj, DWORD fieldOffset);

    //----------------------------------------------------------------------
    // Transaction Integration
    //----------------------------------------------------------------------

    // Get current transaction epoch (from AsyncLocal)
    static INT64 GetCurrentTransactionEpoch();

    // Check if there's an active transaction
    static BOOL HasActiveTransaction();

    //----------------------------------------------------------------------
    // Materialization Control
    //----------------------------------------------------------------------

    // Request managed code to materialize the body
    // Returns pointer to body start (after header)
    static void* RequestMaterialization(Object* obj);

    // Called to update cached body info after materialization
    static void UpdateCachedBodyInfo(Object* obj, void* bodyPtr, INT32 bodySize, INT64 epoch);

    //----------------------------------------------------------------------
    // Statistics
    //----------------------------------------------------------------------

    static VayronFieldAccessStats* GetStats() { return &s_Stats; }
    static void ResetStats() { s_Stats.Reset(); }

private:
    static VayronFieldAccessStats s_Stats;

    // Managed callback for materialization
    // This is set during runtime initialization from managed code
    static void* s_ManagedMaterializeCallback;

    // Internal helper to call managed materialization
    static void* CallManagedMaterialize(Object* obj);
};

//==========================================================================
// JIT Helper Functions
//==========================================================================

// FCalls for managed interop
class VayronJitNative
{
public:
    // Get field address with VAYRON interception
    static FCDECL2(void*, GetFieldAddr, Object* obj, FieldDesc* pFD);

    // Get cached body pointer (fast path for managed code)
    static FCDECL1(void*, GetCachedBodyPtr, Object* obj);

    // Check if needs materialization
    static FCDECL1(FC_BOOL_RET, NeedsMaterialization, Object* obj);

    // Update cached body info from managed
    static FCDECL4(void, UpdateCachedBodyInfo, Object* obj, void* bodyPtr, INT32 bodySize, INT64 epoch);

    // Mark handle as dirty after write
    static FCDECL1(void, MarkDirty, Object* obj);

    // Get performance statistics
    static FCDECL0(void*, GetStats);

    // Reset performance statistics
    static FCDECL0(void, ResetStats);

    // Register managed materialization callback
    static FCDECL1(void, RegisterMaterializeCallback, void* callback);
};

// QCall versions for scenarios needing GC transition
extern "C" void* QCALLTYPE VayronJit_GetFieldAddr(QCall::ObjectHandleOnStack obj, FieldDesc* pFD);
extern "C" void* QCALLTYPE VayronJit_GetCachedBodyPtr(QCall::ObjectHandleOnStack obj);
extern "C" BOOL QCALLTYPE VayronJit_NeedsMaterialization(QCall::ObjectHandleOnStack obj);
extern "C" void QCALLTYPE VayronJit_UpdateCachedBodyInfo(QCall::ObjectHandleOnStack obj, void* bodyPtr, INT32 bodySize, INT64 epoch);
extern "C" void QCALLTYPE VayronJit_MarkDirty(QCall::ObjectHandleOnStack obj);

//==========================================================================
// Inline Helpers for Maximum Performance
//==========================================================================

// Fast check if object is VAYRON handle (inline for JIT helper hot path)
FORCEINLINE BOOL IsVayronHandle_Fast(Object* obj)
{
    if (obj == NULL)
        return FALSE;
    return (obj->GetHeader()->GetBits() & BIT_SBLK_IS_VAYRON_HANDLE) != 0;
}

// Get body header size (matches managed BodyHeader.Size)
#define VAYRON_BODY_HEADER_SIZE 8

// Field offset calculation (field data starts after header)
FORCEINLINE void* GetVayronFieldPtr(void* bodyPtr, DWORD fieldOffset)
{
    return (BYTE*)bodyPtr + VAYRON_BODY_HEADER_SIZE + fieldOffset;
}

#endif // __VAYRONJIT_H__
