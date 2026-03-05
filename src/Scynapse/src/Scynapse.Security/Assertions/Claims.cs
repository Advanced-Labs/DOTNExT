using System.Buffers.Binary;
using System.Text;

namespace Scynapse.Security.Assertions;

/// <summary>
/// "Subject may perform action on resource."
/// Serializable claim payload for Capability assertions.
/// </summary>
public sealed record CapabilityClaim(
    string Resource,
    string Action,
    IReadOnlyDictionary<string, byte[]>? Constraints = null)
{
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        WriteString(ms, Resource);
        WriteString(ms, Action);
        WriteSortedMap(ms, Constraints);
        return ms.ToArray();
    }

    public static CapabilityClaim Deserialize(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        var resource = ReadString(data, ref offset);
        var action = ReadString(data, ref offset);
        var constraints = ReadMap(data, ref offset);
        return new CapabilityClaim(resource, action, constraints);
    }

    internal static void WriteString(Stream s, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, bytes.Length);
        s.Write(lenBuf);
        s.Write(bytes);
    }

    internal static void WriteBytes(Stream s, ReadOnlySpan<byte> value)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, value.Length);
        s.Write(lenBuf);
        s.Write(value);
    }

    internal static void WriteSortedMap(Stream s, IReadOnlyDictionary<string, byte[]>? map)
    {
        if (map == null || map.Count == 0)
        {
            Span<byte> zero = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(zero, 0);
            s.Write(zero);
            return;
        }

        var sorted = map.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        Span<byte> countBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(countBuf, sorted.Count);
        s.Write(countBuf);

        foreach (var (key, value) in sorted)
        {
            WriteString(s, key);
            WriteBytes(s, value);
        }
    }

    internal static string ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
        offset += 4;
        var str = Encoding.UTF8.GetString(data.Slice(offset, len));
        offset += len;
        return str;
    }

    internal static byte[] ReadBytes(ReadOnlySpan<byte> data, ref int offset)
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
        offset += 4;
        var bytes = data.Slice(offset, len).ToArray();
        offset += len;
        return bytes;
    }

    internal static Dictionary<string, byte[]>? ReadMap(ReadOnlySpan<byte> data, ref int offset)
    {
        int count = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
        offset += 4;
        if (count == 0) return null;

        var map = new Dictionary<string, byte[]>(count);
        for (int i = 0; i < count; i++)
        {
            var key = ReadString(data, ref offset);
            var value = ReadBytes(data, ref offset);
            map[key] = value;
        }
        return map;
    }
}

/// <summary>
/// "Subject may issue assertions within scope."
/// Serializable claim payload for Delegation assertions.
/// </summary>
public sealed record DelegationClaim(
    ClaimType[] AllowedClaimTypes,
    string? ResourcePattern = null,
    string? ActionPattern = null,
    byte? MaxDepth = null)
{
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();

        // Allowed claim types
        Span<byte> countBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(countBuf, AllowedClaimTypes.Length);
        ms.Write(countBuf);
        foreach (var ct in AllowedClaimTypes)
            ms.WriteByte((byte)ct);

        // Optional strings
        WriteOptionalString(ms, ResourcePattern);
        WriteOptionalString(ms, ActionPattern);

        // Optional max depth
        ms.WriteByte(MaxDepth.HasValue ? (byte)1 : (byte)0);
        if (MaxDepth.HasValue)
            ms.WriteByte(MaxDepth.Value);

        return ms.ToArray();
    }

    public static DelegationClaim Deserialize(ReadOnlySpan<byte> data)
    {
        int offset = 0;

        int ctCount = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
        offset += 4;
        var claimTypes = new ClaimType[ctCount];
        for (int i = 0; i < ctCount; i++)
            claimTypes[i] = (ClaimType)data[offset++];

        var resourcePattern = ReadOptionalString(data, ref offset);
        var actionPattern = ReadOptionalString(data, ref offset);

        byte? maxDepth = null;
        if (data[offset++] == 1)
            maxDepth = data[offset++];

        return new DelegationClaim(claimTypes, resourcePattern, actionPattern, maxDepth);
    }

    private static void WriteOptionalString(Stream s, string? value)
    {
        if (value == null)
        {
            s.WriteByte(0);
            return;
        }
        s.WriteByte(1);
        CapabilityClaim.WriteString(s, value);
    }

    private static string? ReadOptionalString(ReadOnlySpan<byte> data, ref int offset)
    {
        if (data[offset++] == 0) return null;
        return CapabilityClaim.ReadString(data, ref offset);
    }
}

/// <summary>
/// "Issuer recognizes subject in context." Directed relationship.
/// Serializable claim payload for Relation assertions.
/// </summary>
public sealed record RelationClaim(
    string Context,
    IReadOnlyDictionary<string, byte[]>? Metadata = null)
{
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        CapabilityClaim.WriteString(ms, Context);
        CapabilityClaim.WriteSortedMap(ms, Metadata);
        return ms.ToArray();
    }

    public static RelationClaim Deserialize(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        var context = CapabilityClaim.ReadString(data, ref offset);
        var metadata = CapabilityClaim.ReadMap(data, ref offset);
        return new RelationClaim(context, metadata);
    }
}
