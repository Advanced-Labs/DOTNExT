// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS.Storage
{
    /// <summary>
    /// FieldAccess driver that tracks dirty state for persistence.
    ///
    /// This driver intercepts field write operations to mark objects as dirty,
    /// enabling automatic persistence of modified objects on flush.
    ///
    /// Phase 2: Provides Flush and FlushAll operations for dirty objects.
    /// </summary>
    internal static class PersistentFieldAccessOps
    {
        /// <summary>
        /// Called after a field write operation.
        /// Marks the object as dirty for later persistence.
        /// </summary>
        public static void OnAfterWrite(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            // Only mark dirty if object is routed (virtual)
            if (!TypeDriverHelper.IsNonDefaultRouted(obj))
                return;

            TypeDriverHelper.MarkDirty(obj);
        }

        /// <summary>
        /// Flush a single object to storage.
        /// </summary>
        /// <param name="obj">Object to flush.</param>
        /// <returns>True if flushed or already clean, false on error.</returns>
        [RequiresUnreferencedCode("Persistence uses reflection for serialization")]
        public static bool Flush(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            if (!TypeDriverHelper.IsDirty(obj))
                return true;  // Nothing to do

            // Persist to storage
            if (PersistObject(obj))
            {
                // Clear dirty flag on success
                TypeDriverHelper.ClearDirty(obj);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Flush all dirty objects to storage using a single transaction.
        /// </summary>
        /// <returns>Number of objects flushed.</returns>
        [RequiresUnreferencedCode("Persistence uses reflection for serialization")]
        public static int FlushAll()
        {
            // Get dirty object count first
            int dirtyCount = TypeDriverHelper.GetDirtyCount();
            if (dirtyCount == 0)
                return 0;

            int flushedCount = 0;

            // Use a single write transaction for efficiency
            VoronStorageOps.WithWriteTransaction((tx, tree) =>
            {
                // Enumerate and flush each dirty object
                // Note: Full enumeration requires native support.
                // For Phase 2, we use a simplified approach:
                // The caller is expected to track objects and call Flush individually.
                // FlushAll here persists what we can access through the dirty set.

                var dirtyObjects = EnumerateDirtyObjects();
                foreach (var obj in dirtyObjects)
                {
                    if (FlushInTransaction(obj, tree))
                    {
                        flushedCount++;
                    }
                }
            });

            return flushedCount;
        }

        /// <summary>
        /// Persist an object to storage (single object, own transaction).
        /// </summary>
        [RequiresUnreferencedCode("Serialization uses reflection to access object fields")]
        private static bool PersistObject(object obj)
        {
            // Ensure VUID is assigned
            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            return VoronStorageOps.WithWriteTransaction((tx, tree) =>
            {
                // Build key
                var metaKey = VoronStorageOps.BuildMetadataKey(vuid);

                // Serialize body
                var bodyBytes = BodyEncoder.Serialize(obj);

                // Store in tree
                VoronStorageOps.Put(tree, metaKey, bodyBytes);

                return true;
            });
        }

        /// <summary>
        /// Flush an object within an existing transaction.
        /// </summary>
        [RequiresUnreferencedCode("Serialization uses reflection to access object fields")]
        private static bool FlushInTransaction(object obj, object tree)
        {
            if (!TypeDriverHelper.IsDirty(obj))
                return false;

            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            // Build key and serialize
            var metaKey = VoronStorageOps.BuildMetadataKey(vuid);
            var bodyBytes = BodyEncoder.Serialize(obj);

            // Store
            VoronStorageOps.Put(tree, metaKey, bodyBytes);

            // Clear dirty
            TypeDriverHelper.ClearDirty(obj);

            return true;
        }

        /// <summary>
        /// Enumerate all dirty objects.
        /// </summary>
        /// <remarks>
        /// Phase 2 limitation: Full dirty enumeration requires native QCall support
        /// to iterate the native dirty set and resolve SyncBlock indexes to objects.
        /// For now, we return an empty list - callers should track objects explicitly.
        /// </remarks>
        private static IEnumerable<object> EnumerateDirtyObjects()
        {
            // TODO: Implement via QCall TDSNative_EnumerateDirtyObjects
            // This would iterate the native DirtySet and resolve each entry to an object.
            // For Phase 2, dirty enumeration is deferred - VKernel tracks objects explicitly.
            return Array.Empty<object>();
        }
    }
}
