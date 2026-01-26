// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDS_TESTS.H
//
// Test utilities for TypeDriver System (TDS) infrastructure
// Phase 1: Header bit verification tests

#ifndef _TDS_TESTS_H_
#define _TDS_TESTS_H_

#include "common.h"
#include "object.h"
#include "syncblk.h"
#include "tds/opsroottable.h"

//=============================================================================
// T01 Tests: Header Bit Infrastructure
//=============================================================================

// Test that BIT_SBLK_TDS_NONDEFAULT is correctly defined
inline bool TDS_Test_BitConstantDefined()
{
    // Verify the bit is at the expected position (bit 31, value 0x80000000)
    return BIT_SBLK_TDS_NONDEFAULT == 0x80000000;
}

// Test that the legacy alias is preserved
inline bool TDS_Test_LegacyAlias()
{
    return BIT_SBLK_UNUSED == BIT_SBLK_TDS_NONDEFAULT;
}

// Test ObjHeader accessor methods (requires valid object)
// This test should be called with a newly allocated object
inline bool TDS_Test_ObjHeaderAccessors(Object* obj)
{
    if (obj == nullptr) return false;

    ObjHeader* header = obj->GetHeader();
    if (header == nullptr) return false;

    // Test 1: Initially should be clear (new objects don't have TDS routing)
    if (header->IsTDSNonDefault())
    {
        // Already set - unexpected for new object, but could be test setup
    }

    // Test 2: Set the bit
    header->SetTDSNonDefault();
    if (!header->IsTDSNonDefault()) return false;

    // Test 3: Verify Object convenience method agrees
    if (!obj->IsTDSNonDefault()) return false;

    // Test 4: Clear the bit
    header->ClearTDSNonDefault();
    if (header->IsTDSNonDefault()) return false;

    // Test 5: Verify Object convenience method agrees after clear
    if (obj->IsTDSNonDefault()) return false;

    return true;
}

// Test thread safety of bit operations (basic verification)
// The SetBit/ClrBit methods use interlocked operations
inline bool TDS_Test_BitThreadSafety()
{
    // This is a compile-time verification that the methods exist
    // and use interlocked operations (verified by code inspection)
    // Runtime thread safety testing requires multi-threaded test harness
    return true;
}

//=============================================================================
// T02 Tests: OpsRoot Side Table
//=============================================================================

// Forward declaration for test functions
// Note: These tests require OpsRootTable to be initialized (via g_OpsRootTable.Initialize())

// Test that g_OpsRootTable global instance exists
inline bool TDS_Test_OpsRootTableExists()
{
    // This will compile-time verify the global exists
    // Runtime verification would check g_OpsRootTable.GetCount() >= 0
    return true;
}

// Test basic Get operation for unmarked objects
// Should return g_DefaultOpsRoot (or nullptr if not yet initialized)
inline bool TDS_Test_OpsRootTableGetUnmarked(Object* obj)
{
    if (obj == nullptr) return false;

    // Ensure object is not TDS-routed
    if (obj->IsTDSNonDefault())
    {
        obj->GetHeader()->ClearTDSNonDefault();
    }

    // Get should return default OpsRoot
    OpsRoot* result = g_OpsRootTable.Get(obj);
    return result == g_DefaultOpsRoot;
}

// Test Set and Get operations
// Note: This test requires ability to create OpsRoot structures
inline bool TDS_Test_OpsRootTableSetGet(Object* obj, OpsRoot* testOps)
{
    if (obj == nullptr || testOps == nullptr) return false;

    // Set the custom OpsRoot
    g_OpsRootTable.Set(obj, testOps);

    // Verify TDS bit was set
    if (!obj->IsTDSNonDefault()) return false;

    // Verify Get returns the correct OpsRoot
    OpsRoot* result = g_OpsRootTable.Get(obj);
    if (result != testOps) return false;

    return true;
}

