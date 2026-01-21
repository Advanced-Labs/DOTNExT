// VAYRON - Runtime-Integrated Persistent Storage
// Handle with lazy materialization

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
/// <para>
/// Use <see cref="IsVayronHandleInstance"/> to check if any object is a VAYRON handle
/// using the runtime header bit classification.
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
    ///
    /// <para><b>Usage:</b></para>
    /// <code>
    /// object someObj = GetSomeObject();
    /// if (VayronHandle.IsVayronHandleInstance(someObj))
    /// {
    ///     // Object is a VAYRON handle
    ///     var handle = (VayronHandle)someObj;
    /// }
    /// </code>
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
        // This enables fast O(1) classification via single bit test
        VayronRuntime.MarkAsVayronHandle(this);
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
        // This enables fast O(1) classification via single bit test
        VayronRuntime.MarkAsVayronHandle(this);
    }

    /// <summary>
    /// Gets a field value from the cached body.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T GetField<T>(int offset) where T : unmanaged
    {
        EnsureMaterialized();
        return MemoryMarshal.Read<T>(_cachedBody.AsSpan(BodyHeader.Size + offset));
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
    }

    /// <summary>
    /// Gets a byte span from the cached body (for variable-length data).
    /// </summary>
    protected ReadOnlySpan<byte> GetFieldBytes(int offset, int length)
    {
        EnsureMaterialized();
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
        var meta = VayronMetaTable.GetOrCreate(this, _oid);
        meta.StorageLocation = storageLocation;
        meta.MarkMaterialized(_epoch, IntPtr.Zero, _cachedBody.Length);
    }

    /// <summary>
    /// Initializes a new body for a newly created object.
    /// Override this in derived classes to set initial field values.
    /// </summary>
    protected virtual void InitializeNewBody(VayronTransactionScope scope)
    {
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
        meta.State = MaterializationState.Dirty;
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

        meta?.MarkMaterialized(_epoch, IntPtr.Zero, _cachedBody.Length);
    }

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

        var meta = VayronMetaTable.Get(this);
        if (meta?.StorageLocation.IsValid == true)
        {
            _environment.DeleteBody(scope, meta.StorageLocation);
        }

        _environment.RemoveOidMapping(scope, _oid);

        _cachedBody = null;
        _isDirty = false;
        _oid = VayronOid.Invalid;

        VayronMetaTable.Remove(this);
    }

    /// <summary>
    /// Invalidates the cached body (forces reload on next access).
    /// </summary>
    public void Invalidate()
    {
        _cachedBody = null;
        _epoch = -1;

        VayronMetaTable.Get(this)?.Invalidate();
    }

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

        if (disposing)
        {
            // Clean up managed resources
            VayronMetaTable.Remove(this);
        }

        _cachedBody = null;
    }
}
