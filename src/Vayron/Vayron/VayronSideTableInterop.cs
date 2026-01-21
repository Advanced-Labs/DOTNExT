// VAYRON - Runtime-Integrated Persistent Storage
// Side Table Native Interop
//
// Phase 3: Managed-to-native interop for side table operations
//
// This class provides P/Invoke declarations for the native side table helpers.
// When running on DOTNExT runtime with native VAYRON support, these calls
// go directly to native code for optimal performance.
// When running on standard .NET, these are no-ops and managed fallback is used.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vayron;

/// <summary>
/// Native interop for VAYRON side table operations.
/// </summary>
/// <remarks>
/// <para><b>Usage:</b></para>
/// <para>
/// These methods are used internally by the VAYRON framework. Applications
/// should use the higher-level VayronMetaTable API instead.
/// </para>
///
/// <para><b>Runtime Behavior:</b></para>
/// <list type="bullet">
/// <item><description>DOTNExT Runtime: Calls native FCalls/QCalls</description></item>
/// <item><description>Standard .NET: Returns false, uses managed fallback</description></item>
/// </list>
/// </remarks>
public static unsafe class VayronSideTableInterop
{
    // =====================================================================
    // Native Structure (must match vayronsidetable.h)
    // =====================================================================

    /// <summary>
    /// Native representation of VAYRON metadata.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VayronMetaInfo
    {
        public long Oid;
        public long Epoch;
        public IntPtr CachedBodyPtr;
        public int CachedBodySize;
        public int State;
        public uint TypeToken;
        public ushort SchemaVersion;
        public ushort Flags;
    }

    // =====================================================================
    // Runtime Detection
    // =====================================================================

    private static bool? _isNativeSupported;

    /// <summary>
    /// Gets whether native side table support is available.
    /// </summary>
    public static bool IsNativeSupported
    {
        get
        {
            if (!_isNativeSupported.HasValue)
            {
                _isNativeSupported = DetectNativeSupport();
            }
            return _isNativeSupported.Value;
        }
    }

    private static bool DetectNativeSupport()
    {
        try
        {
            // Try calling native state validation function
            // This should work on DOTNExT runtime
            return Native_IsValidTransition(0, 1);
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    // =====================================================================
    // Native Imports (FCalls)
    // =====================================================================

    // Note: These are defined as FCalls in the DOTNExT runtime.
    // On standard .NET, these will throw EntryPointNotFoundException.

    [DllImport("QCall", EntryPoint = "VayronSideTable_IsValidTransition")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Native_IsValidTransition(int fromState, int toState);

    [DllImport("QCall", EntryPoint = "VayronSideTable_TryGetOid")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Native_TryGetOid(IntPtr objHandle, out long oid);

    [DllImport("QCall", EntryPoint = "VayronSideTable_TryGetState")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Native_TryGetState(IntPtr objHandle, out int state);

    [DllImport("QCall", EntryPoint = "VayronSideTable_TryGetCachedBodyPtr")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Native_TryGetCachedBodyPtr(IntPtr objHandle, out IntPtr bodyPtr, out int bodySize);

    [DllImport("QCall", EntryPoint = "VayronSideTable_TryGetMetaInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Native_TryGetMetaInfo(IntPtr objHandle, out VayronMetaInfo info);

    [DllImport("QCall", EntryPoint = "VayronSideTable_UpdateStatistics")]
    private static extern void Native_UpdateStatistics(int activeCount, long totalBytes, long getCount, long missCount);

    // =====================================================================
    // Public API (with fallback)
    // =====================================================================

    /// <summary>
    /// Checks if a state transition is valid (native call).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTransition(MaterializationState from, MaterializationState to)
    {
        if (IsNativeSupported)
        {
            try
            {
                return Native_IsValidTransition((int)from, (int)to);
            }
            catch
            {
                // Fall through to managed implementation
            }
        }

        // Managed fallback
        return VayronStateManager.IsValidTransition(from, to);
    }

    /// <summary>
    /// Tries to get the OID for a handle using native interop.
    /// </summary>
    /// <param name="handle">The handle object.</param>
    /// <param name="oid">Output: the OID.</param>
    /// <returns>True if the handle is a VAYRON handle.</returns>
    public static bool TryGetOid(object handle, out long oid)
    {
        oid = 0;

        // Fast path: check header bit first
        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        // Use managed implementation for now
        // Native interop would require ObjectHandle marshaling which isn't trivial
        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            oid = meta.Oid.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to get the state for a handle using native interop.
    /// </summary>
    public static bool TryGetState(object handle, out MaterializationState state)
    {
        state = MaterializationState.NotMaterialized;

        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            state = meta.State;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to get the cached body pointer for a handle.
    /// </summary>
    public static bool TryGetCachedBodyPtr(object handle, out IntPtr bodyPtr, out int bodySize)
    {
        bodyPtr = IntPtr.Zero;
        bodySize = 0;

        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        return VayronMetaTable.TryGetCachedBodyPtr(handle, out bodyPtr, out bodySize);
    }

    /// <summary>
    /// Gets full metadata info for a handle.
    /// </summary>
    public static bool TryGetMetaInfo(object handle, out VayronMetaInfo info)
    {
        info = default;

        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            info = new VayronMetaInfo
            {
                Oid = meta.Oid.Value,
                Epoch = meta.Epoch,
                CachedBodyPtr = meta.CachedBodyPtr,
                CachedBodySize = meta.CachedBodySize,
                State = (int)meta.State,
                TypeToken = meta.TypeToken,
                SchemaVersion = meta.SchemaVersion,
                Flags = (ushort)meta.Flags,
            };
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates native statistics from managed side.
    /// </summary>
    public static void UpdateNativeStatistics()
    {
        if (!IsNativeSupported)
            return;

        var stats = VayronMetaTable.GetStatistics();
        try
        {
            Native_UpdateStatistics(
                stats.ActiveCount,
                stats.TotalBytesTracked,
                stats.GetCount,
                stats.MissCount);
        }
        catch
        {
            // Ignore if native call fails
        }
    }

    // =====================================================================
    // Helper Methods
    // =====================================================================

    /// <summary>
    /// Checks if body is available (Materialized or Dirty state).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBodyAvailable(object handle)
    {
        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            return VayronStateManager.IsBodyAvailable(meta.State);
        }

        return false;
    }

    /// <summary>
    /// Checks if body needs loading.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsLoad(object handle)
    {
        if (!VayronRuntime.IsVayronHandle(handle))
            return false;

        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            return VayronStateManager.NeedsLoad(meta.State);
        }

        return true; // Default to needs load
    }

    /// <summary>
    /// Records a field access for a handle (for statistics).
    /// </summary>
    public static void RecordFieldAccess(object handle)
    {
        if (VayronMetaTable.TryGet(handle, out var meta) && meta != null)
        {
            meta.RecordAccess();
        }
    }
}
