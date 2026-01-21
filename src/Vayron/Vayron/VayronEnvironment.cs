// VAYRON - Runtime-Integrated Persistent Storage
// Environment wrapping Voron StorageEnvironment

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Sparrow.Server;
using Voron;
using Voron.Data.Containers;
using Voron.Data.Lookups;
using Voron.Impl;

namespace Vayron;

/// <summary>
/// Configuration options for VAYRON environment.
/// </summary>
public sealed class VayronEnvironmentOptions
{
    /// <summary>
    /// Path to the storage directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Initial size of the data file.
    /// </summary>
    public long? InitialFileSize { get; init; }

    /// <summary>
    /// Maximum size of the data file.
    /// </summary>
    public long? MaxStorageSize { get; init; }

    /// <summary>
    /// Whether to force durability (fsync on commit).
    /// </summary>
    public bool ForceDurability { get; init; } = true;
}

/// <summary>
/// The main entry point for VAYRON persistent storage.
/// Wraps a Voron StorageEnvironment and provides handle management.
/// </summary>
public sealed unsafe class VayronEnvironment : IDisposable
{
    /// <summary>
    /// Well-known tree/container names.
    /// </summary>
    private static class TreeNames
    {
        public const string OidIndex = "vayron:oid-index";
        public const string Bodies = "vayron:bodies";
        public const string TypeRegistry = "vayron:type-registry";
        public const string Metadata = "vayron:metadata";
    }

    /// <summary>
    /// Well-known metadata keys.
    /// </summary>
    private static class MetadataKeys
    {
        public const string NextOid = "next-oid";
    }

    private readonly StorageEnvironmentOptions _voronOptions;
    private readonly StorageEnvironment _voronEnv;
    private long _nextOid;
    private bool _disposed;

    // Lookup for OID -> StorageLocation (ContainerEntryId)
    private LookupState _oidIndexState;

    // Container for object bodies
    private ContainerId _bodiesContainerId;

    // Dirty handles that need to be persisted
    private readonly ConcurrentBag<WeakReference<IVayronHandle>> _dirtyHandles = new();

    /// <summary>
    /// Gets the underlying Voron StorageEnvironment.
    /// </summary>
    public StorageEnvironment VoronEnvironment => _voronEnv;

    /// <summary>
    /// Whether this is a new (empty) environment.
    /// </summary>
    public bool IsNew => _voronEnv.IsNew;

    /// <summary>
    /// Creates a new VAYRON environment.
    /// </summary>
    public VayronEnvironment(VayronEnvironmentOptions options)
    {
        _voronOptions = StorageEnvironmentOptions.ForPath(options.Path);

        if (options.InitialFileSize.HasValue)
        {
            _voronOptions.InitialFileSize = options.InitialFileSize.Value;
        }

        if (options.MaxStorageSize.HasValue)
        {
            _voronOptions.MaxStorageSize = options.MaxStorageSize.Value;
        }

        if (!options.ForceDurability)
        {
            _voronOptions.ManualSyncing = true;
        }

        _voronEnv = new StorageEnvironment(_voronOptions);

        Initialize();
    }

    /// <summary>
    /// Initializes the VAYRON structures.
    /// </summary>
    private void Initialize()
    {
        using var tx = _voronEnv.WriteTransaction();

        // Create or open the OID index lookup
        InitializeOidIndex(tx);

        // Create or open the bodies container
        _bodiesContainerId = tx.OpenContainer(TreeNames.Bodies);

        // Load or initialize next OID
        LoadNextOid(tx);

        tx.Commit();
    }

    private void InitializeOidIndex(Transaction tx)
    {
        var existingState = tx.LowLevelTransaction.RootObjects.DirectRead(TreeNames.OidIndex);
        if (existingState != null)
        {
            _oidIndexState = MemoryMarshal.Read<LookupState>(new ReadOnlySpan<byte>(existingState, sizeof(LookupState)));
        }
        else
        {
            // Create new lookup for OID -> StorageLocation
            Lookup<Int64LookupKey>.Create(tx.LowLevelTransaction, out _oidIndexState);

            // Persist the state
            using (tx.LowLevelTransaction.RootObjects.DirectAdd(TreeNames.OidIndex, sizeof(LookupState), out var ptr))
            {
                MemoryMarshal.Write(new Span<byte>(ptr, sizeof(LookupState)), _oidIndexState);
            }
        }
    }

    private unsafe void LoadNextOid(Transaction tx)
    {
        var tree = tx.ReadTree(TreeNames.Metadata);
        if (tree == null)
        {
            // Create metadata tree
            tree = tx.CreateTree(TreeNames.Metadata);
            _nextOid = 1; // First valid OID
            SaveNextOid(tx, tree);
        }
        else
        {
            using (Slice.From(tx.Allocator, MetadataKeys.NextOid, out var key))
            {
                var reader = tree.Read(key);
                if (reader == null)
                {
                    _nextOid = 1;
                    SaveNextOid(tx, tree);
                }
                else
                {
                    _nextOid = reader.Reader.ReadLittleEndianInt64();
                }
            }
        }
    }

    private unsafe void SaveNextOid(Transaction tx, Voron.Data.BTrees.Tree tree)
    {
        using (Slice.From(tx.Allocator, MetadataKeys.NextOid, out var key))
        {
            using (tree.DirectAdd(key, sizeof(long), out var ptr))
            {
                *(long*)ptr = _nextOid;
            }
        }
    }

