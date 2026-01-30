// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace System.OS.Storage
{
    /// <summary>
    /// Serializes/deserializes virtual object bodies using Tagged Field Map format.
    ///
    /// This encoder is used for embedded blob serialization of non-virtual children.
    /// For searchable field-level storage (primitives, strings), use VoronStorageOps directly.
    ///
    /// Body Format:
    /// - Header: Version (1 byte), FieldCount (2 bytes), Flags (1 byte)
    /// - Field Directory: FieldToken (4 bytes), TypeCode (1 byte), DataOffset (4 bytes) per field
    /// - Data Section: Serialized field values
    /// </summary>
    internal static partial class BodyEncoder
    {
        private const byte VERSION = 1;

        #region Serialize

        /// <summary>
        /// Serialize an object's fields to a byte array.
        /// </summary>
        public static byte[] Serialize(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            var type = obj.GetType();
            var fields = GetSerializableFields(type);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            writer.Write(VERSION);
            writer.Write((ushort)fields.Length);
            writer.Write((byte)0);  // Flags (reserved)

            // Build field directory and data separately
            using var dataStream = new MemoryStream();
            using var dataWriter = new BinaryWriter(dataStream, Encoding.UTF8, leaveOpen: true);

            foreach (var field in fields)
            {
                // Field directory entry
                writer.Write(field.MetadataToken);
                writer.Write((byte)GetTypeCode(field.FieldType));
                writer.Write((uint)dataStream.Position);

                // Field data
                WriteFieldValue(dataWriter, obj, field);
            }

            dataWriter.Flush();

            // Append data section
            writer.Write(dataStream.ToArray());

            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>
        /// Serialize an object's fields to an existing stream.
        /// </summary>
        public static void SerializeTo(object obj, Stream output)
        {
            var bytes = Serialize(obj);
            output.Write(bytes, 0, bytes.Length);
        }

        #endregion

        #region Deserialize

        /// <summary>
        /// Deserialize a byte array into an object of the specified type.
        /// </summary>
        public static object Deserialize(
            byte[] body,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                        DynamicallyAccessedMemberTypes.NonPublicFields |
                                        DynamicallyAccessedMemberTypes.PublicConstructors |
                                        DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type targetType)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(targetType);

            if (body.Length < 4)
                throw new ArgumentException("Invalid body format: too short", nameof(body));

            using var stream = new MemoryStream(body);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            byte version = reader.ReadByte();
            if (version != VERSION)
                throw new NotSupportedException($"Body version {version} not supported, expected {VERSION}");

            ushort fieldCount = reader.ReadUInt16();
            byte flags = reader.ReadByte();

            // Create instance without running constructor
            var obj = RuntimeHelpers.GetUninitializedObject(targetType);

            // Read field directory
            var directory = new (int token, FieldTypeCode typeCode, uint offset)[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                directory[i] = (
                    reader.ReadInt32(),
                    (FieldTypeCode)reader.ReadByte(),
                    reader.ReadUInt32()
                );
            }

            // Data section starts here
            long dataStart = stream.Position;

            // Read each field
            foreach (var (token, typeCode, offset) in directory)
            {
                var field = FindFieldByToken(targetType, token);
                if (field == null)
                    continue;  // Field removed from type - schema evolution

                stream.Position = dataStart + offset;
                var value = ReadFieldValue(reader, typeCode, field.FieldType);
                field.SetValue(obj, value);
            }

            return obj;
        }

        /// <summary>
        /// Deserialize a byte array into an object of type T.
        /// </summary>
        public static T Deserialize<T>(byte[] body) where T : class
        {
            return (T)Deserialize(body, typeof(T));
        }

        #endregion

        #region Field Discovery

        private static FieldInfo[] GetSerializableFields(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                        DynamicallyAccessedMemberTypes.NonPublicFields)] Type type)
        {
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            // Filter and order by metadata token for consistent ordering
            var result = new System.Collections.Generic.List<FieldInfo>();
            foreach (var field in fields)
            {
                // Skip non-serialized and readonly fields
                if (field.GetCustomAttribute<NonSerializedAttribute>() != null || field.IsInitOnly)
                    continue;

                // Skip compiler-generated backing fields for properties (optional)
                // if (field.Name.StartsWith("<") && field.Name.EndsWith(">k__BackingField"))
                //     continue;

                result.Add(field);
            }

            // Sort by metadata token for deterministic ordering
            result.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return result.ToArray();
        }

        private static FieldInfo? FindFieldByToken(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                        DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
            int token)
        {
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                if (field.MetadataToken == token)
                    return field;
            }
            return null;
        }

        #endregion

        #region Type Code Mapping

        private static FieldTypeCode GetTypeCode(Type type)
        {
            if (type == typeof(bool)) return FieldTypeCode.Boolean;
            if (type == typeof(byte)) return FieldTypeCode.Byte;
            if (type == typeof(sbyte)) return FieldTypeCode.SByte;
            if (type == typeof(short)) return FieldTypeCode.Int16;
            if (type == typeof(ushort)) return FieldTypeCode.UInt16;
            if (type == typeof(int)) return FieldTypeCode.Int32;
            if (type == typeof(uint)) return FieldTypeCode.UInt32;
            if (type == typeof(long)) return FieldTypeCode.Int64;
            if (type == typeof(ulong)) return FieldTypeCode.UInt64;
            if (type == typeof(float)) return FieldTypeCode.Single;
            if (type == typeof(double)) return FieldTypeCode.Double;
            if (type == typeof(char)) return FieldTypeCode.Char;
            if (type == typeof(decimal)) return FieldTypeCode.Decimal;
            if (type == typeof(string)) return FieldTypeCode.String;
            if (type == typeof(DateTime)) return FieldTypeCode.DateTime;
            if (type == typeof(DateTimeOffset)) return FieldTypeCode.DateTimeOffset;
            if (type == typeof(TimeSpan)) return FieldTypeCode.TimeSpan;
            if (type == typeof(Guid)) return FieldTypeCode.Guid;
            if (type == typeof(VUID)) return FieldTypeCode.VUID;
            if (type == typeof(byte[])) return FieldTypeCode.ByteArray;

            // Reference types - check if it's a VObject type
            if (type.IsClass && !type.IsSealed)
            {
                // Could be a VObject reference
                return FieldTypeCode.VObjectRef;
            }

            // Fallback: nested/embedded object
            return FieldTypeCode.Nested;
        }

        #endregion

        #region Write Field Values

        private static void WriteFieldValue(BinaryWriter writer, object obj, FieldInfo field)
        {
            var value = field.GetValue(obj);

            if (value == null)
            {
                writer.Write((byte)FieldTypeCode.Null);
                return;
            }

            var type = field.FieldType;

            // Primitives
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

            // String
            if (type == typeof(string))
            {
                var str = (string)value;
                var bytes = Encoding.UTF8.GetBytes(str);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return;
            }

            // DateTime/TimeSpan
            if (type == typeof(DateTime))
            {
                writer.Write(((DateTime)value).Ticks);
                return;
            }

            if (type == typeof(DateTimeOffset))
            {
                var dto = (DateTimeOffset)value;
                writer.Write(dto.Ticks);
                writer.Write(dto.Offset.Ticks);
                return;
            }

            if (type == typeof(TimeSpan))
            {
                writer.Write(((TimeSpan)value).Ticks);
                return;
            }

            // Guid
            if (type == typeof(Guid))
            {
                writer.Write(((Guid)value).ToByteArray());
                return;
            }

            // VUID
            if (type == typeof(VUID))
            {
                var vuid = (VUID)value;
                var buffer = new byte[16];
                vuid.WriteBytes(buffer);
                writer.Write(buffer);
                return;
            }

            // Byte array
            if (type == typeof(byte[]))
            {
                var arr = (byte[])value;
                writer.Write(arr.Length);
                writer.Write(arr);
                return;
            }

            // VObject reference - store VUID only
            if (type.IsClass)
            {
                var vuid = TypeDriverHelper.GetVUID(value);
                if (!vuid.IsEmpty)
                {
                    writer.Write((byte)FieldTypeCode.VObjectRef);
                    var buffer = new byte[16];
                    vuid.WriteBytes(buffer);
                    writer.Write(buffer);
                }
                else
                {
                    // Object doesn't have a VUID yet - treat as null ref
                    writer.Write((byte)FieldTypeCode.NullRef);
                }
                return;
            }

            throw new NotSupportedException($"Field type {type} not supported for serialization");
        }

        #endregion

        #region Read Field Values

        private static object? ReadFieldValue(BinaryReader reader, FieldTypeCode typeCode, Type _fieldType)
        {
            switch (typeCode)
            {
                case FieldTypeCode.Null:
                case FieldTypeCode.NullRef:
                    return null;

                case FieldTypeCode.Boolean:
                    return reader.ReadBoolean();

                case FieldTypeCode.Byte:
                    return reader.ReadByte();

                case FieldTypeCode.SByte:
                    return reader.ReadSByte();

                case FieldTypeCode.Int16:
                    return reader.ReadInt16();

                case FieldTypeCode.UInt16:
                    return reader.ReadUInt16();

                case FieldTypeCode.Int32:
                    return reader.ReadInt32();

                case FieldTypeCode.UInt32:
                    return reader.ReadUInt32();

                case FieldTypeCode.Int64:
                    return reader.ReadInt64();

                case FieldTypeCode.UInt64:
                    return reader.ReadUInt64();

                case FieldTypeCode.Single:
                    return reader.ReadSingle();

                case FieldTypeCode.Double:
                    return reader.ReadDouble();

                case FieldTypeCode.Char:
                    return reader.ReadChar();

                case FieldTypeCode.Decimal:
                    return reader.ReadDecimal();

                case FieldTypeCode.String:
                    int strLen = reader.ReadInt32();
                    var strBytes = reader.ReadBytes(strLen);
                    return Encoding.UTF8.GetString(strBytes);

                case FieldTypeCode.DateTime:
                    return new DateTime(reader.ReadInt64());

                case FieldTypeCode.DateTimeOffset:
                    var ticks = reader.ReadInt64();
                    var offsetTicks = reader.ReadInt64();
                    return new DateTimeOffset(ticks, new TimeSpan(offsetTicks));

                case FieldTypeCode.TimeSpan:
                    return new TimeSpan(reader.ReadInt64());

                case FieldTypeCode.Guid:
                    return new Guid(reader.ReadBytes(16));

                case FieldTypeCode.VUID:
                    return VUID.FromBytes(reader.ReadBytes(16));

                case FieldTypeCode.ByteArray:
                    int arrLen = reader.ReadInt32();
                    return reader.ReadBytes(arrLen);

                case FieldTypeCode.VObjectRef:
                    // Read the VUID reference (discarding for now)
                    _ = reader.ReadBytes(16);
                    // For now, return null - lazy loading will be implemented in VKernel.Get<T>
                    // The caller should use VKernel.Get<T>(vuid) to load the actual object
                    return null;  // TODO: Implement lazy proxy loading

                case FieldTypeCode.Nested:
                    // Nested objects would need recursive deserialization
                    // For Phase 2, we skip this - non-virtual children are handled differently
                    return null;

                default:
                    throw new NotSupportedException($"FieldTypeCode {typeCode} not supported for deserialization");
            }
        }

        #endregion
    }
}
