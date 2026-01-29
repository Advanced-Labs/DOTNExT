# T10: Test Suite

> **Work Package:** WP2.6
> **Dependencies:** T09 (VKernel Managed API)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Create comprehensive tests for all Phase 2 functionality, verifying storage operations, dirty tracking, transactions, and end-to-end persistence workflows.

---

## Background

Phase 2 introduces persistence via Voron. Testing must verify:
1. VUID generation and uniqueness
2. Object serialization/deserialization
3. Dirty tracking behavior
4. Transaction semantics (commit/rollback)
5. Storage operations (persist/materialize/delete)
6. End-to-end workflows

---

## Test Categories

### 1. VUID Tests

```csharp
namespace TDS.Tests
{
    public class VUIDTests
    {
        [Fact]
        public void VUID_New_GeneratesUnique()
        {
            var vuid1 = VUID.New();
            var vuid2 = VUID.New();

            Assert.False(vuid1.IsEmpty);
            Assert.False(vuid2.IsEmpty);
            Assert.NotEqual(vuid1, vuid2);
        }

        [Fact]
        public void VUID_New_IsTimeOrdered()
        {
            var vuid1 = VUID.New();
            Thread.Sleep(2);  // Ensure different timestamp
            var vuid2 = VUID.New();

            // UUID v7 is time-sortable
            Assert.True(vuid2.CompareTo(vuid1) > 0);
        }

        [Fact]
        public void VUID_Serialization_Roundtrip()
        {
            var vuid = VUID.New();
            var bytes = new byte[16];
            vuid.WriteBytes(bytes);

            var restored = VUID.FromBytes(bytes);
            Assert.Equal(vuid, restored);
        }

        [Fact]
        public void VUID_ToString_ParseRoundtrip()
        {
            var vuid = VUID.New();
            var str = vuid.ToString();

            var parsed = VUID.Parse(str);
            Assert.Equal(vuid, parsed);
        }

        [Fact]
        public void VUID_Empty_IsDefault()
        {
            var empty = VUID.Empty;
            Assert.True(empty.IsEmpty);
            Assert.Equal(default(VUID), empty);
        }

        [Fact]
        public void VUID_New_BulkUniqueness()
        {
            var vuids = new HashSet<VUID>();
            for (int i = 0; i < 10000; i++)
            {
                Assert.True(vuids.Add(VUID.New()));
            }
            Assert.Equal(10000, vuids.Count);
        }
    }
}
```

### 2. Dirty Tracking Tests

```csharp
public class DirtyTrackingTests
{
    [Fact]
    public void DirtyTracking_InitiallyClean()
    {
        var obj = VKernel.New<TestObject>();

        // New objects should be dirty (need initial persist)
        Assert.True(TypeDriverHelper.IsDirty(obj));
    }

    [Fact]
    public void DirtyTracking_WriteMarksDirty()
    {
        var obj = VKernel.New<TestObject>();
        VKernel.Persist(obj);  // Clear initial dirty state

        Assert.False(TypeDriverHelper.IsDirty(obj));

        obj.IntField = 42;

        Assert.True(TypeDriverHelper.IsDirty(obj));
    }

    [Fact]
    public void DirtyTracking_PersistClears()
    {
        var obj = VKernel.New<TestObject>();
        obj.IntField = 42;

        Assert.True(TypeDriverHelper.IsDirty(obj));

        VKernel.Persist(obj);

        Assert.False(TypeDriverHelper.IsDirty(obj));
    }

    [Fact]
    public void DirtyTracking_FlushClearsAll()
    {
        var obj1 = VKernel.New<TestObject>();
        var obj2 = VKernel.New<TestObject>();
        obj1.IntField = 1;
        obj2.IntField = 2;

        int flushed = VKernel.FlushAll();

        Assert.Equal(2, flushed);
        Assert.False(TypeDriverHelper.IsDirty(obj1));
        Assert.False(TypeDriverHelper.IsDirty(obj2));
    }

    [Fact]
    public void DirtyTracking_EnumerateDirty()
    {
        var obj1 = VKernel.New<TestObject>();
        var obj2 = VKernel.New<TestObject>();
        VKernel.FlushAll();  // Clear

        obj1.IntField = 1;  // Mark dirty

        var dirtyObjects = TypeDriverHelper.EnumerateDirtyObjects().ToList();

        Assert.Single(dirtyObjects);
        Assert.Same(obj1, dirtyObjects[0]);
    }

    [Fact]
    public void DirtyTracking_GetDirtyCount()
    {
        VKernel.FlushAll();  // Clear existing

        var obj1 = VKernel.New<TestObject>();
        var obj2 = VKernel.New<TestObject>();
        var obj3 = VKernel.New<TestObject>();

        Assert.Equal(3, VKernel.GetPendingFlushCount());

        VKernel.Persist(obj1);

        Assert.Equal(2, VKernel.GetPendingFlushCount());
    }
}
```