    /// <summary>
    /// Generates a new unique OID.
    /// </summary>
    public VayronOid GenerateOid()
    {
        return new VayronOid(Interlocked.Increment(ref _nextOid));
    }

    /// <summary>
    /// Begins a read transaction.
    /// </summary>
    public VayronTransactionScope ReadTransaction()
    {
        return VayronTransaction.BeginRead(this);
    }

    /// <summary>
    /// Begins a write transaction.
    /// </summary>
    public VayronTransactionScope WriteTransaction()
    {
        return VayronTransaction.BeginWrite(this);
    }

    /// <summary>
    /// Allocates storage for an object body.
    /// </summary>
    internal unsafe ContainerEntryId AllocateBody(VayronTransactionScope scope, int size, out Span<byte> buffer)
    {
        return Container.Allocate(scope.VoronTransaction.LowLevelTransaction, _bodiesContainerId, size, out buffer);
    }

    /// <summary>
    /// Gets the body data for a storage location.
    /// </summary>
    internal Span<byte> GetBody(VayronTransactionScope scope, ContainerEntryId storageLocation)
    {
        return Container.GetReadOnly(scope.VoronTransaction.LowLevelTransaction, storageLocation);
    }

    /// <summary>
    /// Gets mutable body data for a storage location.
    /// </summary>
    internal Span<byte> GetMutableBody(VayronTransactionScope scope, ContainerEntryId storageLocation)
    {
        return Container.GetMutable(scope.VoronTransaction.LowLevelTransaction, storageLocation);
    }

    /// <summary>
    /// Deletes body storage for a given entry.
    /// </summary>
    internal void DeleteBody(VayronTransactionScope scope, ContainerEntryId storageLocation)
    {
        Container.Delete(scope.VoronTransaction.LowLevelTransaction, _bodiesContainerId, storageLocation);
    }

    /// <summary>
    /// Adds an OID -> StorageLocation mapping.
    /// </summary>
    internal unsafe void AddOidMapping(VayronTransactionScope scope, VayronOid oid, ContainerEntryId storageLocation)
    {
        var lookup = Lookup<Int64LookupKey>.Open(scope.VoronTransaction.LowLevelTransaction, _oidIndexState);
        lookup.Add(oid.Value, (long)storageLocation);
        _oidIndexState = lookup.State;

        // Update persisted state
        using (scope.VoronTransaction.LowLevelTransaction.RootObjects.DirectAdd(TreeNames.OidIndex, sizeof(LookupState), out var ptr))
        {
            MemoryMarshal.Write(new Span<byte>(ptr, sizeof(LookupState)), _oidIndexState);
        }
    }

    /// <summary>
    /// Looks up the storage location for an OID.
    /// </summary>
    internal bool TryGetStorageLocation(VayronTransactionScope scope, VayronOid oid, out ContainerEntryId storageLocation)
    {
        var lookup = Lookup<Int64LookupKey>.Open(scope.VoronTransaction.LowLevelTransaction, _oidIndexState);
        var key = new Int64LookupKey(oid.Value);

        if (lookup.TryGetValue(ref key, out var location))
        {
            storageLocation = (ContainerEntryId)location;
            return true;
        }

        storageLocation = ContainerEntryId.Invalid;
        return false;
    }

    /// <summary>
    /// Removes an OID mapping.
    /// </summary>
    internal unsafe void RemoveOidMapping(VayronTransactionScope scope, VayronOid oid)
    {
        var lookup = Lookup<Int64LookupKey>.Open(scope.VoronTransaction.LowLevelTransaction, _oidIndexState);
        var key = new Int64LookupKey(oid.Value);
        lookup.TryRemove(ref key, out _);
        _oidIndexState = lookup.State;

        // Update persisted state
        using (scope.VoronTransaction.LowLevelTransaction.RootObjects.DirectAdd(TreeNames.OidIndex, sizeof(LookupState), out var ptr))
        {
            MemoryMarshal.Write(new Span<byte>(ptr, sizeof(LookupState)), _oidIndexState);
        }
    }

    /// <summary>
    /// Registers a dirty handle for persistence on commit.
    /// </summary>
    internal void RegisterDirtyHandle(IVayronHandle handle)
    {
        _dirtyHandles.Add(new WeakReference<IVayronHandle>(handle));
    }

    /// <summary>
    /// Persists all dirty handles during commit.
    /// </summary>
    internal void PersistDirtyHandles(VayronTransactionScope scope)
    {
        // Process all dirty handles
        while (_dirtyHandles.TryTake(out var weakRef))
        {
            if (weakRef.TryGetTarget(out var handle))
            {
                handle.Persist(scope);
            }
        }

        // Update the next OID in storage
        var tree = scope.VoronTransaction.ReadTree(TreeNames.Metadata)
            ?? scope.VoronTransaction.CreateTree(TreeNames.Metadata);
        SaveNextOid(scope.VoronTransaction, tree);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _voronEnv.Dispose();
    }
}

/// <summary>
/// Interface for VAYRON handles that can be persisted.
/// </summary>
public interface IVayronHandle
{
    /// <summary>
    /// The Object Identifier for this handle.
    /// </summary>
    VayronOid Oid { get; }

    /// <summary>
    /// Persists the handle's data to storage.
    /// </summary>
    void Persist(VayronTransactionScope scope);
}
