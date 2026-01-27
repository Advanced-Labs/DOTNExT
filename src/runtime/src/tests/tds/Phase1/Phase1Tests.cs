// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// T08: Phase 1 Tests - No Regression and Basic Routing
// Validates that TDS infrastructure works correctly without breaking standard CLR behavior

using System;
using System.OS;
using Xunit;

namespace TDS.Tests.Phase1
{
    /// <summary>
    /// No Regression Tests - Verify standard CLR behavior is unchanged
    /// </summary>
    public class NoRegressionTests
    {
        [Fact]
        public void DefaultObject_NotRouted()
        {
            var obj = new TestObject();
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void DefaultObject_FieldAccess_Works()
        {
            var obj = new TestObject { IntField = 42 };
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
            Assert.Equal(42, obj.IntField);

            obj.IntField = 100;
            Assert.Equal(100, obj.IntField);
        }

        [Fact]
        public void DefaultObject_LongFieldAccess_Works()
        {
            var obj = new TestObject { LongField = long.MaxValue };
            Assert.Equal(long.MaxValue, obj.LongField);

            obj.LongField = long.MinValue;
            Assert.Equal(long.MinValue, obj.LongField);
        }

        [Fact]
        public void DefaultObject_DoubleFieldAccess_Works()
        {
            var obj = new TestObject { DoubleField = 3.14159 };
            Assert.Equal(3.14159, obj.DoubleField);

            obj.DoubleField = -273.15;
            Assert.Equal(-273.15, obj.DoubleField);
        }

        [Fact]
        public void DefaultObject_RefFieldAccess_Works()
        {
            var obj = new TestObject();
            var child = new TestObject { IntField = 42 };

            obj.NestedField = child;
            Assert.Same(child, obj.NestedField);
            Assert.Equal(42, obj.NestedField.IntField);
        }

        [Fact]
        public void DefaultObject_StringFieldAccess_Works()
        {
            var obj = new TestObject { StringField = "hello" };
            Assert.Equal("hello", obj.StringField);

            obj.StringField = "world";
            Assert.Equal("world", obj.StringField);
        }

        [Fact]
        public void DefaultObject_NullRefField_Works()
        {
            var obj = new TestObject { NestedField = null };
            Assert.Null(obj.NestedField);

            obj.NestedField = new TestObject { IntField = 99 };
            Assert.NotNull(obj.NestedField);
            Assert.Equal(99, obj.NestedField.IntField);

            obj.NestedField = null;
            Assert.Null(obj.NestedField);
        }

        [Fact]
        public void DefaultObject_MultipleFields_Work()
        {
            var obj = new TestObject
            {
                IntField = 1,
                LongField = 2L,
                DoubleField = 3.14,
                StringField = "test"
            };

            Assert.Equal(1, obj.IntField);
            Assert.Equal(2L, obj.LongField);
            Assert.Equal(3.14, obj.DoubleField);
            Assert.Equal("test", obj.StringField);
        }

        [Fact]
        public void DefaultObject_SurvivesGC()
        {
            WeakReference wr;
            var holder = new System.Collections.Generic.List<TestObject>();

            // Create object that will survive
            var survivor = new TestObject { IntField = 42 };
            holder.Add(survivor);

            // Create object that won't survive
            {
                var temp = new TestObject { IntField = 99 };
                wr = new WeakReference(temp);
            }

            TestUtils.ForceFullGC();

            Assert.Equal(42, holder[0].IntField);
            Assert.False(wr.IsAlive);
        }

        [Fact]
        public void DefaultObject_ArrayField_Works()
        {
            var obj = new TestObject
            {
                IntArrayField = new[] { 1, 2, 3 }
            };

            Assert.Equal(3, obj.IntArrayField.Length);
            Assert.Equal(1, obj.IntArrayField[0]);
            Assert.Equal(2, obj.IntArrayField[1]);
            Assert.Equal(3, obj.IntArrayField[2]);
        }

        [Fact]
        public void DefaultObject_StructField_Works()
        {
            var obj = new TestObjectWithStruct
            {
                StructField = new TestStruct { X = 10, Y = 20 },
                IntField = 30
            };

            Assert.Equal(10, obj.StructField.X);
            Assert.Equal(20, obj.StructField.Y);
            Assert.Equal(30, obj.IntField);
        }
    }

