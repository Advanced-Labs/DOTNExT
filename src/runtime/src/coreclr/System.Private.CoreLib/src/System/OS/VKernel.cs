// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// VKernel intentionally calls VoronStorage which uses reflection to load Voron dynamically.
// Suppress IL2026 to prevent the attribute from propagating to all VKernel callers.
#pragma warning disable IL2026

using System.OS.Storage;

namespace System.OS
{
    /// <summary>
    /// VAYRON Kernel - main entry point for virtual object operations.
    ///
    /// VKernel provides the primary API for working with virtual objects:
    /// - Initialize/Shutdown lifecycle management
    /// - Object creation (New) and loading (Get)
    /// - Persistence operations (Flush)
    ///
    /// Phase 2: Basic persistence via Voron storage.
    /// </summary>
    [CLSCompliant(false)]
    public static class VKernel
    {
        private static bool s_initialized;
        private static readonly object s_lock = new();

        /// <summary>
        /// Check if VKernel has been initialized.
        /// </summary>
        public static bool IsInitialized => s_initialized;

        /// <summary>
        /// Initialize VAYRON subsystem.
        /// Called automatically on first use, or explicitly for control.
        /// </summary>
        public static void Initialize()
        {
            if (s_initialized) return;

            lock (s_lock)
            {
                if (s_initialized) return;

                // Initialize Voron storage
                VoronStorage.Initialize();

                // Register shutdown hook
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                s_initialized = true;
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            Shutdown();
        }

        /// <summary>
        /// Shutdown VAYRON subsystem.
        /// Flushes pending changes and releases resources.
        /// </summary>
        public static void Shutdown()
        {
            if (!s_initialized) return;

            lock (s_lock)
            {
                if (!s_initialized) return;

                // Flush pending changes
                FlushAll();

                // Shutdown storage
                VoronStorage.Shutdown();

                s_initialized = false;
            }
        }

        /// <summary>
        /// Ensure VKernel is initialized before operations.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!s_initialized)
                Initialize();
        }

        #region Object Creation

        /// <summary>
        /// Create a new virtual object with auto-generated VUID.
        /// </summary>
        /// <typeparam name="T">Type of object to create (must have parameterless constructor).</typeparam>
        /// <returns>New virtual object instance.</returns>
        public static T New<T>() where T : class, new()
        {
            EnsureInitialized();

            // Create the managed object
            var obj = new T();

            // Generate VUID
            var vuid = VUID.New();

            // Set up for virtual operation
            TypeDriverHelper.SetVUID(obj, vuid);
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            return obj;
        }

        /// <summary>
        /// Create a new virtual object with specific VUID.
        /// </summary>
        /// <typeparam name="T">Type of object to create.</typeparam>
        /// <param name="vuid">VUID to assign to the object.</param>
        /// <returns>New virtual object instance.</returns>
        [CLSCompliant(false)]
        public static T New<T>(VUID vuid) where T : class, new()
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                throw new ArgumentException("VUID cannot be empty", nameof(vuid));

            // Create the managed object
            var obj = new T();

            // Set VUID
            TypeDriverHelper.SetVUID(obj, vuid);
            TypeDriverHelper.EnableNonDefaultRouting(obj);

            return obj;
        }

        #endregion

        #region Object Loading

        /// <summary>
        /// Get an existing virtual object by VUID.
        /// </summary>
        /// <typeparam name="T">Expected type of the object.</typeparam>
        /// <param name="vuid">VUID of the object to load.</param>
        /// <returns>Loaded object, or null if not found.</returns>
        [CLSCompliant(false)]
        public static T? Get<T>(VUID vuid) where T : class
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return null;

            // TODO: Implement in T05 (Storage_Voron Driver)
            // This will materialize the object from Voron storage
            throw new NotImplementedException("Get<T> will be implemented in T05: Storage_Voron Driver");
        }

        /// <summary>
        /// Get an existing virtual object by VUID, or create new if not found.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="vuid">VUID to look up or assign.</param>
        /// <returns>Existing or new object.</returns>
        [CLSCompliant(false)]
        public static T GetOrNew<T>(VUID vuid) where T : class, new()
        {
            var existing = Get<T>(vuid);
            if (existing != null)
                return existing;

            return New<T>(vuid);
        }

        /// <summary>
        /// Check if an object exists in storage.
        /// </summary>
        /// <param name="vuid">VUID to check.</param>
        /// <returns>True if object exists in storage.</returns>
        [CLSCompliant(false)]
        public static bool Exists(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            // TODO: Implement in T05 (Storage_Voron Driver)
            throw new NotImplementedException("Exists will be implemented in T05: Storage_Voron Driver");
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Persist a single object to storage.
        /// </summary>
        /// <param name="obj">Object to persist.</param>
        public static void Persist(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            if (!TypeDriverHelper.IsNonDefaultRouted(obj))
            {
                throw new InvalidOperationException(
                    "Object is not a virtual object. Use VKernel.New<T>() or " +
                    "TypeDriverHelper.EnableNonDefaultRouting() first.");
            }

            // TODO: Implement in T05 (Storage_Voron Driver)
            // This will serialize and store the object in Voron
            throw new NotImplementedException("Persist will be implemented in T05: Storage_Voron Driver");
        }

        /// <summary>
        /// Flush a single dirty object to storage.
        /// </summary>
        /// <param name="obj">Object to flush.</param>
        /// <returns>True if object was flushed, false if not dirty.</returns>
        public static bool Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            if (!TypeDriverHelper.IsDirty(obj))
                return true;  // Nothing to do

            Persist(obj);
            TypeDriverHelper.ClearDirty(obj);
            return true;
        }

        /// <summary>
        /// Flush all dirty objects to storage.
        /// </summary>
        /// <returns>Number of objects flushed.</returns>
        public static int FlushAll()
        {
            if (!s_initialized)
                return 0;

            // TODO: Implement in T07 (FieldAccess_Persist Driver)
            // This will iterate the dirty set and persist each object
            // For now, return 0 as placeholder
            return 0;
        }

        /// <summary>
        /// Get count of objects pending flush.
        /// </summary>
        public static int GetPendingFlushCount()
        {
            return TypeDriverHelper.GetDirtyCount();
        }

        #endregion

        #region Deletion

        /// <summary>
        /// Delete a virtual object from storage.
        /// </summary>
        /// <param name="obj">Object to delete.</param>
        /// <returns>True if deleted, false if not found.</returns>
        public static bool Delete(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
                return false;

            return Delete(vuid);
        }

        /// <summary>
        /// Delete a virtual object by VUID.
        /// </summary>
        /// <param name="vuid">VUID of object to delete.</param>
        /// <returns>True if deleted, false if not found.</returns>
        [CLSCompliant(false)]
        public static bool Delete(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            // TODO: Implement in T05 (Storage_Voron Driver)
            throw new NotImplementedException("Delete will be implemented in T05: Storage_Voron Driver");
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Get the Voron storage data path.
        /// </summary>
        public static string? DataPath
        {
            get
            {
                if (!VoronStorage.IsInitialized)
                    return null;
                return VoronStorage.Instance.DataPath;
            }
        }

        #endregion
    }
}
