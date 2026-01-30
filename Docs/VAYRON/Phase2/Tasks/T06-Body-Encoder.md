# T06: Body Encoder

> **Work Package:** WP2.3
> **Dependencies:** None (parallel track)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement hybrid field-level storage for virtual object persistence, enabling:
- **Searchability**: Primitives and strings stored individually for Corax indexing
- **Object Graph**: `[Memorize]` references stored as VUIDs
- **Embedded Children**: Non-virtual references serialized as blobs

---

## Background

### Original Approach (Pure Blob)
The original spec suggested a single blob per object. This has drawbacks:
- Not searchable by Corax
- No semantic search capability
- No field-level indexing

### Revised Approach: Hybrid Storage (Decided 2026-01-30)

**Key insight**: VAYRON's Memory System needs to be searchable and indexable. Corax (RavenDB's indexing engine) requires field-level access to create indexes.

**Decision**: Use hybrid field-level storage:

| Field Type | Storage Key | Searchable | Notes |
|------------|-------------|------------|-------|
| Primitives (int, bool, etc.) | `{VUID}/f/{FieldToken}` | ✅ Yes | Direct value, Corax indexable |
| String | `{VUID}/f/{FieldToken}` | ✅ Yes | UTF-8, text + semantic search |
| `[Virtual, Memorize]` ref | `{VUID}/r/{FieldToken}` | ✅ Traversable | VUID only, child is independent |
| Non-virtual class ref | `{VUID}/e/{FieldToken}` | ❌ No | Embedded blob, owned by parent |
| Collections | Mixed | Depends | Elements follow same rules |
| Metadata | `{VUID}/meta` | ✅ Yes | TypeToken, Version, Flags |

---

## Voron Storage Layout

```
Voron Tree: "vobjects"
│
├── {VUID-1}/
│   ├── meta                    → TypeToken, Version, Flags
│   ├── f/0x04000001           → int value (searchable)
│   ├── f/0x04000002           → string value (searchable)
│   ├── r/0x04000003           → VUID reference to [Memorize] child
│   └── e/0x04000004           → embedded blob (non-virtual child)
│
├── {VUID-2}/
│   ├── meta                    → ...
│   └── f/...                   → ...
```

---

## Decision: How Child Objects Are Stored

```
Is the child object's type marked [Virtual, Memorize]?
├── YES → Store as VUID reference only
│         - Child is persisted as its own independent entity
│         - Child has its own lifecycle
│         - Loaded lazily when accessed
│
└── NO  → Serialize inline as embedded blob
          - Child is "owned" by parent
          - No independent identity (no VUID)
          - Loaded when parent is loaded
          - "Ephemeral" = no independent lifecycle
```

---

## Load/Save Order

### On Save (obj.Save())

1. Write metadata: `{VUID}/meta` → TypeToken, Version, Flags
2. For each field:
   - Primitive/String → Write to `{VUID}/f/{token}`
   - `[Memorize]` ref → Write VUID to `{VUID}/r/{token}`, ensure child is also saved
   - Non-virtual ref → Serialize blob to `{VUID}/e/{token}`

### On Load (VKernel.Get<T>(vuid))

1. Create empty object instance
2. Read metadata from `{VUID}/meta`
3. For each stored field:
   - `f/` entries → Deserialize and set field value
   - `r/` entries → Store VUID reference (lazy load actual object when accessed)
   - `e/` entries → Deserialize blob and set field value

---

## Body Layout Format (for embedded blobs only)

Embedded non-virtual children use this blob format:

```
┌──────────────────────────────────────────────────────────────┐
│ Header (4 bytes)                                              │
│ ├─ Version: uint8 (= 1 for Phase 2)                          │
│ ├─ FieldCount: uint16                                        │
│ └─ Flags: uint8                                              │
├──────────────────────────────────────────────────────────────┤
│ Field Directory (variable, 9 bytes per field)                 │
│ For each field:                                              │
│ ├─ FieldToken: uint32 (metadata token)                       │
│ ├─ TypeCode: uint8 (primitive type or complex marker)        │
│ └─ DataOffset: uint32 (offset into data section)             │
├──────────────────────────────────────────────────────────────┤
│ Data Section (variable)                                       │
│ └─ Field values in serialized form                           │
└──────────────────────────────────────────────────────────────┘
```

