namespace Scynapse.Security.Crypto;

/// <summary>
/// RFC 4648 Base32 encoding/decoding (no padding variant).
/// Used for human-readable key encoding.
/// </summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static readonly byte[] DecodeMap = CreateDecodeMap();

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return string.Empty;

        int outputLen = (data.Length * 8 + 4) / 5;
        Span<char> result = outputLen <= 128 ? stackalloc char[outputLen] : new char[outputLen];

        int bitBuffer = 0;
        int bitsLeft = 0;
        int pos = 0;

        foreach (byte b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result[pos++] = Alphabet[(bitBuffer >> bitsLeft) & 0x1F];
            }
        }

        if (bitsLeft > 0)
        {
            result[pos++] = Alphabet[(bitBuffer << (5 - bitsLeft)) & 0x1F];
        }

        return new string(result[..pos]);
    }

    public static byte[] Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        // Strip padding if present
        var input = encoded.AsSpan().TrimEnd('=');
        if (input.IsEmpty) return [];

        int outputLen = input.Length * 5 / 8;
        var result = new byte[outputLen];

        int bitBuffer = 0;
        int bitsLeft = 0;
        int pos = 0;

        foreach (char c in input)
        {
            byte val = CharToValue(c);
            bitBuffer = (bitBuffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result[pos++] = (byte)(bitBuffer >> bitsLeft);
            }
        }

        return result[..pos];
    }

    private static byte CharToValue(char c)
    {
        int idx = c switch
        {
            >= 'A' and <= 'Z' => c - 'A',
            >= 'a' and <= 'z' => c - 'a',    // case-insensitive
            >= '2' and <= '7' => c - '2' + 26,
            _ => -1
        };

        if (idx < 0)
            throw new FormatException($"Invalid Base32 character: '{c}'");

        return (byte)idx;
    }

    private static byte[] CreateDecodeMap()
    {
        var map = new byte[128];
        Array.Fill(map, (byte)0xFF);
        for (int i = 0; i < Alphabet.Length; i++)
        {
            map[Alphabet[i]] = (byte)i;
            if (char.IsLetter(Alphabet[i]))
                map[char.ToLowerInvariant(Alphabet[i])] = (byte)i;
        }
        return map;
    }
}
