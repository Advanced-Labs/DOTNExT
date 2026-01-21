// VAYRON - Runtime-Integrated Persistent Storage
// Side table for handle metadata

using System.Runtime.CompilerServices;

namespace Vayron;

/// <summary>
/// Side table storing metadata for VAYRON handles.
/// Uses ConditionalWeakTable for GC-friendly weak keying.
/// </summary>
/// <remarks>
/// This pattern is proven in the CLR (e.g., DependentHandle).
/// When a handle object is collected, its metadata is automatically removed.
/// </remarks>
public static class VayronMetaTable
{
    /// <summary>
    /// The underlying weak table: handle object -> metadata.
    /// </summary>
    private static readonly ConditionalWeakTable<object, VayronMeta> _table = new();

    /// <summary>
    /// Gets the metadata for a handle, or null if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VayronMeta? Get(object handle)
    {
        _table.TryGetValue(handle, out var meta);
        return meta;
    }

    /// <summary>
    /// Gets existing metadata or creates new metadata for the handle.
    /// </summary>
    public static VayronMeta GetOrCreate(object handle, VayronOid oid)
    {
        return _table.GetValue(handle, _ => new VayronMeta(oid));
    }

    /// <summary>
    /// Sets the metadata for a handle.
    /// </summary>
    public static void Set(object handle, VayronMeta meta)
    {
        _table.AddOrUpdate(handle, meta);
    }

    /// <summary>
    /// Removes metadata for a handle.
    /// </summary>
    public static bool Remove(object handle)
    {
        return _table.Remove(handle);
    }

    /// <summary>
    /// Tries to get metadata for a handle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(object handle, out VayronMeta? meta)
    {
        return _table.TryGetValue(handle, out meta);
    }
}
