// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// DIRTYSET.CPP
//
// TypeDriver System (TDS) - Dirty Object Tracking Implementation

#include "common.h"
#include "dirtyset.h"
#include "syncblk.h"
#include "object.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <time.h>
#include <sys/time.h>
#endif

namespace TDS
{
    // Global dirty set instance
    DirtySet g_DirtySet;

    //=========================================================================
    // Platform-specific timestamp for ordering
    //=========================================================================
    static INT64 GetCurrentTimestamp()
    {
#ifdef _WIN32
        return (INT64)GetTickCount64();
#else
        struct timeval tv;
        gettimeofday(&tv, nullptr);
        return (INT64)tv.tv_sec * 1000 + (INT64)tv.tv_usec / 1000;
#endif
    }

    //=========================================================================
    // DirtySet Implementation
    //=========================================================================

    void DirtySet::Initialize()
    {
        CONTRACTL
        {
            THROWS;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        m_lock.Init(CrstTdsDirtySet, CrstFlags(CRST_DEFAULT));
    }

    void DirtySet::Destroy()
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        m_lock.Destroy();
    }

    void DirtySet::MarkDirty(DWORD syncBlockIndex)
    {
        CONTRACTL
        {
            THROWS;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        if (syncBlockIndex == 0)
            return;

        CrstHolder lock(&m_lock);

        // Check if already dirty
        const DirtyEntry* existing = m_set.LookupPtr(syncBlockIndex);
        if (existing != nullptr && !DirtySetTraits::IsDeleted(*existing))
        {
            return;  // Already dirty
        }

        DirtyEntry entry;
        entry.syncBlockIndex = syncBlockIndex;
        entry.dirtyTimestamp = GetCurrentTimestamp();

        m_set.Add(entry);
    }

    void DirtySet::ClearDirty(DWORD syncBlockIndex)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        if (syncBlockIndex == 0)
            return;

        CrstHolder lock(&m_lock);

        DirtyEntry* entry = const_cast<DirtyEntry*>(m_set.LookupPtr(syncBlockIndex));
        if (entry != nullptr && !DirtySetTraits::IsDeleted(*entry))
        {
            m_set.RemovePtr(entry);
        }
    }

    bool DirtySet::IsDirty(DWORD syncBlockIndex)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        if (syncBlockIndex == 0)
            return false;

        CrstHolder lock(&m_lock);

        const DirtyEntry* entry = m_set.LookupPtr(syncBlockIndex);
        return entry != nullptr && !DirtySetTraits::IsDeleted(*entry);
    }

    size_t DirtySet::GetDirtyEntries(DirtyEntry* buffer, size_t maxCount)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        if (buffer == nullptr || maxCount == 0)
            return 0;

        CrstHolder lock(&m_lock);

        size_t count = 0;
        for (auto iter = m_set.Begin(); iter != m_set.End() && count < maxCount; ++iter)
        {
            const DirtyEntry& entry = *iter;
            if (!DirtySetTraits::IsNull(entry) && !DirtySetTraits::IsDeleted(entry))
            {
                buffer[count++] = entry;
            }
        }
        return count;
    }

    void DirtySet::ClearAll()
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        CrstHolder lock(&m_lock);
        m_set.RemoveAll();
    }

    size_t DirtySet::GetCount()
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        CrstHolder lock(&m_lock);
        return m_set.GetCount();
    }

    //=========================================================================
    // Object-level helper functions
    //=========================================================================

    void MarkObjectDirty(Object* obj)
    {
        CONTRACTL
        {
            THROWS;
            GC_NOTRIGGER;
            MODE_COOPERATIVE;
        }
        CONTRACTL_END;

        if (obj == nullptr)
            return;

        ObjHeader* header = obj->GetHeader();
        DWORD syncBlockIndex = header->GetSyncBlockIndex();

        if (syncBlockIndex != 0)
        {
            g_DirtySet.MarkDirty(syncBlockIndex);
        }
    }

    void ClearObjectDirty(Object* obj)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_COOPERATIVE;
        }
        CONTRACTL_END;

        if (obj == nullptr)
            return;

        ObjHeader* header = obj->GetHeader();
        DWORD syncBlockIndex = header->GetSyncBlockIndex();

        if (syncBlockIndex != 0)
        {
            g_DirtySet.ClearDirty(syncBlockIndex);
        }
    }

    bool IsObjectDirty(Object* obj)
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_COOPERATIVE;
        }
        CONTRACTL_END;

        if (obj == nullptr)
            return false;

        ObjHeader* header = obj->GetHeader();
        DWORD syncBlockIndex = header->GetSyncBlockIndex();

        if (syncBlockIndex == 0)
            return false;

        return g_DirtySet.IsDirty(syncBlockIndex);
    }

} // namespace TDS
