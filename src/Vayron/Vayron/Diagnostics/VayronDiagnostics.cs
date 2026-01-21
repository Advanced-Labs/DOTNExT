// VAYRON - Runtime-Integrated Persistent Storage
// Diagnostics support for debugging and SOS integration
//
// DOTNExT VAYRON Phase 2 Implementation

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Vayron.Diagnostics;

/// <summary>
/// Provides diagnostic and debugging support for VAYRON handles.
/// </summary>
/// <remarks>
/// This class provides methods that can be called by:
/// <list type="bullet">
/// <item><description>SOS debugger extensions</description></item>
/// <item><description>Visual Studio diagnostics</description></item>
/// <item><description>Custom debugging tools</description></item>
/// <item><description>Application-level diagnostics</description></item>
/// </list>
///
/// <para><b>Thread Safety:</b> All methods are thread-safe and can be called concurrently.</para>
/// </remarks>
public static class VayronDiagnostics
{
    /// <summary>
    /// The VAYRON handle bit constant.
    /// </summary>
    public const uint VAYRON_BIT = 0x80000000;

    /// <summary>
    /// Checks if an object is a VAYRON handle by examining its object header.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object has the VAYRON bit set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVayronHandle(object? obj)
    {
        return VayronRuntime.IsVayronHandle(obj);
    }

    /// <summary>
    /// Gets comprehensive diagnostic information about a VAYRON handle.
    /// </summary>
    /// <param name="handle">The VAYRON handle to inspect.</param>
    /// <returns>Diagnostic information, or null if not a valid handle.</returns>
    public static VayronHandleDiagInfo? GetHandleDiagnostics(object? handle)
    {
        if (handle is not VayronHandle vayronHandle)
            return null;

        if (!IsVayronHandle(handle))
            return null;

        return new VayronHandleDiagInfo
        {
            Address = GetObjectAddress(handle),
            TypeName = handle.GetType().FullName ?? handle.GetType().Name,
            Oid = vayronHandle.Oid.Value,
            IsDirty = vayronHandle.IsDirty,
            IsMaterialized = vayronHandle.IsMaterialized,
            HeaderInfo = VayronRuntime.GetHeaderInfo(handle)
        };
    }

    /// <summary>
    /// Gets the approximate memory address of an object (for diagnostic display).
    /// </summary>
    /// <remarks>
    /// Note: This address may change during GC. Use only for diagnostic display.
    /// </remarks>
    public static unsafe nint GetObjectAddress(object obj)
    {
        TypedReference tr = __makeref(obj);
        return **(nint**)&tr;
    }

    /// <summary>
    /// Gets detailed object header information for any object.
    /// </summary>
    /// <param name="obj">The object to inspect.</param>
    /// <returns>Header diagnostic information.</returns>
    public static ObjectHeaderDiagInfo GetObjectHeaderInfo(object obj)
    {
        var headerInfo = VayronRuntime.GetHeaderInfo(obj);
        return new ObjectHeaderDiagInfo
        {
            Address = GetObjectAddress(obj),
            TypeName = obj.GetType().FullName ?? obj.GetType().Name,
            RawSyncBlockValue = headerInfo.RawValue,
            IsVayronHandle = headerInfo.IsVayronHandle,
            HasFinalizerRun = headerInfo.HasFinalizerRun,
            HasGCReserve = headerInfo.HasGCReserve,
            HasSpinLock = headerInfo.HasSpinLock,
            HasHashOrSyncBlockIndex = headerInfo.HasHashOrSyncBlockIndex,
            HasHashCode = headerInfo.HasHashCode,
            SyncBlockIndex = headerInfo.SyncBlockIndex,
            HashCode = headerInfo.HashCode
        };
    }

    /// <summary>
    /// Dumps diagnostic information about a VAYRON handle to the debug output.
    /// </summary>
    [Conditional("DEBUG")]
    public static void DumpHandle(VayronHandle handle, string? label = null)
    {
        var info = GetHandleDiagnostics(handle);
        if (info == null)
        {
            Debug.WriteLine($"[VAYRON] {label ?? "Handle"}: Not a valid VAYRON handle");
            return;
        }

        Debug.WriteLine($"[VAYRON] {label ?? "Handle"} Diagnostic Dump:");
        Debug.WriteLine($"  Address:       0x{info.Address:X}");
        Debug.WriteLine($"  Type:          {info.TypeName}");
        Debug.WriteLine($"  OID:           {info.Oid}");
        Debug.WriteLine($"  IsDirty:       {info.IsDirty}");
        Debug.WriteLine($"  IsMaterialized: {info.IsMaterialized}");
        Debug.WriteLine($"  HeaderRaw:     0x{info.HeaderInfo.RawValue:X8}");
        Debug.WriteLine($"  VayronBit:     {info.HeaderInfo.IsVayronHandle}");
    }