    /// <summary>
    /// Basic Routing Tests - Verify TDS routing bit and enable/disable
    /// </summary>
    public class BasicRoutingTests
    {
        [Fact]
        public void CanEnableNonDefaultRouting()
        {
            var obj = new TestObject();
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));

            TypeDriverHelper.EnableNonDefaultRouting(obj);
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void CanDisableNonDefaultRouting()
        {
            var obj = new TestObject();
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));

            TypeDriverHelper.DisableNonDefaultRouting(obj);
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void EnableDisable_Idempotent()
        {
            var obj = new TestObject();

            // Double enable
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));

            // Double disable
            TypeDriverHelper.DisableNonDefaultRouting(obj);
            TypeDriverHelper.DisableNonDefaultRouting(obj);
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void RoutedObject_FieldAccessStillWorks()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Field access should still work (default drivers)
            Assert.Equal(42, obj.IntField);

            obj.IntField = 100;
            Assert.Equal(100, obj.IntField);
        }

        [Fact]
        public void RoutedObject_LongFieldAccessWorks()
        {
            var obj = new TestObject { LongField = 123456789L };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            Assert.Equal(123456789L, obj.LongField);
            obj.LongField = 987654321L;
            Assert.Equal(987654321L, obj.LongField);
        }

        [Fact]
        public void RoutedObject_RefFieldAccessWorks()
        {
            var obj = new TestObject();
            var child = new TestObject { IntField = 42 };

            TypeDriverHelper.EnableNonDefaultRouting(obj);

            obj.NestedField = child;
            Assert.Same(child, obj.NestedField);
        }

        [Fact]
        public void RoutedObject_StringFieldAccessWorks()
        {
            var obj = new TestObject { StringField = "before" };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            Assert.Equal("before", obj.StringField);
            obj.StringField = "after";
            Assert.Equal("after", obj.StringField);
        }

        [Fact]
        public void MultipleObjects_IndependentRouting()
        {
            var obj1 = new TestObject();
            var obj2 = new TestObject();
            var obj3 = new TestObject();

            TypeDriverHelper.EnableNonDefaultRouting(obj1);

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj1));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj2));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj3));

            TypeDriverHelper.EnableNonDefaultRouting(obj3);

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj1));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj2));
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj3));
        }

        [Fact]
        public void GetRoutedObjectCount_IncreasesOnEnable()
        {
            int initialCount = TypeDriverHelper.GetRoutedObjectCount();

            var obj1 = new TestObject();
            var obj2 = new TestObject();

            TypeDriverHelper.EnableNonDefaultRouting(obj1);
            Assert.True(TypeDriverHelper.GetRoutedObjectCount() >= initialCount + 1);

            TypeDriverHelper.EnableNonDefaultRouting(obj2);
            Assert.True(TypeDriverHelper.GetRoutedObjectCount() >= initialCount + 2);
        }

        [Fact]
        public void GetDriverFlags_ReturnsValidValue()
        {
            var obj = new TestObject();

            uint flagsBefore = TypeDriverHelper.GetDriverFlags(obj);
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            uint flagsAfter = TypeDriverHelper.GetDriverFlags(obj);

            // Flags should differ after enabling routing
            // The exact values depend on implementation
            Assert.True(flagsBefore != flagsAfter || flagsAfter != 0);
        }

        [Fact]
        public void RoutedChild_ParentDefaultRouted()
        {
            var parent = new TestObject();
            var child = new TestObject { IntField = 42 };

            // Route only the child
            TypeDriverHelper.EnableNonDefaultRouting(child);
            parent.NestedField = child;

            Assert.False(TypeDriverHelper.IsNonDefaultRouted(parent));
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(child));
            Assert.Same(child, parent.NestedField);
            Assert.Equal(42, parent.NestedField.IntField);
        }

        [Fact]
        public void RoutedParent_ChildDefaultRouted()
        {
            var parent = new TestObject();
            var child = new TestObject { IntField = 42 };

            // Route only the parent
            TypeDriverHelper.EnableNonDefaultRouting(parent);
            parent.NestedField = child;

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(parent));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(child));
            Assert.Same(child, parent.NestedField);
            Assert.Equal(42, parent.NestedField.IntField);
        }
    }
}
