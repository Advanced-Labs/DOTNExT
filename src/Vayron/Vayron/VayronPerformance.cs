// VAYRON - Runtime-Integrated Persistent Storage
// Performance Monitoring and Benchmarking Utilities
//
// DOTNExT VAYRON Phase 5 Implementation
// This file provides comprehensive performance monitoring, benchmarking,
// and stress testing utilities for VAYRON.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Vayron;

/// <summary>
/// Aggregated performance metrics for VAYRON operations.
/// </summary>
public class VayronPerformanceMetrics
{
    /// <summary>JIT field access statistics.</summary>
    public VayronFieldAccessStats JitStats { get; init; }

    /// <summary>Transaction statistics.</summary>
    public TransactionStatistics TransactionStats { get; init; }

    /// <summary>Lifecycle manager statistics.</summary>
    public LifecycleStatistics LifecycleStats { get; init; }

    /// <summary>Side table statistics.</summary>
    public SideTableStatistics SideTableStats { get; init; }

    /// <summary>State machine statistics.</summary>
    public StateStatistics StateStats { get; init; }

    /// <summary>Timestamp when metrics were collected.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets a formatted summary of all metrics.</summary>
    public override string ToString()
    {
        return $"""
            VAYRON Performance Metrics (as of {Timestamp:HH:mm:ss.fff})
            ════════════════════════════════════════════════════════════════════
            JIT Field Access:
              Total: {JitStats.TotalFieldAccesses:N0}  FastPath: {JitStats.FastPathHits:N0} ({JitStats.FastPathHitRate:F1}%)
              Materialize: {JitStats.SlowPathMaterializations:N0}  NoTx: {JitStats.TransactionMisses:N0}

            Transactions:
              {TransactionStats}

            Side Table:
              {SideTableStats}

            State Transitions:
              {StateStats}
            ════════════════════════════════════════════════════════════════════
            """;
    }
}

/// <summary>
/// Performance monitoring and benchmarking utilities for VAYRON.
/// </summary>
public static class VayronPerformance
{
    private static readonly ConcurrentDictionary<string, OperationTimer> _operationTimers = new();
    private static readonly ConcurrentDictionary<string, long> _operationCounts = new();
    private static readonly Stopwatch _startupWatch = Stopwatch.StartNew();

    /// <summary>
    /// Gets all current performance metrics in one call.
    /// </summary>
    public static VayronPerformanceMetrics GetMetrics()
    {
        return new VayronPerformanceMetrics
        {
            JitStats = VayronJitInterop.GetStatistics(),
            TransactionStats = VayronTransactionManager.Instance.GetStatistics(),
            LifecycleStats = VayronLifecycleManager.Instance.GetStatistics(),
            SideTableStats = VayronMetaTable.GetStatistics(),
            StateStats = VayronStateManager.GetStatistics(),
            Timestamp = DateTimeOffset.Now
        };
    }

    /// <summary>
    /// Resets all performance counters and statistics.
    /// </summary>
    public static void ResetAll()
    {
        VayronJitInterop.ResetStatistics();
        VayronTransactionManager.Instance.ResetStatistics();
        VayronLifecycleManager.Instance.ResetStatistics();
        VayronStateManager.ResetStatistics();

        _operationTimers.Clear();
        _operationCounts.Clear();
    }

    /// <summary>
    /// Gets the total time since VAYRON started.
    /// </summary>
    public static TimeSpan Uptime => _startupWatch.Elapsed;

