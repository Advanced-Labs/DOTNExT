// VAYRON - Runtime-Integrated Persistent Storage
// Handle with lazy materialization
//
// Phase 3: Enhanced with native pointer caching, state machine integration, and lifecycle management
// Phase 4: Enhanced with automatic transaction enrollment and operation recording

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Voron.Data.Containers;

namespace Vayron;

/// <summary>
/// A lightweight handle to a persistent object.
/// The handle contains identity (OID) and control; the body lives in Voron storage.
/// </summary>
/// <remarks>
/// <para><b>Design principles:</b></para>
/// <list type="bullet">
/// <item><description>Minimal memory footprint (only OID, epoch, cached pointer)</description></item>
/// <item><description>Lazy materialization (body loaded on first access)</description></item>
/// <item><description>Transaction-aware staleness detection</description></item>
/// <item><description>GC-managed lifecycle with finalizer for cleanup</description></item>
/// </list>
///
/// <para><b>Phase 2: Object Header Tagging</b></para>
/// <para>
/// VAYRON handles are marked with bit 31 (BIT_SBLK_IS_VAYRON_HANDLE) in the
/// object header. This enables fast O(1) classification of VAYRON handles
/// without managed code overhead. The bit is set during construction and
/// remains set for the handle's lifetime.
/// </para>
///
/// <para><b>Phase 3: Side Table Integration</b></para>
/// <para>
/// Metadata is stored in a side table (VayronMetaTable) using ConditionalWeakTable.
/// This enables:
/// <list type="bullet">
/// <item><description>Native pointer caching for hot paths</description></item>
/// <item><description>Formal state machine for materialization</description></item>
/// <item><description>Memory pressure-aware eviction</description></item>
/// <item><description>Background cleanup and lifecycle management</description></item>
/// </list>
/// </para>
///
/// <para><b>Phase 4: Transaction Integration</b></para>
/// <para>
/// Handles automatically participate in transactions:
/// <list type="bullet">
/// <item><description>Auto-enrollment as participants in active transactions</description></item>
/// <item><description>Operation recording for transaction tracking</description></item>
/// <item><description>Automatic staleness detection using transaction epochs</description></item>
/// <item><description>Invalidation on transaction rollback</description></item>
/// </list>
/// </para>
/// </remarks>
public class VayronHandle : IVayronHandle, IDisposable
{
    /// <summary>
    /// Body header stored at the beginning of each body in storage.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected internal struct BodyHeader
    {
        /// <summary>
        /// Type token for runtime type identification.
        /// </summary>
        public uint TypeToken;

        /// <summary>
        /// Schema version for migration support.
        /// </summary>
        public ushort SchemaVersion;

        /// <summary>
        /// Reserved flags for future use.
        /// </summary>
        public ushort Flags;

        public const int Size = 8;
    }

    private readonly VayronEnvironment _environment;
    private VayronOid _oid;
    private long _epoch;
    private byte[]? _cachedBody;
    private bool _isDirty;
    private bool _disposed;

    /// <summary>
    /// Gets the Object Identifier for this handle.
    /// </summary>
    public VayronOid Oid => _oid;

    /// <summary>
    /// Gets the environment this handle belongs to.
    /// </summary>
    protected VayronEnvironment Environment => _environment;

    /// <summary>
    /// Gets whether this handle has been modified.
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Gets whether this handle's body is currently materialized.
    /// </summary>
    public bool IsMaterialized => _cachedBody != null;

    // =====================================================================
    // Phase 2: Object Header Classification
    // =====================================================================

    /// <summary>
    /// Checks if any object is a VAYRON persistent handle using runtime header bit classification.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object has the VAYRON handle bit set in its object header.</returns>
    /// <remarks>
    /// This is an O(1) operation that tests bit 31 in the object header.
    /// It's faster than type checking and works for any object instance.
    ///
    /// <para><b>Performance:</b> ~1-5ns (single bit test instruction)</para>
    /// </remarks>
    public static bool IsVayronHandleInstance(object? obj)
    {
        return VayronRuntime.IsVayronHandle(obj);
    }

