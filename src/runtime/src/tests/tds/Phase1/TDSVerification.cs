// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TDS Phase 1 Verification - Console App for corerun testing
// Run with: corerun TDSVerification.dll

using System;
using System.OS;

namespace TDS.Verification
{
    public class TestObject
    {
        public int IntField;
        public string StringField;
        public TestObject NestedField;
    }

    public class Program
    {
        private static int _passed = 0;
        private static int _failed = 0;

        public static int Main()
        {
            Console.WriteLine("=== TDS Phase 1 Verification ===");
            Console.WriteLine();

            // Basic API tests
            Test_IsNonDefaultRouted_DefaultObject();
            Test_EnableNonDefaultRouting();
            Test_DisableNonDefaultRouting();
            Test_FieldAccess_DefaultObject();
            Test_FieldAccess_RoutedObject();
            Test_RefFieldAccess_RoutedObject();
            Test_MultipleObjects_IndependentRouting();
            Test_GetRoutedObjectCount();
            Test_GC_RoutedObject_Survives();
            Test_EnableDisable_Cycle();

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Passed: {_passed}");
            Console.WriteLine($"Failed: {_failed}");
            Console.WriteLine($"Total:  {_passed + _failed}");
            Console.WriteLine();

            if (_failed > 0)
            {
                Console.WriteLine("VERIFICATION FAILED");
                return 1;
            }

            Console.WriteLine("VERIFICATION PASSED");
            return 100; // Standard success code for runtime tests
        }

        static void Test_IsNonDefaultRouted_DefaultObject()
        {
            Console.Write("Test: IsNonDefaultRouted on default object... ");
            try
            {
                var obj = new TestObject();
                bool result = TypeDriverHelper.IsNonDefaultRouted(obj);
                if (!result)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine($"FAIL (expected false, got {result})");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_EnableNonDefaultRouting()
        {
            Console.Write("Test: EnableNonDefaultRouting... ");
            try
            {
                var obj = new TestObject();
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                bool result = TypeDriverHelper.IsNonDefaultRouted(obj);
                if (result)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine($"FAIL (expected true, got {result})");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_DisableNonDefaultRouting()
        {
            Console.Write("Test: DisableNonDefaultRouting... ");
            try
            {
                var obj = new TestObject();
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                TypeDriverHelper.DisableNonDefaultRouting(obj);
                bool result = TypeDriverHelper.IsNonDefaultRouted(obj);
                if (!result)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine($"FAIL (expected false, got {result})");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_FieldAccess_DefaultObject()
        {
            Console.Write("Test: Field access on default object... ");
            try
            {
                var obj = new TestObject { IntField = 42, StringField = "test" };
                bool pass = (obj.IntField == 42 && obj.StringField == "test");
                obj.IntField = 100;
                obj.StringField = "updated";
                pass = pass && (obj.IntField == 100 && obj.StringField == "updated");

                if (pass)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (field values incorrect)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_FieldAccess_RoutedObject()
        {
            Console.Write("Test: Field access on routed object... ");
            try
            {
                var obj = new TestObject { IntField = 42, StringField = "test" };
                TypeDriverHelper.EnableNonDefaultRouting(obj);

                bool pass = (obj.IntField == 42 && obj.StringField == "test");
                obj.IntField = 100;
                obj.StringField = "updated";
                pass = pass && (obj.IntField == 100 && obj.StringField == "updated");
                pass = pass && TypeDriverHelper.IsNonDefaultRouted(obj);

                if (pass)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (field values incorrect or routing lost)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_RefFieldAccess_RoutedObject()
        {
            Console.Write("Test: Ref field access on routed object... ");
            try
            {
                var parent = new TestObject();
                var child = new TestObject { IntField = 42 };
                TypeDriverHelper.EnableNonDefaultRouting(parent);

                parent.NestedField = child;
                bool pass = (parent.NestedField == child);
                pass = pass && (parent.NestedField.IntField == 42);
                pass = pass && TypeDriverHelper.IsNonDefaultRouted(parent);

                if (pass)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (ref field access incorrect)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_MultipleObjects_IndependentRouting()
        {
            Console.Write("Test: Multiple objects with independent routing... ");
            try
            {
                var obj1 = new TestObject();
                var obj2 = new TestObject();
                var obj3 = new TestObject();

                TypeDriverHelper.EnableNonDefaultRouting(obj1);
                TypeDriverHelper.EnableNonDefaultRouting(obj3);

                bool pass = TypeDriverHelper.IsNonDefaultRouted(obj1);
                pass = pass && !TypeDriverHelper.IsNonDefaultRouted(obj2);
                pass = pass && TypeDriverHelper.IsNonDefaultRouted(obj3);

                if (pass)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (routing not independent)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_GetRoutedObjectCount()
        {
            Console.Write("Test: GetRoutedObjectCount... ");
            try
            {
                int initialCount = TypeDriverHelper.GetRoutedObjectCount();
                var obj = new TestObject();
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                int afterCount = TypeDriverHelper.GetRoutedObjectCount();

                // Count should have increased
                bool pass = (afterCount >= initialCount + 1);

                if (pass)
                {
                    Console.WriteLine($"PASS (count: {initialCount} -> {afterCount})");
                    _passed++;
                }
                else
                {
                    Console.WriteLine($"FAIL (count didn't increase: {initialCount} -> {afterCount})");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_GC_RoutedObject_Survives()
        {
            Console.Write("Test: Routed object survives GC... ");
            try
            {
                var obj = new TestObject { IntField = 42 };
                TypeDriverHelper.EnableNonDefaultRouting(obj);

                // Force GC
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                bool pass = TypeDriverHelper.IsNonDefaultRouted(obj);
                pass = pass && (obj.IntField == 42);

                if (pass)
                {
                    Console.WriteLine("PASS");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (routing or value lost after GC)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }

        static void Test_EnableDisable_Cycle()
        {
            Console.Write("Test: Enable/Disable cycle... ");
            try
            {
                var obj = new TestObject { IntField = 42 };
                bool pass = true;

                for (int i = 0; i < 100; i++)
                {
                    TypeDriverHelper.EnableNonDefaultRouting(obj);
                    if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                    {
                        pass = false;
                        break;
                    }

                    TypeDriverHelper.DisableNonDefaultRouting(obj);
                    if (TypeDriverHelper.IsNonDefaultRouted(obj))
                    {
                        pass = false;
                        break;
                    }

                    if (obj.IntField != 42)
                    {
                        pass = false;
                        break;
                    }
                }

                if (pass)
                {
                    Console.WriteLine("PASS (100 cycles)");
                    _passed++;
                }
                else
                {
                    Console.WriteLine("FAIL (cycle failed)");
                    _failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL (exception: {ex.Message})");
                _failed++;
            }
        }
    }
}
