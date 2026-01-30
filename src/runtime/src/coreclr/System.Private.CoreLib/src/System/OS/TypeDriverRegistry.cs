// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.OS
{
    /// <summary>
    /// Flags for driver registration.
    /// Controls virtual object behavior for registered types.
    /// </summary>
    [Flags]
    public enum DriverFlags
    {
        /// <summary>No special behavior.</summary>
        None = 0,

        /// <summary>Enable persistence for this type.</summary>
        Persist = 0x01,

        /// <summary>Enable dirty tracking for field writes.</summary>
        DirtyTrack = 0x02,

        /// <summary>Auto-flush on transaction commit.</summary>
        AutoFlush = 0x04,

        /// <summary>Objects are immutable (no dirty tracking needed).</summary>
        Immutable = 0x08,
    }

    /// <summary>
    /// Registry for mapping types to virtual object drivers.
    ///
    /// Phase 2: Provides managed API for type registration.
    /// Native driver selection will use this registry when creating/loading objects.
    /// </summary>
    public static class TypeDriverRegistry
    {
        // Managed-side registry for Phase 2 (native registry integration in future)
        private static readonly Dictionary<RuntimeTypeHandle, DriverFlags> s_registrations = new();
        private static readonly object s_lock = new();

        /// <summary>
        /// Register a type for virtual object behavior.
        /// </summary>
        /// <typeparam name="T">Type to register.</typeparam>
        /// <param name="flags">Driver behavior flags.</param>
        public static void Register<T>(DriverFlags flags = DriverFlags.Persist | DriverFlags.DirtyTrack)
        {
            Register(typeof(T), flags);
        }

        /// <summary>
        /// Register a type for virtual object behavior.
        /// </summary>
        /// <param name="type">Type to register.</param>
        /// <param name="flags">Driver behavior flags.</param>
        public static void Register(Type type, DriverFlags flags = DriverFlags.Persist | DriverFlags.DirtyTrack)
        {
            ArgumentNullException.ThrowIfNull(type);

            lock (s_lock)
            {
                s_registrations[type.TypeHandle] = flags;
            }

            // TODO: Native registration via QCall when available
            // RegisterInternal(type.TypeHandle.Value, (uint)flags);
        }

        /// <summary>
        /// Unregister a type.
        /// </summary>
        /// <typeparam name="T">Type to unregister.</typeparam>
        public static void Unregister<T>()
        {
            Unregister(typeof(T));
        }

        /// <summary>
        /// Unregister a type.
        /// </summary>
        /// <param name="type">Type to unregister.</param>
        public static void Unregister(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            lock (s_lock)
            {
                s_registrations.Remove(type.TypeHandle);
            }

            // TODO: Native unregistration via QCall when available
            // UnregisterInternal(type.TypeHandle.Value);
        }

        /// <summary>
        /// Check if a type is registered.
        /// </summary>
        /// <typeparam name="T">Type to check.</typeparam>
        /// <returns>True if type is registered.</returns>
        public static bool IsRegistered<T>()
        {
            return IsRegistered(typeof(T));
        }

        /// <summary>
        /// Check if a type is registered.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if type is registered.</returns>
        public static bool IsRegistered(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            lock (s_lock)
            {
                return s_registrations.ContainsKey(type.TypeHandle);
            }
        }

        /// <summary>
        /// Check if a type is registered for persistence.
        /// </summary>
        /// <typeparam name="T">Type to check.</typeparam>
        /// <returns>True if type is registered with Persist flag.</returns>
        public static bool IsRegisteredForPersist<T>()
        {
            return IsRegisteredForPersist(typeof(T));
        }

        /// <summary>
        /// Check if a type is registered for persistence.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if type is registered with Persist flag.</returns>
        public static bool IsRegisteredForPersist(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            var flags = GetFlags(type);
            return (flags & DriverFlags.Persist) != 0;
        }

        /// <summary>
        /// Get registration flags for a type.
        /// </summary>
        /// <typeparam name="T">Type to query.</typeparam>
        /// <returns>Driver flags, or None if not registered.</returns>
        public static DriverFlags GetFlags<T>()
        {
            return GetFlags(typeof(T));
        }

        /// <summary>
        /// Get registration flags for a type.
        /// </summary>
        /// <param name="type">Type to query.</param>
        /// <returns>Driver flags, or None if not registered.</returns>
        public static DriverFlags GetFlags(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            lock (s_lock)
            {
                if (s_registrations.TryGetValue(type.TypeHandle, out var flags))
                {
                    return flags;
                }
            }

            return DriverFlags.None;
        }

        /// <summary>
        /// Get the number of registered types.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (s_lock)
                {
                    return s_registrations.Count;
                }
            }
        }

        /// <summary>
        /// Clear all registrations.
        /// </summary>
        internal static void Clear()
        {
            lock (s_lock)
            {
                s_registrations.Clear();
            }
        }

        // Native QCall stubs for future implementation
        // [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_RegisterType")]
        // private static partial void RegisterInternal(IntPtr typeHandle, uint flags);

        // [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_UnregisterType")]
        // private static partial void UnregisterInternal(IntPtr typeHandle);

        // [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ShouldPersist")]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // private static partial bool ShouldPersistInternal(IntPtr typeHandle);

        // [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetTypeFlags")]
        // private static partial uint GetFlagsInternal(IntPtr typeHandle);
    }
}