    /// <summary>
    /// Gets diagnostic header information for this handle.
    /// </summary>
    /// <returns>Header information including all bit flags.</returns>
    public VayronHeaderInfo GetHeaderInfo()
    {
        return VayronRuntime.GetHeaderInfo(this);
    }

    // =====================================================================
    // Phase 3: Side Table Access
    // =====================================================================

    /// <summary>
    /// Gets the metadata for this handle from the side table.
    /// </summary>
    /// <returns>The metadata, or null if not found.</returns>
    public VayronMeta? GetMetadata()
    {
        return VayronMetaTable.Get(this);
    }

    /// <summary>
    /// Gets the current materialization state.
    /// </summary>
    public MaterializationState MaterializationState
    {
        get
        {
            var meta = VayronMetaTable.Get(this);
            return meta?.State ?? MaterializationState.NotMaterialized;
        }
    }

    /// <summary>
    /// Gets comprehensive diagnostic information about this handle.
    /// </summary>
    public VayronHandleDiagInfo GetDiagnostics()
    {
        var meta = VayronMetaTable.Get(this);
        var headerInfo = GetHeaderInfo();

        return new VayronHandleDiagInfo
        {
            Oid = _oid,
            Epoch = _epoch,
            IsDirty = _isDirty,
            IsMaterialized = IsMaterialized,
            CachedBodySize = _cachedBody?.Length ?? 0,
            MaterializationState = meta?.State ?? MaterializationState.NotMaterialized,
            StorageLocation = meta?.StorageLocation ?? ContainerEntryId.Invalid,
            TypeToken = meta?.TypeToken ?? 0,
            SchemaVersion = meta?.SchemaVersion ?? 0,
            IsPinned = meta?.IsPinned ?? false,
            CachedBodyPtr = meta?.CachedBodyPtr ?? IntPtr.Zero,
            LastAccessTicks = meta?.LastAccessTicks ?? 0,
            AccessCount = meta?.AccessCount ?? 0,
            HeaderInfo = headerInfo,
        };
    }

    // =====================================================================
    // Constructors
    // =====================================================================

    /// <summary>
    /// Creates a new handle with a new OID (for creating new objects).
    /// </summary>
    protected VayronHandle(VayronEnvironment environment)
    {
        _environment = environment;
        _oid = environment.GenerateOid();
        _epoch = -1;
        _cachedBody = null;
        _isDirty = false;

        // Phase 2: Mark this object as a VAYRON handle in the object header
        VayronRuntime.MarkAsVayronHandle(this);

        // Phase 3: Initialize metadata in side table
        var meta = VayronMetaTable.GetOrCreate(this, _oid);
        meta.State = MaterializationState.NotMaterialized;
    }

    /// <summary>
    /// Creates a handle for an existing OID (for loading objects).
    /// </summary>
    protected VayronHandle(VayronEnvironment environment, VayronOid oid)
    {
        _environment = environment;
        _oid = oid;
        _epoch = -1;
        _cachedBody = null;
        _isDirty = false;

        // Phase 2: Mark this object as a VAYRON handle in the object header
        VayronRuntime.MarkAsVayronHandle(this);

        // Phase 3: Initialize metadata in side table
        var meta = VayronMetaTable.GetOrCreate(this, _oid);
        meta.State = MaterializationState.NotMaterialized;
    }

    // =====================================================================
    // Field Access
    // =====================================================================

    /// <summary>
    /// Gets a field value from the cached body.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T GetField<T>(int offset) where T : unmanaged
    {
        EnsureMaterialized();

        // Phase 3: Record access for LRU tracking
        VayronMetaTable.Get(this)?.RecordAccess();

        // Phase 4: Record read operation in transaction context
        RecordReadOperation();

        return MemoryMarshal.Read<T>(_cachedBody.AsSpan(BodyHeader.Size + offset));
    }

