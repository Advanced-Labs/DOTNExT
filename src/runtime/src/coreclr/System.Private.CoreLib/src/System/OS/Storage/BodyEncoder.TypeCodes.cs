// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.OS.Storage
{
    /// <summary>
    /// Type codes for body encoding.
    /// Used in field directory to indicate value types.
    /// </summary>
    internal static partial class BodyEncoder
    {
        internal enum FieldTypeCode : byte
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
            DateTimeOffset = 25,

            // Reference types
            VObjectRef = 30,      // Reference to another VObject (by VUID)
            NullRef = 31,         // Null reference

            // Collections (Phase 2+)
            ByteArray = 40,
            // Array = 41,
            // List = 42,

            // Complex
            Nested = 100,         // Nested inline object (embedded blob)
        }
    }
}
