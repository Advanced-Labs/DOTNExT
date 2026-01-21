// VAYRON - Runtime-Integrated Persistent Storage
// Phase 3 Unit Tests: Side Table Integration

using System.Runtime.InteropServices;

namespace Vayron.Tests;

/// <summary>
/// Unit tests for Phase 3: Side Table Integration.
/// Tests metadata management, state machine, lifecycle, and native interop.
/// </summary>
[TestClass]
public class VayronPhase3Tests : VayronTestBase
{
    // =========================================================================
    // State Machine Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("StateMachine")]
    public void StateManager_ValidTransitions_AreAllowed()
    {
        // NotMaterialized -> Materializing
        Assert.IsTrue(VayronStateManager.IsValidTransition(
            MaterializationState.NotMaterialized,
            MaterializationState.Materializing));

        // Materializing -> Materialized
        Assert.IsTrue(VayronStateManager.IsValidTransition(
            MaterializationState.Materializing,
            MaterializationState.Materialized));

        // Materialized -> Dirty
        Assert.IsTrue(VayronStateManager.IsValidTransition(
            MaterializationState.Materialized,
            MaterializationState.Dirty));

        // Dirty -> Materialized
        Assert.IsTrue(VayronStateManager.IsValidTransition(
            MaterializationState.Dirty,
            MaterializationState.Materialized));

        // Stale -> Materializing
        Assert.IsTrue(VayronStateManager.IsValidTransition(
            MaterializationState.Stale,
            MaterializationState.Materializing));
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("StateMachine")]
    public void StateManager_InvalidTransitions_AreRejected()
    {
        // NotMaterialized -> Stale (invalid)
        Assert.IsFalse(VayronStateManager.IsValidTransition(
            MaterializationState.NotMaterialized,
            MaterializationState.Stale));

        // Materializing -> Dirty (invalid)
        Assert.IsFalse(VayronStateManager.IsValidTransition(
            MaterializationState.Materializing,
            MaterializationState.Dirty));

        // Dirty -> NotMaterialized (invalid)
        Assert.IsFalse(VayronStateManager.IsValidTransition(
            MaterializationState.Dirty,
            MaterializationState.NotMaterialized));
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("StateMachine")]
    public void StateManager_SameStateTransition_IsValid()
    {
        foreach (MaterializationState state in Enum.GetValues<MaterializationState>())
        {
            Assert.IsTrue(VayronStateManager.IsValidTransition(state, state),
                $"Self-transition for {state} should be valid");
        }
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("StateMachine")]
    public void StateManager_GetValidTransitions_ReturnsCorrectSet()
    {
        var fromNotMaterialized = VayronStateManager.GetValidTransitions(MaterializationState.NotMaterialized);
        Assert.IsTrue(fromNotMaterialized.Contains(MaterializationState.Materializing));
        Assert.IsTrue(fromNotMaterialized.Contains(MaterializationState.Dirty));

        var fromMaterialized = VayronStateManager.GetValidTransitions(MaterializationState.Materialized);
        Assert.IsTrue(fromMaterialized.Contains(MaterializationState.Dirty));
        Assert.IsTrue(fromMaterialized.Contains(MaterializationState.Stale));
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("StateMachine")]
    public void StateManager_HelperMethods_WorkCorrectly()
    {
        // IsBodyAvailable
        Assert.IsTrue(VayronStateManager.IsBodyAvailable(MaterializationState.Materialized));
        Assert.IsTrue(VayronStateManager.IsBodyAvailable(MaterializationState.Dirty));
        Assert.IsFalse(VayronStateManager.IsBodyAvailable(MaterializationState.NotMaterialized));
        Assert.IsFalse(VayronStateManager.IsBodyAvailable(MaterializationState.Stale));

        // NeedsLoad
        Assert.IsTrue(VayronStateManager.NeedsLoad(MaterializationState.NotMaterialized));
        Assert.IsTrue(VayronStateManager.NeedsLoad(MaterializationState.Stale));
        Assert.IsFalse(VayronStateManager.NeedsLoad(MaterializationState.Materialized));

        // CanEvict
        Assert.IsTrue(VayronStateManager.CanEvict(MaterializationState.Materialized));
        Assert.IsFalse(VayronStateManager.CanEvict(MaterializationState.Dirty));
    }