### 3. Body Encoder Tests

```csharp
public class BodyEncoderTests
{
    [Fact]
    public void BodyEncoder_SerializePrimitives()
    {
        var obj = new PrimitiveObject
        {
            IntField = 42,
            LongField = 123456789L,
            FloatField = 3.14f,
            DoubleField = 2.71828,
            BoolField = true,
            CharField = 'X',
            ByteField = 0xAB,
        };

        var bytes = BodyEncoder.Serialize(obj);
        var restored = BodyEncoder.Deserialize<PrimitiveObject>(bytes);

        Assert.Equal(obj.IntField, restored.IntField);
        Assert.Equal(obj.LongField, restored.LongField);
        Assert.Equal(obj.FloatField, restored.FloatField);
        Assert.Equal(obj.DoubleField, restored.DoubleField);
        Assert.Equal(obj.BoolField, restored.BoolField);
        Assert.Equal(obj.CharField, restored.CharField);
        Assert.Equal(obj.ByteField, restored.ByteField);
    }

    [Fact]
    public void BodyEncoder_SerializeString()
    {
        var obj = new StringObject
        {
            Name = "Hello, World!",
            Description = null,
        };

        var bytes = BodyEncoder.Serialize(obj);
        var restored = BodyEncoder.Deserialize<StringObject>(bytes);

        Assert.Equal("Hello, World!", restored.Name);
        Assert.Null(restored.Description);
    }

    [Fact]
    public void BodyEncoder_SerializeVUID()
    {
        var vuid = VUID.New();
        var obj = new VUIDObject { Reference = vuid };

        var bytes = BodyEncoder.Serialize(obj);
        var restored = BodyEncoder.Deserialize<VUIDObject>(bytes);

        Assert.Equal(vuid, restored.Reference);
    }

    [Fact]
    public void BodyEncoder_SerializeNested()
    {
        var obj = new NestedObject
        {
            Name = "Parent",
            Child = new NestedObject { Name = "Child", Child = null }
        };

        var bytes = BodyEncoder.Serialize(obj);
        var restored = BodyEncoder.Deserialize<NestedObject>(bytes);

        Assert.Equal("Parent", restored.Name);
        Assert.NotNull(restored.Child);
        Assert.Equal("Child", restored.Child.Name);
        Assert.Null(restored.Child.Child);
    }

    [Fact]
    public void BodyEncoder_SerializeArray()
    {
        var obj = new ArrayObject
        {
            Numbers = new[] { 1, 2, 3, 4, 5 },
            Names = new[] { "A", "B", "C" }
        };

        var bytes = BodyEncoder.Serialize(obj);
        var restored = BodyEncoder.Deserialize<ArrayObject>(bytes);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, restored.Numbers);
        Assert.Equal(new[] { "A", "B", "C" }, restored.Names);
    }

    [Fact]
    public void BodyEncoder_HandlesMissingFields()
    {
        // Simulate old version without new field
        var oldObj = new OldVersion { Id = 1, Name = "Test" };
        var bytes = BodyEncoder.Serialize(oldObj);

        // Deserialize as new version with extra field
        var newObj = BodyEncoder.Deserialize<NewVersion>(bytes);

        Assert.Equal(1, newObj.Id);
        Assert.Equal("Test", newObj.Name);
        Assert.Equal(default(int), newObj.NewField);  // Default value
    }

    class OldVersion { public int Id; public string Name; }
    class NewVersion { public int Id; public string Name; public int NewField; }
}
```

### 4. Storage Operations Tests

