// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// DIRTYSET.H
//
// TypeDriver System (TDS) - Dirty Object Tracking
// Tracks objects that have been modified and need to be persisted.

#ifndef _TDS_DIRTYSET_H_
#define _TDS_DIRTYSET_H_

#include "common.h"
#include "shash.h"
#include "crst.h"

namespace TDS
{
    //=========================================================================
    // DirtyEntry - Entry in the dirty set
    //=========================================================================
    struct DirtyEntry
    {
        DWORD syncBlockIndex;   // Key: SyncBlockIndex of the dirty object
        INT64 dirtyTimestamp;   // When first marked dirty (for ordering)
    };

    //=========================================================================
    // DirtySetTraits - SHash traits for DirtySet
    //=========================================================================
    class DirtySetTraits : public DefaultSHashTraits<DirtyEntry>
    {
    public:
        typedef DWORD key_t;

        static key_t GetKey(const DirtyEntry& e)
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

        static DirtyEntry Null()
        {
            LIMITED_METHOD_CONTRACT;
            DirtyEntry e;
            e.syncBlockIndex = 0;
            e.dirtyTimestamp = 0;
            return e;
        }

        static DirtyEntry Deleted()
        {
            LIMITED_METHOD_CONTRACT;
            DirtyEntry e;
            e.syncBlockIndex = (DWORD)-1;
            e.dirtyTimestamp = 0;
            return e;
        }

        static bool IsNull(const DirtyEntry& e)
        {
            LIMITED_METHOD_CONTRACT;
            return e.syncBlockIndex == 0 && e.dirtyTimestamp == 0;
        }

        static bool IsDeleted(const DirtyEntry& e)
        {
            LIMITED_METHOD_CONTRACT;
            return e.syncBlockIndex == (DWORD)-1;
        }
    };

    //=========================================================================
    // DirtySet - Thread-safe set of dirty objects
    //
    // Tracks objects that have been modified and need to be persisted.
    // Uses SyncBlockIndex as key for GC-stability.
    //=========================================================================
    class DirtySet
    {
    private:
        SHash<DirtySetTraits> m_set;
        CrstExplicitInit m_lock;

    public:
        // Lifecycle
        void Initialize();
        void Destroy();

        //---------------------------------------------------------------------
        // Dirty tracking operations
        //---------------------------------------------------------------------

        // Mark an object as dirty
        void MarkDirty(DWORD syncBlockIndex);

        // Clear dirty state (after successful persist)
        void ClearDirty(DWORD syncBlockIndex);

        // Check if object is dirty
        bool IsDirty(DWORD syncBlockIndex);

        //---------------------------------------------------------------------
        // Bulk operations
        //---------------------------------------------------------------------

        // Get all dirty entries for flush
        // Returns count of entries copied, fills buffer up to maxCount
        size_t GetDirtyEntries(DirtyEntry* buffer, size_t maxCount);

        // Clear all dirty entries (after flush all)
        void ClearAll();

        // Get count of dirty objects
        size_t GetCount();
    };

    // Global dirty set instance
    extern DirtySet g_DirtySet;

    //=========================================================================
    // Helper functions for object-level access
    //=========================================================================

    // Mark object as dirty (by Object*)
    void MarkObjectDirty(class Object* obj);

    // Clear object's dirty state
    void ClearObjectDirty(class Object* obj);

    // Check if object is dirty
    bool IsObjectDirty(class Object* obj);

} // namespace TDS

#endif // _TDS_DIRTYSET_H_
