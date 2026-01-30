// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace System.OS.Storage
{
    /// <summary>
    /// High-level storage operations for virtual objects.
    /// Provides key-value operations backed by Voron via VoronStorage.
    ///
    /// Key format for hybrid storage model:
    /// - {VUID}/meta           - Object metadata
    /// - {VUID}/f/{FieldToken} - Primitive/string field (searchable)
    /// - {VUID}/r/{FieldToken} - VUID reference to [Memorize] child
    /// - {VUID}/e/{FieldToken} - Embedded blob for non-virtual child
    /// </summary>
    [RequiresUnreferencedCode("Uses VoronStorage which loads Voron dynamically via reflection")]
    internal sealed class VoronStorageOps
    {
        private static VoronStorageOps? s_instance;
        private static readonly object s_lock = new();

        /// <summary>
        /// Get the singleton instance.
        /// </summary>
        public static VoronStorageOps Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lock)
                    {
                        s_instance ??= new VoronStorageOps();
                    }
                }
                return s_instance;
            }
        }

        private VoronStorageOps() { }

        #region Key Building

        /// <summary>
        /// Build storage key for metadata: {VUID}/meta
        /// </summary>
        public static byte[] BuildMetadataKey(VUID vuid)
        {
            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);

            // Key format: 16 bytes VUID + "/meta"
            var suffix = "/meta"u8;
            var key = new byte[16 + suffix.Length];
            vuidBytes.CopyTo(key, 0);
            suffix.CopyTo(key.AsSpan(16));
            return key;
        }

        /// <summary>
        /// Build storage key for field: {VUID}/f/{FieldToken}
        /// </summary>
        public static byte[] BuildFieldKey(VUID vuid, int fieldToken)
        {
            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);

            // Key format: 16 bytes VUID + "/f/" + 4 bytes token
            var key = new byte[16 + 3 + 4];
            vuidBytes.CopyTo(key, 0);
            key[16] = (byte)'/';
            key[17] = (byte)'f';
            key[18] = (byte)'/';
            BitConverter.TryWriteBytes(key.AsSpan(19), fieldToken);
            return key;
        }

        /// <summary>
        /// Build storage key for reference: {VUID}/r/{FieldToken}
        /// </summary>
        public static byte[] BuildReferenceKey(VUID vuid, int fieldToken)
        {
            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);

            // Key format: 16 bytes VUID + "/r/" + 4 bytes token
            var key = new byte[16 + 3 + 4];
            vuidBytes.CopyTo(key, 0);
            key[16] = (byte)'/';
            key[17] = (byte)'r';
            key[18] = (byte)'/';
            BitConverter.TryWriteBytes(key.AsSpan(19), fieldToken);
            return key;
        }

        /// <summary>
        /// Build storage key for embedded blob: {VUID}/e/{FieldToken}
        /// </summary>
        public static byte[] BuildEmbeddedKey(VUID vuid, int fieldToken)
        {
            var vuidBytes = new byte[16];
            vuid.WriteBytes(vuidBytes);

            // Key format: 16 bytes VUID + "/e/" + 4 bytes token
            var key = new byte[16 + 3 + 4];
            vuidBytes.CopyTo(key, 0);
            key[16] = (byte)'/';
            key[17] = (byte)'e';
            key[18] = (byte)'/';
            BitConverter.TryWriteBytes(key.AsSpan(19), fieldToken);
            return key;
        }

        #endregion

        #region Low-Level Operations

        /// <summary>
        /// Store bytes at a key within a transaction.
        /// </summary>
        public static void Put(object tree, byte[] key, byte[] value)
        {
            VoronStorage.TreeAdd(tree, key, value);
        }

        /// <summary>
        /// Read bytes from a key within a transaction.
        /// </summary>
        public static byte[]? Get(object tree, byte[] key)
        {
            return VoronStorage.TreeRead(tree, key);
        }

        /// <summary>
        /// Delete a key within a transaction.
        /// </summary>
        public static bool Delete(object tree, byte[] key)
        {
            return VoronStorage.TreeDelete(tree, key);
        }

        #endregion

        #region Object Operations

        /// <summary>
        /// Check if an object exists in storage.
        /// </summary>
        public static bool Exists(VUID vuid)
        {
            if (vuid.IsEmpty) return false;

            object? tx = null;
            try
            {
                tx = VoronStorage.Instance.ReadTransaction();
                var tree = VoronStorage.ReadTree(tx, "vobjects");
                if (tree == null) return false;

                var metaKey = BuildMetadataKey(vuid);
                return Get(tree, metaKey) != null;
            }
            finally
            {
                VoronStorage.DisposeTransaction(tx);
            }
        }

        /// <summary>
        /// Delete all keys for an object.
        /// </summary>
        public static bool DeleteObject(VUID vuid)
        {
            if (vuid.IsEmpty) return false;

            object? tx = null;
            try
            {
                tx = VoronStorage.Instance.WriteTransaction();
                var tree = VoronStorage.CreateTree(tx, "vobjects");

                // Delete metadata key (presence indicates object exists)
                var metaKey = BuildMetadataKey(vuid);
                var existed = Delete(tree, metaKey);

                // Note: In a full implementation, we would iterate and delete
                // all keys with this VUID prefix. For now, we just delete metadata.
                // T06 (Body Encoder) will handle field-level keys.

                VoronStorage.Commit(tx);
                return existed;
            }
            catch
            {
                VoronStorage.DisposeTransaction(tx);
                throw;
            }
            finally
            {
                VoronStorage.DisposeTransaction(tx);
            }
        }

        #endregion

        #region Transaction Helpers

        /// <summary>
        /// Execute an action within a read transaction.
        /// </summary>
        public static T WithReadTransaction<T>(Func<object, object, T> action)
        {
            object? tx = null;
            try
            {
                tx = VoronStorage.Instance.ReadTransaction();
                var tree = VoronStorage.ReadTree(tx, "vobjects");
                if (tree == null)
                    throw new InvalidOperationException("vobjects tree not found");
                return action(tx, tree);
            }
            finally
            {
                VoronStorage.DisposeTransaction(tx);
            }
        }

        /// <summary>
        /// Execute an action within a write transaction.
        /// </summary>
        public static T WithWriteTransaction<T>(Func<object, object, T> action)
        {
            object? tx = null;
            try
            {
                tx = VoronStorage.Instance.WriteTransaction();
                var tree = VoronStorage.CreateTree(tx, "vobjects");
                var result = action(tx, tree);
                VoronStorage.Commit(tx);
                return result;
            }
            catch
            {
                VoronStorage.DisposeTransaction(tx);
                throw;
            }
            finally
            {
                VoronStorage.DisposeTransaction(tx);
            }
        }

        /// <summary>
        /// Execute an action within a write transaction (no return value).
        /// </summary>
        public static void WithWriteTransaction(Action<object, object> action)
        {
            object? tx = null;
            try
            {
                tx = VoronStorage.Instance.WriteTransaction();
                var tree = VoronStorage.CreateTree(tx, "vobjects");
                action(tx, tree);
                VoronStorage.Commit(tx);
            }
            catch
            {
                VoronStorage.DisposeTransaction(tx);
                throw;
            }
            finally
            {
                VoronStorage.DisposeTransaction(tx);
            }
        }

        #endregion

        #region Primitive Serialization Helpers

        /// <summary>
        /// Serialize a primitive value to bytes.
        /// </summary>
        public static byte[] SerializePrimitive(object value)
        {
            return value switch
            {
                bool b => BitConverter.GetBytes(b),
                byte by => new[] { by },
                sbyte sb => new[] { (byte)sb },
                char c => BitConverter.GetBytes(c),
                short s => BitConverter.GetBytes(s),
                ushort us => BitConverter.GetBytes(us),
                int i => BitConverter.GetBytes(i),
                uint ui => BitConverter.GetBytes(ui),
                long l => BitConverter.GetBytes(l),
                ulong ul => BitConverter.GetBytes(ul),
                float f => BitConverter.GetBytes(f),
                double d => BitConverter.GetBytes(d),
                decimal dec => DecimalToBytes(dec),
                string str => Encoding.UTF8.GetBytes(str),
                DateTime dt => BitConverter.GetBytes(dt.Ticks),
                DateTimeOffset dto => DateTimeOffsetToBytes(dto),
                TimeSpan ts => BitConverter.GetBytes(ts.Ticks),
                Guid g => g.ToByteArray(),
                VUID v => VuidToBytes(v),
                _ => throw new NotSupportedException($"Cannot serialize primitive of type {value.GetType()}")
            };
        }

        /// <summary>
        /// Deserialize a primitive value from bytes.
        /// </summary>
        public static object DeserializePrimitive(byte[] bytes, Type type)
        {
            if (type == typeof(bool)) return BitConverter.ToBoolean(bytes, 0);
            if (type == typeof(byte)) return bytes[0];
            if (type == typeof(sbyte)) return (sbyte)bytes[0];
            if (type == typeof(char)) return BitConverter.ToChar(bytes, 0);
            if (type == typeof(short)) return BitConverter.ToInt16(bytes, 0);
            if (type == typeof(ushort)) return BitConverter.ToUInt16(bytes, 0);
            if (type == typeof(int)) return BitConverter.ToInt32(bytes, 0);
            if (type == typeof(uint)) return BitConverter.ToUInt32(bytes, 0);
            if (type == typeof(long)) return BitConverter.ToInt64(bytes, 0);
            if (type == typeof(ulong)) return BitConverter.ToUInt64(bytes, 0);
            if (type == typeof(float)) return BitConverter.ToSingle(bytes, 0);
            if (type == typeof(double)) return BitConverter.ToDouble(bytes, 0);
            if (type == typeof(decimal)) return BytesToDecimal(bytes);
            if (type == typeof(string)) return Encoding.UTF8.GetString(bytes);
            if (type == typeof(DateTime)) return new DateTime(BitConverter.ToInt64(bytes, 0));
            if (type == typeof(DateTimeOffset)) return BytesToDateTimeOffset(bytes);
            if (type == typeof(TimeSpan)) return new TimeSpan(BitConverter.ToInt64(bytes, 0));
            if (type == typeof(Guid)) return new Guid(bytes);
            if (type == typeof(VUID)) return VUID.FromBytes(bytes);

            throw new NotSupportedException($"Cannot deserialize primitive of type {type}");
        }

        /// <summary>
        /// Check if a type is a primitive or string (searchable).
        /// </summary>
        public static bool IsPrimitiveOrString(Type type)
        {
            return type.IsPrimitive
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid)
                || type == typeof(VUID);
        }

        private static byte[] DecimalToBytes(decimal d)
        {
            var bits = decimal.GetBits(d);
            var bytes = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), bits[i]);
            }
            return bytes;
        }

        private static decimal BytesToDecimal(byte[] bytes)
        {
            var bits = new int[4];
            for (int i = 0; i < 4; i++)
            {
                bits[i] = BitConverter.ToInt32(bytes, i * 4);
            }
            return new decimal(bits);
        }

        private static byte[] DateTimeOffsetToBytes(DateTimeOffset dto)
        {
            var bytes = new byte[16];
            BitConverter.TryWriteBytes(bytes.AsSpan(0), dto.Ticks);
            BitConverter.TryWriteBytes(bytes.AsSpan(8), dto.Offset.Ticks);
            return bytes;
        }

        private static DateTimeOffset BytesToDateTimeOffset(byte[] bytes)
        {
            var ticks = BitConverter.ToInt64(bytes, 0);
            var offsetTicks = BitConverter.ToInt64(bytes, 8);
            return new DateTimeOffset(ticks, new TimeSpan(offsetTicks));
        }

        private static byte[] VuidToBytes(VUID v)
        {
            var bytes = new byte[16];
            v.WriteBytes(bytes);
            return bytes;
        }

        #endregion
    }
}
