// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// T08: Test Infrastructure
// Shared test helpers and test object classes for Phase 1 TDS tests

using System;
using System.Collections.Generic;

namespace TDS.Tests.Phase1
{
    /// <summary>
    /// Test helper class with various field types for testing TDS infrastructure.
    /// </summary>
    public class TestObject
    {
        public int IntField;
        public long LongField;
        public double DoubleField;
        public string StringField;
        public object RefField;
        public TestObject NestedField;

        // Array fields for more complex scenarios
        public int[] IntArrayField;
        public TestObject[] ObjectArrayField;
    }

    /// <summary>
    /// Simple value type for testing struct handling.
    /// </summary>
    public struct TestStruct
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Test object with struct field.
    /// </summary>
    public class TestObjectWithStruct
    {
        public TestStruct StructField;
        public int IntField;
    }

    /// <summary>
    /// Large object for LOH testing.
    /// </summary>
    public class LargeTestObject
    {
        public byte[] LargeArray;
        public int IntField;

        public LargeTestObject()
        {
            // Ensure it goes to LOH (>85KB)
            LargeArray = new byte[100_000];
        }
    }

    /// <summary>
    /// Test utilities for TDS tests.
    /// </summary>
    public static class TestUtils
    {
        /// <summary>
        /// Force a full GC and wait for finalization.
        /// </summary>
        public static void ForceFullGC()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        }

        /// <summary>
        /// Allocate memory to trigger GC pressure.
        /// </summary>
        public static void AllocatePressure(int count = 10000)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new byte[1024]);
            }
            list.Clear();
        }

        /// <summary>
        /// Create objects of various generations.
        /// </summary>
        public static (TestObject gen0, TestObject gen1, TestObject gen2) CreateMultiGenObjects()
        {
            // Gen2 object - allocate and promote
            var gen2 = new TestObject { IntField = 2 };
            ForceFullGC();
            ForceFullGC();

            // Gen1 object - allocate and promote once
            var gen1 = new TestObject { IntField = 1 };
            GC.Collect(0, GCCollectionMode.Forced, true);

            // Gen0 object - freshly allocated
            var gen0 = new TestObject { IntField = 0 };

            return (gen0, gen1, gen2);
        }

        /// <summary>
        /// Verify object field values are intact.
        /// </summary>
        public static bool VerifyTestObject(TestObject obj, int expectedInt, string expectedString = null)
        {
            if (obj == null) return false;
            if (obj.IntField != expectedInt) return false;
            if (expectedString != null && obj.StringField != expectedString) return false;
            return true;
        }
    }

    /// <summary>
    /// Test result tracking for stress tests.
    /// </summary>
    public class TestResults
    {
        public int TotalOperations { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public TimeSpan Duration { get; set; }

        public void AddError(string error)
        {
            lock (Errors)
            {
                Errors.Add(error);
                FailureCount++;
            }
        }

        public void AddSuccess()
        {
            System.Threading.Interlocked.Increment(ref SuccessCount);
        }

        public bool AllSucceeded => FailureCount == 0;
    }
}
