// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS
{
    /// <summary>
    /// Low-level field access through TypeDriver routing.
    /// Phase 1: For testing driver dispatch.
    /// Note: Generic versions deferred to Phase 2.
    /// </summary>
    internal static partial class VIntrinsics
    {
        /// <summary>
        /// Read a 32-bit integer field through TypeDriver routing.
        /// </summary>
        public static int ReadInt32Field(object obj, int fieldOffset)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return ReadInt32FieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ReadInt32Field")]
        private static partial int ReadInt32FieldInternal(ObjectHandleOnStack obj, int fieldOffset);

        /// <summary>
        /// Write a 32-bit integer field through TypeDriver routing.
        /// </summary>
        public static void WriteInt32Field(object obj, int fieldOffset, int value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            WriteInt32FieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset, value);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_WriteInt32Field")]
        private static partial void WriteInt32FieldInternal(ObjectHandleOnStack obj, int fieldOffset, int value);

        /// <summary>
        /// Read a 64-bit integer field through TypeDriver routing.
        /// </summary>
        public static long ReadInt64Field(object obj, int fieldOffset)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return ReadInt64FieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ReadInt64Field")]
        private static partial long ReadInt64FieldInternal(ObjectHandleOnStack obj, int fieldOffset);

        /// <summary>
        /// Write a 64-bit integer field through TypeDriver routing.
        /// </summary>
        public static void WriteInt64Field(object obj, int fieldOffset, long value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            WriteInt64FieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset, value);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_WriteInt64Field")]
        private static partial void WriteInt64FieldInternal(ObjectHandleOnStack obj, int fieldOffset, long value);

        /// <summary>
        /// Read a reference field through TypeDriver routing.
        /// </summary>
        public static object? ReadRefField(object obj, int fieldOffset)
        {
            ArgumentNullException.ThrowIfNull(obj);
            object? result = null;
            ReadRefFieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset, ObjectHandleOnStack.Create(ref result));
            return result;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ReadRefField")]
        private static partial void ReadRefFieldInternal(ObjectHandleOnStack obj, int fieldOffset, ObjectHandleOnStack result);

        /// <summary>
        /// Write a reference field through TypeDriver routing (with barrier).
        /// </summary>
        public static void WriteRefField(object obj, int fieldOffset, object? value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            WriteRefFieldInternal(ObjectHandleOnStack.Create(ref obj), fieldOffset, ObjectHandleOnStack.Create(ref value));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_WriteRefField")]
        private static partial void WriteRefFieldInternal(ObjectHandleOnStack obj, int fieldOffset, ObjectHandleOnStack value);
    }
}
