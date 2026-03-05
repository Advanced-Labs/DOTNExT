using System.Buffers.Binary;

namespace Scynapse.Security.Crypto;

/// <summary>
/// Encodes and decodes Scynapse keys as human-readable strings with typed prefixes.
/// Format: Base32(prefix_byte | raw_key_bytes | crc16_le)
/// Inspired by NATS NKeys encoding (Base32 + CRC16 + prefix byte).
/// </summary>
public static class ScynapseKeyEncoding
{
    // Maps ScynapseKeyType to the prefix byte embedded in encoded strings.
    // Public key prefixes use the uppercase letter's shifted value.
    // These are arbitrary but chosen to produce recognizable prefix characters in Base32.
    private static ReadOnlySpan<byte> PublicKeyPrefixes => [
        0 << 3,   // Organization  → 'O'-like
        3 << 3,   // Domain        → 'D'-like
        13 << 3,  // Node          → 'N'-like
        19 << 3,  // ComponentType → 'T'-like
        8 << 3,   // Instance      → 'I'-like
        20 << 3,  // User          → 'U'-like
        23 << 3,  // Encryption    → 'X'-like
        15 << 3,  // Seed          → 'P'-like
    ];

    // Seed prefix: 18 << 3 = 144, giving 'S'-like first character.
    private const byte SeedPrefixBase = 18 << 3;

    /// <summary>
    /// Encode a public key with a typed prefix.
    /// </summary>
    public static string EncodePublicKey(ScynapseKeyType keyType, ReadOnlySpan<byte> publicKey)
    {
        if (publicKey.Length != 32)
            throw new ArgumentException("Ed25519 public key must be exactly 32 bytes.", nameof(publicKey));

        byte prefix = PublicKeyPrefixes[(int)keyType];
        return EncodeWithPrefix(prefix, publicKey);
    }

    /// <summary>
    /// Encode a seed (private key material) with a two-byte prefix indicating both
    /// "this is a seed" and "for what key type."
    /// </summary>
    public static string EncodeSeed(ScynapseKeyType keyType, ReadOnlySpan<byte> seed)
    {
        if (seed.Length != 32)
            throw new ArgumentException("Ed25519 seed must be exactly 32 bytes.", nameof(seed));

        // Two-byte prefix: first byte = seed marker, second byte = key type marker.
        // This lets us decode: "this is a seed for a Node key" vs "this is a seed for a User key."
        byte prefix1 = SeedPrefixBase;
        byte prefix2 = PublicKeyPrefixes[(int)keyType];
        return EncodeWithTwoBytePrefix(prefix1, prefix2, seed);
    }

    /// <summary>
    /// Decode an encoded public key string. Returns the key type and raw 32-byte public key.
    /// </summary>
    public static (ScynapseKeyType KeyType, byte[] PublicKey) DecodePublicKey(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var raw = Base32.Decode(encoded);
        if (raw.Length != 1 + 32 + 2) // prefix + key + crc16
            throw new FormatException($"Invalid encoded public key length: expected {1 + 32 + 2} bytes, got {raw.Length}.");

        byte prefix = raw[0];
        var keyBytes = raw.AsSpan(1, 32);
        var crcBytes = raw.AsSpan(33, 2);

        ushort expectedCrc = Crc16.Compute(raw.AsSpan(0, 33));
        ushort actualCrc = BinaryPrimitives.ReadUInt16LittleEndian(crcBytes);
        if (expectedCrc != actualCrc)
            throw new FormatException("CRC checksum mismatch — encoded key is corrupted.");

        ScynapseKeyType keyType = ResolvePublicKeyPrefix(prefix);
        return (keyType, keyBytes.ToArray());
    }

    /// <summary>
    /// Decode an encoded seed string. Returns the key type and raw 32-byte seed.
    /// </summary>
    public static (ScynapseKeyType KeyType, byte[] Seed) DecodeSeed(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var raw = Base32.Decode(encoded);
        if (raw.Length != 2 + 32 + 2) // two-byte prefix + seed + crc16
            throw new FormatException($"Invalid encoded seed length: expected {2 + 32 + 2} bytes, got {raw.Length}.");

        byte prefix1 = raw[0];
        byte prefix2 = raw[1];
        var seedBytes = raw.AsSpan(2, 32);
        var crcBytes = raw.AsSpan(34, 2);

        if (prefix1 != SeedPrefixBase)
            throw new FormatException("Not a seed-encoded string (missing seed prefix marker).");

        ushort expectedCrc = Crc16.Compute(raw.AsSpan(0, 34));
        ushort actualCrc = BinaryPrimitives.ReadUInt16LittleEndian(crcBytes);
        if (expectedCrc != actualCrc)
            throw new FormatException("CRC checksum mismatch — encoded seed is corrupted.");

        ScynapseKeyType keyType = ResolvePublicKeyPrefix(prefix2);
        return (keyType, seedBytes.ToArray());
    }

    /// <summary>
    /// Determines whether the encoded string represents a seed (vs a public key).
    /// </summary>
    public static bool IsSeed(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        try
        {
            var raw = Base32.Decode(encoded);
            return raw.Length == 2 + 32 + 2 && raw[0] == SeedPrefixBase;
        }
        catch
        {
            return false;
        }
    }

    private static string EncodeWithPrefix(byte prefix, ReadOnlySpan<byte> data)
    {
        // Layout: [prefix(1)] [data(32)] [crc16_le(2)] = 35 bytes
        Span<byte> buf = stackalloc byte[1 + data.Length + 2];
        buf[0] = prefix;
        data.CopyTo(buf[1..]);

        ushort crc = Crc16.Compute(buf[..(1 + data.Length)]);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[(1 + data.Length)..], crc);

        return Base32.Encode(buf);
    }

    private static string EncodeWithTwoBytePrefix(byte prefix1, byte prefix2, ReadOnlySpan<byte> data)
    {
        // Layout: [prefix1(1)] [prefix2(1)] [data(32)] [crc16_le(2)] = 36 bytes
        Span<byte> buf = stackalloc byte[2 + data.Length + 2];
        buf[0] = prefix1;
        buf[1] = prefix2;
        data.CopyTo(buf[2..]);

        ushort crc = Crc16.Compute(buf[..(2 + data.Length)]);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[(2 + data.Length)..], crc);

        return Base32.Encode(buf);
    }

    private static ScynapseKeyType ResolvePublicKeyPrefix(byte prefix)
    {
        var prefixes = PublicKeyPrefixes;
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (prefixes[i] == prefix)
                return (ScynapseKeyType)i;
        }
        throw new FormatException($"Unknown key type prefix: 0x{prefix:X2}");
    }
}
