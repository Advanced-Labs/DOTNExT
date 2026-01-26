// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// OPSROOTTABLE.H
//
// TypeDriver System (TDS) - OpsRoot Side Table
// Maps SyncBlockIndex -> OpsRoot* for objects with non-default drivers
//
// Phase 1 implementation: Uses SyncBlockIndex as the key because it's stable
// across GC compaction (unlike object addresses).

#ifndef _OPSROOTTABLE_H_
#define _OPSROOTTABLE_H_

#include "common.h"
#include "shash.h"
#include "crst.h"

// Forward declarations
struct OpsRoot;
class Object;

//-----------------------------------------------------------------------------
// OpsRootEntry - Entry in the OpsRoot side table
// Contains the OpsRoot pointer and a generation tag for stale detection
//-----------------------------------------------------------------------------
struct OpsRootEntry
{
    DWORD syncBlockIndex;   // Key: SyncBlockIndex of the object
    OpsRoot* ops;           // Value: Pointer to OpsRoot dispatch table
    UINT32 generationTag;   // Safety net: validates entry is not stale
};

//-----------------------------------------------------------------------------
// OpsRootTableTraits - SHash traits for the OpsRoot table
// Uses SyncBlockIndex (DWORD) as the key
//-----------------------------------------------------------------------------
class OpsRootTableTraits : public DefaultSHashTraits<OpsRootEntry>
{
public:
    typedef DWORD key_t;

    static key_t GetKey(const OpsRootEntry& e)
    {
        return e.syncBlockIndex;
    }

    static BOOL Equals(key_t k1, key_t k2)
    {
        return k1 == k2;
    }

    static count_t Hash(key_t k)
    {
        return (count_t)k;
    }

    static OpsRootEntry Null()
    {
        LIMITED_METHOD_CONTRACT;
        OpsRootEntry e;
        e.syncBlockIndex = 0;
        e.ops = nullptr;
        e.generationTag = 0;
        return e;
    }

    static OpsRootEntry Deleted()
    {
        LIMITED_METHOD_CONTRACT;
        OpsRootEntry e;
        e.syncBlockIndex = (DWORD)-1;
        e.ops = nullptr;
        e.generationTag = 0;
        return e;
    }

    static bool IsNull(const OpsRootEntry& e)
    {
        LIMITED_METHOD_CONTRACT;
        return e.syncBlockIndex == 0 && e.ops == nullptr;
    }

    static bool IsDeleted(const OpsRootEntry& e)
    {
        LIMITED_METHOD_CONTRACT;
        return e.syncBlockIndex == (DWORD)-1;
    }
};

//-----------------------------------------------------------------------------
// OpsRootTable - Thread-safe table mapping SyncBlockIndex -> OpsRoot*
//
// This table provides the core mapping for TDS: given an object with the
// TDS routing bit set, lookup its associated OpsRoot dispatch table.
//
// Key design decisions:
// - Uses SyncBlockIndex as key (stable across GC compaction)
// - Generation tag provides safety net for SyncBlock reuse scenarios
// - Thread-safe via CrstExplicitInit
// - Returns g_DefaultOpsRoot for unmarked objects (fast path optimization)
//-----------------------------------------------------------------------------
class OpsRootTable
{
private:
    typedef SHash<OpsRootTableTraits> TableType;

    TableType m_table;
    CrstExplicitInit m_lock;
    UINT32 m_currentGeneration;  // Incremented on recycle events

public:
    // Lifecycle
    void Initialize();
    void Destroy();

    //-------------------------------------------------------------------------
    // Primary accessors
    //-------------------------------------------------------------------------

    // Get OpsRoot for object
    // Returns g_DefaultOpsRoot if object is not TDS-routed or not found
    OpsRoot* Get(Object* obj);

    // Get by SyncBlockIndex directly (for internal use)
    OpsRoot* GetByIndex(DWORD syncBlockIndex);

    // Set OpsRoot for object
    // - Ensures object has a SyncBlock
    // - Sets the TDS routing bit
    // - Stores the association with current generation tag
    void Set(Object* obj, OpsRoot* ops);

    // Remove OpsRoot association
    // - Clears the TDS routing bit
    // - Removes the table entry
    void Remove(Object* obj);

    // Remove by SyncBlockIndex directly
    void RemoveByIndex(DWORD syncBlockIndex);

    //-------------------------------------------------------------------------
    // SyncBlock lifecycle hooks
    //-------------------------------------------------------------------------

    // Called when SyncBlock is recycled
    // Removes any stale entry and increments generation
    void OnSyncBlockRecycled(DWORD syncBlockIndex);

    //-------------------------------------------------------------------------
    // Generation management
    //-------------------------------------------------------------------------

    // Get current generation for validation
    UINT32 GetCurrentGeneration() const
    {
        LIMITED_METHOD_CONTRACT;
        return m_currentGeneration;
    }

    //-------------------------------------------------------------------------
    // Debug/diagnostics
    //-------------------------------------------------------------------------

    // Enumerate all entries (callback receives syncBlockIndex, ops, context)
    typedef void (*EnumerateCallback)(DWORD syncBlockIndex, OpsRoot* ops, void* context);
    void EnumerateEntries(EnumerateCallback callback, void* context);

    // Get count of entries
    size_t GetCount();

    // Validate an entry's generation tag
    bool IsEntryValid(DWORD syncBlockIndex);
};

// Global instance
extern OpsRootTable g_OpsRootTable;

// Default OpsRoot - used for objects without custom drivers
// Will be defined in T04 (Default Drivers)
extern OpsRoot* g_DefaultOpsRoot;

#endif // _OPSROOTTABLE_H_