    /// <summary>
    /// Gets a field value using native pointer (Phase 3 hot path).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected unsafe T GetFieldFast<T>(int offset) where T : unmanaged
    {
        var meta = VayronMetaTable.Get(this);
        if (meta != null && meta.CachedBodyPtr != IntPtr.Zero)
        {
            meta.RecordAccess();
            return *(T*)((byte*)meta.CachedBodyPtr + BodyHeader.Size + offset);
        }

        // Fall back to managed path
        return GetField<T>(offset);
    }

    /// <summary>
    /// Sets a field value in the cached body.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void SetField<T>(int offset, T value) where T : unmanaged
    {
        EnsureMaterialized();
        MemoryMarshal.Write(_cachedBody.AsSpan(BodyHeader.Size + offset), in value);
        MarkDirty();

        // Phase 4: Record write operation in transaction context
        RecordWriteOperation();
    }

    /// <summary>
    /// Sets a field value using native pointer (Phase 3 hot path).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected unsafe void SetFieldFast<T>(int offset, T value) where T : unmanaged
    {
        var meta = VayronMetaTable.Get(this);
        if (meta != null && meta.CachedBodyPtr != IntPtr.Zero)
        {
            *(T*)((byte*)meta.CachedBodyPtr + BodyHeader.Size + offset) = value;
            MarkDirty();
            return;
        }

        // Fall back to managed path
        SetField(offset, value);
    }

    // =====================================================================
    // Phase 5: JIT-Optimized Field Access
    // =====================================================================

    /// <summary>
    /// Gets a field value using JIT-optimized path when available.
    /// </summary>
    /// <remarks>
    /// <para><b>Phase 5 Optimization:</b></para>
    /// When running on DOTNExT runtime with JIT helper interception enabled,
    /// this method can be transparently called by the JIT when accessing fields.
    /// The JIT helper checks the VAYRON header bit and dispatches to this method
    /// for materialization if needed.
    ///
    /// <para><b>Performance Characteristics:</b></para>
    /// <list type="bullet">
    /// <item><description>Fast path (cached + pinned): ~5ns - direct pointer access</description></item>
    /// <item><description>Warm path (cached, not pinned): ~15ns - managed array access</description></item>
    /// <item><description>Cold path (not cached): ~200-500ns - Voron read + cache</description></item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected unsafe T GetFieldJitOptimized<T>(int offset) where T : unmanaged
    {
        // Try JIT fast path first - check if we have a pinned body pointer
        var meta = VayronMetaTable.Get(this);
        if (meta != null)
        {
            // Fast path: pinned body with valid cached pointer
            if (meta.IsPinned && meta.CachedBodyPtr != IntPtr.Zero && !meta.IsStale(_epoch))
            {
                meta.RecordAccess();
                return *(T*)((byte*)meta.CachedBodyPtr + BodyHeader.Size + offset);
            }

            // Warm path: have cached managed body
            var managedBody = meta.GetManagedBody();
            if (managedBody != null && !meta.IsStale(_epoch))
            {
                meta.RecordAccess();
                RecordReadOperation();
                return MemoryMarshal.Read<T>(managedBody.AsSpan(BodyHeader.Size + offset));
            }
        }

        // Cold path: need to materialize
        return GetField<T>(offset);
    }

    /// <summary>
    /// Sets a field value using JIT-optimized path when available.
    /// </summary>
    /// <remarks>
    /// See <see cref="GetFieldJitOptimized{T}"/> for performance characteristics.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected unsafe void SetFieldJitOptimized<T>(int offset, T value) where T : unmanaged
    {
        // Try JIT fast path first - check if we have a pinned body pointer
        var meta = VayronMetaTable.Get(this);
        if (meta != null)
        {
            // Fast path: pinned body with valid cached pointer
            if (meta.IsPinned && meta.CachedBodyPtr != IntPtr.Zero && !meta.IsStale(_epoch))
            {
                *(T*)((byte*)meta.CachedBodyPtr + BodyHeader.Size + offset) = value;
                MarkDirty();
                RecordWriteOperation();

                // Notify JIT interop layer about the dirty state
                VayronJitInterop.MarkDirty(this);
                return;
            }
        }

        // Managed path
        SetField(offset, value);
    }

