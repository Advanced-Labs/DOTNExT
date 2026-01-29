// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSINTERFACES.H
//
// TypeDriver System (TDS) - Device Interface Definitions
// Defines the abstract contracts for device classes that handle
// object operations in the microkernel architecture.

#ifndef _TDS_INTERFACES_H_
#define _TDS_INTERFACES_H_

#include "common.h"

// Forward declarations
class Object;
class FieldDesc;
class MethodTable;
struct ScanContext;

//=============================================================================
// Version constants for ABI compatibility
//=============================================================================
#define TDS_OBJECTMODEL_VERSION   1
#define TDS_FIELDACCESS_VERSION   1
#define TDS_STORAGE_VERSION       1
#define TDS_CALLDISPATCH_VERSION  1

//=============================================================================
// VContext version constants
//=============================================================================
#define VCONTEXT_VERSION_1  1   // Phase 1 (empty placeholder)
#define VCONTEXT_VERSION_2  2   // Phase 2 (transaction support)
#define VCONTEXT_VERSION    VCONTEXT_VERSION_2

//=============================================================================
// VContext - Execution context for virtual object operations
//
// VContext provides ambient execution information to device drivers.
// Phase 1: Unused (all calls received &g_NullContext)
// Phase 2: Carries transaction handles for persistence operations
// Future: Security principal, activation context, call dispatch context
//=============================================================================
struct VContext
{
    uint32_t version;           // = VCONTEXT_VERSION
    uint32_t flags;             // VCONTEXT_FLAG_* values

    // Phase 2: Transaction state
    void* transaction;          // Voron transaction handle (managed object pointer)
    void* transactionScope;     // Transaction scope marker (for nested transactions)

    // Future phases (reserved)
    void* securityCtx;          // Phase 3+: capability/security principal
    void* activationCtx;        // Phase 4+: distributed activation context

    void* reserved[2];          // Future expansion
};

// VContext flags
#define VCONTEXT_FLAG_NONE          0x0000
#define VCONTEXT_FLAG_INTRANSACTION 0x0001  // Context has active transaction
#define VCONTEXT_FLAG_READONLY      0x0002  // Transaction is read-only
#define VCONTEXT_FLAG_WRITE_TX      0x0004  // Transaction is read-write
#define VCONTEXT_FLAG_DIRTY         0x0008  // Context has dirty objects pending flush

// Global null context (Phase 1 compatibility, non-transactional operations)
extern VContext g_NullContext;

//=============================================================================
// Reference enumeration callback
// Used by IObjectModelOps::ScanRefs to report reference fields to GC
//=============================================================================
typedef void (*TDSRefEnumCallback)(Object** refLocation, ScanContext* sc, void* context);

//=============================================================================
// IObjectModelOps - What an object IS to the runtime
//
// This device defines the fundamental object structure: size, layout,
// reference fields for GC, and type information. The default driver
// delegates to standard CLR object layout.
//=============================================================================
struct IObjectModelOps
{
    uint32_t version;

    // Get total object size in bytes
    // Default: MethodTable::GetBaseSize() + array elements if applicable
    size_t (STDMETHODCALLTYPE *GetSize)(VContext* ctx, Object* obj);

    // Enumerate reference fields for GC
    // Default: Use CGCDesc from MethodTable
    void (STDMETHODCALLTYPE *ScanRefs)(
        VContext* ctx,
        Object* obj,
        TDSRefEnumCallback callback,
        ScanContext* sc,
        void* context);

    // Get direct field address (null = use IFieldAccessOps)
    // Default: Return standard field offset address
    void* (STDMETHODCALLTYPE *GetFieldAddress)(VContext* ctx, Object* obj, FieldDesc* field);

    // Get MethodTable for type information
    // Default: obj->GetMethodTable()
    MethodTable* (STDMETHODCALLTYPE *GetMethodTable)(VContext* ctx, Object* obj);

    // Check if object is valid/materialized
    // Default: Always returns true
    bool (STDMETHODCALLTYPE *IsValid)(VContext* ctx, Object* obj);

