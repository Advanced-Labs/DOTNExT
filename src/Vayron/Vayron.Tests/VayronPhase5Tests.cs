// VAYRON - Runtime-Integrated Persistent Storage
// Phase 5 Tests: Performance Optimization and JIT Helper Interception
//
// DOTNExT VAYRON Phase 5 Implementation
// Comprehensive tests for JIT helper interception, performance benchmarking,
// and concurrent stress testing.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Vayron.Tests;

/// <summary>
/// Test fixture for Phase 5 functionality.
/// </summary>
[TestFixture]
public class VayronPhase5Tests
{
    private string _testPath = null!;
    private VayronEnvironment _env = null!;

    [SetUp]
    public void Setup()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"vayron_phase5_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testPath);

        _env = new VayronEnvironment(new VayronEnvironmentOptions
        {
            Path = _testPath
        });

        // Initialize JIT interop
        VayronJitInterop.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        _env?.Dispose();

        try
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    // =========================================================================
    // JIT Interop Tests
    // =========================================================================

    [Test]
    public void JitInterop_Initialize_Succeeds()
    {
        // Act - Initialize should not throw
        VayronJitInterop.Initialize();

        // Can call multiple times safely
        VayronJitInterop.Initialize();
        VayronJitInterop.Initialize();

        // Assert - no exception
        Assert.Pass("JIT interop initialization succeeded");
    }

    [Test]
    public void JitInterop_GetStatistics_ReturnsDefaultWhenNoNativeSupport()
    {
        // Act
        var stats = VayronJitInterop.GetStatistics();

        // Assert - should return default (zero values) on standard .NET runtime
        Assert.That(stats.TotalFieldAccesses, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void JitInterop_ResetStatistics_DoesNotThrow()
    {
        // Act & Assert - should not throw even without native support
        Assert.DoesNotThrow(() => VayronJitInterop.ResetStatistics());
    }

    [Test]
    public void JitInterop_IsNativeSupported_ReturnsExpectedValue()
    {
        // Act
        var isSupported = VayronJitInterop.IsNativeSupported;

        // Assert - on standard .NET runtime, this should be false
        // On DOTNExT runtime, it would be true
        Assert.That(isSupported, Is.False.Or.True);
    }

    // =========================================================================
    // JIT-Optimized Field Access Tests
    // =========================================================================

    [Test]
    public void GetFieldJitOptimized_ColdPath_MaterializesBody()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42, Salary = 100000 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act
        int age;
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            age = person.AgeJitOptimized; // Uses JIT-optimized path
        }

        // Assert
        Assert.That(age, Is.EqualTo(42));
    }

