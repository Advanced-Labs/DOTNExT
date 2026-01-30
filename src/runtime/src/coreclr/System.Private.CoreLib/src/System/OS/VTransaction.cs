// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.OS.Storage;
using System.Threading;

namespace System.OS
{
    /// <summary>
    /// Transaction scope for batched virtual object operations.
    ///
    /// VTransaction provides ACID semantics for virtual object operations:
    /// - All changes are batched until Commit()
    /// - Rollback() discards all changes
    /// - Auto-rollback on Dispose() if not committed
    ///
    /// Usage:
    /// <code>
    /// using var tx = VKernel.BeginTransaction();
    /// var obj1 = VKernel.New&lt;MyObject&gt;();
    /// var obj2 = VKernel.New&lt;MyObject&gt;();
    /// obj1.Value = 1;
    /// obj2.Value = 2;
    /// tx.Commit(); // Both objects persisted atomically
    /// </code>
    /// </summary>
    [CLSCompliant(false)]
    public sealed class VTransaction : IDisposable
    {
        // Ambient transaction tracking - allows nested operations to use the existing transaction
        private static readonly AsyncLocal<VTransaction?> s_ambient = new();

        /// <summary>
        /// Get the current ambient transaction, or null if none is active.
        /// </summary>
        public static VTransaction? Current => s_ambient.Value;

        private object? _voronTransaction;
        private object? _tree;
        private bool _committed;
        private bool _rolledBack;
        private bool _disposed;

        /// <summary>
        /// Create a new transaction. Use VKernel.BeginTransaction() instead.
        /// </summary>
        [RequiresUnreferencedCode("Uses VoronStorage which loads Voron dynamically")]
        internal VTransaction()
        {
            _voronTransaction = VoronStorage.Instance.WriteTransaction();
            _tree = VoronStorage.CreateTree(_voronTransaction, "vobjects");

            // Set as ambient transaction for nested operations
            s_ambient.Value = this;
        }

        /// <summary>
        /// Check if transaction is still active (not committed or rolled back).
        /// </summary>
        public bool IsActive => !_committed && !_rolledBack && !_disposed;

        /// <summary>
        /// Commit all changes in this transaction.
        /// </summary>
        [RequiresUnreferencedCode("Uses VoronStorage which loads Voron dynamically")]
        public void Commit()
        {
            ThrowIfDisposed();
            ThrowIfCompleted();

            // Commit the Voron transaction
            VoronStorage.Commit(_voronTransaction!);
            _committed = true;
        }

        /// <summary>
        /// Rollback all changes in this transaction.
        /// </summary>
        [RequiresUnreferencedCode("Uses VoronStorage which loads Voron dynamically")]
        public void Rollback()
        {
            ThrowIfDisposed();
            ThrowIfCompleted();

            // Dispose without commit = rollback
            VoronStorage.DisposeTransaction(_voronTransaction);
            _voronTransaction = null;
            _tree = null;
            _rolledBack = true;
        }

        /// <summary>
        /// Get the Voron tree for operations within this transaction.
        /// </summary>
        internal object? Tree => _tree;

        /// <summary>
        /// Get the underlying Voron transaction.
        /// </summary>
        internal object? VoronTransaction => _voronTransaction;

        /// <summary>
        /// Persist an object within this transaction.
        /// </summary>
        [RequiresUnreferencedCode("Uses BodyEncoder for serialization")]
        internal void Persist(object obj)
        {
            ThrowIfDisposed();
            ThrowIfCompleted();

            var vuid = TypeDriverHelper.GetVUID(obj);
            if (vuid.IsEmpty)
            {
                vuid = VUID.New();
                TypeDriverHelper.SetVUID(obj, vuid);
            }

            var metaKey = VoronStorageOps.BuildMetadataKey(vuid);
            var bodyBytes = BodyEncoder.Serialize(obj);
            VoronStorageOps.Put(_tree!, metaKey, bodyBytes);

            TypeDriverHelper.ClearDirty(obj);
        }

        /// <summary>
        /// Dispose the transaction. Auto-rollback if not committed.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "VoronStorage uses reflection intentionally to load Voron dynamically")]
        public void Dispose()
        {
            if (_disposed) return;

            // Clear ambient transaction
            if (s_ambient.Value == this)
                s_ambient.Value = null;

            if (!_committed && !_rolledBack && _voronTransaction != null)
            {
                // Auto-rollback on dispose without commit
                VoronStorage.DisposeTransaction(_voronTransaction);
            }

            _voronTransaction = null;
            _tree = null;
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private void ThrowIfCompleted()
        {
            if (_committed)
                throw new InvalidOperationException("Transaction already committed");
            if (_rolledBack)
                throw new InvalidOperationException("Transaction already rolled back");
        }
    }
}
