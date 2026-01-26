# T08: Test Suite

> **Work Package:** WP8
> **Dependencies:** T07 (Managed API Surface)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Validate Phase 1 infrastructure through comprehensive testing:
1. No regression on standard CLR behavior
2. TypeDriver routing works correctly
3. GC handles TDS objects properly
4. Performance acceptable for testing

---

## Naming Convention

| Context | Convention | Example |
|---------|------------|---------|
| Test directory | `tds/` | `src/tests/tds/` |
| C# helper class | `TypeDriverHelper` | `TypeDriverHelper.IsNonDefaultRouted()` |
| Test namespace | `TDS.Tests` | Test organization namespace |

---

## Test Categories

| Category | Purpose | Count |
|----------|---------|-------|
| No Regression | Verify standard objects unchanged | ~10 |
| Basic Routing | TDS bit and OpsRoot association | ~10 |
| GC Integration | Survival, compaction, cleanup | ~10 |
| Driver Dispatch | Field access through drivers | ~10 |
| Stress Tests | Concurrency, load | ~5 |
| Performance | Baseline measurements | ~5 |

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/tests/tds/Phase1Tests.cs` | Main test file |
| `src/tests/tds/Phase1StressTests.cs` | Stress tests |
| `src/tests/tds/Phase1PerfTests.cs` | Performance benchmarks |
| `src/tests/tds/TestInfrastructure.cs` | Test helpers |

---

## Implementation

### Test Infrastructure

**File:** `TestInfrastructure.cs`

```csharp
using System.OS;

namespace TDS.Tests
{
    /// <summary>
    /// Test helper class with various field types.
    /// </summary>
    public class TestObject
    {
        public int IntField;
        public long LongField;
        public double DoubleField;
        public string StringField;
        public object RefField;
        public TestObject NestedField;
    }

    /// <summary>
    /// Custom driver that traces all operations.
    /// </summary>
    public static class TracingDriver
    {
        public static List<string> Log { get; } = new();

        public static void Reset() => Log.Clear();

        // Native registration would hook these to a custom OpsRoot
    }

    /// <summary>
    /// Test utilities.
    /// </summary>
    public static class TestUtils
    {
        public static void ForceFullGC()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        }

        public static void AllocatePressure(int count = 10000)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new byte[1024]);
            }
            list.Clear();
        }
    }
}
```

### No Regression Tests

**File:** `Phase1Tests.cs`

```csharp
using System.OS;

namespace TDS.Tests
{
    public class NoRegressionTests
    {
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
        public void DefaultObject_RefFieldAccess_Works()
        {
            var obj = new TestObject();
            var child = new TestObject { IntField = 42 };

            obj.NestedField = child;
            Assert.Same(child, obj.NestedField);
            Assert.Equal(42, obj.NestedField.IntField);
        }

        [Fact]
        public void DefaultObject_SurvivesGC()
        {
            WeakReference wr;
            var holder = new List<TestObject>();

            // Create object that will survive
            var survivor = new TestObject { IntField = 42 };
            holder.Add(survivor);

            // Create object that won't survive
            {
                var temp = new TestObject { IntField = 99 };
                wr = new WeakReference(temp);
            }

            TestUtils.ForceFullGC();

            Assert.True(holder[0].IntField == 42);
            Assert.False(wr.IsAlive);
        }

        [Fact]
        public void DefaultObject_StringField_Works()
        {
            var obj = new TestObject { StringField = "hello" };
            Assert.Equal("hello", obj.StringField);

            obj.StringField = "world";
            Assert.Equal("world", obj.StringField);
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
    }
}
```

### Basic Routing Tests

```csharp
using System.OS;

namespace TDS.Tests
{
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
        public void RoutedObject_RefFieldAccessWorks()
        {
            var obj = new TestObject();
            var child = new TestObject { IntField = 42 };

            TypeDriverHelper.EnableNonDefaultRouting(obj);

            obj.NestedField = child;
            Assert.Same(child, obj.NestedField);
        }

        [Fact]
        public void MultipleObjects_IndependentRouting()
        {
            var obj1 = new TestObject();
            var obj2 = new TestObject();

            TypeDriverHelper.EnableNonDefaultRouting(obj1);

            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj1));
            Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj2));
        }

        [Fact]
        public void GetRoutedObjectCount_Accurate()
        {
            int initialCount = TypeDriverHelper.GetRoutedObjectCount();

            var obj1 = new TestObject();
            var obj2 = new TestObject();

            TypeDriverHelper.EnableNonDefaultRouting(obj1);
            Assert.Equal(initialCount + 1, TypeDriverHelper.GetRoutedObjectCount());

            TypeDriverHelper.EnableNonDefaultRouting(obj2);
            Assert.Equal(initialCount + 2, TypeDriverHelper.GetRoutedObjectCount());

            TypeDriverHelper.DisableNonDefaultRouting(obj1);
            Assert.Equal(initialCount + 1, TypeDriverHelper.GetRoutedObjectCount());
        }
    }
}
```

### GC Integration Tests

```csharp
using System.OS;

