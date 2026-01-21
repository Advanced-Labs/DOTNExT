// VAYRON - Phase 2 unit tests for object header tagging
// Tests for BIT_SBLK_IS_VAYRON_HANDLE (bit 31) classification

using Vayron.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Vayron.Tests;

/// <summary>
/// Tests for Phase 2: Object Header Tagging functionality.
/// </summary>
/// <remarks>
/// Phase 2 enables fast O(1) classification of VAYRON handles via a single bit test
/// in the object header (bit 31 = BIT_SBLK_IS_VAYRON_HANDLE = 0x80000000).
/// </remarks>
public class VayronPhase2Tests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testPath;
    private readonly VayronEnvironment _env;

    public VayronPhase2Tests(ITestOutputHelper output)
    {
        _output = output;
        _testPath = Path.Combine(Path.GetTempPath(), "vayron-phase2-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testPath);

        _env = new VayronEnvironment(new VayronEnvironmentOptions
        {
            Path = _testPath,
            ForceDurability = false
        });
    }

    public void Dispose()
    {
        _env.Dispose();

        try
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    // =========================================================================
    // VayronRuntime Tests
    // =========================================================================

    [Fact]
    public void VayronRuntime_IsSupported_ReturnsTrue()
    {
        // The managed implementation should always be supported
        Assert.True(VayronRuntime.IsSupported);
    }

    [Fact]
    public void VayronRuntime_RegularObject_NotMarkedAsVayronHandle()
    {
        // Regular objects should NOT be VAYRON handles
        var normalObject = new object();
        Assert.False(VayronRuntime.IsVayronHandle(normalObject));

        var list = new List<int>();
        Assert.False(VayronRuntime.IsVayronHandle(list));

        var str = "hello world";
        Assert.False(VayronRuntime.IsVayronHandle(str));
    }

    [Fact]
    public void VayronRuntime_NullObject_ReturnsFalse()
    {
        Assert.False(VayronRuntime.IsVayronHandle(null));
    }

    [Fact]
    public void VayronRuntime_CanMarkAndCheckObject()
    {
        var testObj = new object();

        // Initially not marked
        Assert.False(VayronRuntime.IsVayronHandle(testObj));

        // Mark it
        VayronRuntime.MarkAsVayronHandle(testObj);

        // Now should be marked
        Assert.True(VayronRuntime.IsVayronHandle(testObj));

        // Can clear
        VayronRuntime.ClearVayronHandle(testObj);

        // Now should NOT be marked
        Assert.False(VayronRuntime.IsVayronHandle(testObj));
    }

    [Fact]
    public void VayronRuntime_MarkingIsIdempotent()
    {
        var testObj = new object();

        // Mark multiple times
        VayronRuntime.MarkAsVayronHandle(testObj);
        VayronRuntime.MarkAsVayronHandle(testObj);
        VayronRuntime.MarkAsVayronHandle(testObj);

        // Should still be marked
        Assert.True(VayronRuntime.IsVayronHandle(testObj));

        // Single clear should work
        VayronRuntime.ClearVayronHandle(testObj);
        Assert.False(VayronRuntime.IsVayronHandle(testObj));
    }

    [Fact]
    public void VayronRuntime_ClearingIsIdempotent()
    {
        var testObj = new object();

        // Clear multiple times (even though never marked)
        VayronRuntime.ClearVayronHandle(testObj);
        VayronRuntime.ClearVayronHandle(testObj);
        VayronRuntime.ClearVayronHandle(testObj);

        // Should still NOT be marked
        Assert.False(VayronRuntime.IsVayronHandle(testObj));
    }

    [Fact]
    public void VayronRuntime_GetSyncBlockValue_ReturnsValidValue()
    {
        var testObj = new object();

        // Get initial value
        var value1 = VayronRuntime.GetSyncBlockValue(testObj);
        _output.WriteLine($"Initial sync block value: 0x{value1:X8}");

        // Mark as VAYRON handle
        VayronRuntime.MarkAsVayronHandle(testObj);
        var value2 = VayronRuntime.GetSyncBlockValue(testObj);
        _output.WriteLine($"After marking: 0x{value2:X8}");

        // VAYRON bit should be set
        Assert.True((value2 & VayronRuntime.BIT_SBLK_IS_VAYRON_HANDLE) != 0);

        // Clear
        VayronRuntime.ClearVayronHandle(testObj);
        var value3 = VayronRuntime.GetSyncBlockValue(testObj);
        _output.WriteLine($"After clearing: 0x{value3:X8}");

        // VAYRON bit should be clear
        Assert.True((value3 & VayronRuntime.BIT_SBLK_IS_VAYRON_HANDLE) == 0);
    }

    [Fact]
    public void VayronRuntime_GetHeaderInfo_ReturnsCorrectInfo()
    {
        var testObj = new object();

        // Mark as VAYRON handle
        VayronRuntime.MarkAsVayronHandle(testObj);

        var info = VayronRuntime.GetHeaderInfo(testObj);

        Assert.True(info.IsVayronHandle);
        Assert.True((info.RawValue & VayronRuntime.BIT_SBLK_IS_VAYRON_HANDLE) != 0);

        _output.WriteLine($"Header info: {info}");
    }

    [Fact]
    public void VayronRuntime_ThreadSafety_ConcurrentMarkAndCheck()
    {
        var testObj = new object();
        var errors = new List<Exception>();
        var iterations = 10000;
        var threadCount = 4;

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            try
            {
                for (int j = 0; j < iterations; j++)
                {
                    if (j % 2 == 0)
                    {
                        VayronRuntime.MarkAsVayronHandle(testObj);
                    }
                    else
                    {
                        VayronRuntime.ClearVayronHandle(testObj);
                    }

                    // Always safe to check
                    _ = VayronRuntime.IsVayronHandle(testObj);
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(ex);
                }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.Empty(errors);
    }

    // =========================================================================
    // VayronHandle Integration Tests
    // =========================================================================

    [Fact]
    public void VayronHandle_AutomaticallyMarkedOnConstruction()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);

        // Handle should be automatically marked
        Assert.True(VayronHandle.IsVayronHandleInstance(person));
        Assert.True(VayronRuntime.IsVayronHandle(person));

        var headerInfo = person.GetHeaderInfo();
        Assert.True(headerInfo.IsVayronHandle);
        _output.WriteLine($"Person header: {headerInfo}");
    }

    [Fact]
    public void VayronHandle_ExistingOidConstruction_AlsoMarked()
    {
        VayronOid savedOid;

        // Create
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env) { Age = 25, Salary = 50000, IsActive = true };
            savedOid = person.Oid;
            tx.Commit();
        }

        // Load existing
        using (var tx = _env.ReadTransaction())
        {
            var person = new Person(_env, savedOid);

            // Should be marked even when loaded from existing OID
            Assert.True(VayronHandle.IsVayronHandleInstance(person));
            Assert.True(VayronRuntime.IsVayronHandle(person));
        }
    }

    [Fact]
    public void VayronHandle_StaticIsVayronHandleInstance_Works()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);
        var normalObj = new object();
        var list = new List<int> { 1, 2, 3 };

        Assert.True(VayronHandle.IsVayronHandleInstance(person));
        Assert.False(VayronHandle.IsVayronHandleInstance(normalObj));
        Assert.False(VayronHandle.IsVayronHandleInstance(list));
        Assert.False(VayronHandle.IsVayronHandleInstance(null));
    }

    [Fact]
    public void VayronHandle_GetHeaderInfo_ReturnsValidInfo()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);
        var headerInfo = person.GetHeaderInfo();

        Assert.True(headerInfo.IsVayronHandle);
        _output.WriteLine($"Raw value: 0x{headerInfo.RawValue:X8}");
        _output.WriteLine($"Is VAYRON: {headerInfo.IsVayronHandle}");
        _output.WriteLine($"Full info: {headerInfo}");
    }

    // =========================================================================
    // Diagnostics Tests
    // =========================================================================

    [Fact]
    public void VayronDiagnostics_IsVayronHandle_Works()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);
        var normalObj = new object();

        Assert.True(VayronDiagnostics.IsVayronHandle(person));
        Assert.False(VayronDiagnostics.IsVayronHandle(normalObj));
    }

    [Fact]
    public void VayronDiagnostics_GetHandleDiagnostics_ReturnsInfo()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 30, Salary = 75000, IsActive = true };
        var diagInfo = VayronDiagnostics.GetHandleDiagnostics(person);

        Assert.NotNull(diagInfo);
        Assert.Equal(person.Oid.Value, diagInfo.Oid);
        Assert.Contains("Person", diagInfo.TypeName);
        Assert.True(diagInfo.HeaderInfo.IsVayronHandle);

        _output.WriteLine(diagInfo.ToString());
    }

    [Fact]
    public void VayronDiagnostics_GetHandleDiagnostics_ReturnsNull_ForNonHandle()
    {
        var normalObj = new object();
        var diagInfo = VayronDiagnostics.GetHandleDiagnostics(normalObj);

        Assert.Null(diagInfo);
    }

    [Fact]
    public void VayronDiagnostics_GetObjectHeaderInfo_Works()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);
        var headerInfo = VayronDiagnostics.GetObjectHeaderInfo(person);

        Assert.NotNull(headerInfo);
        Assert.True(headerInfo.IsVayronHandle);
        Assert.Contains("Person", headerInfo.TypeName);

        _output.WriteLine(headerInfo.ToString());
    }

    [Fact]
    public void VayronDiagnostics_GetObjectHeaderInfo_NormalObject()
    {
        var normalObj = new object();
        var headerInfo = VayronDiagnostics.GetObjectHeaderInfo(normalObj);

        Assert.NotNull(headerInfo);
        Assert.False(headerInfo.IsVayronHandle);
        Assert.Contains("Object", headerInfo.TypeName);

        _output.WriteLine(headerInfo.ToString());
    }

    [Fact]
    public void VayronDiagnostics_GetObjectAddress_ReturnsNonZero()
    {
        var obj = new object();
        var address = VayronDiagnostics.GetObjectAddress(obj);

        Assert.NotEqual(nint.Zero, address);
        _output.WriteLine($"Object address: 0x{address:X}");
    }

    // =========================================================================
    // Performance Characterization Tests
    // =========================================================================

    [Fact]
    public void Performance_IsVayronHandle_Fast()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);
        var normalObj = new object();

        // Warm up
        for (int i = 0; i < 1000; i++)
        {
            _ = VayronRuntime.IsVayronHandle(person);
            _ = VayronRuntime.IsVayronHandle(normalObj);
        }

        // Measure
        var iterations = 100000;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            _ = VayronRuntime.IsVayronHandle(person);
        }

        sw.Stop();
        var nsPerOp = sw.Elapsed.TotalNanoseconds / iterations;

        _output.WriteLine($"IsVayronHandle (VAYRON obj): {nsPerOp:F2}ns per operation");
        _output.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds}ms");

        // Should be fast - less than 100ns per operation
        Assert.True(nsPerOp < 100, $"IsVayronHandle is too slow: {nsPerOp}ns");
    }

    [Fact]
    public void Performance_MarkAsVayronHandle_Fast()
    {
        // Create many objects to test marking performance
        var objects = new object[1000];
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i] = new object();
        }

        // Warm up
        foreach (var obj in objects)
        {
            VayronRuntime.MarkAsVayronHandle(obj);
            VayronRuntime.ClearVayronHandle(obj);
        }

        // Measure marking
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var obj in objects)
        {
            VayronRuntime.MarkAsVayronHandle(obj);
        }

        sw.Stop();
        var nsPerOp = sw.Elapsed.TotalNanoseconds / objects.Length;

        _output.WriteLine($"MarkAsVayronHandle: {nsPerOp:F2}ns per operation");

        // Should be reasonably fast - less than 1000ns per operation
        Assert.True(nsPerOp < 1000, $"MarkAsVayronHandle is too slow: {nsPerOp}ns");
    }

    // =========================================================================
    // Edge Cases and Error Handling
    // =========================================================================

    [Fact]
    public void VayronRuntime_MarkAsVayronHandle_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => VayronRuntime.MarkAsVayronHandle(null!));
    }

    [Fact]
    public void VayronRuntime_ClearVayronHandle_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => VayronRuntime.ClearVayronHandle(null!));
    }

    [Fact]
    public void VayronRuntime_GetSyncBlockValue_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => VayronRuntime.GetSyncBlockValue(null!));
    }

    [Fact]
    public void VayronRuntime_GetHeaderInfo_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => VayronRuntime.GetHeaderInfo(null!));
    }

    [Fact]
    public void VayronRuntime_DoesNotInterfereWithHashCode()
    {
        var testObj = new object();

        // Get hash code first (this may store hash in object header)
        var hash1 = testObj.GetHashCode();

        // Mark as VAYRON handle
        VayronRuntime.MarkAsVayronHandle(testObj);

        // Hash code should still work
        var hash2 = testObj.GetHashCode();

        Assert.Equal(hash1, hash2);
        Assert.True(VayronRuntime.IsVayronHandle(testObj));
    }

    [Fact]
    public void VayronRuntime_DoesNotInterfereWithLocking()
    {
        var testObj = new object();

        // Mark as VAYRON handle
        VayronRuntime.MarkAsVayronHandle(testObj);

        // Lock should still work
        lock (testObj)
        {
            Assert.True(VayronRuntime.IsVayronHandle(testObj));
        }

        Assert.True(VayronRuntime.IsVayronHandle(testObj));
    }
}
