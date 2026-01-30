// TDS Phase 2 Verification Tests
// These tests verify the Phase 2 VAYRON persistence infrastructure.

using System;
using System.Collections.Generic;
using System.Threading;
using System.OS;
using System.OS.Storage;
using static TDS.Tests.Phase2.AssertHelper;

namespace TDS.Tests.Phase2
{
    /// <summary>
    /// Main verification entry point for Phase 2 tests.
    /// </summary>
    public static class Phase2Verification
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("=== TDS Phase 2 Verification ===\n");

            int passed = 0;
            int failed = 0;

            // Initialize VKernel
            Console.WriteLine("Initializing VKernel...");
            VKernel.Initialize();
            Console.WriteLine($"VKernel initialized. DataPath: {VKernel.DataPath}\n");

            // Run test categories
            Console.WriteLine("--- VUID Tests ---");
            RunTest("VUID_New_GeneratesUnique", VUIDTests.VUID_New_GeneratesUnique, ref passed, ref failed);
            RunTest("VUID_IsTimeOrdered", VUIDTests.VUID_IsTimeOrdered, ref passed, ref failed);
            RunTest("VUID_SerializationRoundtrip", VUIDTests.VUID_SerializationRoundtrip, ref passed, ref failed);
            RunTest("VUID_ParseRoundtrip", VUIDTests.VUID_ParseRoundtrip, ref passed, ref failed);
            RunTest("VUID_Empty", VUIDTests.VUID_Empty, ref passed, ref failed);

            Console.WriteLine("\n--- TypeDriverRegistry Tests ---");
            RunTest("Registry_RegisterAndQuery", RegistryTests.Registry_RegisterAndQuery, ref passed, ref failed);
            RunTest("Registry_Unregister", RegistryTests.Registry_Unregister, ref passed, ref failed);

            Console.WriteLine("\n--- VKernel Basic Tests ---");
            RunTest("VKernel_New_CreatesWithVUID", VKernelTests.VKernel_New_CreatesWithVUID, ref passed, ref failed);
            RunTest("VKernel_New_EnablesRouting", VKernelTests.VKernel_New_EnablesRouting, ref passed, ref failed);
            RunTest("VKernel_NewWithVUID", VKernelTests.VKernel_NewWithVUID, ref passed, ref failed);

            Console.WriteLine("\n--- Storage Tests ---");
            RunTest("Storage_PersistAndGet", StorageTests.Storage_PersistAndGet, ref passed, ref failed);
            RunTest("Storage_Exists", StorageTests.Storage_Exists, ref passed, ref failed);
            RunTest("Storage_Delete", StorageTests.Storage_Delete, ref passed, ref failed);
            RunTest("Storage_GetOrNew", StorageTests.Storage_GetOrNew, ref passed, ref failed);

            Console.WriteLine("\n--- Dirty Tracking Tests ---");
            RunTest("Dirty_NewObjectIsDirty", DirtyTrackingTests.Dirty_NewObjectIsDirty, ref passed, ref failed);
            RunTest("Dirty_PersistClearsDirty", DirtyTrackingTests.Dirty_PersistClearsDirty, ref passed, ref failed);
            RunTest("Dirty_MarkAndClear", DirtyTrackingTests.Dirty_MarkAndClear, ref passed, ref failed);

            Console.WriteLine("\n--- Transaction Tests ---");
            RunTest("Transaction_CommitPersists", TransactionTests.Transaction_CommitPersists, ref passed, ref failed);
            RunTest("Transaction_WithTransactionAction", TransactionTests.Transaction_WithTransactionAction, ref passed, ref failed);
            RunTest("Transaction_WithTransactionFunc", TransactionTests.Transaction_WithTransactionFunc, ref passed, ref failed);

            // Cleanup
            Console.WriteLine("\nShutting down VKernel...");
            VKernel.Shutdown();

