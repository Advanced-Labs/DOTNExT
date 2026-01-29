// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS
{
    /// <summary>
    /// Runtime services for TypeDriver System (TDS).
    /// Phase 1: Testing and diagnostics only.
    /// </summary>
    public static partial class TypeDriverHelper
    {
        /// <summary>
        /// Check if object is using non-default TypeDriver routing.
        /// </summary>
        public static bool IsNonDefaultRouted(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return IsNonDefaultRoutedInternal(ObjectHandleOnStack.Create(ref obj));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_IsNonDefaultRouted")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsNonDefaultRoutedInternal(ObjectHandleOnStack obj);

        /// <summary>
        /// Enable non-default routing for an object.
        /// Creates default OpsRoot (all default drivers).
        /// </summary>
        public static void EnableNonDefaultRouting(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnableNonDefaultRoutingInternal(ObjectHandleOnStack.Create(ref obj));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_EnableNonDefaultRouting")]
        private static partial void EnableNonDefaultRoutingInternal(ObjectHandleOnStack obj);

        /// <summary>
        /// Disable non-default routing for an object.
        /// Returns object to standard CLR behavior.
        /// </summary>
        public static void DisableNonDefaultRouting(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            DisableNonDefaultRoutingInternal(ObjectHandleOnStack.Create(ref obj));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_DisableNonDefaultRouting")]
        private static partial void DisableNonDefaultRoutingInternal(ObjectHandleOnStack obj);

        /// <summary>
        /// Get driver flags for an object.
        /// Returns 0 for default objects.
        /// </summary>
        [CLSCompliant(false)]
        public static uint GetDriverFlags(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetDriverFlagsInternal(ObjectHandleOnStack.Create(ref obj));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetDriverFlags")]
        private static partial uint GetDriverFlagsInternal(ObjectHandleOnStack obj);

        /// <summary>
        /// Get count of routed objects (diagnostics).
        /// </summary>
        public static int GetRoutedObjectCount()
        {
            return GetRoutedObjectCountInternal();
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetRoutedObjectCount")]
        private static partial int GetRoutedObjectCountInternal();

        //=====================================================================
        // Phase 2: VUID Operations
        //=====================================================================

        /// <summary>
        /// Get the VUID for an object (empty if not assigned).
        /// </summary>
        public static VUID GetVUID(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            GetObjectVUIDInternal(ObjectHandleOnStack.Create(ref obj), out ulong hi, out ulong lo);
            if (hi == 0 && lo == 0)
                return VUID.Empty;

            // Reconstruct VUID from parts
            Span<byte> bytes = stackalloc byte[16];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes, hi);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(8), lo);
            return VUID.FromBytes(bytes);
        }

        /// <summary>
        /// Set the VUID for an object (must be TDS-routed).
        /// </summary>
        public static void SetVUID(object obj, VUID vuid)
        {
            ArgumentNullException.ThrowIfNull(obj);
            Span<byte> bytes = stackalloc byte[16];
            vuid.WriteBytes(bytes);
            ulong hi = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes);
            ulong lo = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
            SetObjectVUIDInternal(ObjectHandleOnStack.Create(ref obj), hi, lo);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetObjectVUID")]
        private static partial void GetObjectVUIDInternal(ObjectHandleOnStack obj, out ulong hi, out ulong lo);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_SetObjectVUID")]
        private static partial void SetObjectVUIDInternal(ObjectHandleOnStack obj, ulong hi, ulong lo);
    }
}