```csharp
public class StorageOperationsTests
{
    [Fact]
    public void Storage_PersistAndMaterialize()
    {
        var obj = VKernel.New<TestObject>();
        obj.IntField = 42;
        obj.StringField = "Hello";

        VKernel.Persist(obj);

        var vuid = TypeDriverHelper.GetVUID(obj);
        var loaded = VKernel.Get<TestObject>(vuid);

        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.IntField);
        Assert.Equal("Hello", loaded.StringField);
    }

    [Fact]
    public void Storage_GetNonExistent_ReturnsNull()
    {
        var randomVuid = VUID.New();
        var result = VKernel.Get<TestObject>(randomVuid);

        Assert.Null(result);
    }

    [Fact]
    public void Storage_Exists()
    {
        var obj = VKernel.New<TestObject>();
        var vuid = TypeDriverHelper.GetVUID(obj);

        Assert.False(VKernel.Exists(vuid));  // Not yet persisted

        VKernel.Persist(obj);

        Assert.True(VKernel.Exists(vuid));
    }

    [Fact]
    public void Storage_Delete()
    {
        var obj = VKernel.New<TestObject>();
        VKernel.Persist(obj);

        var vuid = TypeDriverHelper.GetVUID(obj);
        Assert.True(VKernel.Exists(vuid));

        var deleted = VKernel.Delete(vuid);

        Assert.True(deleted);
        Assert.False(VKernel.Exists(vuid));
    }

    [Fact]
    public void Storage_DeleteNonExistent_ReturnsFalse()
    {
        var randomVuid = VUID.New();
        var deleted = VKernel.Delete(randomVuid);

        Assert.False(deleted);
    }

    [Fact]
    public void Storage_Update()
    {
        var obj = VKernel.New<TestObject>();
        obj.IntField = 1;
        VKernel.Persist(obj);

        var vuid = TypeDriverHelper.GetVUID(obj);

        // Modify and re-persist
        obj.IntField = 2;
        VKernel.Persist(obj);

        // Load and verify
        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.Equal(2, loaded.IntField);
    }

    [Fact]
    public void Storage_GetOrNew_ExistingReturnsExisting()
    {
        var obj = VKernel.New<TestObject>();
        obj.IntField = 42;
        VKernel.Persist(obj);

        var vuid = TypeDriverHelper.GetVUID(obj);
        var result = VKernel.GetOrNew<TestObject>(vuid);

        Assert.Equal(42, result.IntField);
    }

    [Fact]
    public void Storage_GetOrNew_NonExistingCreatesNew()
    {
        var randomVuid = VUID.New();
        var result = VKernel.GetOrNew<TestObject>(randomVuid);

        Assert.NotNull(result);
        Assert.Equal(default(int), result.IntField);
    }
}
```

### 5. Transaction Tests

```csharp
public class TransactionTests
{
    [Fact]
    public void Transaction_Commit()
    {
        var obj = VKernel.New<TestObject>();

        using (var tx = VKernel.BeginTransaction())
        {
            obj.IntField = 42;
            tx.Commit();
        }

        Assert.False(TypeDriverHelper.IsDirty(obj));

        // Verify persisted
        var vuid = TypeDriverHelper.GetVUID(obj);
        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.Equal(42, loaded.IntField);
    }

    [Fact]
    public void Transaction_Rollback()
    {
        var obj = VKernel.New<TestObject>();
        VKernel.Persist(obj);  // Initial state
        var vuid = TypeDriverHelper.GetVUID(obj);

        using (var tx = VKernel.BeginTransaction())
        {
            obj.IntField = 999;
            tx.Rollback();
        }

        // Changes should NOT be persisted
        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.NotEqual(999, loaded.IntField);
    }

    [Fact]
    public void Transaction_AutoRollbackOnDispose()
    {
        var obj = VKernel.New<TestObject>();
        VKernel.Persist(obj);
        var vuid = TypeDriverHelper.GetVUID(obj);

        using (var tx = VKernel.BeginTransaction())
        {
            obj.IntField = 999;
            // No commit - should auto-rollback
        }

        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.NotEqual(999, loaded.IntField);
    }

    [Fact]
    public void Transaction_WithTransactionAction()
    {
        var obj = VKernel.New<TestObject>();

        VKernel.WithTransaction(() =>
        {
            obj.IntField = 42;
            obj.StringField = "Test";
        });

        Assert.False(TypeDriverHelper.IsDirty(obj));
    }

    [Fact]
    public void Transaction_WithTransactionFunc()
    {
        var result = VKernel.WithTransaction(() =>
        {
            var obj = VKernel.New<TestObject>();
            obj.IntField = 42;
            return TypeDriverHelper.GetVUID(obj);
        });

        Assert.False(result.IsEmpty);
        var loaded = VKernel.Get<TestObject>(result);
        Assert.Equal(42, loaded.IntField);
    }

    [Fact]
    public void Transaction_ExceptionRollsBack()
    {
        var obj = VKernel.New<TestObject>();
        VKernel.Persist(obj);
        var vuid = TypeDriverHelper.GetVUID(obj);

        Assert.Throws<InvalidOperationException>(() =>
        {
            VKernel.WithTransaction(() =>
            {
                obj.IntField = 999;
                throw new InvalidOperationException("Test exception");
            });
        });

        // Should have rolled back
        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.NotEqual(999, loaded.IntField);
    }

    [Fact]
    public void Transaction_BatchedPersist()
    {
        var objects = new List<TestObject>();

        VKernel.WithTransaction(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var obj = VKernel.New<TestObject>();
                obj.IntField = i;
                objects.Add(obj);
            }
        });

        // All should be persisted
        foreach (var obj in objects)
        {
            Assert.False(TypeDriverHelper.IsDirty(obj));
        }
    }
}
```

