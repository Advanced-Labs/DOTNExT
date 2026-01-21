// VAYRON - Runtime-Integrated Persistent Storage
// Object Identity (OID) structure

using System.Runtime.InteropServices;

namespace Vayron;

/// <summary>
/// Stable 64-bit Object Identifier for VAYRON persistent objects.
/// The OID uniquely identifies an object across process restarts.
/// </summary>
/// <remarks>
/// OID Layout (64 bits):
/// - Monotonically increasing sequence (similar to Voron's page numbers)
/// - Zero is reserved as invalid/null OID
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = sizeof(long))]
public readonly struct VayronOid : IEquatable<VayronOid>, IComparable<VayronOid>
{
    [FieldOffset(0)]
    private readonly long _value;

    /// <summary>
    /// Invalid/null OID constant.
    /// </summary>
    public static readonly VayronOid Invalid = new(0);

    public VayronOid(long value) => _value = value;

    /// <summary>
    /// Gets the raw 64-bit value of this OID.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Returns true if this OID is valid (non-zero).
    /// </summary>
    public bool IsValid => _value > 0;

    public static explicit operator long(VayronOid oid) => oid._value;
    public static explicit operator VayronOid(long value) => new(value);

    public bool Equals(VayronOid other) => _value == other._value;
    public override bool Equals(object? obj) => obj is VayronOid other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
    public int CompareTo(VayronOid other) => _value.CompareTo(other._value);

    public static bool operator ==(VayronOid left, VayronOid right) => left._value == right._value;
    public static bool operator !=(VayronOid left, VayronOid right) => left._value != right._value;
    public static bool operator <(VayronOid left, VayronOid right) => left._value < right._value;
    public static bool operator >(VayronOid left, VayronOid right) => left._value > right._value;
    public static bool operator <=(VayronOid left, VayronOid right) => left._value <= right._value;
    public static bool operator >=(VayronOid left, VayronOid right) => left._value >= right._value;

    public override string ToString() => $"OID({_value})";
}
