// VAYRON Phase 4 Tests - Transaction Integration

using System.Collections.Concurrent;

namespace Vayron.Tests;

/// <summary>
/// Unit tests for VAYRON Phase 4 (Transaction Integration).
/// </summary>
public class VayronPhase4Tests : IDisposable
{
    private readonly string _testPath;
    private readonly VayronEnvironment _env;

    public VayronPhase4Tests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), "VayronPhase4Tests_" + Guid.NewGuid().ToString("N"));
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
        try { Directory.Delete(_testPath, recursive: true); }
        catch { /* ignore cleanup errors */ }
    }

    // =====================================================================
    // VayronTransactionContext Tests
    // =====================================================================

    [Fact]
    public void TransactionContext_TracksParticipants()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 30 };

        // Context should track participants
        Assert.True(tx.Context.ParticipantCount >= 0);

        // Enroll explicitly
        tx.Enroll(person);
        Assert.True(tx.Context.IsEnrolled(person.Oid));

        tx.Commit();
    }

    [Fact]
    public void TransactionContext_RecordsOperations()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 25 };

        // Access fields should record operations
        _ = person.Age;  // Read
        person.Age = 26; // Write

        // Context should track operations
        Assert.True(tx.Context.OperationCount > 0);
        Assert.True(tx.Context.WriteCount > 0);

        tx.Commit();
    }

    [Fact]
    public void TransactionContext_HasCorrectProperties()
    {
        using var tx = _env.WriteTransaction();

        Assert.Equal(TransactionState.Active, tx.Context.State);
        Assert.True(tx.Context.IsWriteTransaction);
        Assert.True(tx.Context.Epoch > 0);
        Assert.NotEqual(Guid.Empty, tx.Context.Id);

        tx.Commit();

        Assert.Equal(TransactionState.Committed, tx.Context.State);
    }

    [Fact]
    public void TransactionContext_SupportsMetadata()
    {
        using var tx = _env.WriteTransaction();

        tx.SetMetadata("key1", "value1");
        tx.SetMetadata("key2", 42);

        Assert.Equal("value1", tx.GetMetadata<string>("key1"));

        tx.Commit();
    }

    [Fact]
    public void TransactionContext_FiresCommittingEvent()
    {
        bool eventFired = false;

        using var tx = _env.WriteTransaction();

        tx.Context.Committing += (sender, args) =>
        {
            eventFired = true;
            Assert.Equal(tx.Context, args.Context);
        };

        var person = new Person(_env) { Age = 30 };
        tx.Commit();

        Assert.True(eventFired);
    }

    [Fact]
    public void TransactionContext_FiresCommittedEvent()
    {
        bool eventFired = false;
        TimeSpan duration = TimeSpan.Zero;

        using var tx = _env.WriteTransaction();

        tx.Context.Committed += (sender, args) =>
        {
            eventFired = true;
            duration = args.Duration;
            Assert.Equal(tx.Context, args.Context);
        };

        var person = new Person(_env) { Age = 30 };
        tx.Commit();

        Assert.True(eventFired);
        Assert.True(duration > TimeSpan.Zero);
    }

    [Fact]
    public void TransactionContext_FiresRolledBackEvent()
    {
        bool eventFired = false;
        string? rollbackReason = null;

        using var tx = _env.WriteTransaction();

        tx.Context.RolledBack += (sender, args) =>
        {
            eventFired = true;
            rollbackReason = args.Reason;
        };

        var person = new Person(_env) { Age = 30 };
        tx.Rollback("Test rollback");

        Assert.True(eventFired);
        Assert.Equal("Test rollback", rollbackReason);
    }

    // =====================================================================
    // Savepoint Tests
    // =====================================================================

    [Fact]
    public void Savepoint_CanCreateAndRelease()
    {
        using var tx = _env.WriteTransaction();

        var sp1 = tx.CreateSavepoint("SP1");
        Assert.Equal("SP1", sp1.Name);
        Assert.Equal(1, tx.Context.SavepointCount);

        var sp2 = tx.CreateSavepoint("SP2");
        Assert.Equal(2, tx.Context.SavepointCount);

        tx.ReleaseSavepoint(sp2);
        Assert.Equal(1, tx.Context.SavepointCount);

        tx.ReleaseSavepoint(sp1);
        Assert.Equal(0, tx.Context.SavepointCount);

        tx.Commit();
    }

    [Fact]
    public void Savepoint_RollbackInvalidatesHandles()
    {
        using var tx = _env.WriteTransaction();

        // Create initial object
        var person1 = new Person(_env) { Age = 30 };
        tx.Enroll(person1);

        // Create savepoint
        var sp = tx.CreateSavepoint();

        // Create another object after savepoint
        var person2 = new Person(_env) { Age = 40 };
        tx.Enroll(person2);

        // Rollback to savepoint - person2 should be affected
        tx.RollbackToSavepoint(sp);

        // person1 should still be enrolled (created before savepoint)
        Assert.True(tx.Context.IsEnrolled(person1.Oid));

        tx.Commit();
    }

    [Fact]
    public void Savepoint_OnlyAllowedInWriteTransaction()
    {
        using var tx = _env.ReadTransaction();

        Assert.Throws<InvalidOperationException>(() => tx.CreateSavepoint());
    }

    // =====================================================================
    // VayronTransactionManager Tests
    // =====================================================================

    [Fact]
    public void TransactionManager_TracksActiveTransactions()
    {
        var manager = VayronTransactionManager.Instance;

        using var tx = _env.WriteTransaction();

        Assert.True(manager.ActiveTransactionCount >= 1);
        Assert.True(manager.HasActiveWriteTransaction);
    }

    [Fact]
    public void TransactionManager_RecordsStatistics()
    {
        var manager = VayronTransactionManager.Instance;
        manager.ResetStatistics();

        // Create and commit a write transaction
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env) { Age = 30 };
            tx.Commit();
        }

        var stats = manager.GetStatistics();
        Assert.True(stats.TotalWriteTransactions >= 1);
        Assert.True(stats.TotalCommits >= 1);
    }

    [Fact]
    public void TransactionManager_FiresTransactionStartedEvent()
    {
        var manager = VayronTransactionManager.Instance;
        bool eventFired = false;

        manager.TransactionStarted += (sender, args) =>
        {
            eventFired = true;
        };

        using var tx = _env.WriteTransaction();
        tx.Commit();

        Assert.True(eventFired);
    }

    [Fact]
    public void TransactionManager_FiresTransactionCommittedEvent()
    {
        var manager = VayronTransactionManager.Instance;
        bool eventFired = false;

        manager.TransactionCommitted += (sender, args) =>
        {
            eventFired = true;
            Assert.True(args.WasCommitted);
        };

        using var tx = _env.WriteTransaction();
        var person = new Person(_env) { Age = 30 };
        tx.Commit();

        Assert.True(eventFired);
    }

    [Fact]
    public void TransactionManager_ExecuteInWriteTransaction()
    {
        var manager = VayronTransactionManager.Instance;
        VayronOid oid = VayronOid.Invalid;

        manager.ExecuteInWriteTransaction(_env, tx =>
        {
            var person = new Person(_env) { Age = 50 };
            oid = person.Oid;
        });

        // Verify the object was persisted
        Assert.True(oid.IsValid);

        using var readTx = _env.ReadTransaction();
        var loaded = new Person(_env, oid);
        Assert.Equal(50, loaded.Age);
    }

    [Fact]
    public async Task TransactionManager_ExecuteInWriteTransactionAsync()
    {
        var manager = VayronTransactionManager.Instance;
        VayronOid oid = VayronOid.Invalid;

        await manager.ExecuteInWriteTransactionAsync(_env, async tx =>
        {
            await Task.Yield(); // Simulate async work
            var person = new Person(_env) { Age = 60 };
            oid = person.Oid;
        });

        // Verify the object was persisted
        Assert.True(oid.IsValid);

        using var readTx = _env.ReadTransaction();
        var loaded = new Person(_env, oid);
        Assert.Equal(60, loaded.Age);
    }

    // =====================================================================
    // Transaction Timeout Tests
    // =====================================================================

    [Fact]
    public void Transaction_WithTimeout_TracksElapsedTime()
    {
        using var tx = VayronTransaction.BeginWrite(_env, TimeSpan.FromSeconds(30));

        Assert.False(tx.Context.IsTimedOut);
        Assert.NotNull(tx.Context.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), tx.Context.Timeout);

        // Elapsed time should be greater than zero
        Thread.Sleep(10);
        Assert.True(tx.Context.Elapsed > TimeSpan.Zero);

        tx.Commit();
    }

    [Fact]
    public void Transaction_CommitThrowsOnTimeout()
    {
        using var tx = VayronTransaction.BeginWrite(_env, TimeSpan.FromMilliseconds(1));

        // Wait for timeout
        Thread.Sleep(50);

        Assert.True(tx.Context.IsTimedOut);
        Assert.Throws<TransactionAbortedException>(() => tx.Commit());
    }

    // =====================================================================
    // VayronTransaction Static Methods Tests
    // =====================================================================

    [Fact]
    public void VayronTransaction_HasActiveTransaction()
    {
        Assert.False(VayronTransaction.HasActiveTransaction);

        using var tx = _env.ReadTransaction();
        Assert.True(VayronTransaction.HasActiveTransaction);
    }

    [Fact]
    public void VayronTransaction_HasActiveWriteTransaction()
    {
        Assert.False(VayronTransaction.HasActiveWriteTransaction);

        using (var tx = _env.ReadTransaction())
        {
            Assert.False(VayronTransaction.HasActiveWriteTransaction);
        }

        using (var tx = _env.WriteTransaction())
        {
            Assert.True(VayronTransaction.HasActiveWriteTransaction);
        }
    }

    [Fact]
    public void VayronTransaction_Require_ThrowsWhenNoTransaction()
    {
        Assert.Throws<InvalidOperationException>(() => VayronTransaction.Require());
    }

    [Fact]
    public void VayronTransaction_RequireWrite_ThrowsWhenReadTransaction()
    {
        using var tx = _env.ReadTransaction();
        Assert.Throws<InvalidOperationException>(() => VayronTransaction.RequireWrite());
    }

    [Fact]
    public void VayronTransaction_CurrentContext()
    {
        Assert.Null(VayronTransaction.CurrentContext);

        using var tx = _env.WriteTransaction();
        Assert.NotNull(VayronTransaction.CurrentContext);
        Assert.Same(tx.Context, VayronTransaction.CurrentContext);
    }

    [Fact]
    public void VayronTransaction_ExecuteRead()
    {
        int result = 0;

        VayronTransaction.ExecuteRead(_env, () =>
        {
            Assert.True(VayronTransaction.HasActiveTransaction);
            result = 42;
        });

        Assert.Equal(42, result);
        Assert.False(VayronTransaction.HasActiveTransaction);
    }

    [Fact]
    public void VayronTransaction_ExecuteWrite()
    {
        VayronOid oid = VayronOid.Invalid;

        VayronTransaction.ExecuteWrite(_env, () =>
        {
            Assert.True(VayronTransaction.HasActiveWriteTransaction);
            var person = new Person(_env) { Age = 70 };
            oid = person.Oid;
        });

        Assert.True(oid.IsValid);
        Assert.False(VayronTransaction.HasActiveTransaction);

        // Verify committed
        using var tx = _env.ReadTransaction();
        var loaded = new Person(_env, oid);
        Assert.Equal(70, loaded.Age);
    }

    [Fact]
    public async Task VayronTransaction_ExecuteReadAsync()
    {
        int result = 0;

        await VayronTransaction.ExecuteReadAsync(_env, async () =>
        {
            await Task.Yield();
            Assert.True(VayronTransaction.HasActiveTransaction);
            result = 42;
        });

        Assert.Equal(42, result);
        Assert.False(VayronTransaction.HasActiveTransaction);
    }

    // =====================================================================
    // Handle Transaction Integration Tests
    // =====================================================================

    [Fact]
    public void Handle_IsEnrolledInTransaction()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 30 };

        // Force enrollment
        tx.Enroll(person);

        Assert.True(person.IsEnrolledInTransaction);
    }

    [Fact]
    public void Handle_WithReadTransaction_ReusesExisting()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 30 };

        // WithReadTransaction should use existing transaction
        person.WithReadTransaction(() =>
        {
            Assert.Same(tx, VayronTransaction.Current);
        });

        tx.Commit();
    }

    [Fact]
    public void Handle_TransactionContext_ReturnsCurrentContext()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env);

        Assert.Same(tx.Context, person.TransactionContext);
    }

    // =====================================================================
    // Nested Transaction Tests
    // =====================================================================

    [Fact]
    public void NestedWriteTransaction_SharesContext()
    {
        using var tx1 = _env.WriteTransaction();
        var ctx1 = tx1.Context;

        using var tx2 = _env.WriteTransaction();
        var ctx2 = tx2.Context;

        // Same transaction, same context
        Assert.Same(ctx1, ctx2);

        tx2.Commit();
        tx1.Commit();
    }

    [Fact]
    public void NestedReadTransaction_SharesContext()
    {
        using var tx1 = _env.ReadTransaction();
        var ctx1 = tx1.Context;

        using var tx2 = _env.ReadTransaction();
        var ctx2 = tx2.Context;

        // Same transaction, same context
        Assert.Same(ctx1, ctx2);
    }

    [Fact]
    public void CannotStartWriteInReadTransaction()
    {
        using var readTx = _env.ReadTransaction();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var writeTx = _env.WriteTransaction();
        });
    }

    // =====================================================================
    // Rollback Tests
    // =====================================================================

    [Fact]
    public void Rollback_InvalidatesHandles()
    {
        VayronOid oid;

        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env) { Age = 30 };
            oid = person.Oid;
            tx.Rollback();
        }

        // Object should not be persisted
        using var readTx = _env.ReadTransaction();
        Assert.False(_env.TryGetStorageLocation(readTx, oid, out _));
    }

    [Fact]
    public void DisposeWithoutCommit_RollsBack()
    {
        VayronOid oid;

        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env) { Age = 30 };
            oid = person.Oid;
            // No commit - dispose will rollback
        }

        // Object should not be persisted
        using var readTx = _env.ReadTransaction();
        Assert.False(_env.TryGetStorageLocation(readTx, oid, out _));
    }

    // =====================================================================
    // Transaction Summary Tests
    // =====================================================================

    [Fact]
    public void TransactionSummary_HasCorrectData()
    {
        using var tx = _env.WriteTransaction();

        var person = new Person(_env) { Age = 30 };
        _ = person.Age;
        person.Age = 31;

        var summary = tx.Context.GetSummary();

        Assert.Equal(tx.Context.Id, summary.Id);
        Assert.Equal(tx.Epoch, summary.Epoch);
        Assert.True(summary.IsWriteTransaction);
        Assert.Equal(TransactionState.Active, summary.State);
        Assert.True(summary.OperationCount > 0);

        tx.Commit();
    }

    // =====================================================================
    // AsyncLocal Flow Tests
    // =====================================================================

    [Fact]
    public async Task Transaction_FlowsAcrossAsyncAwait()
    {
        using var tx = _env.WriteTransaction();
        var contextId = tx.Context.Id;

        await Task.Yield();

        // Transaction should still be available after await
        Assert.Same(tx, VayronTransaction.Current);
        Assert.Equal(contextId, VayronTransaction.CurrentContext!.Id);

        await Task.Delay(1);

        // Still available
        Assert.Same(tx, VayronTransaction.Current);

        tx.Commit();
    }

    [Fact]
    public async Task Transaction_IsolatedBetweenTasks()
    {
        var task1Context = new TaskCompletionSource<Guid>();
        var task2Context = new TaskCompletionSource<Guid>();

        var t1 = Task.Run(async () =>
        {
            using var tx = _env.ReadTransaction();
            task1Context.SetResult(tx.Context.Id);
            await Task.Delay(50);
            Assert.Equal(task1Context.Task.Result, VayronTransaction.CurrentContext!.Id);
        });

        var t2 = Task.Run(async () =>
        {
            using var tx = _env.ReadTransaction();
            task2Context.SetResult(tx.Context.Id);
            await Task.Delay(50);
            Assert.Equal(task2Context.Task.Result, VayronTransaction.CurrentContext!.Id);
        });

        await Task.WhenAll(t1, t2);

        // Each task had its own transaction context
        Assert.NotEqual(await task1Context.Task, await task2Context.Task);
    }

    // =====================================================================
    // Committing Event Cancellation Tests
    // =====================================================================

    [Fact]
    public void CommittingEvent_CanCancelCommit()
    {
        using var tx = _env.WriteTransaction();

        tx.Context.Committing += (sender, args) =>
        {
            args.Cancel = true;
        };

        var person = new Person(_env) { Age = 30 };

        Assert.Throws<TransactionAbortedException>(() => tx.Commit());
        Assert.Equal(TransactionState.Committing, tx.Context.State);
    }

    // =====================================================================
    // Operation Recording Event Tests
    // =====================================================================

    [Fact]
    public void OperationRecorded_FiresOnFieldAccess()
    {
        var recordedOperations = new List<(OperationType Type, VayronOid Oid)>();

        using var tx = _env.WriteTransaction();

        tx.Context.OperationRecorded += (sender, args) =>
        {
            recordedOperations.Add((args.Type, args.Oid));
        };

        var person = new Person(_env) { Age = 30 };
        _ = person.Age;
        person.Age = 31;

        // Should have recorded create, read, and write operations
        Assert.Contains(recordedOperations, op => op.Type == OperationType.Create);
        Assert.Contains(recordedOperations, op => op.Type == OperationType.Read);
        Assert.Contains(recordedOperations, op => op.Type == OperationType.Write);

        tx.Commit();
    }
}
