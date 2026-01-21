// VAYRON - Runtime-Integrated Persistent Storage
// JIT Interop - Managed interface for JIT helper integration
//
// DOTNExT VAYRON Phase 5 Implementation
// This file provides the managed-side interface for JIT helper interception.
//
// Architecture:
// The JIT helpers (in native code) intercept field access to VAYRON handles
// and call back to managed code for materialization when needed. This file
// provides:
// 1. Performance statistics structure matching native layout
// 2. Managed callbacks for materialization
// 3. Cache management for fast native access
// 4. Thread-safe operation tracking

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vayron;

/// <summary>
/// Performance statistics for VAYRON JIT field access operations.
/// Layout must match native VayronFieldAccessStats exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VayronFieldAccessStats
{
    /// <summary>Total field access interceptions.</summary>
    public long TotalFieldAccesses;

    /// <summary>Cache hit - body already materialized.</summary>
    public long FastPathHits;

    /// <summary>Cache miss - needed to materialize.</summary>
    public long SlowPathMaterializations;

    /// <summary>No transaction - fallback to managed.</summary>
    public long TransactionMisses;

    /// <summary>Null object handled.</summary>
    public long NullObjectAccesses;

    /// <summary>Object not VAYRON - standard path.</summary>
    public long NonVayronFallbacks;

    /// <summary>Body cache invalidated (stale).</summary>
    public long CacheInvalidations;

    /// <summary>Total time spent in VAYRON path (nanoseconds).</summary>
    public long TotalNanoseconds;

    /// <summary>Gets the fast path hit rate.</summary>
    public readonly double FastPathHitRate =>
        TotalFieldAccesses > 0 ? (double)FastPathHits / TotalFieldAccesses * 100.0 : 0.0;

    /// <summary>Gets the average time per access in nanoseconds.</summary>
    public readonly double AverageNanosecondsPerAccess =>
        TotalFieldAccesses > 0 ? (double)TotalNanoseconds / TotalFieldAccesses : 0.0;

    /// <summary>Gets a human-readable summary of the statistics.</summary>
    public override readonly string ToString()
    {
        return $"VAYRON JIT Stats: Total={TotalFieldAccesses:N0} FastPath={FastPathHits:N0} ({FastPathHitRate:F1}%) " +
               $"Materialize={SlowPathMaterializations:N0} NoTx={TransactionMisses:N0} NonVayron={NonVayronFallbacks:N0}";
    }
}

/// <summary>
/// Managed interface for VAYRON JIT helper integration.
/// </summary>
/// <remarks>
/// This class provides the bridge between native JIT helpers and managed VAYRON code.
/// When the JIT intercepts a field access to a VAYRON handle, it can call back to
/// this class for materialization and cache management.
///
/// <para><b>Thread Safety:</b> All methods are thread-safe.</para>
///
/// <para><b>Usage:</b> This is an internal class used by the VAYRON infrastructure.
/// User code should interact with VayronHandle and VayronTransaction instead.</para>
/// </remarks>
public static unsafe class VayronJitInterop
{
    private static bool _isInitialized;
    private static readonly object _initLock = new();

    // Delegate types for callbacks
    private delegate IntPtr MaterializeCallbackDelegate(IntPtr objHandle);

    // Pinned callback delegate to prevent GC collection
    private static GCHandle _materializeCallbackHandle;