    // Prepare object for access (lazy materialization hook)
    // Default: No-op, returns true
    bool (STDMETHODCALLTYPE *EnsureMaterialized)(VContext* ctx, Object* obj);

    // Reserved for future expansion
    void* reserved[4];
};

//=============================================================================
// IFieldAccessOps - Field read/write interception
//
// This device intercepts field access for features like change tracking,
// lazy loading, computed fields, or remote proxies. The default driver
// performs direct memory access.
//=============================================================================
struct IFieldAccessOps
{
    uint32_t version;

    // Read field value into buffer
    // Default: memcpy from field offset
    // Returns: bytes read, or -1 on error
    intptr_t (STDMETHODCALLTYPE *Read)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        void* buffer,
        size_t bufferSize);

    // Write field value from buffer
    // Default: memcpy to field offset
    void (STDMETHODCALLTYPE *Write)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        const void* value,
        size_t valueSize);

    // Write barrier for reference fields
    // CRITICAL: Must call real GC write barrier or equivalent
    // Default: Standard GC write barrier
    void (STDMETHODCALLTYPE *WriteBarrier)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        Object* newRef);

    // Pre-access hook (return true to skip default access)
    // Use case: lazy loading, computed fields
    // Default: Returns false (proceed with normal access)
    bool (STDMETHODCALLTYPE *OnBeforeAccess)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        bool isWrite);

    // Post-access hook
    // Use case: change tracking, logging
    // Default: No-op
    void (STDMETHODCALLTYPE *OnAfterAccess)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field,
        bool isWrite);

    // Get effective field address after hooks
    // Returns direct memory address if applicable, null otherwise
    // Default: Return standard field offset address
    void* (STDMETHODCALLTYPE *GetEffectiveAddress)(
        VContext* ctx,
        Object* obj,
        FieldDesc* field);

    void* reserved[4];
};

//=============================================================================
// IStorageOps - Persistence (Phase 2, interface reserved)
//
// This device handles object persistence: saving to storage, loading
// on demand, transaction support, and dirty tracking.
//=============================================================================
struct IStorageOps
{
    uint32_t version;

    // Persist object to storage, return VUID
    bool (STDMETHODCALLTYPE *Persist)(VContext* ctx, Object* obj, uint64_t* outVuid);

    // Materialize object from storage by VUID
    Object* (STDMETHODCALLTYPE *Materialize)(VContext* ctx, uint64_t vuid, MethodTable* expectedType);

    // Check if object has uncommitted changes
    bool (STDMETHODCALLTYPE *IsDirty)(VContext* ctx, Object* obj);

    // Mark object as having changes
    void (STDMETHODCALLTYPE *MarkDirty)(VContext* ctx, Object* obj);

    // Transaction support
    void* (STDMETHODCALLTYPE *BeginTransaction)(VContext* ctx);
    bool (STDMETHODCALLTYPE *CommitTransaction)(VContext* ctx, void* txHandle);
    void (STDMETHODCALLTYPE *RollbackTransaction)(VContext* ctx, void* txHandle);

    void* reserved[8];
};

//=============================================================================
// ICallDispatchOps - Remote invocation (Phase 4, interface reserved)
//
// This device handles method dispatch for remote/distributed objects:
// marshaling, network transport, and location transparency.
//=============================================================================
struct ICallDispatchOps
{
    uint32_t version;

    // Invoke method on object (potentially remote)
    void* (STDMETHODCALLTYPE *Invoke)(
        VContext* ctx,
        Object* obj,
        void* methodDesc,
        void* args,
        void* returnBuffer);

    // Check if object is local to this process
    bool (STDMETHODCALLTYPE *IsLocal)(VContext* ctx, Object* obj);

    // Get location identifier (node ID, process ID, etc.)
    uint64_t (STDMETHODCALLTYPE *GetLocationId)(VContext* ctx, Object* obj);

    void* reserved[8];
};

#endif // _TDS_INTERFACES_H_
