// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// T08: Phase 1 Performance Tests
// Baseline performance measurements for TDS infrastructure

using System;
using System.Diagnostics;
using System.OS;
using Xunit;
using Xunit.Abstractions;

namespace TDS.Tests.Phase1
{
    /// <summary>
    /// Performance Tests - Measure TDS overhead and establish baselines
    /// </summary>
    public class PerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public PerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private const int WarmupIterations = 1000;
        private const int MeasureIterations = 1_000_000;

        [Fact]
        public void FieldAccess_DefaultVsRouted_Overhead()
        {
            var defaultObj = new TestObject { IntField = 0 };
            var routedObj = new TestObject { IntField = 0 };
            TypeDriverHelper.EnableNonDefaultRouting(routedObj);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                defaultObj.IntField = i;
                _ = defaultObj.IntField;
                routedObj.IntField = i;
                _ = routedObj.IntField;
            }

            // Measure default
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                defaultObj.IntField = i;
                _ = defaultObj.IntField;
            }
            sw1.Stop();

            // Measure routed
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                routedObj.IntField = i;
                _ = routedObj.IntField;
            }
            sw2.Stop();

            // Report
            double defaultNs = (double)sw1.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            double routedNs = (double)sw2.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            double overhead = routedNs / defaultNs;

            _output.WriteLine($"Default field access: {defaultNs:F2}ns");
            _output.WriteLine($"Routed field access:  {routedNs:F2}ns");
            _output.WriteLine($"Overhead:             {overhead:F2}x");

            // Phase 1 accepts up to 100x overhead for intrinsic path (per spec)
            Assert.True(overhead < 100, $"Overhead too high: {overhead:F2}x (limit: 100x)");
        }

        [Fact]
        public void RefFieldAccess_DefaultVsRouted_Overhead()
        {
            var defaultObj = new TestObject();
            var routedObj = new TestObject();
            var child = new TestObject { IntField = 42 };
            TypeDriverHelper.EnableNonDefaultRouting(routedObj);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                defaultObj.NestedField = child;
                _ = defaultObj.NestedField;
                routedObj.NestedField = child;
                _ = routedObj.NestedField;
            }

            // Measure default
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                defaultObj.NestedField = child;
                _ = defaultObj.NestedField;
            }
            sw1.Stop();

            // Measure routed
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                routedObj.NestedField = child;
                _ = routedObj.NestedField;
            }
            sw2.Stop();

            double defaultNs = (double)sw1.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            double routedNs = (double)sw2.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            double overhead = routedNs / defaultNs;

            _output.WriteLine($"Default ref field access: {defaultNs:F2}ns");
            _output.WriteLine($"Routed ref field access:  {routedNs:F2}ns");
            _output.WriteLine($"Overhead:                 {overhead:F2}x");

            Assert.True(overhead < 100, $"Overhead too high: {overhead:F2}x (limit: 100x)");
        }

        [Fact]
        public void IsNonDefaultRouted_Performance()
        {
            var obj = new TestObject();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = TypeDriverHelper.IsNonDefaultRouted(obj);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                _ = TypeDriverHelper.IsNonDefaultRouted(obj);
            }
            sw.Stop();

            double nsPerCall = (double)sw.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"IsNonDefaultRouted: {nsPerCall:F2}ns/call");

            // Should be very fast (just bit check)
            Assert.True(nsPerCall < 100, $"Too slow: {nsPerCall:F2}ns (limit: 100ns)");
        }

        [Fact]
        public void IsNonDefaultRouted_RoutedObject_Performance()
        {
            var obj = new TestObject();
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = TypeDriverHelper.IsNonDefaultRouted(obj);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                _ = TypeDriverHelper.IsNonDefaultRouted(obj);
            }
            sw.Stop();

            double nsPerCall = (double)sw.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"IsNonDefaultRouted (routed): {nsPerCall:F2}ns/call");

            Assert.True(nsPerCall < 100, $"Too slow: {nsPerCall:F2}ns (limit: 100ns)");
        }

        [Fact]
        public void EnableDisable_Performance()
        {
            var obj = new TestObject();
            const int iterations = 100_000;

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                TypeDriverHelper.DisableNonDefaultRouting(obj);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                TypeDriverHelper.DisableNonDefaultRouting(obj);
            }
            sw.Stop();

            double nsPerPair = (double)sw.ElapsedTicks / iterations * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"Enable+Disable pair: {nsPerPair:F2}ns");

            // No strict limit, just report
        }

        [Fact]
        public void EnableRouting_Performance()
        {
            const int objectCount = 10_000;
            var objects = new TestObject[objectCount];
            for (int i = 0; i < objectCount; i++)
            {
                objects[i] = new TestObject { IntField = i };
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < objectCount; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(objects[i]);
            }
            sw.Stop();

            double nsPerEnable = (double)sw.ElapsedTicks / objectCount * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"EnableNonDefaultRouting: {nsPerEnable:F2}ns/object");
            _output.WriteLine($"Total for {objectCount} objects: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void GetRoutedObjectCount_Performance()
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = TypeDriverHelper.GetRoutedObjectCount();
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                _ = TypeDriverHelper.GetRoutedObjectCount();
            }
            sw.Stop();

            double nsPerCall = (double)sw.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"GetRoutedObjectCount: {nsPerCall:F2}ns/call");
        }

        [Fact]
        public void GetDriverFlags_Performance()
        {
            var obj = new TestObject();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = TypeDriverHelper.GetDriverFlags(obj);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                _ = TypeDriverHelper.GetDriverFlags(obj);
            }
            sw.Stop();

            double nsPerCall = (double)sw.ElapsedTicks / MeasureIterations * 1_000_000_000 / Stopwatch.Frequency;
            _output.WriteLine($"GetDriverFlags: {nsPerCall:F2}ns/call");
        }

        [Fact]
        public void GCWithRoutedObjects_Performance()
        {
            const int objectCount = 10_000;
            var objects = new TestObject[objectCount];

            // Create routed objects
            for (int i = 0; i < objectCount; i++)
            {
                objects[i] = new TestObject { IntField = i };
                TypeDriverHelper.EnableNonDefaultRouting(objects[i]);
            }

            // Measure GC time
            var sw = Stopwatch.StartNew();
            TestUtils.ForceFullGC();
            sw.Stop();

            _output.WriteLine($"GC with {objectCount} routed objects: {sw.ElapsedMilliseconds}ms");

            // Verify all survived
            for (int i = 0; i < objectCount; i++)
            {
                Assert.True(TypeDriverHelper.IsNonDefaultRouted(objects[i]));
            }
        }
    }
}