---

## Implementation

### 1. Type Codes

**File:** `System.Private.CoreLib/src/System/OS/Storage/BodyEncoder.TypeCodes.cs` (new)

```csharp
namespace System.OS.Storage
{
    internal static partial class BodyEncoder
    {
        // Type codes for body encoding
        internal enum TypeCode : byte
        {
            Null = 0,

            // Primitives
            Boolean = 1,
            Byte = 2,
            SByte = 3,
            Int16 = 4,
            UInt16 = 5,
            Int32 = 6,
            UInt32 = 7,
            Int64 = 8,
            UInt64 = 9,
            Single = 10,
            Double = 11,
            Char = 12,
            Decimal = 13,

            // Special types
            String = 20,
            DateTime = 21,
            TimeSpan = 22,
            Guid = 23,
            VUID = 24,

            // Reference types
            VObjectRef = 30,      // Reference to another VObject (by VUID)
            NullRef = 31,         // Null reference

            // Collections (Phase 2+)
            ByteArray = 40,
            // Array = 41,
            // List = 42,

            // Complex
            Nested = 100,         // Nested inline object
        }
    }
}
```

### 2. BodyEncoder Class

**File:** `System.Private.CoreLib/src/System/OS/Storage/BodyEncoder.cs` (new)

```csharp
namespace System.OS.Storage
{
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Serializes/deserializes virtual object bodies using Tagged Field Map format.
    /// </summary>
    internal static partial class BodyEncoder
    {
        private const byte VERSION = 1;

        /// <summary>
        /// Serialize an object's fields to a byte array.
        /// </summary>
        public static byte[] Serialize(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            var type = obj.GetType();
            var fields = GetSerializableFields(type);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Header
            writer.Write(VERSION);
            writer.Write((ushort)fields.Length);
            writer.Write((byte)0);  // Flags

            // Build field directory and data
            var dataStream = new MemoryStream();
            using var dataWriter = new BinaryWriter(dataStream);

            foreach (var field in fields)
            {
                // Field directory entry
                writer.Write(field.MetadataToken);
                writer.Write((byte)GetTypeCode(field.FieldType));
                writer.Write((uint)dataStream.Position);

                // Field data
                WriteFieldValue(dataWriter, obj, field);
            }

            // Append data section
            writer.Write(dataStream.ToArray());

            return stream.ToArray();
        }

        /// <summary>
        /// Deserialize a byte array into an object's fields.
        /// </summary>
        public static object Deserialize(byte[] body, Type targetType)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(targetType);

            if (body.Length < 4)
                throw new ArgumentException("Invalid body format");

            using var stream = new MemoryStream(body);
            using var reader = new BinaryReader(stream);

            // Header
            byte version = reader.ReadByte();
            if (version != VERSION)
                throw new NotSupportedException($"Body version {version} not supported");

            ushort fieldCount = reader.ReadUInt16();
            byte flags = reader.ReadByte();

            // Create instance
            var obj = RuntimeHelpers.GetUninitializedObject(targetType);

            // Read field directory
            var directory = new (int token, TypeCode type, uint offset)[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                directory[i] = (
                    reader.ReadInt32(),
                    (TypeCode)reader.ReadByte(),
                    reader.ReadUInt32()
                );
            }

            // Data section starts here
            long dataStart = stream.Position;

            // Read each field
            foreach (var (token, typeCode, offset) in directory)
            {
                var field = FindFieldByToken(targetType, token);
                if (field == null) continue;  // Field removed from type

                stream.Position = dataStart + offset;
                var value = ReadFieldValue(reader, typeCode, field.FieldType);
                field.SetValue(obj, value);
            }

            return obj;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
                .Where(f => !f.IsNotSerialized && !f.IsInitOnly)
                .OrderBy(f => f.MetadataToken)
                .ToArray();
        }

        private static FieldInfo? FindFieldByToken(Type type, int token)
        {
            return type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
                .FirstOrDefault(f => f.MetadataToken == token);
        }

        private static TypeCode GetTypeCode(Type type)
        {
            if (type == typeof(bool)) return TypeCode.Boolean;
            if (type == typeof(byte)) return TypeCode.Byte;
            if (type == typeof(sbyte)) return TypeCode.SByte;
            if (type == typeof(short)) return TypeCode.Int16;
            if (type == typeof(ushort)) return TypeCode.UInt16;
            if (type == typeof(int)) return TypeCode.Int32;
            if (type == typeof(uint)) return TypeCode.UInt32;
            if (type == typeof(long)) return TypeCode.Int64;
            if (type == typeof(ulong)) return TypeCode.UInt64;
            if (type == typeof(float)) return TypeCode.Single;
            if (type == typeof(double)) return TypeCode.Double;
            if (type == typeof(char)) return TypeCode.Char;
            if (type == typeof(decimal)) return TypeCode.Decimal;
            if (type == typeof(string)) return TypeCode.String;
            if (type == typeof(DateTime)) return TypeCode.DateTime;
            if (type == typeof(TimeSpan)) return TypeCode.TimeSpan;
            if (type == typeof(Guid)) return TypeCode.Guid;
            if (type == typeof(VUID)) return TypeCode.VUID;
            if (type == typeof(byte[])) return TypeCode.ByteArray;

            // Reference types that are VObjects
            if (type.IsClass && !type.IsSealed)
                return TypeCode.VObjectRef;

            return TypeCode.Nested;
        }

        private static void WriteFieldValue(BinaryWriter writer, object obj, FieldInfo field)
        {
            var value = field.GetValue(obj);

            if (value == null)
            {
                writer.Write((byte)TypeCode.Null);
                return;
            }

            var type = field.FieldType;

            if (type == typeof(bool)) { writer.Write((bool)value); return; }
            if (type == typeof(byte)) { writer.Write((byte)value); return; }
            if (type == typeof(sbyte)) { writer.Write((sbyte)value); return; }
            if (type == typeof(short)) { writer.Write((short)value); return; }
            if (type == typeof(ushort)) { writer.Write((ushort)value); return; }
            if (type == typeof(int)) { writer.Write((int)value); return; }
            if (type == typeof(uint)) { writer.Write((uint)value); return; }
            if (type == typeof(long)) { writer.Write((long)value); return; }
            if (type == typeof(ulong)) { writer.Write((ulong)value); return; }
            if (type == typeof(float)) { writer.Write((float)value); return; }
            if (type == typeof(double)) { writer.Write((double)value); return; }
            if (type == typeof(char)) { writer.Write((char)value); return; }
            if (type == typeof(decimal)) { writer.Write((decimal)value); return; }

            if (type == typeof(string))
            {
                var str = (string)value;
                writer.Write(str.Length);
                writer.Write(Encoding.UTF8.GetBytes(str));
                return;
            }

            if (type == typeof(DateTime))
            {
                writer.Write(((DateTime)value).Ticks);
                return;
            }

            if (type == typeof(TimeSpan))
            {
                writer.Write(((TimeSpan)value).Ticks);
                return;
            }

            if (type == typeof(Guid))
            {
                writer.Write(((Guid)value).ToByteArray());
                return;
            }

            if (type == typeof(VUID))
            {
                var vuid = (VUID)value;
                Span<byte> buffer = stackalloc byte[16];
                vuid.WriteBytes(buffer);
                writer.Write(buffer);
                return;
            }

            if (type == typeof(byte[]))
            {
                var arr = (byte[])value;
                writer.Write(arr.Length);
                writer.Write(arr);
                return;
            }

            // VObject reference - store VUID
            if (type.IsClass)
            {
                var vuid = TypeDriverHelper.GetVUID(value);
                if (!vuid.IsEmpty)
                {
                    writer.Write((byte)TypeCode.VObjectRef);
                    Span<byte> buffer = stackalloc byte[16];
                    vuid.WriteBytes(buffer);
                    writer.Write(buffer);
                }
                else
                {
                    writer.Write((byte)TypeCode.NullRef);
                }
                return;
            }

            throw new NotSupportedException($"Field type {type} not supported");
        }

        private static object? ReadFieldValue(BinaryReader reader, TypeCode typeCode, Type fieldType)
        {
            switch (typeCode)
            {
                case TypeCode.Null:
                case TypeCode.NullRef:
                    return null;

                case TypeCode.Boolean: return reader.ReadBoolean();
                case TypeCode.Byte: return reader.ReadByte();
                case TypeCode.SByte: return reader.ReadSByte();
                case TypeCode.Int16: return reader.ReadInt16();
                case TypeCode.UInt16: return reader.ReadUInt16();
                case TypeCode.Int32: return reader.ReadInt32();
                case TypeCode.UInt32: return reader.ReadUInt32();
                case TypeCode.Int64: return reader.ReadInt64();
                case TypeCode.UInt64: return reader.ReadUInt64();
                case TypeCode.Single: return reader.ReadSingle();
                case TypeCode.Double: return reader.ReadDouble();
                case TypeCode.Char: return reader.ReadChar();
                case TypeCode.Decimal: return reader.ReadDecimal();

                case TypeCode.String:
                    int len = reader.ReadInt32();
                    var bytes = reader.ReadBytes(len);
                    return Encoding.UTF8.GetString(bytes);

                case TypeCode.DateTime:
                    return new DateTime(reader.ReadInt64());

                case TypeCode.TimeSpan:
                    return new TimeSpan(reader.ReadInt64());

                case TypeCode.Guid:
                    return new Guid(reader.ReadBytes(16));

                case TypeCode.VUID:
                    return VUID.FromBytes(reader.ReadBytes(16));

                case TypeCode.ByteArray:
                    int arrLen = reader.ReadInt32();
                    return reader.ReadBytes(arrLen);

                case TypeCode.VObjectRef:
                    var refVuid = VUID.FromBytes(reader.ReadBytes(16));
                    // Lazy load - will be resolved later
                    // For now, return placeholder or null
                    return null;  // TODO: Implement lazy loading

                default:
                    throw new NotSupportedException($"TypeCode {typeCode} not supported");
            }
        }
    }
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `System/OS/Storage/BodyEncoder.cs` | Create | Main encoder class |
| `System/OS/Storage/BodyEncoder.TypeCodes.cs` | Create | Type code definitions |

---

## Acceptance Criteria

- [ ] Header written correctly (version, field count, flags)
- [ ] Field directory correctly maps tokens to offsets
- [ ] All primitive types serialize/deserialize correctly
- [ ] String fields handle UTF-8 encoding
- [ ] DateTime/TimeSpan use ticks representation
- [ ] VUID fields round-trip correctly
- [ ] VObject references stored as VUID
- [ ] Unknown fields (removed from type) are skipped gracefully

---

## Testing

```csharp
[Fact]
public void BodyEncoder_PrimitiveTypes_RoundTrip()
{
    var original = new TestAllTypes
    {
        BoolField = true,
        ByteField = 42,
        IntField = -12345,
        LongField = long.MaxValue,
        FloatField = 3.14f,
        DoubleField = 2.718281828,
        DecimalField = 123.456m,
        CharField = 'X',
        StringField = "Hello, World!",
        DateTimeField = DateTime.UtcNow,
        GuidField = Guid.NewGuid()
    };

    var bytes = BodyEncoder.Serialize(original);
    var restored = (TestAllTypes)BodyEncoder.Deserialize(bytes, typeof(TestAllTypes));

    Assert.Equal(original.BoolField, restored.BoolField);
    Assert.Equal(original.IntField, restored.IntField);
    Assert.Equal(original.StringField, restored.StringField);
    // ... etc
}

[Fact]
public void BodyEncoder_NullFields_Handled()
{
    var original = new TestObject { StringField = null };

    var bytes = BodyEncoder.Serialize(original);
    var restored = (TestObject)BodyEncoder.Deserialize(bytes, typeof(TestObject));

    Assert.Null(restored.StringField);
}

[Fact]
public void BodyEncoder_TypeEvolution_NewFieldsIgnored()
{
    // Serialize with current type
    var original = new TestObject { IntField = 42 };
    var bytes = BodyEncoder.Serialize(original);

    // Deserialize would work even if new fields added to type
    // (new fields get default values)
}
```

---

## References

- Phase 2 Main Doc: Section 8 (Body Layer Encoding)
- Voron-Integration-Guide.md: Section 5 (Body Serialization Patterns)
