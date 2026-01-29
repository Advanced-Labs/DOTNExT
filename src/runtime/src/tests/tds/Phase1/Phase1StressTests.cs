// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// T08: Phase 1 Stress Tests
// High-load and concurrency tests for TDS infrastructure

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.OS;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TDS.Tests.Phase1
{
    /// <summary>
    /// Stress Tests - Verify TDS handles load and concurrency correctly
    /// </summary>
    public class StressTests
    {
        [Fact]
        public void ConcurrentRouting_NoCorruption()
        {
            var objects = new ConcurrentBag<TestObject>();
            var errors = new ConcurrentBag<string>();

            Parallel.For(0, 1000, i =>
            {
                try
                {
                    var obj = new TestObject { IntField = i };
                    TypeDriverHelper.EnableNonDefaultRouting(obj);
                    objects.Add(obj);

                    // Verify immediately
                    if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                    {
                        errors.Add($"Object {i} not routed after enable");
                    }
                    if (obj.IntField != i)
                    {
                        errors.Add($"Object {i} has wrong IntField: {obj.IntField}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Exception at {i}: {ex.Message}");
                }
            });

            Assert.Empty(errors);
            Assert.Equal(1000, objects.Count);
        }

        [Fact]
        public void RapidEnableDisable_NoCorruption()
        {
            var obj = new TestObject { IntField = 42 };
            var errors = new List<string>();

            for (int i = 0; i < 10000; i++)
            {
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                {
                    errors.Add($"Iteration {i}: Not routed after enable");
                }
                if (obj.IntField != 42)
                {
                    errors.Add($"Iteration {i}: Wrong IntField after enable: {obj.IntField}");
                }

                TypeDriverHelper.DisableNonDefaultRouting(obj);
                if (TypeDriverHelper.IsNonDefaultRouted(obj))
                {
                    errors.Add($"Iteration {i}: Still routed after disable");
                }
                if (obj.IntField != 42)
                {
                    errors.Add($"Iteration {i}: Wrong IntField after disable: {obj.IntField}");
                }
            }

            Assert.Empty(errors);
        }

        [Fact]
        public void GCUnderLoad_NoCorruption()
        {
            var objects = new List<TestObject>();
            var errors = new List<string>();

            for (int round = 0; round < 50; round++)
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
                    if (!TypeDriverHelper.IsNonDefaultRouted(objects[i]))
                    {
                        errors.Add($"Round {round}, Object {i}: Lost routing after GC");
                    }
                }

                // Remove half
                int halfCount = objects.Count / 2;
                objects.RemoveRange(0, halfCount);
            }

            Assert.Empty(errors);
        }

        [Fact]
        public void ConcurrentFieldAccess_RoutedObjects()
        {
            var obj = new TestObject { IntField = 0 };
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            int iterations = 10000;
            int threadCount = 4;
            var tasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        // Read/write cycle
                        int val = obj.IntField;
                        obj.IntField = val + 1;
                    }
                });
            }

            Task.WaitAll(tasks);

            // Final value should be iterations * threadCount
            // (may be less due to race conditions, but no corruption)
            Assert.True(obj.IntField > 0);
            Assert.True(TypeDriverHelper.IsNonDefaultRouted(obj));
        }

        [Fact]
        public void ConcurrentEnableDisable_SameObject()
        {
            var obj = new TestObject { IntField = 42 };
            var errors = new ConcurrentBag<string>();
            int iterations = 5000;

            var enableTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    TypeDriverHelper.EnableNonDefaultRouting(obj);
                    if (obj.IntField != 42)
                    {
                        errors.Add($"Enable iteration {i}: Wrong IntField");
                    }
                }
            });

            var disableTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    TypeDriverHelper.DisableNonDefaultRouting(obj);
                    if (obj.IntField != 42)
                    {
                        errors.Add($"Disable iteration {i}: Wrong IntField");
                    }
                }
            });

            Task.WaitAll(enableTask, disableTask);

            Assert.Empty(errors);
            // IntField should never have been corrupted
            Assert.Equal(42, obj.IntField);
        }

        [Fact]
        public void ManyObjectsWithGCPressure()
        {
            const int objectCount = 1000;
            var routedObjects = new List<TestObject>();
            var errors = new List<string>();

            // Phase 1: Create many routed objects
            for (int i = 0; i < objectCount; i++)
            {
                var obj = new TestObject { IntField = i, StringField = $"obj_{i}" };
                TypeDriverHelper.EnableNonDefaultRouting(obj);
                routedObjects.Add(obj);
            }

            // Phase 2: Apply GC pressure while verifying
            for (int round = 0; round < 10; round++)
            {
                // Create garbage
                TestUtils.AllocatePressure(10000);

                // Verify all objects
                for (int i = 0; i < routedObjects.Count; i++)
                {
                    var obj = routedObjects[i];
                    if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                    {
                        errors.Add($"Round {round}, Obj {i}: Lost routing");
                    }
                    if (obj.IntField != i)
                    {
                        errors.Add($"Round {round}, Obj {i}: Wrong IntField");
                    }
                }

                TestUtils.ForceFullGC();
            }

            Assert.Empty(errors);
        }

        [Fact]
        public void LinkedListOfRoutedObjects()
        {
            const int chainLength = 100;
            var head = new TestObject { IntField = 0 };
            TypeDriverHelper.EnableNonDefaultRouting(head);

            var current = head;
            for (int i = 1; i < chainLength; i++)
            {
                var next = new TestObject { IntField = i };
                TypeDriverHelper.EnableNonDefaultRouting(next);
                current.NestedField = next;
                current = next;
            }

            // GC stress
            for (int round = 0; round < 5; round++)
            {
                TestUtils.AllocatePressure();
                TestUtils.ForceFullGC();

                // Traverse and verify
                current = head;
                int count = 0;
                while (current != null)
                {
                    Assert.True(TypeDriverHelper.IsNonDefaultRouted(current));
                    Assert.Equal(count, current.IntField);
                    current = current.NestedField;
                    count++;
                }
                Assert.Equal(chainLength, count);
            }
        }

        [Fact]
        public void AlternatingRoutedAndDefault()
        {
            const int count = 500;
            var objects = new TestObject[count];
            var errors = new List<string>();

            // Create alternating routed/default objects
            for (int i = 0; i < count; i++)
            {
                objects[i] = new TestObject { IntField = i };
                if (i % 2 == 0)
                {
                    TypeDriverHelper.EnableNonDefaultRouting(objects[i]);
                }
            }

            // GC and verify
            for (int round = 0; round < 10; round++)
            {
                TestUtils.ForceFullGC();

                for (int i = 0; i < count; i++)
                {
                    bool expectedRouted = (i % 2 == 0);
                    bool actualRouted = TypeDriverHelper.IsNonDefaultRouted(objects[i]);

                    if (expectedRouted != actualRouted)
                    {
                        errors.Add($"Round {round}, Obj {i}: Expected routed={expectedRouted}, got {actualRouted}");
                    }
                    if (objects[i].IntField != i)
                    {
                        errors.Add($"Round {round}, Obj {i}: Wrong IntField");
                    }
                }
            }

            Assert.Empty(errors);
        }
    }
}