    /// <summary>
    /// Enables JIT-optimized access by pinning the body and notifying the JIT layer.
    /// </summary>
    /// <remarks>
    /// Call this before hot loops that repeatedly access fields on this handle.
    /// The body will remain pinned until <see cref="DisableJitOptimization"/> is called.
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// person.EnableJitOptimization();
    /// try
    /// {
    ///     for (int i = 0; i &lt; 1000000; i++)
    ///     {
    ///         sum += person.Age; // Fast path access
    ///     }
    /// }
    /// finally
    /// {
    ///     person.DisableJitOptimization();
    /// }
    /// </code>
    /// </remarks>
    public void EnableJitOptimization()
    {
        // Ensure materialized first
        EnsureMaterialized();

        // Pin the body
        Pin();

        // Notify JIT interop layer
        var meta = VayronMetaTable.Get(this);
        if (meta != null)
        {
            VayronJitInterop.UpdateCachedBodyInfo(this, meta.CachedBodyPtr, meta.CachedBodySize, _epoch);
        }
    }

    /// <summary>
    /// Disables JIT-optimized access and unpins the body.
    /// </summary>
    public void DisableJitOptimization()
    {
        Unpin();
    }

    /// <summary>
    /// Gets whether JIT optimization is currently enabled for this handle.
    /// </summary>
    public bool IsJitOptimizationEnabled => IsPinned;

    /// <summary>
    /// Gets a scoped JIT optimization that automatically disables when disposed.
    /// </summary>
    /// <returns>A disposable scope for JIT optimization.</returns>
    public JitOptimizationScope GetJitOptimizationScope()
    {
        EnableJitOptimization();
        return new JitOptimizationScope(this);
    }

    /// <summary>
    /// Gets a byte span from the cached body (for variable-length data).
    /// </summary>
    protected ReadOnlySpan<byte> GetFieldBytes(int offset, int length)
    {
        EnsureMaterialized();
        VayronMetaTable.Get(this)?.RecordAccess();
        return _cachedBody.AsSpan(BodyHeader.Size + offset, length);
    }

    /// <summary>
    /// Sets bytes in the cached body.
    /// </summary>
    protected void SetFieldBytes(int offset, ReadOnlySpan<byte> data)
    {
        EnsureMaterialized();
        data.CopyTo(_cachedBody.AsSpan(BodyHeader.Size + offset));
        MarkDirty();
    }

    // =====================================================================
    // Materialization
    // =====================================================================

    /// <summary>
    /// Ensures the body is materialized (loaded from storage).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EnsureMaterialized()
    {
        if (_cachedBody != null && !IsStale())
        {
            return;
        }

        Materialize();
    }

    /// <summary>
    /// Checks if the cached body is stale (transaction changed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsStale()
    {
        var currentEpoch = VayronTransaction.CurrentEpoch;
        return currentEpoch > _epoch;
    }

    /// <summary>
    /// Materializes the body from storage.
    /// </summary>
    protected virtual void Materialize()
    {
        var scope = VayronTransaction.Current
            ?? throw new InvalidOperationException("No active VAYRON transaction. Use VayronTransaction.BeginRead() or BeginWrite().");

        // Phase 4: Enroll in transaction and record materialization
        EnrollInTransaction();
        VayronTransaction.CurrentContext?.RecordOperation(OperationType.Materialize, _oid);

        var meta = VayronMetaTable.GetOrCreate(this, _oid);

        // Phase 3: Validate state transition
        if (!meta.TryTransitionState(MaterializationState.Materializing))
        {
            // If can't transition, we might already be materializing or materialized
            if (meta.State == MaterializationState.Materialized && !meta.IsStale(_epoch))
            {
                // Already materialized and not stale - use cached body
                var managedBody = meta.GetManagedBody();
                if (managedBody != null)
                {
                    _cachedBody = managedBody;
                    _epoch = meta.Epoch;
                    return;
                }
            }
        }

        try
        {
            // Look up storage location
            if (!_environment.TryGetStorageLocation(scope, _oid, out var storageLocation))
            {
                // New object - allocate body
                InitializeNewBody(scope);
                return;
            }

            // Load existing body
            var storedBody = _environment.GetBody(scope, storageLocation);
            _cachedBody = storedBody.ToArray();
            _epoch = scope.Epoch;

            // Update metadata in side table
            meta.StorageLocation = storageLocation;
            meta.SetManagedBody(_cachedBody);
            meta.MarkMaterialized(_epoch, _cachedBody);

            // Phase 3: Auto-pin if configured for hot bodies
            if ((meta.Flags & VayronMetaFlags.PreferPinned) != 0)
            {
                meta.PinBody(_cachedBody);
            }
        }
        catch
        {
            // On failure, transition to stale
            meta.TryTransitionState(MaterializationState.Stale);
            throw;
        }
    }

