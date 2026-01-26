// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// OPSROOTTABLE.CPP
//
// TypeDriver System (TDS) - OpsRoot Side Table Implementation
// Maps SyncBlockIndex -> OpsRoot* for objects with non-default drivers

#include "common.h"
#include "tds/opsroottable.h"
#include "tds/opsroot.h"
#include "syncblk.h"
#include "object.h"

// Global instance
OpsRootTable g_OpsRootTable;

// Note: g_DefaultOpsRoot is defined in defaultdrivers.cpp

//-----------------------------------------------------------------------------
// OpsRootTable Implementation
//-----------------------------------------------------------------------------

void OpsRootTable::Initialize()
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    m_lock.Init(CrstOpsRootTable, CrstFlags(CRST_DEFAULT));
    m_currentGeneration = 1;
}

void OpsRootTable::Destroy()
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

OpsRoot* OpsRootTable::Get(Object* obj)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);

    // Fast path: check the TDS routing bit
    if (!obj->IsTDSNonDefault())
    {
        return &g_DefaultOpsRoot;
    }

    // Get the SyncBlockIndex
    ObjHeader* header = obj->GetHeader();
    DWORD syncBlockIndex = header->GetSyncBlockIndex();

    // No SyncBlock means no custom routing (shouldn't happen if bit is set)
    if (syncBlockIndex == 0)
    {
        return &g_DefaultOpsRoot;
    }

    return GetByIndex(syncBlockIndex);
}

OpsRoot* OpsRootTable::GetByIndex(DWORD syncBlockIndex)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    CrstHolder lock(&m_lock);

    const OpsRootEntry* entry = m_table.LookupPtr(syncBlockIndex);
    if (entry == nullptr)
    {
        return &g_DefaultOpsRoot;
    }

    // Validate generation (safety net for reuse)
    if (entry->generationTag != m_currentGeneration)
    {
        // Stale entry - remove it
        // Note: We're modifying while iterating, but this is safe since we
        // hold the lock and return immediately after
        const_cast<OpsRootTable*>(this)->m_table.Remove(syncBlockIndex);
        return &g_DefaultOpsRoot;
    }

    return entry->ops;
}

void OpsRootTable::Set(Object* obj, OpsRoot* ops)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);
    _ASSERTE(ops != nullptr);

    // Ensure object has a SyncBlock (this may trigger GC)
    // GetSyncBlock() will allocate one if needed
    SyncBlock* syncBlock = obj->GetSyncBlock();
    _ASSERTE(syncBlock != nullptr);

    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
    _ASSERTE(syncBlockIndex != 0);

    {
        CrstHolder lock(&m_lock);

        OpsRootEntry entry;
        entry.syncBlockIndex = syncBlockIndex;
        entry.ops = ops;
        entry.generationTag = m_currentGeneration;

        // Remove existing entry if present, then add new one
        // (SHash doesn't support AddOrReplace for removable tables)
        m_table.Remove(syncBlockIndex);
        m_table.Add(entry);
    }

    // Set the routing bit (thread-safe, uses interlocked operations)
    obj->GetHeader()->SetTDSNonDefault();
}

void OpsRootTable::Remove(Object* obj)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    _ASSERTE(obj != nullptr);

    DWORD syncBlockIndex = obj->GetHeader()->GetSyncBlockIndex();
    if (syncBlockIndex == 0)
    {
        return;  // No SyncBlock, nothing to remove
    }

    RemoveByIndex(syncBlockIndex);

    // Clear the routing bit (thread-safe)
    obj->GetHeader()->ClearTDSNonDefault();
}

void OpsRootTable::RemoveByIndex(DWORD syncBlockIndex)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    CrstHolder lock(&m_lock);
    m_table.Remove(syncBlockIndex);
}

void OpsRootTable::OnSyncBlockRecycled(DWORD syncBlockIndex)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    CrstHolder lock(&m_lock);

    // Remove any stale entry for this index
    m_table.Remove(syncBlockIndex);

    // Increment generation to invalidate any cached lookups
    // Note: This uses a global generation counter for simplicity.
    // A per-index generation would be more precise but adds complexity.
    m_currentGeneration++;

    // Wrap-around protection (very unlikely to hit in practice)
    if (m_currentGeneration == 0)
    {
        m_currentGeneration = 1;
    }
}

void OpsRootTable::EnumerateEntries(EnumerateCallback callback, void* context)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    _ASSERTE(callback != nullptr);

    CrstHolder lock(&m_lock);

    for (auto iter = m_table.Begin(); iter != m_table.End(); ++iter)
    {
        const OpsRootEntry& entry = *iter;
        if (!OpsRootTableTraits::IsNull(entry) && !OpsRootTableTraits::IsDeleted(entry))
        {
            callback(entry.syncBlockIndex, entry.ops, context);
        }
    }
}

size_t OpsRootTable::GetCount()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    CrstHolder lock(&m_lock);
    return m_table.GetCount();
}

bool OpsRootTable::IsEntryValid(DWORD syncBlockIndex)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    CrstHolder lock(&m_lock);

    const OpsRootEntry* entry = m_table.LookupPtr(syncBlockIndex);
    if (entry == nullptr)
    {
        return false;
    }

    return entry->generationTag == m_currentGeneration;
}
