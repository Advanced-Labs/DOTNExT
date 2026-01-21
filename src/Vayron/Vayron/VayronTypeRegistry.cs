// VAYRON - Runtime-Integrated Persistent Storage
// Type registry for schema management

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Vayron;

/// <summary>
/// Schema information for a registered VAYRON type.
/// </summary>
public sealed class VayronTypeSchema
{
    /// <summary>
    /// The unique type token.
    /// </summary>
    public uint TypeToken { get; init; }

    /// <summary>
    /// The CLR type this schema represents.
    /// </summary>
    public Type ClrType { get; init; } = null!;

    /// <summary>
    /// Current schema version.
    /// </summary>
    public ushort SchemaVersion { get; init; }

    /// <summary>
    /// Total size of the body (excluding header).
    /// </summary>
    public int BodySize { get; init; }

    /// <summary>
    /// Field definitions in order.
    /// </summary>
    public VayronFieldSchema[] Fields { get; init; } = [];
}

/// <summary>
/// Schema information for a single field.
/// </summary>
public sealed class VayronFieldSchema
{
    /// <summary>
    /// Field name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Offset within the body (after header).
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// Size of the field in bytes.
    /// </summary>
    public int Size { get; init; }

    /// <summary>
    /// The CLR type of the field.
    /// </summary>
    public Type FieldType { get; init; } = null!;

    /// <summary>
    /// Whether this is a reference to another VAYRON handle.
    /// </summary>
    public bool IsReference { get; init; }
}

/// <summary>
/// Attribute to mark a class as a VAYRON persistent type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class VayronPersistentAttribute : Attribute
{
    /// <summary>
    /// Optional explicit type token. If not specified, one is generated.
    /// </summary>
    public uint TypeToken { get; set; }

    /// <summary>
    /// Schema version. Default is 1.
    /// </summary>
    public ushort SchemaVersion { get; set; } = 1;
}

/// <summary>
/// Attribute to mark a field as persistent.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class VayronFieldAttribute : Attribute
{
    /// <summary>
    /// Order of this field in the body layout.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Optional explicit size. If not specified, size is inferred from type.
    /// </summary>
    public int Size { get; set; } = -1;
}

/// <summary>
/// Registry for VAYRON type schemas.
/// </summary>
public static class VayronTypeRegistry
{
    private static readonly ConcurrentDictionary<Type, VayronTypeSchema> _schemasByType = new();
    private static readonly ConcurrentDictionary<uint, VayronTypeSchema> _schemasByToken = new();
    private static uint _nextTypeToken = 1000; // Reserve low numbers for built-in types

    /// <summary>
    /// Registers a type and returns its schema.
    /// </summary>
    public static VayronTypeSchema Register<T>() where T : VayronHandle
    {
        return Register(typeof(T));
    }

    /// <summary>
    /// Registers a type and returns its schema.
    /// </summary>
    public static VayronTypeSchema Register(Type type)
    {
        if (_schemasByType.TryGetValue(type, out var existing))
        {
            return existing;
        }

        var attr = type.GetCustomAttribute<VayronPersistentAttribute>();
        var typeToken = attr?.TypeToken ?? GenerateTypeToken(type);
        var schemaVersion = attr?.SchemaVersion ?? 1;

        // Scan for fields
        var fields = new List<VayronFieldSchema>();
        int currentOffset = 0;

        var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<VayronFieldAttribute>() != null)
            .Select(m => (Member: m, Attr: m.GetCustomAttribute<VayronFieldAttribute>()!))
            .OrderBy(x => x.Attr.Order)
            .ToList();

        foreach (var (member, fieldAttr) in members)
        {
            Type fieldType;
            if (member is PropertyInfo prop)
            {
                fieldType = prop.PropertyType;
            }
            else if (member is FieldInfo field)
            {
                fieldType = field.FieldType;
            }
            else
            {
                continue;
            }

            var fieldSize = fieldAttr.Size > 0 ? fieldAttr.Size : GetTypeSize(fieldType);
            var isReference = typeof(VayronHandle).IsAssignableFrom(fieldType);

            fields.Add(new VayronFieldSchema
            {
                Name = member.Name,
                Offset = currentOffset,
                Size = fieldSize,
                FieldType = fieldType,
                IsReference = isReference
            });

            currentOffset += fieldSize;
            // Align to 8 bytes for next field
            currentOffset = (currentOffset + 7) & ~7;
        }

        var schema = new VayronTypeSchema
        {
            TypeToken = typeToken,
            ClrType = type,
            SchemaVersion = schemaVersion,
            BodySize = currentOffset,
            Fields = fields.ToArray()
        };

        _schemasByType[type] = schema;
        _schemasByToken[typeToken] = schema;

        return schema;
    }

    /// <summary>
    /// Gets the schema for a type.
    /// </summary>
    public static VayronTypeSchema? GetSchema<T>() where T : VayronHandle
    {
        return GetSchema(typeof(T));
    }

    /// <summary>
    /// Gets the schema for a type.
    /// </summary>
    public static VayronTypeSchema? GetSchema(Type type)
    {
        _schemasByType.TryGetValue(type, out var schema);
        return schema;
    }

    /// <summary>
    /// Gets the schema for a type token.
    /// </summary>
    public static VayronTypeSchema? GetSchema(uint typeToken)
    {
        _schemasByToken.TryGetValue(typeToken, out var schema);
        return schema;
    }

    /// <summary>
    /// Generates a type token from the type name.
    /// </summary>
    private static uint GenerateTypeToken(Type type)
    {
        // Use a hash of the full type name as the token
        var hash = (uint)type.FullName!.GetHashCode();
        if (hash == 0) hash = 1; // Ensure non-zero

        // Check for collision and increment if needed
        while (_schemasByToken.ContainsKey(hash))
        {
            hash++;
        }

        return hash;
    }

    /// <summary>
    /// Gets the size of a type for storage.
    /// </summary>
    private static int GetTypeSize(Type type)
    {
        if (type == typeof(bool)) return 1;
        if (type == typeof(byte)) return 1;
        if (type == typeof(sbyte)) return 1;
        if (type == typeof(short)) return 2;
        if (type == typeof(ushort)) return 2;
        if (type == typeof(int)) return 4;
        if (type == typeof(uint)) return 4;
        if (type == typeof(long)) return 8;
        if (type == typeof(ulong)) return 8;
        if (type == typeof(float)) return 4;
        if (type == typeof(double)) return 8;
        if (type == typeof(decimal)) return 16;
        if (type == typeof(Guid)) return 16;
        if (type == typeof(DateTime)) return 8;
        if (type == typeof(DateTimeOffset)) return 16;
        if (type == typeof(TimeSpan)) return 8;

        // For reference types (other handles), store the OID (8 bytes)
        if (typeof(VayronHandle).IsAssignableFrom(type))
        {
            return 8; // OID size
        }

        // For structs, try to get the size
        if (type.IsValueType)
        {
            try
            {
                return Marshal.SizeOf(type);
            }
            catch
            {
                // Fall through to default
            }
        }

        // Default for unknown types
        return 8;
    }
}