    /// <summary>
    /// Initializes a new body for a newly created object.
    /// Override this in derived classes to set initial field values.
    /// </summary>
    protected virtual void InitializeNewBody(VayronTransactionScope scope)
    {
        // Phase 4: Record create operation
        VayronTransaction.CurrentContext?.RecordOperation(OperationType.Create, _oid);

        // Default implementation creates minimal body
        var bodySize = GetBodySize();
        _cachedBody = new byte[BodyHeader.Size + bodySize];

        // Write header
        ref var header = ref MemoryMarshal.AsRef<BodyHeader>(_cachedBody);
        header.TypeToken = GetTypeToken();
        header.SchemaVersion = GetSchemaVersion();
        header.Flags = 0;

        _epoch = scope.Epoch;
        _isDirty = true;

        // Register for persistence
        _environment.RegisterDirtyHandle(this);

        // Update metadata
        var meta = VayronMetaTable.GetOrCreate(this, _oid);
        meta.TypeToken = header.TypeToken;
        meta.SchemaVersion = header.SchemaVersion;
        meta.SetManagedBody(_cachedBody);
        meta.TryTransitionState(MaterializationState.Dirty);
    }

    /// <summary>
    /// Gets the size of the body (excluding header). Override in derived classes.
    /// </summary>
    protected virtual int GetBodySize() => 0;

    /// <summary>
    /// Gets the type token for this handle type. Override in derived classes.
    /// </summary>
    protected virtual uint GetTypeToken() => 0;

    /// <summary>
    /// Gets the schema version. Override in derived classes.
    /// </summary>
    protected virtual ushort GetSchemaVersion() => 1;

    // =====================================================================
    // Dirty Tracking
    // =====================================================================

    /// <summary>
    /// Marks the handle as dirty (modified).
    /// </summary>
    protected void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            _environment.RegisterDirtyHandle(this);

