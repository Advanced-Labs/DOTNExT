// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// T08: Phase 1 GC Integration Tests
// Validates that TDS-routed objects behave correctly with GC

using System;
using System.OS;
using Xunit;

namespace TDS.Tests.Phase1
{
    /// <summary>
    /// GC Integration Tests - Verify routed objects work correctly with garbage collection
    /// </summary>
    public class GCIntegrationTests
    {
        [Fact]
        public void RoutedObject_SurvivesGC()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            TestUtils.ForceFullGC();

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void RoutedObject_SurvivesMultipleGCs()
        {
            var obj = new TestObject { IntField = 42, StringField = "test" };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            for (int i = 0; i < 5; i++)
            {
                TestUtils.ForceFullGC();
                Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
                Assert.Equal(42, obj.IntField);
                Assert.Equal("test", obj.StringField);
            }
        }

        [Fact]
        public void RoutedObject_SurvivesCompaction()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Allocate to trigger compaction
            TestUtils.AllocatePressure(50000);
            TestUtils.ForceFullGC();

            // Routing should survive compaction
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void RoutedObject_PromotesToGen1()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Promote to Gen1
            GC.Collect(0, GCCollectionMode.Forced, true);

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void RoutedObject_PromotesToGen2()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Promote through generations
            GC.Collect(0, GCCollectionMode.Forced, true);
            GC.Collect(1, GCCollectionMode.Forced, true);
            TestUtils.ForceFullGC();

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void RoutedObject_ChildSurvivesGC()
        {
            var parent = new TestObject();
            var child = new TestObject { IntField = 42 };

            parent.NestedField = child;
            TypeDriverHelper.EnableNonDefaultRouting(parent);

            WeakReference childWeak = new WeakReference(child);
            child = null;

            TestUtils.ForceFullGC();

            // Child should survive (referenced by parent)
            Assert.True(childWeak.IsAlive);
            Assert.Equal(42, parent.NestedField.IntField);
        }

        [Fact]
        public void RoutedObject_CollectedWhenUnreferenced()
        {
            WeakReference wr;
            {
                var obj = new TestObject { IntField = 99 };
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                wr = new WeakReference(obj);
            }

            // Object should be collectible
            TestUtils.ForceFullGC();

            Assert.False(wr.IsAlive);
        }

        [Fact]
        public void ManyRoutedObjects_SurviveGC()
        {
            var objects = new System.Collections.Generic.List<TestObject>();
            for (int i = 0; i < 100; i++)
            {
                var obj = new TestObject { IntField = i };
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                objects.Add(obj);
            }

            TestUtils.ForceFullGC();

            for (int i = 0; i < 100; i++)
            {
                Assert.True(TypeDriverHelper.IsNonDefaultRouted(objects[i]));
                Assert.Equal(i, objects[i].IntField);
            }
        }

        [Fact]
        public void MixedRoutedAndDefault_SurviveGC()
        {
            var routed = new System.Collections.Generic.List<TestObject>();
            var defaults = new System.Collections.Generic.List<TestObject>();

            for (int i = 0; i < 50; i++)
            {
                var routedObj = new TestObject { IntField = i };
                TypeDriverHelper.EnableNonDefaultRouting(routedObj);
                routed.Add(routedObj);

                var defaultObj = new TestObject { IntField = i + 1000 };
                defaults.Add(defaultObj);
            }

            TestUtils.ForceFullGC();

            for (int i = 0; i < 50; i++)
            {
                Assert.True(TypeDriverHelper.IsNonDefaultRouted(routed[i]));
                Assert.Equal(i, routed[i].IntField);

                Assert.False(TypeDriverHelper.IsNonDefaultRouted(defaults[i]));
                Assert.Equal(i + 1000, defaults[i].IntField);
            }
        }

        [Fact]
        public void RoutedObject_RefFieldsSurviveGC()
        {
            var parent = new TestObject { IntField = 1 };
            var child1 = new TestObject { IntField = 2 };
            var child2 = new TestObject { IntField = 3 };

            parent.NestedField = child1;
            parent.RefField = child2;

            TypeDriverHelper.EnableNonDefaultRouting(parent);
            TypeDriverHelper.EnableNonDefaultRouting(child1);
            // child2 stays default

            TestUtils.ForceFullGC();

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(parent));
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(child1));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(child2));

            Assert.Same(child1, parent.NestedField);
            Assert.Same(child2, parent.RefField);
            Assert.Equal(2, parent.NestedField.IntField);
            Assert.Equal(3, ((TestObject)parent.RefField).IntField);
        }

        [Fact]
        public void RoutedObject_ObjectArraySurvivesGC()
        {
            var obj = new TestObject();
            obj.ObjectArrayField = new TestObject[3];
            obj.ObjectArrayField[0] = new TestObject { IntField = 10 };
            obj.ObjectArrayField[1] = new TestObject { IntField = 20 };
            obj.ObjectArrayField[2] = new TestObject { IntField = 30 };

            TypeDriverHelper.EnableNonDefaultRouting(obj);
            TypeDriverHelper.EnableNonDefaultRouting(obj.ObjectArrayField[1]);

            TestUtils.ForceFullGC();

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.NotNull(obj.ObjectArrayField);
            Assert.Equal(10, obj.ObjectArrayField[0].IntField);
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj.ObjectArrayField[1]));
            Assert.Equal(20, obj.ObjectArrayField[1].IntField);
            Assert.Equal(30, obj.ObjectArrayField[2].IntField);
        }

        [Fact]
        public void DisableRouting_ThenCollect()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            TestUtils.ForceFullGC();
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));

            TypeDriverHelper.DisableNonDefaultRouting(obj);

            TestUtils.ForceFullGC();
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void LargeObject_Routing_SurvivesGC()
        {
            var large = new LargeTestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(large);

            TestUtils.ForceFullGC();

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(large));
            Assert.Equal(42, large.IntField);
            Assert.Equal(100_000, large.LargeArray.Length);
        }
    }
}