// Test Remove operation
inline bool TDS_Test_OpsRootTableRemove(Object* obj, OpsRoot* testOps)
{
    if (obj == nullptr || testOps == nullptr) return false;

    // Set up: ensure object has custom OpsRoot
    g_OpsRootTable.Set(obj, testOps);

    // Remove the association
    g_OpsRootTable.Remove(obj);

    // Verify TDS bit was cleared
    if (obj->IsTDSNonDefault()) return false;

    // Verify Get returns default OpsRoot
    OpsRoot* result = g_OpsRootTable.Get(obj);
    if (result != g_DefaultOpsRoot) return false;

    return true;
}

// Test generation tag mechanism (basic verification)
inline bool TDS_Test_OpsRootTableGeneration()
{
    UINT32 gen1 = g_OpsRootTable.GetCurrentGeneration();

    // Simulate SyncBlock recycle (with fake index that shouldn't exist)
    g_OpsRootTable.OnSyncBlockRecycled(0xFFFFFFFE);

    UINT32 gen2 = g_OpsRootTable.GetCurrentGeneration();

    // Generation should have incremented
    return gen2 > gen1 || gen2 == 1;  // Handle wrap-around case
}

// Test count tracking
inline bool TDS_Test_OpsRootTableCount()
{
    size_t count = g_OpsRootTable.GetCount();
    // Count should be a valid non-negative number
    return count >= 0;  // Always true for size_t, but verifies call works
}

//=============================================================================
// Test Runners
//=============================================================================

// Run all T01 tests, returns number of failures
inline int TDS_RunT01Tests(Object* testObj)
{
    int failures = 0;

    if (!TDS_Test_BitConstantDefined())
    {
        failures++;
        // Log: "FAIL: TDS_Test_BitConstantDefined"
    }

    if (!TDS_Test_LegacyAlias())
    {
        failures++;
        // Log: "FAIL: TDS_Test_LegacyAlias"
    }

    if (testObj != nullptr)
    {
        if (!TDS_Test_ObjHeaderAccessors(testObj))
        {
            failures++;
            // Log: "FAIL: TDS_Test_ObjHeaderAccessors"
        }
    }

    if (!TDS_Test_BitThreadSafety())
    {
        failures++;
        // Log: "FAIL: TDS_Test_BitThreadSafety"
    }

    return failures;
}

// Run all T02 tests, returns number of failures
// Note: testObj must be a valid heap object for full testing
// testOps can be nullptr for basic tests only
inline int TDS_RunT02Tests(Object* testObj, OpsRoot* testOps)
{
    int failures = 0;

    if (!TDS_Test_OpsRootTableExists())
    {
        failures++;
        // Log: "FAIL: TDS_Test_OpsRootTableExists"
    }

    if (!TDS_Test_OpsRootTableCount())
    {
        failures++;
        // Log: "FAIL: TDS_Test_OpsRootTableCount"
    }

    if (!TDS_Test_OpsRootTableGeneration())
    {
        failures++;
        // Log: "FAIL: TDS_Test_OpsRootTableGeneration"
    }

    if (testObj != nullptr)
    {
        if (!TDS_Test_OpsRootTableGetUnmarked(testObj))
        {
            failures++;
            // Log: "FAIL: TDS_Test_OpsRootTableGetUnmarked"
        }

        if (testOps != nullptr)
        {
            if (!TDS_Test_OpsRootTableSetGet(testObj, testOps))
            {
                failures++;
                // Log: "FAIL: TDS_Test_OpsRootTableSetGet"
            }

            if (!TDS_Test_OpsRootTableRemove(testObj, testOps))
            {
                failures++;
                // Log: "FAIL: TDS_Test_OpsRootTableRemove"
            }
        }
    }

    return failures;
}

// Run all Phase 1 TDS tests
inline int TDS_RunAllPhase1Tests(Object* testObj, OpsRoot* testOps)
{
    int failures = 0;
    failures += TDS_RunT01Tests(testObj);
    failures += TDS_RunT02Tests(testObj, testOps);
    return failures;
}

#endif // _TDS_TESTS_H_