    /// <summary>
    /// Records an operation for performance tracking.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="duration">The duration of the operation.</param>
    public static void RecordOperation(string operationName, TimeSpan duration)
    {
        var timer = _operationTimers.GetOrAdd(operationName, _ => new OperationTimer());
        timer.Record(duration);

        _operationCounts.AddOrUpdate(operationName, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Gets statistics for a specific operation.
    /// </summary>
    public static OperationStatistics? GetOperationStatistics(string operationName)
    {
        if (!_operationTimers.TryGetValue(operationName, out var timer))
            return null;

        _operationCounts.TryGetValue(operationName, out var count);

        return timer.GetStatistics(operationName, count);
    }

    /// <summary>
    /// Gets statistics for all tracked operations.
    /// </summary>
    public static IEnumerable<OperationStatistics> GetAllOperationStatistics()
    {
        foreach (var (name, timer) in _operationTimers)
        {
            _operationCounts.TryGetValue(name, out var count);
            yield return timer.GetStatistics(name, count);
        }
    }

    /// <summary>
    /// Creates a timed operation scope.
    /// </summary>
    /// <param name="operationName">The name of the operation to time.</param>
    /// <returns>A disposable scope that records the duration when disposed.</returns>
    public static TimedOperation TimeOperation(string operationName)
    {
        return new TimedOperation(operationName);
    }
}

/// <summary>
/// A disposable scope for timing an operation.
/// </summary>
public readonly struct TimedOperation : IDisposable
{
    private readonly string _operationName;
    private readonly long _startTicks;

    internal TimedOperation(string operationName)
    {
        _operationName = operationName;
        _startTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Completes the timed operation and records the duration.
    /// </summary>
    public void Dispose()
    {
        var endTicks = Stopwatch.GetTimestamp();
        var duration = TimeSpan.FromTicks((endTicks - _startTicks) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
        VayronPerformance.RecordOperation(_operationName, duration);
    }
}

/// <summary>
/// Internal timer for tracking operation durations.
/// </summary>
internal class OperationTimer
{
    private long _totalTicks;
    private long _minTicks = long.MaxValue;
    private long _maxTicks;
    private int _count;
    private readonly object _lock = new();

    public void Record(TimeSpan duration)
    {
        var ticks = duration.Ticks;
        lock (_lock)
        {
            _totalTicks += ticks;
            _count++;
            if (ticks < _minTicks) _minTicks = ticks;
            if (ticks > _maxTicks) _maxTicks = ticks;
        }
    }

    public OperationStatistics GetStatistics(string name, long totalOperations)
    {
        lock (_lock)
        {
            return new OperationStatistics
            {
                OperationName = name,
                TotalOperations = totalOperations,
                TimedOperations = _count,
                TotalDuration = TimeSpan.FromTicks(_totalTicks),
                MinDuration = _minTicks == long.MaxValue ? TimeSpan.Zero : TimeSpan.FromTicks(_minTicks),
                MaxDuration = TimeSpan.FromTicks(_maxTicks),
                AverageDuration = _count > 0 ? TimeSpan.FromTicks(_totalTicks / _count) : TimeSpan.Zero
            };
        }
    }
}

/// <summary>
/// Statistics for a tracked operation.
/// </summary>
public readonly struct OperationStatistics
{
    /// <summary>The name of the operation.</summary>
    public string OperationName { get; init; }

    /// <summary>Total number of times the operation was invoked.</summary>
    public long TotalOperations { get; init; }

    /// <summary>Number of operations that were timed.</summary>
    public int TimedOperations { get; init; }

    /// <summary>Total duration of all timed operations.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Minimum operation duration.</summary>
    public TimeSpan MinDuration { get; init; }

    /// <summary>Maximum operation duration.</summary>
    public TimeSpan MaxDuration { get; init; }

    /// <summary>Average operation duration.</summary>
    public TimeSpan AverageDuration { get; init; }

    /// <summary>Operations per second (based on timed operations).</summary>
    public double OperationsPerSecond =>
        TotalDuration.TotalSeconds > 0 ? TimedOperations / TotalDuration.TotalSeconds : 0;

    public override string ToString() =>
        $"{OperationName}: {TotalOperations:N0} ops, Avg={AverageDuration.TotalMicroseconds:F1}µs, " +
        $"Min={MinDuration.TotalMicroseconds:F1}µs, Max={MaxDuration.TotalMicroseconds:F1}µs";
}

/// <summary>
/// Benchmark runner for VAYRON performance testing.
/// </summary>
public static class VayronBenchmark
{
    /// <summary>
    /// Runs a benchmark for field access operations.
    /// </summary>
    /// <param name="env">The VAYRON environment.</param>
    /// <param name="iterations">Number of iterations.</param>
    /// <param name="warmupIterations">Number of warmup iterations.</param>
    /// <returns>Benchmark results.</returns>
    public static BenchmarkResult RunFieldAccessBenchmark(
        VayronEnvironment env,
        int iterations = 100000,
        int warmupIterations = 10000)
    {
        // Create a test entity
        VayronOid testOid;
        using (var tx = env.WriteTransaction())
        {
            var entity = new BenchmarkEntity(env) { Value = 42 };
            testOid = entity.Oid;
            tx.Commit();
        }

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            using var tx = env.ReadTransaction();
            var entity = new BenchmarkEntity(env, testOid);
            _ = entity.Value;
        }

        // Reset statistics
        VayronPerformance.ResetAll();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Benchmark
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var tx = env.ReadTransaction();
            var entity = new BenchmarkEntity(env, testOid);
            _ = entity.Value;
        }
        sw.Stop();

        var metrics = VayronPerformance.GetMetrics();

        return new BenchmarkResult
        {
            Name = "FieldAccess",
            Iterations = iterations,
            TotalDuration = sw.Elapsed,
            AverageNanoseconds = sw.Elapsed.TotalNanoseconds / iterations,
            OperationsPerSecond = iterations / sw.Elapsed.TotalSeconds,
            Metrics = metrics
        };
    }

    /// <summary>
    /// Runs a benchmark for write operations.
    /// </summary>
    public static BenchmarkResult RunWriteBenchmark(
        VayronEnvironment env,
        int iterations = 10000,
        int warmupIterations = 1000)
    {
        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            using var tx = env.WriteTransaction();
            var entity = new BenchmarkEntity(env) { Value = i };
            tx.Commit();
        }

        // Reset statistics
        VayronPerformance.ResetAll();
        GC.Collect();

        // Benchmark
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var tx = env.WriteTransaction();
            var entity = new BenchmarkEntity(env) { Value = i };
            tx.Commit();
        }
        sw.Stop();

        var metrics = VayronPerformance.GetMetrics();

        return new BenchmarkResult
        {
            Name = "Write",
            Iterations = iterations,
            TotalDuration = sw.Elapsed,
            AverageNanoseconds = sw.Elapsed.TotalNanoseconds / iterations,
            OperationsPerSecond = iterations / sw.Elapsed.TotalSeconds,
            Metrics = metrics
        };
    }

