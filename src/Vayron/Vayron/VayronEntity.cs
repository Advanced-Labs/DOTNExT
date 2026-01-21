// VAYRON - Runtime-Integrated Persistent Storage
// Base class for user-defined persistent entities

namespace Vayron;

/// <summary>
/// Base class for user-defined VAYRON persistent entities.
/// Inherit from this class to create custom persistent types.
/// </summary>
/// <example>
/// <code>
/// [VayronPersistent]
/// public class Person : VayronEntity
/// {
///     [VayronField(Order = 0)]
///     public int Age
///     {
///         get => GetField&lt;int&gt;(0);
///         set => SetField(0, value);
///     }
///
///     [VayronField(Order = 1)]
///     public long Salary
///     {
///         get => GetField&lt;long&gt;(8);
///         set => SetField(8, value);
///     }
///
///     public Person(VayronEnvironment env) : base(env) { }
///     public Person(VayronEnvironment env, VayronOid oid) : base(env, oid) { }
/// }
/// </code>
/// </example>
public abstract class VayronEntity : VayronHandle
{
    private VayronTypeSchema? _schema;

    /// <summary>
    /// Gets the schema for this entity type.
    /// </summary>
    protected VayronTypeSchema Schema => _schema ??= VayronTypeRegistry.Register(GetType());

    /// <summary>
    /// Creates a new entity with a new OID.
    /// </summary>
    protected VayronEntity(VayronEnvironment environment) : base(environment)
    {
    }

    /// <summary>
    /// Creates an entity for an existing OID.
    /// </summary>
    protected VayronEntity(VayronEnvironment environment, VayronOid oid) : base(environment, oid)
    {
    }

    /// <inheritdoc />
    protected override int GetBodySize() => Schema.BodySize;

    /// <inheritdoc />
    protected override uint GetTypeToken() => Schema.TypeToken;

    /// <inheritdoc />
    protected override ushort GetSchemaVersion() => Schema.SchemaVersion;
}

/// <summary>
/// Static helper methods for working with VAYRON entities.
/// </summary>
public static class VayronEntityExtensions
{
    /// <summary>
    /// Loads an entity by OID.
    /// </summary>
    public static T Load<T>(this VayronEnvironment env, VayronOid oid) where T : VayronEntity
    {
        // Use reflection to call the (env, oid) constructor
        var ctor = typeof(T).GetConstructor([typeof(VayronEnvironment), typeof(VayronOid)])
            ?? throw new InvalidOperationException(
                $"Type {typeof(T).Name} must have a constructor (VayronEnvironment, VayronOid)");

        return (T)ctor.Invoke([env, oid]);
    }

    /// <summary>
    /// Creates a new entity.
    /// </summary>
    public static T Create<T>(this VayronEnvironment env) where T : VayronEntity
    {
        // Use reflection to call the (env) constructor
        var ctor = typeof(T).GetConstructor([typeof(VayronEnvironment)])
            ?? throw new InvalidOperationException(
                $"Type {typeof(T).Name} must have a constructor (VayronEnvironment)");

        return (T)ctor.Invoke([env]);
    }
}