            var meta = VayronMetaTable.Get(this);
            meta?.MarkDirty();
        }
    }

    // =====================================================================
    // Persistence
    // =====================================================================

    /// <summary>
    /// Persists the handle's data to storage.
    /// </summary>
    public virtual void Persist(VayronTransactionScope scope)
    {
        if (!_isDirty || _cachedBody == null)
        {
            return;
        }

        var meta = VayronMetaTable.Get(this);

        // Check if we have an existing storage location
        if (meta?.StorageLocation.IsValid == true)
        {
            // Update existing body
            var mutableBody = _environment.GetMutableBody(scope, meta.StorageLocation);
            if (mutableBody.Length >= _cachedBody.Length)
            {
                _cachedBody.AsSpan().CopyTo(mutableBody);
            }
            else
            {
                // Body grew - need to reallocate
                _environment.DeleteBody(scope, meta.StorageLocation);
                var newLocation = _environment.AllocateBody(scope, _cachedBody.Length, out var newBody);
                _cachedBody.AsSpan().CopyTo(newBody);
                _environment.RemoveOidMapping(scope, _oid);
                _environment.AddOidMapping(scope, _oid, newLocation);
                meta.StorageLocation = newLocation;
            }
        }
        else
        {
            // New object - allocate storage
            var storageLocation = _environment.AllocateBody(scope, _cachedBody.Length, out var body);
            _cachedBody.AsSpan().CopyTo(body);
            _environment.AddOidMapping(scope, _oid, storageLocation);

            if (meta != null)
            {
                meta.StorageLocation = storageLocation;
            }
        }

        _isDirty = false;
        _epoch = scope.Epoch;

        // Phase 3: Update metadata state
        meta?.MarkMaterialized(_epoch, _cachedBody);
    }

    // =====================================================================
    // Deletion
    // =====================================================================

    /// <summary>
    /// Deletes this object from storage.
    /// </summary>
    public virtual void Delete()
    {
        var scope = VayronTransaction.Current
            ?? throw new InvalidOperationException("No active VAYRON transaction.");

        if (!scope.IsWriteTransaction)
        {
            throw new InvalidOperationException("Cannot delete in a read transaction.");
        }

        // Phase 4: Record delete operation
        VayronTransaction.CurrentContext?.RecordOperation(OperationType.Delete, _oid);

        var meta = VayronMetaTable.Get(this);
        if (meta?.StorageLocation.IsValid == true)
        {
            _environment.DeleteBody(scope, meta.StorageLocation);
        }

        _environment.RemoveOidMapping(scope, _oid);

        // Phase 3: Mark for deletion in metadata
        if (meta != null)
        {
            meta.Flags |= VayronMetaFlags.MarkedForDeletion;
        }

        _cachedBody = null;
        _isDirty = false;
        _oid = VayronOid.Invalid;

        VayronMetaTable.Remove(this);
    }

    // =====================================================================
    // Invalidation
    // =====================================================================

    /// <summary>
    /// Invalidates the cached body (forces reload on next access).
    /// </summary>
    public void Invalidate()
    {
        _cachedBody = null;
        _epoch = -1;

        VayronMetaTable.Get(this)?.Invalidate();
    }

    // =====================================================================
    // Phase 3: Pinning Support
    // =====================================================================

    /// <summary>
    /// Pins the cached body in memory for fast native pointer access.
    /// </summary>
    /// <remarks>
    /// Pinning prevents GC from moving the body, enabling fast pointer-based access.
    /// Call <see cref="Unpin"/> when done to allow GC to reclaim memory.
    /// </remarks>
    public void Pin()
    {
        if (_cachedBody == null)
            throw new InvalidOperationException("Cannot pin: body not materialized.");

        var meta = VayronMetaTable.Get(this);
        if (meta != null && !meta.IsPinned)
        {
            meta.PinBody(_cachedBody);
        }
    }

    /// <summary>
    /// Unpins the cached body, allowing GC to move it.
    /// </summary>
    public void Unpin()
    {
        VayronMetaTable.Get(this)?.Unpin();
    }

    /// <summary>
    /// Gets whether the cached body is currently pinned.
    /// </summary>
    public bool IsPinned => VayronMetaTable.Get(this)?.IsPinned ?? false;

    // =====================================================================
    // Phase 4: Transaction Integration
    // =====================================================================

    /// <summary>
    /// Enrolls this handle in the current transaction as a participant.
    /// </summary>
    /// <remarks>
    /// This method is called automatically during materialization and field access,
    /// but can also be called explicitly to ensure a handle is tracked by the transaction.
    /// </remarks>
    protected void EnrollInTransaction()
    {
        VayronTransaction.Current?.Enroll(this);
    }

    /// <summary>
    /// Records a read operation on this handle.
    /// </summary>
    protected void RecordReadOperation()
    {
        VayronTransaction.Current?.RecordRead(_oid);
    }

    /// <summary>
    /// Records a write operation on this handle.
    /// </summary>
    protected void RecordWriteOperation()
    {
        VayronTransaction.Current?.RecordWrite(_oid);
    }

    /// <summary>
    /// Gets the current transaction context, if any.
    /// </summary>
    public VayronTransactionContext? TransactionContext => VayronTransaction.CurrentContext;

    /// <summary>
    /// Gets whether this handle is enrolled in the current transaction.
    /// </summary>
    public bool IsEnrolledInTransaction
    {
        get
        {
            var ctx = VayronTransaction.CurrentContext;
            return ctx?.IsEnrolled(_oid) == true;
        }
    }

    /// <summary>
    /// Executes an action within a read transaction, using the existing transaction if available.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void WithReadTransaction(Action action)
    {
        if (VayronTransaction.HasActiveTransaction)
        {
            action();
        }
        else
        {
            VayronTransaction.ExecuteRead(_environment, action);
        }
    }

    /// <summary>
    /// Executes an action within a write transaction, using the existing transaction if available.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void WithWriteTransaction(Action action)
    {
        if (VayronTransaction.HasActiveWriteTransaction)
        {
            action();
        }
        else
        {
            VayronTransaction.ExecuteWrite(_environment, action);
        }
    }

    /// <summary>
    /// Executes a function within a read transaction, using the existing transaction if available.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    public T WithReadTransaction<T>(Func<T> func)
    {
        if (VayronTransaction.HasActiveTransaction)
        {
            return func();
        }
        else
        {
            return VayronTransaction.ExecuteRead(_environment, func);
        }
    }

    /// <summary>
    /// Executes a function within a write transaction, using the existing transaction if available.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    public T WithWriteTransaction<T>(Func<T> func)
    {
        if (VayronTransaction.HasActiveWriteTransaction)
        {
            return func();
        }
        else
        {
            return VayronTransaction.ExecuteWrite(_environment, func);
        }
    }

    // =====================================================================
    // Disposal
    // =====================================================================

    /// <summary>
    /// Finalizer for cleanup.
    /// </summary>
    ~VayronHandle()
    {
        Dispose(disposing: false);
    }

    /// <summary>
    /// Disposes the handle.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the handle.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Phase 3: Record finalization for lifecycle manager
        if (!disposing && _oid.IsValid)
        {
            VayronLifecycleManager.Instance.RecordFinalization(_oid, _cachedBody?.Length ?? 0);
        }

        if (disposing)
        {
            // Clean up managed resources
            VayronMetaTable.Remove(this);
        }

        _cachedBody = null;
    }
}

