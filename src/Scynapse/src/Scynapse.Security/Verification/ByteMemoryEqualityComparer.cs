namespace Scynapse.Security.Verification;

/// <summary>
/// Structural equality comparer for ReadOnlyMemory&lt;byte&gt;.
/// Required because ReadOnlyMemory uses reference equality by default.
/// </summary>
public sealed class ByteMemoryEqualityComparer : IEqualityComparer<ReadOnlyMemory<byte>>
{
    public static readonly ByteMemoryEqualityComparer Instance = new();

    public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        => x.Span.SequenceEqual(y.Span);

    public int GetHashCode(ReadOnlyMemory<byte> obj)
    {
        var span = obj.Span;
        // FNV-1a over first 32 bytes (sufficient for crypto hashes and public keys)
        uint hash = 2166136261;
        int len = Math.Min(span.Length, 32);
        for (int i = 0; i < len; i++)
        {
            hash ^= span[i];
            hash *= 16777619;
        }
        return (int)hash;
    }
}