    /// <summary>
    /// Runs a concurrent access stress test.
    /// </summary>
    public static StressTestResult RunConcurrentStressTest(
        VayronEnvironment env,
        int threads = 8,
        int operationsPerThread = 10000,
        TimeSpan? duration = null)
    {
        var targetDuration = duration ?? TimeSpan.FromSeconds(10);
        var cts = new CancellationTokenSource(targetDuration);

        var errors = new ConcurrentBag<Exception>();
        var completedOperations = new int[threads];
        var threadResults = new ConcurrentBag<ThreadStressResult>();

        // Create some test entities
        var testOids = new List<VayronOid>();
        using (var tx = env.WriteTransaction())
        {
            for (int i = 0; i < 100; i++)
            {
                var entity = new BenchmarkEntity(env) { Value = i };
                testOids.Add(entity.Oid);
            }
            tx.Commit();
        }

        VayronPerformance.ResetAll();

        var sw = Stopwatch.StartNew();

        // Start worker threads
        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var random = new Random(threadId);
                var localSw = Stopwatch.StartNew();
                int ops = 0;

                try
                {
                    while (!cts.Token.IsCancellationRequested && ops < operationsPerThread)
                    {
                        try
                        {
                            // Randomly choose read or write operation
                            if (random.Next(10) < 8) // 80% reads
                            {
                                using var tx = env.ReadTransaction();
                                var oid = testOids[random.Next(testOids.Count)];
                                var entity = new BenchmarkEntity(env, oid);
                                _ = entity.Value;
                            }
                            else // 20% writes
                            {
                                using var tx = env.WriteTransaction();
                                var oid = testOids[random.Next(testOids.Count)];
                                var entity = new BenchmarkEntity(env, oid)
                                {
                                    Value = random.Next()
                                };
                                tx.Commit();
                            }

                            ops++;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            errors.Add(ex);
                        }
                    }
                }
                finally
                {
                    localSw.Stop();
                    completedOperations[threadId] = ops;
                    threadResults.Add(new ThreadStressResult
                    {
                        ThreadId = threadId,
                        Operations = ops,
                        Duration = localSw.Elapsed
                    });
                }
            }, cts.Token);
        }

        Task.WaitAll(tasks);
        sw.Stop();

        var totalOps = completedOperations.Sum();

        return new StressTestResult
        {
            Threads = threads,
            TotalOperations = totalOps,
            TotalDuration = sw.Elapsed,
            OperationsPerSecond = totalOps / sw.Elapsed.TotalSeconds,
            Errors = errors.ToArray(),
            ThreadResults = threadResults.ToArray(),
            Metrics = VayronPerformance.GetMetrics()
        };
    }

    // Internal benchmark entity
    [VayronPersistent(SchemaVersion = 1)]
    private class BenchmarkEntity : VayronEntity
    {
        [VayronField(Order = 0)]
        public int Value
        {
            get => GetField<int>(0);
            set => SetField(0, value);
        }

        public BenchmarkEntity(VayronEnvironment env) : base(env) { }
        public BenchmarkEntity(VayronEnvironment env, VayronOid oid) : base(env, oid) { }

        protected override int GetBodySize() => sizeof(int);
        protected override uint GetTypeToken() => 0xBENC; // Benchmark type token
    }
}