/// <summary>
/// Comprehensive diagnostic information about a VAYRON handle.
/// </summary>
public readonly struct VayronHandleDiagInfo
{
    /// <summary>The OID.</summary>
    public VayronOid Oid { get; init; }

    /// <summary>The epoch.</summary>
    public long Epoch { get; init; }

    /// <summary>Whether dirty.</summary>
    public bool IsDirty { get; init; }

    /// <summary>Whether materialized.</summary>
    public bool IsMaterialized { get; init; }

    /// <summary>Cached body size.</summary>
    public int CachedBodySize { get; init; }

    /// <summary>Materialization state.</summary>
    public MaterializationState MaterializationState { get; init; }

    /// <summary>Storage location.</summary>
    public ContainerEntryId StorageLocation { get; init; }

    /// <summary>Type token.</summary>
    public uint TypeToken { get; init; }

    /// <summary>Schema version.</summary>
    public ushort SchemaVersion { get; init; }

    /// <summary>Whether pinned.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Cached body pointer (if pinned).</summary>
    public IntPtr CachedBodyPtr { get; init; }

    /// <summary>Last access ticks.</summary>
    public long LastAccessTicks { get; init; }

    /// <summary>Access count.</summary>
    public int AccessCount { get; init; }

    /// <summary>Object header info.</summary>
    public VayronHeaderInfo HeaderInfo { get; init; }

    public override string ToString()
    {
        return $"Handle[OID={Oid.Value}] State={MaterializationState} Size={CachedBodySize} " +
               $"Pinned={IsPinned} Dirty={IsDirty} Access={AccessCount}";
    }
}

/// <summary>
/// A scoped JIT optimization that automatically disables when disposed.
/// </summary>
/// <remarks>
/// Use with <c>using</c> statement for automatic cleanup:
/// <code>
/// using (handle.GetJitOptimizationScope())
/// {
///     // Fast field access here
/// }
/// </code>
/// </remarks>
public readonly struct JitOptimizationScope : IDisposable
{
    private readonly VayronHandle _handle;

    internal JitOptimizationScope(VayronHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Disables JIT optimization.
    /// </summary>
    public void Dispose()
    {
        _handle?.DisableJitOptimization();
    }
}