### 6. End-to-End Tests

```csharp
public class EndToEndTests
{
    [Fact]
    public void E2E_CreateModifyPersistLoad()
    {
        // Create
        var customer = VKernel.New<Customer>();
        customer.Name = "John Doe";
        customer.Email = "john@example.com";

        // Persist
        VKernel.Persist(customer);
        var vuid = TypeDriverHelper.GetVUID(customer);

        // Simulate app restart by clearing managed reference
        customer = null;
        GC.Collect();

        // Load
        var loaded = VKernel.Get<Customer>(vuid);

        Assert.NotNull(loaded);
        Assert.Equal("John Doe", loaded.Name);
        Assert.Equal("john@example.com", loaded.Email);
    }

    [Fact]
    public void E2E_ComplexObjectGraph()
    {
        var order = VKernel.New<Order>();
        order.OrderNumber = "ORD-001";
        order.Items.Add(new OrderItem { ProductId = 1, Quantity = 2, Price = 10.00m });
        order.Items.Add(new OrderItem { ProductId = 2, Quantity = 1, Price = 25.00m });

        VKernel.Persist(order);
        var vuid = TypeDriverHelper.GetVUID(order);

        var loaded = VKernel.Get<Order>(vuid);

        Assert.Equal("ORD-001", loaded.OrderNumber);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(45.00m, loaded.Items.Sum(i => i.Price * i.Quantity));
    }

    [Fact]
    public void E2E_ReferentialIntegrity()
    {
        var customer = VKernel.New<Customer>();
        customer.Name = "Alice";
        VKernel.Persist(customer);
        var customerVuid = TypeDriverHelper.GetVUID(customer);

        var order = VKernel.New<Order>();
        order.CustomerVuid = customerVuid;
        order.OrderNumber = "ORD-002";
        VKernel.Persist(order);
        var orderVuid = TypeDriverHelper.GetVUID(order);

        // Load order and resolve customer
        var loadedOrder = VKernel.Get<Order>(orderVuid);
        var loadedCustomer = VKernel.Get<Customer>(loadedOrder.CustomerVuid);

        Assert.Equal("Alice", loadedCustomer.Name);
    }

    [Fact]
    public void E2E_BulkOperations()
    {
        const int Count = 1000;
        var vuids = new List<VUID>();

        // Bulk create and persist
        VKernel.WithTransaction(() =>
        {
            for (int i = 0; i < Count; i++)
            {
                var item = VKernel.New<TestObject>();
                item.IntField = i;
                vuids.Add(TypeDriverHelper.GetVUID(item));
            }
        });

        // Verify all persisted
        for (int i = 0; i < Count; i++)
        {
            var loaded = VKernel.Get<TestObject>(vuids[i]);
            Assert.Equal(i, loaded.IntField);
        }
    }

    [Fact]
    public void E2E_DurabilityAfterRestart()
    {
        // This test simulates durability by:
        // 1. Creating and persisting objects
        // 2. Shutting down VKernel
        // 3. Re-initializing VKernel
        // 4. Verifying objects still exist

        var vuid = VUID.New();

        // Phase 1: Create
        var obj = VKernel.New<TestObject>(vuid);
        obj.IntField = 12345;
        VKernel.Persist(obj);

        // Phase 2: Shutdown
        VKernel.Shutdown();

        // Phase 3: Reinitialize
        VKernel.Initialize();

        // Phase 4: Verify
        var loaded = VKernel.Get<TestObject>(vuid);
        Assert.NotNull(loaded);
        Assert.Equal(12345, loaded.IntField);
    }
}
```

---

## Test Domain Objects