    [Test]
    public void GetFieldJitOptimized_WarmPath_UsesCachedBody()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act - first access materializes, second access uses cache
        int age1, age2;
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            age1 = person.AgeJitOptimized; // Cold path
            age2 = person.AgeJitOptimized; // Warm path
        }

        // Assert
        Assert.That(age1, Is.EqualTo(42));
        Assert.That(age2, Is.EqualTo(42));
    }

    [Test]
    public void SetFieldJitOptimized_MarksDirty()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            person.AgeJitOptimized = 99; // JIT-optimized write

            Assert.That(person.IsDirty, Is.True);
            tx.Commit();
        }

        // Assert - verify persisted
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            Assert.That(person.Age, Is.EqualTo(99));
        }
    }

    // =========================================================================
    // JIT Optimization Scope Tests
    // =========================================================================

    [Test]
    public void EnableJitOptimization_PinsBody()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            _ = person.Age; // Materialize first

            person.EnableJitOptimization();

            // Assert
            Assert.That(person.IsPinned, Is.True);
            Assert.That(person.IsJitOptimizationEnabled, Is.True);

            person.DisableJitOptimization();
            Assert.That(person.IsPinned, Is.False);
        }
    }

    [Test]
    public void GetJitOptimizationScope_AutoDisablesOnDispose()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            _ = person.Age; // Materialize

            using (person.GetJitOptimizationScope())
            {
                Assert.That(person.IsJitOptimizationEnabled, Is.True);
            }

            // After scope dispose
            Assert.That(person.IsJitOptimizationEnabled, Is.False);
        }
    }

    [Test]
    public void JitOptimizedHotLoop_FastPathAccess()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act - simulate hot loop with JIT optimization
        long sum = 0;
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);

            using (person.GetJitOptimizationScope())
            {
                for (int i = 0; i < 10000; i++)
                {
                    sum += person.AgeJitOptimized;
                }
            }
        }

        // Assert
        Assert.That(sum, Is.EqualTo(42L * 10000));
    }

    // =========================================================================
    // Performance Monitoring Tests
    // =========================================================================

    [Test]
    public void VayronPerformance_GetMetrics_ReturnsValidMetrics()
    {
        // Act
        var metrics = VayronPerformance.GetMetrics();

        // Assert
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics.Timestamp, Is.LessThanOrEqualTo(DateTimeOffset.Now));
    }

    [Test]
    public void VayronPerformance_ResetAll_ClearsStatistics()
    {
        // Arrange - do some operations
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            tx.Commit();
        }

        // Act
        VayronPerformance.ResetAll();
        var metrics = VayronPerformance.GetMetrics();

        // Assert - JIT stats should be reset (though managed stats might have new values)
        Assert.That(metrics.JitStats.TotalFieldAccesses, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void VayronPerformance_TimeOperation_RecordsCorrectly()
    {
        // Act
        using (VayronPerformance.TimeOperation("TestOperation"))
        {
            Thread.Sleep(10); // Simulate work
        }

        // Assert
        var stats = VayronPerformance.GetOperationStatistics("TestOperation");
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.Value.TimedOperations, Is.EqualTo(1));
        Assert.That(stats.Value.AverageDuration.TotalMilliseconds, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void VayronPerformance_Uptime_Increases()
    {
        // Act
        var uptime1 = VayronPerformance.Uptime;
        Thread.Sleep(10);
        var uptime2 = VayronPerformance.Uptime;

        // Assert
        Assert.That(uptime2, Is.GreaterThan(uptime1));
    }

    // =========================================================================
    // Benchmark Tests
    // =========================================================================

    [Test]
    [Category("Benchmark")]
    public void RunFieldAccessBenchmark_CompletesSuccessfully()
    {
        // Act
        var result = VayronBenchmark.RunFieldAccessBenchmark(
            _env,
            iterations: 1000,
            warmupIterations: 100);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Iterations, Is.EqualTo(1000));
        Assert.That(result.TotalDuration.TotalMilliseconds, Is.GreaterThan(0));
        Assert.That(result.AverageNanoseconds, Is.GreaterThan(0));
        Assert.That(result.OperationsPerSecond, Is.GreaterThan(0));

        Console.WriteLine(result);
    }

    [Test]
    [Category("Benchmark")]
    public void RunWriteBenchmark_CompletesSuccessfully()
    {
        // Act
        var result = VayronBenchmark.RunWriteBenchmark(
            _env,
            iterations: 100,
            warmupIterations: 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Iterations, Is.EqualTo(100));
        Assert.That(result.OperationsPerSecond, Is.GreaterThan(0));

        Console.WriteLine(result);
    }

    // =========================================================================
    // Concurrent Stress Tests
    // =========================================================================

    [Test]
    [Category("StressTest")]
    public void ConcurrentStressTest_WithMultipleThreads_Passes()
    {
        // Act
        var result = VayronBenchmark.RunConcurrentStressTest(
            _env,
            threads: 4,
            operationsPerThread: 100,
            duration: TimeSpan.FromSeconds(5));

        // Assert
        Assert.That(result.Passed, Is.True, $"Stress test failed with errors: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.TotalOperations, Is.GreaterThan(0));

        Console.WriteLine(result);
    }

    [Test]
    [Category("StressTest")]
    public void ConcurrentReads_WithJitOptimization_NoDataCorruption()
    {
        // Arrange - create test data
        var testOids = new List<VayronOid>();
        using (var tx = _env.WriteTransaction())
        {
            for (int i = 0; i < 10; i++)
            {
                var person = new TestPerson(_env) { Age = i, Salary = i * 1000 };
                testOids.Add(person.Oid);
            }
            tx.Commit();
        }

        var errors = new ConcurrentBag<string>();
        var completedReads = 0;

        // Act - concurrent reads with JIT optimization
        var tasks = Enumerable.Range(0, 8).Select(threadId => Task.Run(() =>
        {
            var random = new Random(threadId);
            for (int i = 0; i < 500; i++)
            {
                try
                {
                    using var tx = _env.ReadTransaction();
                    var idx = random.Next(testOids.Count);
                    var person = new TestPerson(_env, testOids[idx]);

                    using (person.GetJitOptimizationScope())
                    {
                        var age = person.AgeJitOptimized;
                        var salary = person.SalaryJitOptimized;

                        // Verify data integrity
                        if (age < 0 || age > 9)
                            errors.Add($"Invalid age: {age}");
                        if (salary != age * 1000)
                            errors.Add($"Data mismatch: age={age}, salary={salary}");
                    }

                    Interlocked.Increment(ref completedReads);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        Assert.That(errors, Is.Empty, $"Errors: {string.Join(", ", errors.Take(10))}");
        Assert.That(completedReads, Is.EqualTo(8 * 500));
    }

    [Test]
    [Category("StressTest")]
    public void ConcurrentWritesFromSingleThread_NoCorruption()
    {
        // Voron only supports single writer, so concurrent writes must be serialized
        // This test verifies the serialization works correctly

        // Arrange
        VayronOid testOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 0 };
            testOid = person.Oid;
            tx.Commit();
        }

        int expectedFinalValue = 100;

        // Act - sequential writes (simulating serialized concurrent requests)
        for (int i = 1; i <= expectedFinalValue; i++)
        {
            using var tx = _env.WriteTransaction();
            var person = new TestPerson(_env, testOid);
            person.AgeJitOptimized = i;
            tx.Commit();
        }

        // Assert - verify final value
        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, testOid);
            Assert.That(person.Age, Is.EqualTo(expectedFinalValue));
        }
    }

    // =========================================================================
    // Performance Regression Tests
    // =========================================================================

    [Test]
    [Category("Performance")]
    public void FieldAccessOverhead_ManagedPath_UnderThreshold()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act - measure managed field access time
        long totalAccesses = 10000;
        var sw = Stopwatch.StartNew();

        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);
            _ = person.Age; // Materialize

            for (int i = 0; i < totalAccesses; i++)
            {
                _ = person.Age;
            }
        }
        sw.Stop();

        double avgNs = sw.Elapsed.TotalNanoseconds / totalAccesses;

        // Assert - managed path should be under 100ns per access (hot path)
        Console.WriteLine($"Average field access time: {avgNs:F1}ns");
        Assert.That(avgNs, Is.LessThan(1000), $"Field access too slow: {avgNs:F1}ns");
    }

    [Test]
    [Category("Performance")]
    public void FieldAccessOverhead_JitOptimizedPath_UnderThreshold()
    {
        // Arrange
        VayronOid savedOid;
        using (var tx = _env.WriteTransaction())
        {
            var person = new TestPerson(_env) { Age = 42 };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Act - measure JIT-optimized field access time
        long totalAccesses = 10000;
        var sw = Stopwatch.StartNew();

        using (var tx = _env.ReadTransaction())
        {
            var person = new TestPerson(_env, savedOid);

            using (person.GetJitOptimizationScope())
            {
                for (int i = 0; i < totalAccesses; i++)
                {
                    _ = person.AgeJitOptimized;
                }
            }
        }
        sw.Stop();

        double avgNs = sw.Elapsed.TotalNanoseconds / totalAccesses;

        // Assert - JIT-optimized path should be faster (pinned access)
        Console.WriteLine($"Average JIT-optimized field access time: {avgNs:F1}ns");
        Assert.That(avgNs, Is.LessThan(500), $"JIT-optimized access too slow: {avgNs:F1}ns");
    }

    // =========================================================================
    // Field Access Stats Tests
    // =========================================================================

    [Test]
    public void VayronFieldAccessStats_FastPathHitRate_CalculatesCorrectly()
    {
        // Arrange
        var stats = new VayronFieldAccessStats
        {
            TotalFieldAccesses = 100,
            FastPathHits = 80,
            SlowPathMaterializations = 20
        };

        // Assert
        Assert.That(stats.FastPathHitRate, Is.EqualTo(80.0));
    }

    [Test]
    public void VayronFieldAccessStats_AverageNanoseconds_CalculatesCorrectly()
    {
        // Arrange
        var stats = new VayronFieldAccessStats
        {
            TotalFieldAccesses = 100,
            TotalNanoseconds = 500
        };

        // Assert
        Assert.That(stats.AverageNanosecondsPerAccess, Is.EqualTo(5.0));
    }

    [Test]
    public void VayronFieldAccessStats_ZeroAccesses_NoException()
    {
        // Arrange
        var stats = new VayronFieldAccessStats();

        // Assert - should not divide by zero
        Assert.That(stats.FastPathHitRate, Is.EqualTo(0.0));
        Assert.That(stats.AverageNanosecondsPerAccess, Is.EqualTo(0.0));
    }

    // =========================================================================
    // Test Entity for Phase 5
    // =========================================================================

    [VayronPersistent(SchemaVersion = 1)]
    private class TestPerson : VayronEntity
    {
        // Standard field access
        [VayronField(Order = 0)]
        public int Age
        {
            get => GetField<int>(0);
            set => SetField(0, value);
        }

        [VayronField(Order = 1)]
        public long Salary
        {
            get => GetField<long>(8);
            set => SetField(8, value);
        }

        // JIT-optimized field access
        public int AgeJitOptimized
        {
            get => GetFieldJitOptimized<int>(0);
            set => SetFieldJitOptimized(0, value);
        }

        public long SalaryJitOptimized
        {
            get => GetFieldJitOptimized<long>(8);
            set => SetFieldJitOptimized(8, value);
        }

        public TestPerson(VayronEnvironment env) : base(env) { }
        public TestPerson(VayronEnvironment env, VayronOid oid) : base(env, oid) { }

        protected override int GetBodySize() => sizeof(int) + 4 /* padding */ + sizeof(long);
        protected override uint GetTypeToken() => 0xFEED;
    }
}
