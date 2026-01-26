// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// OPSROOT.H
//
// TypeDriver System (TDS) - OpsRoot Dispatch Table
// The OpsRoot is the per-object dispatch table that points to device
// drivers for various runtime operations.

#ifndef _OPSROOT_H_
#define _OPSROOT_H_

#include "tds/tdsinterfaces.h"

// Forward declarations
class Object;

//=============================================================================
// OpsRoot - Per-object driver dispatch table
//
// Every object with the TDS routing bit set has an associated OpsRoot
// that determines how runtime operations are handled for that object.
// Objects without the routing bit use g_DefaultOpsRoot implicitly.
//=============================================================================
struct OpsRoot
{
    uint32_t version;       // Structure version for ABI compat
    uint32_t flags;         // Driver combination flags

    //-------------------------------------------------------------------------
    // Core devices (never null after initialization)
    //-------------------------------------------------------------------------

    // How the object appears to the runtime (size, layout, GC)
    IObjectModelOps*  objectModelOps;

    // How fields are accessed (read, write, barriers)
    IFieldAccessOps*  fieldAccessOps;

    //-------------------------------------------------------------------------
    // Optional devices (null = capability not present)
    //-------------------------------------------------------------------------

    // Persistence support (Phase 2)
    IStorageOps*      storageOps;

    // Remote/distributed dispatch (Phase 4)
    ICallDispatchOps* callDispatchOps;

    // Reserved for future device types
    void* reserved[8];

    //-------------------------------------------------------------------------
    // Convenience methods
    //-------------------------------------------------------------------------

    inline bool HasStorage() const
    {
        return storageOps != nullptr;
    }

    inline bool HasRemoteDispatch() const
    {
        return callDispatchOps != nullptr;
    }

    inline bool IsPersistent() const
    {
        return (flags & OPSROOT_FLAG_PERSISTENT) != 0;
    }

    inline bool IsDistributed() const
    {
        return (flags & OPSROOT_FLAG_DISTRIBUTED) != 0;
    }
};

//=============================================================================
// OpsRoot flags
//=============================================================================
#define OPSROOT_FLAG_NONE           0x0000
#define OPSROOT_FLAG_PERSISTENT     0x0001  // Object supports persistence
#define OPSROOT_FLAG_DISTRIBUTED    0x0002  // Object may be remote
#define OPSROOT_FLAG_VERSIONED      0x0004  // Object has version tracking
#define OPSROOT_FLAG_READONLY       0x0008  // Object is read-only
#define OPSROOT_FLAG_COMPUTED       0x0010  // Object has computed fields

//=============================================================================
// OpsRoot version
//=============================================================================
#define OPSROOT_VERSION 1

//=============================================================================
// Global instances (defined in T04 Default Drivers)
//=============================================================================

// Default OpsRoot for standard CLR objects
extern OpsRoot g_DefaultOpsRoot;

// Default device implementations (passthrough to CLR)
extern IObjectModelOps g_DefaultObjectModelOps;
extern IFieldAccessOps g_DefaultFieldAccessOps;

//=============================================================================
// TDS management functions
//=============================================================================

// Initialize TDS subsystem (called during EEStartup)
void TDS_Initialize();

// Shutdown TDS subsystem (called during EEShutDown)
void TDS_Shutdown();

// Create a new OpsRoot with specified drivers
// - objectModel: Required, must not be null
// - fieldAccess: Required, must not be null
// - storage: Optional (Phase 2)
// - dispatch: Optional (Phase 4)
// Returns: Allocated OpsRoot, or nullptr on failure
OpsRoot* TDS_CreateOpsRoot(
    IObjectModelOps* objectModel,
    IFieldAccessOps* fieldAccess,
    IStorageOps* storage,
    ICallDispatchOps* dispatch);

// Free an OpsRoot created by TDS_CreateOpsRoot
// Note: Does NOT free the device implementations themselves
void TDS_FreeOpsRoot(OpsRoot* ops);

//=============================================================================
// Inline accessors for performance-critical paths
//=============================================================================

// Get OpsRoot for object
// - Returns g_DefaultOpsRoot if object has no custom routing
// - Uses OpsRootTable for objects with TDS routing bit set
// Declared here, implemented in opsroottable.cpp
inline OpsRoot* TDS_GetOpsRoot(Object* obj);

// Set OpsRoot for object
// - Sets the TDS routing bit
// - Associates ops with object in OpsRootTable
void TDS_SetOpsRoot(Object* obj, OpsRoot* ops);

// Remove OpsRoot association from object
// - Clears the TDS routing bit
// - Removes from OpsRootTable
void TDS_ClearOpsRoot(Object* obj);

#endif // _OPSROOT_H_