/// <summary>
/// Result of a benchmark run.
/// </summary>
public class BenchmarkResult
{
    /// <summary>Benchmark name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of iterations.</summary>
    public int Iterations { get; init; }

    /// <summary>Total duration.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Average nanoseconds per operation.</summary>
    public double AverageNanoseconds { get; init; }

    /// <summary>Operations per second.</summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>Performance metrics at end of benchmark.</summary>
    public VayronPerformanceMetrics? Metrics { get; init; }

    public override string ToString() =>
        $"Benchmark '{Name}': {Iterations:N0} iterations in {TotalDuration.TotalMilliseconds:F2}ms " +
        $"({AverageNanoseconds:F1}ns/op, {OperationsPerSecond:N0} ops/sec)";
}

/// <summary>
/// Result from a single thread in a stress test.
/// </summary>
public readonly struct ThreadStressResult
{
    public int ThreadId { get; init; }
    public int Operations { get; init; }
    public TimeSpan Duration { get; init; }
    public double OperationsPerSecond => Operations / Duration.TotalSeconds;
}

/// <summary>
/// Result of a concurrent stress test.
/// </summary>
public class StressTestResult
{
    /// <summary>Number of threads.</summary>
    public int Threads { get; init; }

    /// <summary>Total operations across all threads.</summary>
    public long TotalOperations { get; init; }

    /// <summary>Total duration.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Operations per second.</summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>Errors encountered.</summary>
    public Exception[] Errors { get; init; } = [];

    /// <summary>Per-thread results.</summary>
    public ThreadStressResult[] ThreadResults { get; init; } = [];

    /// <summary>Performance metrics at end of test.</summary>
    public VayronPerformanceMetrics? Metrics { get; init; }

    /// <summary>Whether the test passed (no errors).</summary>
    public bool Passed => Errors.Length == 0;

    public override string ToString()
    {
        var status = Passed ? "PASSED" : $"FAILED ({Errors.Length} errors)";
        return $"StressTest [{status}]: {Threads} threads, {TotalOperations:N0} ops in {TotalDuration.TotalSeconds:F1}s " +
               $"({OperationsPerSecond:N0} ops/sec)";
    }
}
