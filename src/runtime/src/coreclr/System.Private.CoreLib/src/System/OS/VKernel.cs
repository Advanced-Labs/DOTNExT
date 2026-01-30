// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorage uses reflection intentionally to load Voron dynamically")]
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
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorage uses reflection intentionally to load Voron dynamically")]
        public static void Shutdown()
        {
            if (!s_initialized) return;

            lock (s_lock)
            {
                if (!s_initialized) return;

                // Check for active ambient transaction - can't safely FlushAll if one exists
                // This can happen if a test failed mid-transaction
                var ambientTx = VTransaction.Current;
                if (ambientTx == null || !ambientTx.IsActive)
                {
                    // Safe to flush - no active transaction
                    try
                    {
                        FlushAll();
                    }
                    catch
                    {
                        // Ignore flush errors during shutdown
                    }
                }
                // If there's an active transaction, skip flush to avoid nested tx error
                // The transaction will be cleaned up when VoronStorage shuts down

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
            // NOTE: EnableNonDefaultRouting MUST come first - it creates the OpsRootTable entry
            // SetVUID only updates an existing entry, it doesn't create one
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            TypeDriverHelper.SetVUID(obj, vuid);

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

            // Set up for virtual operation
            // NOTE: EnableNonDefaultRouting MUST come first - it creates the OpsRootTable entry
            // SetVUID only updates an existing entry, it doesn't create one
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            TypeDriverHelper.SetVUID(obj, vuid);

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
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "BodyEncoder uses reflection intentionally for deserialization")]
        public static T? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                                          DynamicallyAccessedMemberTypes.NonPublicFields |
                                                          DynamicallyAccessedMemberTypes.PublicConstructors |
                                                          DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(VUID vuid) where T : class
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return null;

            // Read from storage
            var bodyBytes = VoronStorageOps.WithReadTransaction((tx, tree) =>
            {
                var metaKey = VoronStorageOps.BuildMetadataKey(vuid);
                return VoronStorageOps.Get(tree, metaKey);
            });

            if (bodyBytes == null)
                return null;

            // Deserialize
            var obj = BodyEncoder.Deserialize<T>(bodyBytes);

            // Set up virtual object state
            // NOTE: EnableNonDefaultRouting MUST come first - it creates the OpsRootTable entry
            TypeDriverHelper.EnableNonDefaultRouting(obj);
            TypeDriverHelper.SetVUID(obj, vuid);

            return obj;
        }

        /// <summary>
        /// Get an existing virtual object by VUID, or create new if not found.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="vuid">VUID to look up or assign.</param>
        /// <returns>Existing or new object.</returns>
        [CLSCompliant(false)]
        public static T GetOrNew<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                                              DynamicallyAccessedMemberTypes.NonPublicFields |
                                                              DynamicallyAccessedMemberTypes.PublicConstructors |
                                                              DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(VUID vuid) where T : class, new()
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
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorageOps uses reflection intentionally")]
        public static bool Exists(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            return VoronStorageOps.Exists(vuid);
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Persist a single object to storage.
        /// </summary>
        /// <param name="obj">Object to persist.</param>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "BodyEncoder uses reflection intentionally for serialization")]
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

            // Check for ambient transaction - use it to avoid nested write transaction
            var ambientTx = VTransaction.Current;
            if (ambientTx != null && ambientTx.IsActive)
            {
                ambientTx.Persist(obj);
                return;
            }

            // Ensure VUID is assigned
            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            // Serialize and store in new transaction
            VoronStorageOps.WithWriteTransaction((tx, tree) =>
            {
                var metaKey = VoronStorageOps.BuildMetadataKey(vuid);
                var bodyBytes = BodyEncoder.Serialize(obj);
                VoronStorageOps.Put(tree, metaKey, bodyBytes);
            });
        }

        /// <summary>
        /// Flush a single dirty object to storage.
        /// </summary>
        /// <param name="obj">Object to flush.</param>
        /// <returns>True if object was flushed, false if not dirty.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "PersistentFieldAccessOps uses reflection intentionally")]
        public static bool Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            EnsureInitialized();

            return PersistentFieldAccessOps.Flush(obj);
        }

        /// <summary>
        /// Flush all dirty objects to storage.
        /// </summary>
        /// <returns>Number of objects flushed.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "PersistentFieldAccessOps uses reflection intentionally")]
        public static int FlushAll()
        {
            if (!s_initialized)
                return 0;

            return PersistentFieldAccessOps.FlushAll();
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
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorageOps uses reflection intentionally")]
        public static bool Delete(VUID vuid)
        {
            EnsureInitialized();

            if (vuid.IsEmpty)
                return false;

            return VoronStorageOps.DeleteObject(vuid);
        }

        #endregion

        #region Transactions

        /// <summary>
        /// Begin a new transaction for batched operations.
        /// </summary>
        /// <returns>A new VTransaction that must be committed or disposed.</returns>
        [CLSCompliant(false)]
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VTransaction uses VoronStorage which loads Voron dynamically")]
        public static VTransaction BeginTransaction()
        {
            EnsureInitialized();
            return new VTransaction();
        }

        /// <summary>
        /// Execute an action within a transaction.
        /// The transaction is committed on success, rolled back on exception.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VTransaction uses VoronStorage which loads Voron dynamically")]
        public static void WithTransaction(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            EnsureInitialized();

            using var tx = new VTransaction();
            try
            {
                action();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Execute a function within a transaction.
        /// The transaction is committed on success, rolled back on exception.
        /// </summary>
        /// <typeparam name="TResult">Return type.</typeparam>
        /// <param name="func">Function to execute.</param>
        /// <returns>Result from the function.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VTransaction uses VoronStorage which loads Voron dynamically")]
        public static TResult WithTransaction<TResult>(Func<TResult> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            EnsureInitialized();

            using var tx = new VTransaction();
            try
            {
                var result = func();
                tx.Commit();
                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Get the Voron storage data path.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorage uses reflection intentionally to load Voron dynamically")]
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
