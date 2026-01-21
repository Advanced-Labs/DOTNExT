// VAYRON - Basic unit tests for Phase 1 implementation

using Xunit;

namespace Vayron.Tests;

public class VayronBasicTests : IDisposable
{
    private readonly string _testPath;
    private readonly VayronEnvironment _env;

    public VayronBasicTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), "vayron-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testPath);

        _env = new VayronEnvironment(new VayronEnvironmentOptions
        {
            Path = _testPath,
            ForceDurability = false // Faster tests
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

    [Fact]
    public void CanCreateEnvironment()
    {
        Assert.NotNull(_env);
        Assert.True(_env.IsNew);
    }

    [Fact]
    public void CanGenerateOids()
    {
        var oid1 = _env.GenerateOid();
        var oid2 = _env.GenerateOid();
        var oid3 = _env.GenerateOid();

        Assert.True(oid1.IsValid);
        Assert.True(oid2.IsValid);
        Assert.True(oid3.IsValid);
        Assert.NotEqual(oid1, oid2);
        Assert.NotEqual(oid2, oid3);
        Assert.True(oid2 > oid1);
        Assert.True(oid3 > oid2);
    }

    [Fact]
    public void CanCreateAndReadPerson()
    {
        VayronOid savedOid;

        // Create and save
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env)
            {
                Age = 30,
                Salary = 75000,
                IsActive = true
            };

            savedOid = person.Oid;
            Assert.True(savedOid.IsValid);

            tx.Commit();
        }

        // Read back
        using (var tx = _env.ReadTransaction())
        {
            var person = new Person(_env, savedOid);

            Assert.Equal(30, person.Age);
            Assert.Equal(75000, person.Salary);
            Assert.True(person.IsActive);
        }
    }

    [Fact]
    public void CanUpdatePerson()
    {
        VayronOid savedOid;

        // Create
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env)
            {
                Age = 25,
                Salary = 50000,
                IsActive = true
            };

            savedOid = person.Oid;
            tx.Commit();
        }

        // Update
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env, savedOid)
            {
                Age = 26,
                Salary = 55000
            };

            tx.Commit();
        }

        // Verify
        using (var tx = _env.ReadTransaction())
        {
            var person = new Person(_env, savedOid);

            Assert.Equal(26, person.Age);
            Assert.Equal(55000, person.Salary);
            Assert.True(person.IsActive); // Unchanged
        }
    }

    [Fact]
    public void CanDeletePerson()
    {
        VayronOid savedOid;

        // Create
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env)
            {
                Age = 30,
                Salary = 60000,
                IsActive = true
            };

            savedOid = person.Oid;
            tx.Commit();
        }

        // Delete
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env, savedOid);
            person.Delete();

            tx.Commit();
        }

        // Verify deleted - should throw or return empty
        using (var tx = _env.ReadTransaction())
        {
            var person = new Person(_env, savedOid);

            // After deletion, accessing fields should fail or return defaults
            // (depending on implementation - here we expect the OID lookup to fail)
            Assert.False(person.Oid.IsValid);
        }
    }

    [Fact]
    public void CanCreateProduct()
    {
        VayronOid savedOid;
        var productId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // Create
        using (var tx = _env.WriteTransaction())
        {
            var product = new Product(_env)
            {
                Price = 99.99m,
                Quantity = 100,
                CreatedAt = createdAt,
                ProductId = productId
            };

            savedOid = product.Oid;
            tx.Commit();
        }

        // Read back
        using (var tx = _env.ReadTransaction())
        {
            var product = new Product(_env, savedOid);

            Assert.Equal(99.99m, product.Price);
            Assert.Equal(100, product.Quantity);
            Assert.Equal(createdAt.Ticks, product.CreatedAt.Ticks);
            Assert.Equal(productId, product.ProductId);
        }
    }

    [Fact]
    public void TransactionRollbackDoesNotPersist()
    {
        VayronOid savedOid;

        // Create and commit
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env)
            {
                Age = 30,
                Salary = 60000,
                IsActive = true
            };

            savedOid = person.Oid;
            tx.Commit();
        }

        // Modify but don't commit (rollback)
        using (var tx = _env.WriteTransaction())
        {
            var person = new Person(_env, savedOid)
            {
                Age = 99,
                Salary = 999999
            };

            // No commit - transaction will rollback on dispose
        }

        // Verify original values
        using (var tx = _env.ReadTransaction())
        {
            var person = new Person(_env, savedOid);

            Assert.Equal(30, person.Age);
            Assert.Equal(60000, person.Salary);
        }
    }

    [Fact]
    public void MultipleEntitiesInOneTransaction()
    {
        VayronOid oid1, oid2, oid3;

        // Create multiple entities
        using (var tx = _env.WriteTransaction())
        {
            var p1 = new Person(_env) { Age = 20, Salary = 40000, IsActive = true };
            var p2 = new Person(_env) { Age = 30, Salary = 60000, IsActive = true };
            var p3 = new Person(_env) { Age = 40, Salary = 80000, IsActive = false };

            oid1 = p1.Oid;
            oid2 = p2.Oid;
            oid3 = p3.Oid;

            tx.Commit();
        }

        // Verify all
        using (var tx = _env.ReadTransaction())
        {
            var p1 = new Person(_env, oid1);
            var p2 = new Person(_env, oid2);
            var p3 = new Person(_env, oid3);

            Assert.Equal(20, p1.Age);
            Assert.Equal(30, p2.Age);
            Assert.Equal(40, p3.Age);

            Assert.Equal(40000, p1.Salary);
            Assert.Equal(60000, p2.Salary);
            Assert.Equal(80000, p3.Salary);

            Assert.True(p1.IsActive);
            Assert.True(p2.IsActive);
            Assert.False(p3.IsActive);
        }
    }

    [Fact]
    public void TypeRegistryCreatesCorrectSchema()
    {
        var schema = VayronTypeRegistry.Register<Person>();

        Assert.NotNull(schema);
        Assert.Equal(typeof(Person), schema.ClrType);
        Assert.Equal((ushort)1, schema.SchemaVersion);
        Assert.True(schema.TypeToken != 0);
        Assert.Equal(3, schema.Fields.Length);

        // Verify fields
        Assert.Equal("Age", schema.Fields[0].Name);
        Assert.Equal(typeof(int), schema.Fields[0].FieldType);

        Assert.Equal("Salary", schema.Fields[1].Name);
        Assert.Equal(typeof(long), schema.Fields[1].FieldType);

        Assert.Equal("IsActive", schema.Fields[2].Name);
        Assert.Equal(typeof(bool), schema.Fields[2].FieldType);
    }
}
