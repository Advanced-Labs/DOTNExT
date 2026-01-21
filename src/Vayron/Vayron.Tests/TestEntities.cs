// VAYRON - Test entities for unit tests

namespace Vayron.Tests;

/// <summary>
/// A simple persistent person entity for testing.
/// </summary>
[VayronPersistent(SchemaVersion = 1)]
public class Person : VayronEntity
{
    // Field layout:
    // Offset 0: Age (int, 4 bytes) -> aligned to 8
    // Offset 8: Salary (long, 8 bytes)
    // Offset 16: IsActive (bool, 1 byte) -> aligned to 8
    // Total: 24 bytes (with alignment)

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

    [VayronField(Order = 2)]
    public bool IsActive
    {
        get => GetField<bool>(16);
        set => SetField(16, value);
    }

    /// <summary>
    /// Creates a new Person with a new OID.
    /// </summary>
    public Person(VayronEnvironment env) : base(env) { }

    /// <summary>
    /// Loads an existing Person by OID.
    /// </summary>
    public Person(VayronEnvironment env, VayronOid oid) : base(env, oid) { }
}

/// <summary>
/// A more complex entity with various field types.
/// </summary>
[VayronPersistent(SchemaVersion = 1)]
public class Product : VayronEntity
{
    // Field layout:
    // Offset 0: Price (decimal, 16 bytes)
    // Offset 16: Quantity (int, 4 bytes) -> aligned to 8 = 24
    // Offset 24: CreatedAt (DateTime, 8 bytes)
    // Offset 32: ProductId (Guid, 16 bytes)
    // Total: 48 bytes

    [VayronField(Order = 0)]
    public decimal Price
    {
        get => GetField<decimal>(0);
        set => SetField(0, value);
    }

    [VayronField(Order = 1)]
    public int Quantity
    {
        get => GetField<int>(16);
        set => SetField(16, value);
    }

    [VayronField(Order = 2)]
    public DateTime CreatedAt
    {
        get => DateTime.FromBinary(GetField<long>(24));
        set => SetField(24, value.ToBinary());
    }

    [VayronField(Order = 3)]
    public Guid ProductId
    {
        get => GetField<Guid>(32);
        set => SetField(32, value);
    }

    public Product(VayronEnvironment env) : base(env) { }
    public Product(VayronEnvironment env, VayronOid oid) : base(env, oid) { }
}
