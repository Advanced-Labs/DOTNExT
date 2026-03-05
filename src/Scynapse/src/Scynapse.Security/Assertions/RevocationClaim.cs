using System.Buffers.Binary;
using System.Text;

namespace Scynapse.Security.Assertions;

/// <summary>
/// "Target assertion is revoked."
/// Serializable claim payload for Revocation assertions.
/// The target is the content hash of the assertion being revoked.
/// </summary>
public sealed record RevocationClaim(
    byte[] Target,
    string? Reason = null)
{
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        CapabilityClaim.WriteBytes(ms, Target);
        WriteOptionalString(ms, Reason);
        return ms.ToArray();
    }

    public static RevocationClaim Deserialize(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        var target = CapabilityClaim.ReadBytes(data, ref offset);
        var reason = ReadOptionalString(data, ref offset);
        return new RevocationClaim(target, reason);
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