    // =========================================================================
    // VayronMeta Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_InitializesCorrectly()
    {
        var oid = new VayronOid(42);
        var meta = new VayronMeta(oid);

        Assert.AreEqual(oid, meta.Oid);
        Assert.AreEqual(MaterializationState.NotMaterialized, meta.State);
        Assert.AreEqual(-1, meta.Epoch);
        Assert.AreEqual(IntPtr.Zero, meta.CachedBodyPtr);
        Assert.AreEqual(0, meta.CachedBodySize);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_StateTransition_ValidatesAndRaisesEvent()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var stateChangedRaised = false;
        MaterializationState? oldState = null;
        MaterializationState? newState = null;

        meta.StateChanged += (sender, e) =>
        {
            stateChangedRaised = true;
            oldState = e.OldState;
            newState = e.NewState;
        };

        // Valid transition
        Assert.IsTrue(meta.TryTransitionState(MaterializationState.Materializing));
        Assert.IsTrue(stateChangedRaised);
        Assert.AreEqual(MaterializationState.NotMaterialized, oldState);
        Assert.AreEqual(MaterializationState.Materializing, newState);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_InvalidTransition_ReturnsFalse()
    {
        var meta = new VayronMeta(new VayronOid(1));

        // NotMaterialized -> Stale is invalid
        Assert.IsFalse(meta.TryTransitionState(MaterializationState.Stale));
        Assert.AreEqual(MaterializationState.NotMaterialized, meta.State);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_Locking_WorksCorrectly()
    {
        var meta = new VayronMeta(new VayronOid(1));

        // Should be able to acquire lock
        Assert.IsTrue(meta.TryEnterLock());

        // Should not be able to acquire again
        Assert.IsFalse(meta.TryEnterLock());

        // After release, should be able to acquire
        meta.ExitLock();
        Assert.IsTrue(meta.TryEnterLock());
        meta.ExitLock();
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_WithLock_ExecutesSafely()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var executed = false;

        meta.WithLock(() => { executed = true; });

        Assert.IsTrue(executed);

        // Should be unlocked after
        Assert.IsTrue(meta.TryEnterLock());
        meta.ExitLock();
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_MarkMaterialized_UpdatesState()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[100];

        meta.TryTransitionState(MaterializationState.Materializing);
        meta.MarkMaterialized(42, body);

        Assert.AreEqual(MaterializationState.Materialized, meta.State);
        Assert.AreEqual(42, meta.Epoch);
        Assert.AreEqual(100, meta.CachedBodySize);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Metadata")]
    public void VayronMeta_Invalidate_ClearsBody()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[100];

        meta.TryTransitionState(MaterializationState.Materializing);
        meta.MarkMaterialized(42, body);
        meta.Invalidate();

        Assert.AreEqual(MaterializationState.Stale, meta.State);
        Assert.AreEqual(IntPtr.Zero, meta.CachedBodyPtr);
        Assert.IsNull(meta.GetManagedBody());
    }

    // =========================================================================
    // Native Pointer Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("NativePointer")]
    public void VayronMeta_PinBody_SetsPointer()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[] { 1, 2, 3, 4, 5 };

        meta.PinBody(body);

        Assert.IsTrue(meta.IsPinned);
        Assert.AreNotEqual(IntPtr.Zero, meta.CachedBodyPtr);
        Assert.AreEqual(5, meta.CachedBodySize);

        // Verify we can read through pointer
        unsafe
        {
            var ptr = (byte*)meta.CachedBodyPtr;
            Assert.AreEqual(1, ptr[0]);
            Assert.AreEqual(5, ptr[4]);
        }

        meta.Unpin();
        Assert.IsFalse(meta.IsPinned);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("NativePointer")]
    public void VayronMeta_AllocateNativeBody_CopiesData()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[] { 10, 20, 30, 40, 50 };

        meta.AllocateNativeBody(body);

        Assert.AreNotEqual(IntPtr.Zero, meta.CachedBodyPtr);
        Assert.AreEqual(5, meta.CachedBodySize);

        // Verify data was copied
        unsafe
        {
            var ptr = (byte*)meta.CachedBodyPtr;
            Assert.AreEqual(10, ptr[0]);
            Assert.AreEqual(50, ptr[4]);
        }

        meta.FreeNativeBody();
        Assert.AreEqual(IntPtr.Zero, meta.CachedBodyPtr);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("NativePointer")]
    public void VayronMeta_GetBodySpan_WorksForBothModes()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[] { 1, 2, 3 };