namespace TDS.Tests
{
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
        public void RoutedObject_SurvivesCompaction()
        {
            var obj = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Allocate to trigger compaction
            TestUtils.AllocatePressure();
            TestUtils.ForceFullGC();

            // Routing should survive
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
        public void RoutedObject_CleanedUpOnCollection()
        {
            int countBefore = TypeDriverHelper.GetRoutedObjectCount();

            WeakReference wr;
            {
                var obj = new TestObject();
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                wr = new WeakReference(obj);
            }

            // Object should be collectible
            TestUtils.ForceFullGC();

            Assert.False(wr.IsAlive);
            // Note: Count may not decrease immediately due to generation tag
        }

        [Fact]
        public void ManyRoutedObjects_SurviveGC()
        {
            var objects = new List<TestObject>();
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
    }
}
```

### Stress Tests

**File:** `Phase1StressTests.cs`

```csharp
using System.OS;

namespace TDS.Tests
{
    public class StressTests
    {
        [Fact]
        public void ConcurrentRouting_NoCorruption()
        {
            var objects = new ConcurrentBag<TestObject>();
            var errors = new ConcurrentBag<Exception>();

            Parallel.For(0, 1000, i =>
            {
                try
                {
                    var obj = new TestObject { IntField = i };
                    TypeDriverHelper.EnableNonDefaultRouting(obj);
                    objects.Add(obj);

                    // Verify immediately
                    Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
                    Assert.Equal(i, obj.IntField);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            Assert.Empty(errors);
        }

        [Fact]
        public void RapidEnableDisable_NoCorruption()
        {
            var obj = new TestObject { IntField = 42 };

            for (int i = 0; i < 10000; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
                Assert.Equal(42, obj.IntField);

                TypeDriverHelper.DisableNonDefaultRouting(obj);
                Assert.False(TypeDriverHelper.IsNonDefaultRouted(obj));
                Assert.Equal(42, obj.IntField);
            }
        }

        [Fact]
        public void GCUnderLoad_NoCorruption()
        {
            var objects = new List<TestObject>();

            for (int round = 0; round < 100; round++)
            {
                // Create some routed objects
                for (int i = 0; i < 100; i++)
                {
                    var obj = new TestObject { IntField = round * 100 + i };
                    TypeDriverHelper.EnableNonDefaultRouting(obj);
                    objects.Add(obj);
                }

                // Force GC
                TestUtils.ForceFullGC();

                // Verify all objects
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.True(TypeDriverHelper.IsNonDefaultRouted(objects[i]));
                }

                // Remove half
                objects.RemoveRange(0, objects.Count / 2);
            }
        }
    }
}
```

### Performance Tests

**File:** `Phase1PerfTests.cs`

```csharp
using System.OS;

namespace TDS.Tests
{
    public class PerformanceTests
    {
        private const int Iterations = 1_000_000;

        [Fact]
        public void FieldAccess_DefaultVsRouted_Overhead()
        {
            var defaultObj = new TestObject();
            var routedObj = new TestObject();
            TypeDriverHelper.EnableNonDefaultRouting(routedObj);

            // Warmup
            for (int i = 0; i < 1000; i++)
            {
                defaultObj.IntField = i;
                routedObj.IntField = i;
            }

            // Measure default
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                defaultObj.IntField = i;
                _ = defaultObj.IntField;
            }
            sw1.Stop();

            // Measure routed
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                routedObj.IntField = i;
                _ = routedObj.IntField;
            }
            sw2.Stop();

            // Report
            double defaultNs = (double)sw1.ElapsedTicks / Iterations * 1_000_000_000 / Stopwatch.Frequency;
            double routedNs = (double)sw2.ElapsedTicks / Iterations * 1_000_000_000 / Stopwatch.Frequency;
            double overhead = routedNs / defaultNs;

            Console.WriteLine($"Default: {defaultNs:F1}ns, Routed: {routedNs:F1}ns, Overhead: {overhead:F1}x");

            // Phase 1 accepts up to 10x overhead for intrinsic path
            Assert.True(overhead < 100, $"Overhead too high: {overhead}x");
        }

        [Fact]
        public void IsNonDefaultRouted_Performance()
        {
            var obj = new TestObject();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                _ = TypeDriverHelper.IsNonDefaultRouted(obj);
            }
            sw.Stop();

            double nsPerCall = (double)sw.ElapsedTicks / Iterations * 1_000_000_000 / Stopwatch.Frequency;
            Console.WriteLine($"IsNonDefaultRouted: {nsPerCall:F1}ns");

            // Should be very fast (just bit check)
            Assert.True(nsPerCall < 100, $"Too slow: {nsPerCall}ns");
        }

        [Fact]
        public void EnableDisable_Performance()
        {
            var obj = new TestObject();
            const int iterations = 100_000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                TypeDriverHelper.DisableNonDefaultRouting(obj);
            }
            sw.Stop();

            double nsPerPair = (double)sw.ElapsedTicks / iterations * 1_000_000_000 / Stopwatch.Frequency;
            Console.WriteLine($"Enable+Disable: {nsPerPair:F1}ns");
        }
    }
}
```

---

## Running Tests

### Using dotnet test

```bash
cd src/tests/tds
dotnet test --filter "FullyQualifiedName~Phase1"
```

### Using Core_Root

```bash
# Build runtime first
./build.cmd -subset clr+libs

# Generate Core_Root
src/tests/build.cmd generatelayoutonly

# Run tests with Core_Root
export CORE_ROOT=/path/to/artifacts/tests/coreclr/...
dotnet test --filter "Category=Phase1"
```

---

## Acceptance Criteria

- [ ] All NoRegression tests pass
- [ ] All BasicRouting tests pass
- [ ] All GCIntegration tests pass
- [ ] All StressTests pass without errors
- [ ] Performance overhead < 100x for routed objects (Phase 1 intrinsics)
- [ ] IsNonDefaultRouted check < 100ns
- [ ] No GC corruption under stress
- [ ] No memory leaks (monitored via counters)

---

## References

- Main Doc: Part III SS3.2 WP8
- Main Doc: Part VII (Success Criteria)
- .NET Test Framework documentation