    /// <summary>
    /// Dumps all bit flags in an object header.
    /// </summary>
    [Conditional("DEBUG")]
    public static void DumpObjectHeader(object obj, string? label = null)
    {
        var info = GetObjectHeaderInfo(obj);

        Debug.WriteLine($"[VAYRON] {label ?? "Object"} Header Dump:");
        Debug.WriteLine($"  Address:       0x{info.Address:X}");
        Debug.WriteLine($"  Type:          {info.TypeName}");
        Debug.WriteLine($"  SyncBlock:     0x{info.RawSyncBlockValue:X8}");
        Debug.WriteLine($"  Bits:");
        Debug.WriteLine($"    [31] VAYRON:     {info.IsVayronHandle}");
        Debug.WriteLine($"    [30] FinRun:     {info.HasFinalizerRun}");
        Debug.WriteLine($"    [29] GCReserve:  {info.HasGCReserve}");
        Debug.WriteLine($"    [28] SpinLock:   {info.HasSpinLock}");
        Debug.WriteLine($"    [27] HashOrSBI:  {info.HasHashOrSyncBlockIndex}");
        Debug.WriteLine($"    [26] HashCode:   {info.HasHashCode}");
        if (info.HasHashOrSyncBlockIndex && !info.HasHashCode)
            Debug.WriteLine($"  SyncBlockIndex: {info.SyncBlockIndex}");
        if (info.HasHashCode)
            Debug.WriteLine($"  HashCodeValue:  {info.HashCode}");
    }
}

/// <summary>
/// Diagnostic information for a VAYRON handle.
/// </summary>
public class VayronHandleDiagInfo
{
    /// <summary>Object memory address (may change on GC).</summary>
    public nint Address { get; init; }

    /// <summary>Full type name of the handle.</summary>
    public required string TypeName { get; init; }

    /// <summary>Object Identifier.</summary>
    public long Oid { get; init; }

    /// <summary>Whether the handle has unsaved changes.</summary>
    public bool IsDirty { get; init; }

    /// <summary>Whether the body is currently loaded.</summary>
    public bool IsMaterialized { get; init; }

    /// <summary>Raw object header information.</summary>
    public VayronHeaderInfo HeaderInfo { get; init; }

    public override string ToString()
    {
        return $"VayronHandle[0x{Address:X}] Type={TypeName} OID={Oid} " +
               $"Dirty={IsDirty} Materialized={IsMaterialized}";
    }
}

/// <summary>
/// Diagnostic information for an object header.
/// </summary>
public class ObjectHeaderDiagInfo
{
    /// <summary>Object memory address (may change on GC).</summary>
    public nint Address { get; init; }

    /// <summary>Full type name of the object.</summary>
    public required string TypeName { get; init; }

    /// <summary>Raw 32-bit sync block value.</summary>
    public uint RawSyncBlockValue { get; init; }

    /// <summary>True if VAYRON handle bit (31) is set.</summary>
    public bool IsVayronHandle { get; init; }

    /// <summary>True if finalizer has run bit (30) is set.</summary>
    public bool HasFinalizerRun { get; init; }

    /// <summary>True if GC reserve bit (29) is set.</summary>
    public bool HasGCReserve { get; init; }

    /// <summary>True if spin lock bit (28) is set.</summary>
    public bool HasSpinLock { get; init; }

    /// <summary>True if hash/sync block index bit (27) is set.</summary>
    public bool HasHashOrSyncBlockIndex { get; init; }

    /// <summary>True if hash code bit (26) is set.</summary>
    public bool HasHashCode { get; init; }

    /// <summary>Sync block index if present.</summary>
    public int SyncBlockIndex { get; init; }

    /// <summary>Hash code if stored in header.</summary>
    public int HashCode { get; init; }

    public override string ToString()
    {
        return $"ObjHeader[0x{Address:X}] Type={TypeName} SyncBlock=0x{RawSyncBlockValue:X8} " +
               $"VAYRON={IsVayronHandle}";
    }
}