        // Managed mode
        meta.SetManagedBody(body);
        var span1 = meta.GetBodySpan();
        Assert.AreEqual(3, span1.Length);
        Assert.AreEqual(1, span1[0]);

        // Pinned mode
        meta.PinBody(body);
        var span2 = meta.GetBodySpan();
        Assert.AreEqual(3, span2.Length);
        Assert.AreEqual(1, span2[0]);
        meta.Unpin();
    }

    // =========================================================================
    // VayronMetaTable Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("SideTable")]
    public void VayronMetaTable_GetOrCreate_CreatesMeta()
    {
        var handle = new object();
        var oid = new VayronOid(100);

        var meta = VayronMetaTable.GetOrCreate(handle, oid);

        Assert.IsNotNull(meta);
        Assert.AreEqual(oid, meta.Oid);

        // Second call returns same instance
        var meta2 = VayronMetaTable.GetOrCreate(handle, oid);
        Assert.AreSame(meta, meta2);

        VayronMetaTable.Remove(handle);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("SideTable")]
    public void VayronMetaTable_TryGetByOid_FindsHandle()
    {
        var handle = new object();
        var oid = new VayronOid(200);

        VayronMetaTable.GetOrCreate(handle, oid);

        Assert.IsTrue(VayronMetaTable.TryGetByOid(oid, out var meta));
        Assert.IsNotNull(meta);
        Assert.AreEqual(oid, meta!.Oid);

        VayronMetaTable.Remove(handle);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("SideTable")]
    public void VayronMetaTable_Statistics_AreTracked()
    {
        VayronMetaTable.ResetStatistics();

        var handle1 = new object();
        var handle2 = new object();

        VayronMetaTable.GetOrCreate(handle1, new VayronOid(1));
        VayronMetaTable.GetOrCreate(handle2, new VayronOid(2));

        var stats = VayronMetaTable.GetStatistics();
        Assert.IsTrue(stats.SetCount >= 2);
        Assert.IsTrue(stats.ActiveCount >= 2);

        VayronMetaTable.Remove(handle1);
        VayronMetaTable.Remove(handle2);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("SideTable")]
    public void VayronMetaTable_GetAllOids_EnumeratesHandles()
    {
        var handles = new List<object>();
        var oids = new List<VayronOid>();

        for (int i = 0; i < 5; i++)
        {
            var handle = new object();
            var oid = new VayronOid(1000 + i);
            handles.Add(handle);
            oids.Add(oid);
            VayronMetaTable.GetOrCreate(handle, oid);
        }

        var foundOids = VayronMetaTable.GetAllOids().ToList();
        foreach (var oid in oids)
        {
            Assert.IsTrue(foundOids.Contains(oid), $"OID {oid.Value} not found");
        }

        // Cleanup
        foreach (var handle in handles)
        {
            VayronMetaTable.Remove(handle);
        }
    }

    // =========================================================================
    // VayronHandle Phase 3 Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Handle")]
    public void Handle_GetMetadata_ReturnsMetadata()
    {
        using var env = CreateTestEnvironment();
        using var tx = env.WriteTransaction();

        var person = new Person(env) { Age = 25 };
        var meta = person.GetMetadata();

        Assert.IsNotNull(meta);
        Assert.AreEqual(person.Oid, meta!.Oid);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Handle")]
    public void Handle_MaterializationState_TracksCorrectly()
    {
        using var env = CreateTestEnvironment();
        using var tx = env.WriteTransaction();

        var person = new Person(env);

        // Should be dirty after field access
        person.Age = 30;
        Assert.AreEqual(MaterializationState.Dirty, person.MaterializationState);

        tx.Commit();
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Handle")]
    public void Handle_GetDiagnostics_ReturnsCompleteInfo()
    {
        using var env = CreateTestEnvironment();
        using var tx = env.WriteTransaction();

        var person = new Person(env) { Age = 35, Salary = 50000 };
        var diag = person.GetDiagnostics();

        Assert.AreEqual(person.Oid, diag.Oid);
        Assert.IsTrue(diag.IsDirty);
        Assert.IsTrue(diag.IsMaterialized);
        Assert.IsTrue(diag.CachedBodySize > 0);
        Assert.IsTrue(diag.HeaderInfo.IsVayronHandle);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Handle")]
    public void Handle_Pin_EnablesFastAccess()
    {
        using var env = CreateTestEnvironment();
        using var tx = env.WriteTransaction();

        var person = new Person(env) { Age = 40 };

        Assert.IsFalse(person.IsPinned);

        person.Pin();
        Assert.IsTrue(person.IsPinned);

        // Should still work
        Assert.AreEqual(40, person.Age);

        person.Unpin();
        Assert.IsFalse(person.IsPinned);
    }

    // =========================================================================
    // Lifecycle Manager Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Lifecycle")]
    public void LifecycleManager_Instance_IsSingleton()
    {
        var instance1 = VayronLifecycleManager.Instance;
        var instance2 = VayronLifecycleManager.Instance;

        Assert.AreSame(instance1, instance2);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Lifecycle")]
    public void LifecycleManager_ForceCleanup_DoesNotThrow()
    {
        VayronLifecycleManager.Instance.ForceCleanup();
        // Should complete without exception
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Lifecycle")]
    public void LifecycleManager_Statistics_AreTracked()
    {
        VayronLifecycleManager.Instance.ResetStatistics();

        var stats = VayronLifecycleManager.Instance.GetStatistics();
        Assert.IsTrue(stats.IsBackgroundCleanupEnabled);
        Assert.IsTrue(stats.MaxTotalBytes > 0);
    }

    // =========================================================================
    // Eviction Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Eviction")]
    public void VayronMeta_Evict_FreesMemory()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[1000];

        meta.TryTransitionState(MaterializationState.Materializing);
        meta.MarkMaterialized(42, body);

        var freedBytes = meta.Evict(EvictionReason.MemoryPressure);

        Assert.AreEqual(1000, freedBytes);
        Assert.AreEqual(MaterializationState.Stale, meta.State);
        Assert.IsNull(meta.GetManagedBody());
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Eviction")]
    public void VayronMeta_Evict_DoesNotEvictDirty()
    {
        var meta = new VayronMeta(new VayronOid(1));
        var body = new byte[1000];

        meta.TryTransitionState(MaterializationState.Materializing);
        meta.MarkMaterialized(42, body);
        meta.MarkDirty();

        var freedBytes = meta.Evict(EvictionReason.MemoryPressure);

        Assert.AreEqual(0, freedBytes);
        Assert.AreEqual(MaterializationState.Dirty, meta.State);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("Eviction")]
    public void VayronMetaTable_RequestEviction_FreesMemory()
    {
        // Create some handles
        var handles = new List<object>();
        for (int i = 0; i < 10; i++)
        {
            var handle = new object();
            var meta = VayronMetaTable.GetOrCreate(handle, new VayronOid(2000 + i));
            var body = new byte[100];
            meta.TryTransitionState(MaterializationState.Materializing);
            meta.MarkMaterialized(1, body);
            handles.Add(handle);
        }

        var freed = VayronMetaTable.RequestEviction(500);

        // Should have freed some memory
        Assert.IsTrue(freed > 0);

        // Cleanup
        foreach (var handle in handles)
        {
            VayronMetaTable.Remove(handle);
        }
    }

    // =========================================================================
    // Access Tracking Tests
    // =========================================================================

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("AccessTracking")]
    public void VayronMeta_RecordAccess_UpdatesTimestamp()
    {
        var meta = new VayronMeta(new VayronOid(1));

        var before = meta.LastAccessTicks;
        Thread.Sleep(10);
        meta.RecordAccess();
        var after = meta.LastAccessTicks;

        Assert.IsTrue(after > before);
        Assert.AreEqual(1, meta.AccessCount);
    }

    [TestMethod]
    [TestCategory("Phase3")]
    [TestCategory("AccessTracking")]
    public void Handle_FieldAccess_TracksAccess()
    {
        using var env = CreateTestEnvironment();
        using var tx = env.WriteTransaction();

        var person = new Person(env) { Age = 25 };
        var meta = person.GetMetadata();
        var initialCount = meta!.AccessCount;

        // Access field multiple times
        _ = person.Age;
        _ = person.Age;
        _ = person.Age;

        Assert.IsTrue(meta.AccessCount > initialCount);
    }
}

/// <summary>
/// Base class for VAYRON tests providing common setup.
/// </summary>
public abstract class VayronTestBase
{
    private static int _tempDirCounter;

    protected VayronEnvironment CreateTestEnvironment()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "VayronTests",
            $"test_{Interlocked.Increment(ref _tempDirCounter)}_{Guid.NewGuid():N}");

        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);

        return new VayronEnvironment(new VayronEnvironmentOptions
        {
            Path = path,
            ForceDurability = false
        });
    }
}