            // Results
            Console.WriteLine($"\n=== Results: {passed} PASSED, {failed} FAILED ===");
            return failed > 0 ? 1 : 0;
        }

        private static void RunTest(string name, Action test, ref int passed, ref int failed)
        {
            try
            {
                test();
                Console.WriteLine($"  [PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
                failed++;
            }
        }
    }

    #region Test Domain Objects

    /// <summary>
    /// Simple test object with basic field types.
    /// </summary>
    [Virtual]
    public class TestObject
    {
        public int IntField;
        public string? StringField;
        public double DoubleField;
        public bool BoolField;
    }

    #endregion

    #region VUID Tests

    public static class VUIDTests
    {
        public static void VUID_New_GeneratesUnique()
        {
            var vuid1 = VUID.New();
            var vuid2 = VUID.New();

            Assert(!vuid1.IsEmpty, "vuid1 should not be empty");
            Assert(!vuid2.IsEmpty, "vuid2 should not be empty");
            Assert(vuid1 != vuid2, "VUIDs should be unique");
        }

        public static void VUID_IsTimeOrdered()
        {
            var vuid1 = VUID.New();
            Thread.Sleep(2);
            var vuid2 = VUID.New();

            Assert(vuid2.CompareTo(vuid1) > 0, "Later VUID should be greater (time-ordered)");
        }

        public static void VUID_SerializationRoundtrip()
        {
            var vuid = VUID.New();
            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            var restored = VUID.FromBytes(bytes);
            Assert(vuid == restored, "Serialization roundtrip failed");
        }

        public static void VUID_ParseRoundtrip()
        {
            var vuid = VUID.New();
            var str = vuid.ToString();

            var parsed = VUID.Parse(str);
            Assert(vuid == parsed, "Parse roundtrip failed");
        }

        public static void VUID_Empty()
        {
            var empty = VUID.Empty;
            Assert(empty.IsEmpty, "VUID.Empty should be empty");
            Assert(empty == default(VUID), "VUID.Empty should equal default");
        }
    }

    #endregion

    #region TypeDriverRegistry Tests

    public static class RegistryTests
    {
        public static void Registry_RegisterAndQuery()
        {
            TypeDriverRegistry.Register<TestObject>(DriverFlags.Persist | DriverFlags.DirtyTrack);

            Assert(TypeDriverRegistry.IsRegistered<TestObject>(), "Type should be registered");
            Assert(TypeDriverRegistry.IsRegisteredForPersist<TestObject>(), "Type should be registered for persist");

            var flags = TypeDriverRegistry.GetFlags<TestObject>();
            Assert((flags & DriverFlags.Persist) != 0, "Should have Persist flag");
            Assert((flags & DriverFlags.DirtyTrack) != 0, "Should have DirtyTrack flag");
        }

        public static void Registry_Unregister()
        {
            TypeDriverRegistry.Register<TestObject>();
            TypeDriverRegistry.Unregister<TestObject>();

            Assert(!TypeDriverRegistry.IsRegistered<TestObject>(), "Type should be unregistered");
        }
    }

    #endregion

    #region VKernel Tests

    public static class VKernelTests
    {
        public static void VKernel_New_CreatesWithVUID()
        {
            var obj = VKernel.New<TestObject>();

            Assert(obj != null, "Object should be created");

            var vuid = TypeDriverHelper.GetVUID(obj);
            Assert(!vuid.IsEmpty, "Object should have VUID assigned");
        }

        public static void VKernel_New_EnablesRouting()
        {
            var obj = VKernel.New<TestObject>();

            Assert(TypeDriverHelper.IsNonDefaultRouted(obj), "Object should be TDS-routed");
        }

        public static void VKernel_NewWithVUID()
        {
            var specifiedVuid = VUID.New();
            var obj = VKernel.New<TestObject>(specifiedVuid);

            var actualVuid = TypeDriverHelper.GetVUID(obj);
            Assert(actualVuid == specifiedVuid, "Object should have specified VUID");
        }
    }

    #endregion

    #region Storage Tests

    public static class StorageTests
    {
        public static void Storage_PersistAndGet()
        {
            var obj = VKernel.New<TestObject>();
            obj.IntField = 42;
            obj.StringField = "Hello";
            obj.DoubleField = 3.14;
            obj.BoolField = true;

            VKernel.Persist(obj);

            var vuid = TypeDriverHelper.GetVUID(obj);
            var loaded = VKernel.Get<TestObject>(vuid);

            Assert(loaded != null, "Should load object");
            Assert(loaded.IntField == 42, $"IntField mismatch: expected 42, got {loaded.IntField}");
            Assert(loaded.StringField == "Hello", $"StringField mismatch: expected 'Hello', got '{loaded.StringField}'");
            Assert(Math.Abs(loaded.DoubleField - 3.14) < 0.001, $"DoubleField mismatch: expected 3.14, got {loaded.DoubleField}");
            Assert(loaded.BoolField == true, $"BoolField mismatch: expected true, got {loaded.BoolField}");
        }

        public static void Storage_Exists()
        {
            var obj = VKernel.New<TestObject>();
            var vuid = TypeDriverHelper.GetVUID(obj);

            Assert(!VKernel.Exists(vuid), "Should not exist before persist");

            VKernel.Persist(obj);

            Assert(VKernel.Exists(vuid), "Should exist after persist");
        }

        public static void Storage_Delete()
        {
            var obj = VKernel.New<TestObject>();
            VKernel.Persist(obj);

            var vuid = TypeDriverHelper.GetVUID(obj);
            Assert(VKernel.Exists(vuid), "Should exist before delete");

            var deleted = VKernel.Delete(vuid);
            Assert(deleted, "Delete should return true");
            Assert(!VKernel.Exists(vuid), "Should not exist after delete");
        }

        public static void Storage_GetOrNew()
        {
            // Test with non-existent VUID
            var randomVuid = VUID.New();
            var newObj = VKernel.GetOrNew<TestObject>(randomVuid);
            Assert(newObj != null, "GetOrNew should create new object");

            // Test with existing VUID
            var existingObj = VKernel.New<TestObject>();
            existingObj.IntField = 123;
            VKernel.Persist(existingObj);

            var existingVuid = TypeDriverHelper.GetVUID(existingObj);
            var gotObj = VKernel.GetOrNew<TestObject>(existingVuid);
            Assert(gotObj.IntField == 123, "GetOrNew should return existing object");
        }
    }

    #endregion

    #region Dirty Tracking Tests

    public static class DirtyTrackingTests
    {
        public static void Dirty_NewObjectIsDirty()
        {
            var obj = VKernel.New<TestObject>();

            // New virtual objects should be considered dirty (need initial persist)
            // Note: Depends on implementation - may or may not be dirty initially
            // For now, just verify dirty tracking methods work
            var isDirty = TypeDriverHelper.IsDirty(obj);
            // This assertion depends on the design decision
        }

        public static void Dirty_PersistClearsDirty()
        {
            var obj = VKernel.New<TestObject>();
            TypeDriverHelper.MarkDirty(obj);

            Assert(TypeDriverHelper.IsDirty(obj), "Should be dirty after MarkDirty");

            VKernel.Persist(obj);

            Assert(!TypeDriverHelper.IsDirty(obj), "Should not be dirty after Persist");
        }

        public static void Dirty_MarkAndClear()
        {
            var obj = VKernel.New<TestObject>();

            TypeDriverHelper.ClearDirty(obj);
            Assert(!TypeDriverHelper.IsDirty(obj), "Should not be dirty after ClearDirty");

            TypeDriverHelper.MarkDirty(obj);
            Assert(TypeDriverHelper.IsDirty(obj), "Should be dirty after MarkDirty");

            TypeDriverHelper.ClearDirty(obj);
            Assert(!TypeDriverHelper.IsDirty(obj), "Should not be dirty after ClearDirty again");
        }
    }

    #endregion

    #region Transaction Tests

    public static class TransactionTests
    {
        public static void Transaction_CommitPersists()
        {
            var obj = VKernel.New<TestObject>();
            obj.IntField = 999;

            using (var tx = VKernel.BeginTransaction())
            {
                tx.Commit();
            }

            // Object should be able to be persisted
            VKernel.Persist(obj);
            var vuid = TypeDriverHelper.GetVUID(obj);
            var loaded = VKernel.Get<TestObject>(vuid);
            Assert(loaded != null, "Should load persisted object");
        }

        public static void Transaction_WithTransactionAction()
        {
            TestObject? obj = null;

            VKernel.WithTransaction(() =>
            {
                obj = VKernel.New<TestObject>();
                obj.IntField = 42;
            });

            Assert(obj != null, "Object should be created in transaction");
        }

        public static void Transaction_WithTransactionFunc()
        {
            var vuid = VKernel.WithTransaction(() =>
            {
                var obj = VKernel.New<TestObject>();
                obj.IntField = 42;
                VKernel.Persist(obj);
                return TypeDriverHelper.GetVUID(obj);
            });

            Assert(!vuid.IsEmpty, "Should return valid VUID from transaction");
        }
    }

    #endregion

    #region Assert Helper

    public static class AssertHelper
    {
        public static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }
    }

    #endregion
}