    /// <summary>
    /// Initializes the JIT interop system.
    /// </summary>
    /// <remarks>
    /// This should be called during application startup to register managed
    /// callbacks with the native runtime. It's safe to call multiple times.
    /// </remarks>
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        lock (_initLock)
        {
            if (_isInitialized)
                return;

            // Create and pin the callback delegate
            MaterializeCallbackDelegate callback = MaterializeCallback;
            _materializeCallbackHandle = GCHandle.Alloc(callback);

            // Register with native runtime (when DOTNExT runtime is used)
            // For standard .NET runtime, this is a no-op
            try
            {
                var callbackPtr = Marshal.GetFunctionPointerForDelegate(callback);
                NativeRegisterMaterializeCallback(callbackPtr);
            }
            catch
            {
                // Native runtime not available - use managed-only path
            }

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Gets the current JIT field access statistics.
    /// </summary>
    /// <returns>Performance statistics, or default if native support unavailable.</returns>
    public static VayronFieldAccessStats GetStatistics()
    {
        try
        {
            var statsPtr = NativeGetStats();
            if (statsPtr == IntPtr.Zero)
                return default;

            return Marshal.PtrToStructure<VayronFieldAccessStats>(statsPtr);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Resets the JIT field access statistics.
    /// </summary>
    public static void ResetStatistics()
    {
        try
        {
            NativeResetStats();
        }
        catch
        {
            // Native runtime not available
        }
    }

    /// <summary>
    /// Gets whether native JIT support is available.
    /// </summary>
    public static bool IsNativeSupported
    {
        get
        {
            try
            {
                _ = NativeGetStats();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Updates the cached body information for a handle in the native cache.
    /// </summary>
    /// <remarks>
    /// This should be called after materialization to update the native cache
    /// for fast subsequent access.
    /// </remarks>
    public static void UpdateCachedBodyInfo(VayronHandle handle, IntPtr bodyPtr, int bodySize, long epoch)
    {
        if (handle == null)
            return;

        try
        {
            TypedReference tr = __makeref(handle);
            IntPtr objPtr = **(IntPtr**)&tr;
            NativeUpdateCachedBodyInfo(objPtr, bodyPtr, bodySize, epoch);
        }
        catch
        {
            // Native runtime not available
        }
    }

    /// <summary>
    /// Marks a handle as dirty in the native cache.
    /// </summary>
    public static void MarkDirty(VayronHandle handle)
    {
        if (handle == null)
            return;

        try
        {
            TypedReference tr = __makeref(handle);
            IntPtr objPtr = **(IntPtr**)&tr;
            NativeMarkDirty(objPtr);
        }
        catch
        {
            // Native runtime not available
        }
    }

    // Native callback for materialization
    // Called from native JIT helper when body needs to be materialized
    private static IntPtr MaterializeCallback(IntPtr objHandle)
    {
        try
        {
            // Convert raw pointer back to managed object
            // This is unsafe but necessary for native interop
            if (objHandle == IntPtr.Zero)
                return IntPtr.Zero;

            // Get the object from the handle
            var handle = GetObjectFromPointer(objHandle) as VayronHandle;
            if (handle == null)
                return IntPtr.Zero;

            // Get metadata from side table
            var meta = VayronMetaTable.Get(handle);
            if (meta == null)
                return IntPtr.Zero;

            // Return cached body pointer if available and pinned
            if (meta.CachedBodyPtr != IntPtr.Zero && meta.IsPinned)
            {
                return meta.CachedBodyPtr;
            }

            // Need to materialize - this requires an active transaction
            if (!VayronTransaction.HasActiveTransaction)
                return IntPtr.Zero;

            // Trigger materialization through normal path
            // The handle's EnsureMaterialized will be called
            // For now, return the cached pointer if available
            return meta.CachedBodyPtr;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    // Helper to convert raw object pointer to managed object
    // This is a well-known pattern but is inherently unsafe
    private static object? GetObjectFromPointer(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;

        // Create a typed reference from the pointer
        // This is extremely unsafe but necessary for native interop
        return Unsafe.Read<object?>(&ptr);
    }

    // Native method declarations (FCalls when using DOTNExT runtime)
    // These are placeholders that will be replaced by actual FCalls

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeRegisterMaterializeCallback(IntPtr callback)
    {
        // In DOTNExT runtime, this would be an FCall to VayronJitNative::RegisterMaterializeCallback
        // For standard .NET runtime, this is a no-op
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IntPtr NativeGetStats()
    {
        // In DOTNExT runtime, this would be an FCall to VayronJitNative::GetStats
        // For standard .NET runtime, return IntPtr.Zero
        return IntPtr.Zero;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeResetStats()
    {
        // In DOTNExT runtime, this would be an FCall to VayronJitNative::ResetStats
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeUpdateCachedBodyInfo(IntPtr obj, IntPtr bodyPtr, int bodySize, long epoch)
    {
        // In DOTNExT runtime, this would be an FCall to VayronJitNative::UpdateCachedBodyInfo
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeMarkDirty(IntPtr obj)
    {
        // In DOTNExT runtime, this would be an FCall to VayronJitNative::MarkDirty
    }
}

/// <summary>
/// Optimized field accessor that uses JIT interception when available.
/// </summary>
/// <remarks>
/// This class provides optimized field access methods that take advantage
/// of JIT helper interception when running on DOTNExT runtime. On standard
/// .NET runtime, it falls back to managed materialization.
/// </remarks>
public static unsafe class VayronFieldAccessor
{
    /// <summary>
    /// Gets a field value using the most efficient path available.
    /// </summary>
    /// <typeparam name="T">The unmanaged field type.</typeparam>
    /// <param name="handle">The VAYRON handle.</param>
    /// <param name="offset">The field offset within the body.</param>
    /// <returns>The field value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetField<T>(VayronHandle handle, int offset) where T : unmanaged
    {
        // Try fast path: pinned body pointer
        var meta = VayronMetaTable.Get(handle);
        if (meta != null && meta.CachedBodyPtr != IntPtr.Zero && meta.IsPinned)
        {
            return *(T*)((byte*)meta.CachedBodyPtr + VayronHandle.BodyHeader.Size + offset);
        }

        // Fall back to managed path
        return handle.GetFieldInternal<T>(offset);
    }

    /// <summary>
    /// Sets a field value using the most efficient path available.
    /// </summary>
    /// <typeparam name="T">The unmanaged field type.</typeparam>
    /// <param name="handle">The VAYRON handle.</param>
    /// <param name="offset">The field offset within the body.</param>
    /// <param name="value">The value to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetField<T>(VayronHandle handle, int offset, T value) where T : unmanaged
    {
        // Try fast path: pinned body pointer
        var meta = VayronMetaTable.Get(handle);
        if (meta != null && meta.CachedBodyPtr != IntPtr.Zero && meta.IsPinned)
        {
            *(T*)((byte*)meta.CachedBodyPtr + VayronHandle.BodyHeader.Size + offset) = value;
            VayronJitInterop.MarkDirty(handle);
            return;
        }

        // Fall back to managed path
        handle.SetFieldInternal(offset, value);
    }
}

/// <summary>
/// Extension methods for VayronHandle to support JIT-optimized field access.
/// </summary>
internal static class VayronHandleJitExtensions
{
    /// <summary>
    /// Internal method for getting field value (used by VayronFieldAccessor).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T GetFieldInternal<T>(this VayronHandle handle, int offset) where T : unmanaged
    {
        // This calls the protected GetField method through reflection or internal access
        // For now, this is a placeholder - the actual implementation uses the protected method
        throw new NotImplementedException("Use the protected GetField<T> method in derived classes");
    }

    /// <summary>
    /// Internal method for setting field value (used by VayronFieldAccessor).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetFieldInternal<T>(this VayronHandle handle, int offset, T value) where T : unmanaged
    {
        // This calls the protected SetField method through reflection or internal access
        // For now, this is a placeholder - the actual implementation uses the protected method
        throw new NotImplementedException("Use the protected SetField<T> method in derived classes");
    }
}