```csharp
[Virtual]
public class TestObject
{
    public int IntField;
    public string StringField;
    public double DoubleField;
}

[Virtual]
public class PrimitiveObject
{
    public int IntField;
    public long LongField;
    public float FloatField;
    public double DoubleField;
    public bool BoolField;
    public char CharField;
    public byte ByteField;
}

[Virtual]
public class StringObject
{
    public string Name;
    public string Description;
}

[Virtual]
public class VUIDObject
{
    public VUID Reference;
}

[Virtual]
public class NestedObject
{
    public string Name;
    public NestedObject Child;
}

[Virtual]
public class ArrayObject
{
    public int[] Numbers;
    public string[] Names;
}

[Virtual]
public class Customer
{
    public string Name;
    public string Email;
}

[Virtual]
public class Order
{
    public string OrderNumber;
    public VUID CustomerVuid;
    public List<OrderItem> Items = new();
}

public class OrderItem
{
    public int ProductId;
    public int Quantity;
    public decimal Price;
}
```

---

## Test Execution

### TAI Build Test Pattern

```
TAI Build Test #XX - Phase 2 Verification

Steps:
1. Build runtime with TDS Phase 2 infrastructure
2. Build CoreLib with VKernel API
3. Run TDSPhase2Verification.exe

Expected: ALL tests PASS

Results:
[Test output will show here]
```

### Verification Console App

**File:** `TDSPhase2Verification/Program.cs`

```csharp
class Program
{
    static int Main()
    {
        Console.WriteLine("=== TDS Phase 2 Verification ===\n");

        int passed = 0, failed = 0;

        // VUID tests
        RunTest("VUID_New", VUIDTests.VUID_New_GeneratesUnique, ref passed, ref failed);
        RunTest("VUID_Roundtrip", VUIDTests.VUID_Serialization_Roundtrip, ref passed, ref failed);

        // Storage tests
        RunTest("Storage_PersistLoad", StorageTests.Storage_PersistAndMaterialize, ref passed, ref failed);
        RunTest("Storage_Delete", StorageTests.Storage_Delete, ref passed, ref failed);

        // Dirty tracking
        RunTest("Dirty_WriteMarks", DirtyTests.DirtyTracking_WriteMarksDirty, ref passed, ref failed);
        RunTest("Dirty_FlushClears", DirtyTests.DirtyTracking_FlushClearsAll, ref passed, ref failed);

        // Transactions
        RunTest("Transaction_Commit", TransactionTests.Transaction_Commit, ref passed, ref failed);
        RunTest("Transaction_Rollback", TransactionTests.Transaction_Rollback, ref passed, ref failed);

        // End-to-end
        RunTest("E2E_Durability", E2ETests.E2E_DurabilityAfterRestart, ref passed, ref failed);
        RunTest("E2E_Bulk", E2ETests.E2E_BulkOperations, ref passed, ref failed);

        Console.WriteLine($"\n=== Results: {passed} PASSED, {failed} FAILED ===");
        return failed > 0 ? 1 : 0;
    }

    static void RunTest(string name, Action test, ref int passed, ref int failed)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {name}");
            passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {name}: {ex.Message}");
            failed++;
        }
    }
}
```

---

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `TDSPhase2Verification/Program.cs` | Create | Main test console app |
| `TDSPhase2Verification/VUIDTests.cs` | Create | VUID tests |
| `TDSPhase2Verification/DirtyTrackingTests.cs` | Create | Dirty tracking tests |
| `TDSPhase2Verification/BodyEncoderTests.cs` | Create | Serialization tests |
| `TDSPhase2Verification/StorageOperationsTests.cs` | Create | Storage tests |
| `TDSPhase2Verification/TransactionTests.cs` | Create | Transaction tests |
| `TDSPhase2Verification/EndToEndTests.cs` | Create | E2E workflow tests |
| `TDSPhase2Verification/TestDomainObjects.cs` | Create | Test domain classes |

---

## Acceptance Criteria

- [ ] All VUID tests pass
- [ ] All dirty tracking tests pass
- [ ] All body encoder tests pass
- [ ] All storage operation tests pass
- [ ] All transaction tests pass
- [ ] All end-to-end tests pass
- [ ] Durability test verifies data survives restart
- [ ] Bulk operations complete without errors
- [ ] No memory leaks in stress tests

---

## References

- Phase 2 Main Doc: Section 13 (WP2.6 Tests)
- Phase 1 T08: Test Suite (pattern reference)
- All Phase 2 task files (T01-T09)
