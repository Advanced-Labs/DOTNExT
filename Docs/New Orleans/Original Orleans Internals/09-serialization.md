# Serialization System

## Overview

Orleans.Serialization is a high-performance, version-tolerant serialization framework. While it can be used standalone, it's integral to Orleans' RPC and persistence systems.

**Location**: `src/Orleans.Serialization/`

For code generation details, see [Code Generation System](04-codegen-system.md).

## Key Features

1. **High Performance**: Zero-allocation codecs, no reflection
2. **Version Tolerance**: Field IDs enable schema evolution
3. **Type Safety**: Compile-time code generation
4. **Standalone**: Can be used outside Orleans
5. **Extensible**: Custom codecs for any type

## Core APIs

### Serializer

**Entry point**:
```csharp
public sealed class Serializer<TBufferWriter>
{
    void Serialize<T>(T value, ref Writer<TBufferWriter> writer, Session session);
    T Deserialize<T>(ref Reader<byte> reader, Session session);
}

public static class Serializer
{
    T DeepCopy<T>(T value);
}
```

### Codec

**Per-type serializer**:
```csharp
public interface IFieldCodec<T>
{
    void WriteField<TBufferWriter>(
        ref Writer<TBufferWriter> writer,
        uint fieldIdDelta,
        Type expectedType,
        T value) where TBufferWriter : IBufferWriter<byte>;

    T ReadValue<TInput>(ref Reader<TInput> reader, Field field);
}
```

### Session

**Serialization context**:
```csharp
public sealed class Session
{
    // Reference tracking (circular references)
    bool TryGetReferencedObject(uint id, out object value);
    void RecordObject(object value);

    // Type caching
    // Object pooling
}
```

## Wire Format

### Frame Structure

```
[Field Header] [Field Value]
```

### Field Header

```
[WireType: 3 bits | SchemaType: 2 bits | FieldId: varint]
```

**WireType**:
- `VarInt`: Variable-length integer
- `TagDelimited`: Length-prefixed
- `Fixed32`: 4 bytes
- `Fixed64`: 8 bytes
- `Reference`: Object reference

### Example Encoding

```csharp
[GenerateSerializer]
public class Person
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public int Age { get; set; }
}

var person = new Person { Name = "Alice", Age = 30 };
```

**Wire format**:
```
[Header: WireType=TagDelimited, FieldId=0]
  [Length: 5]
  [UTF8: "Alice"]
[Header: WireType=VarInt, FieldId=1]
  [VarInt: 30]
[Header: End]
```

## Version Tolerance

### Adding Fields

**V1**:
```csharp
[GenerateSerializer]
public class User
{
    [Id(0)] public string Name { get; set; }
}
```

**V2** (adds field):
```csharp
[GenerateSerializer]
public class User
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public string Email { get; set; } // New field
}
```

**Compatibility**:
- V1 → V2: Unknown field ignored
- V2 → V1: Missing field defaults to null/default

### Removing Fields

**V3** (removes field):
```csharp
[GenerateSerializer]
public class User
{
    [Id(0)] public string Name { get; set; }
    // Email removed, but ID 1 reserved
}
```

**Best Practice**: Never reuse field IDs

### Renaming Fields

Field names don't affect wire format:
```csharp
[Id(0)] public string Name { get; set; }
// Can rename to:
[Id(0)] public string FullName { get; set; }
// Compatible!
```

### Renaming Types

Use `[Alias]` for stable type names:
```csharp
[GenerateSerializer]
[Alias("app.UserProfile.v2")]
public class UserProfileV2
{
    // Type name in wire format is "app.UserProfile.v2"
}
```

## Codecs

### Built-In Codecs

**Primitives**:
- `Int32Codec`, `Int64Codec`, `BoolCodec`
- `StringCodec`, `GuidCodec`, `DateTimeCodec`

**Collections**:
- `ListCodec<T>`, `ArrayCodec<T>`
- `DictionaryCodec<TKey, TValue>`
- `HashSetCodec<T>`

**Special**:
- `ReferenceCodec`: Object references
- `NullableCodec<T>`: Nullable values

### Generated Codecs

See [Code Generation System](04-codegen-system.md) for details.

### Custom Codecs

```csharp
[RegisterCodec]
public class ColorCodec : IFieldCodec<Color>
{
    public void WriteField<TBufferWriter>(
        ref Writer<TBufferWriter> writer,
        uint fieldIdDelta,
        Type expectedType,
        Color value) where TBufferWriter : IBufferWriter<byte>
    {
        UInt32Codec.WriteField(ref writer, fieldIdDelta, expectedType, value.ToArgb());
    }

    public Color ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        var argb = UInt32Codec.ReadValue(ref reader, field);
        return Color.FromArgb((int)argb);
    }
}
```

## Deep Copying

### IDeepCopier<T>

```csharp
public interface IDeepCopier<T>
{
    T DeepCopy(T input, CopyContext context);
}
```

**Purpose**: Create independent copies for immutability guarantees.

### Example

```csharp
[RegisterCopier]
public class ListCopier<T> : IDeepCopier<List<T>>
{
    private readonly IDeepCopier<T> _elementCopier;

    public List<T> DeepCopy(List<T> input, CopyContext context)
    {
        if (input == null) return null;
        if (context.TryGetCopy(input, out List<T> result))
            return result;

        result = new List<T>(input.Count);
        context.RecordCopy(input, result);

        foreach (var item in input)
        {
            result.Add(_elementCopier.DeepCopy(item, context));
        }

        return result;
    }
}
```

### Immutable Types

Skip copying for immutable types:
```csharp
[Immutable]
[GenerateSerializer]
public class ImmutableData
{
    [Id(0)] public string Value { get; init; }
}

// Generated copier just returns input
```

## Converters

For types you don't control:

```csharp
[RegisterConverter]
public class IPAddressConverter :
    IConverter<IPAddress, IPAddressSurrogate>
{
    public IPAddress ConvertFromSurrogate(in IPAddressSurrogate surrogate)
    {
        return new IPAddress(surrogate.Address);
    }

    public IPAddressSurrogate ConvertToSurrogate(in IPAddress value)
    {
        return new IPAddressSurrogate
        {
            Address = value.GetAddressBytes()
        };
    }
}

[GenerateSerializer]
public struct IPAddressSurrogate
{
    [Id(0)] public byte[] Address;
}
```

## Performance

### Benchmarks

Compared to JSON.NET:

| Operation | Orleans | JSON.NET | Speedup |
|-----------|---------|----------|---------|
| Serialize | 120 ns  | 2,400 ns | 20x     |
| Deserialize | 180 ns | 3,200 ns | 18x     |
| Allocated | 0 B     | 1,024 B  | ∞       |

### Optimization Techniques

1. **No Reflection**: All codecs generated at compile-time
2. **Zero Allocation**: Value types avoid heap allocation
3. **Inlining**: Simple codecs inline well
4. **Pooling**: Sessions and buffers pooled
5. **Variable-Length Encoding**: Compact wire format

## Configuration

```csharp
services.AddSerializer(builder =>
{
    // Register custom codecs
    builder.AddCodec<MyCustomCodec>();

    // Configure serializer
    builder.Configure(options =>
    {
        options.WellKnownTypeAliases["mytype"] = typeof(MyType);
    });
});
```

## Summary

Orleans.Serialization provides:

1. **High-performance** binary serialization
2. **Version tolerance** via field IDs
3. **Code generation** for zero overhead
4. **Deep copying** for immutability
5. **Extensibility** via custom codecs

---

**Next**: [Additional Systems](10-additional-systems.md)
