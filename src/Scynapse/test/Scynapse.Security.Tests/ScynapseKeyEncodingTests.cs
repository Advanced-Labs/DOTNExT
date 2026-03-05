using Scynapse.Security.Crypto;
using Xunit;

namespace Scynapse.Security.Tests;

public class ScynapseKeyEncodingTests
{
    [Theory]
    [InlineData(ScynapseKeyType.Organization)]
    [InlineData(ScynapseKeyType.Domain)]
    [InlineData(ScynapseKeyType.Node)]
    [InlineData(ScynapseKeyType.ComponentType)]
    [InlineData(ScynapseKeyType.Instance)]
    [InlineData(ScynapseKeyType.User)]
    public void EncodeDecodePublicKey_Roundtrip(ScynapseKeyType keyType)
    {
        using var kp = ScynapseKeyPair.Generate(keyType);
        var pubBytes = kp.PublicKeyBytes.ToArray();

        var encoded = ScynapseKeyEncoding.EncodePublicKey(keyType, pubBytes);
        var (decodedType, decodedKey) = ScynapseKeyEncoding.DecodePublicKey(encoded);

        Assert.Equal(keyType, decodedType);
        Assert.Equal(pubBytes, decodedKey);
    }

    [Theory]
    [InlineData(ScynapseKeyType.Organization)]
    [InlineData(ScynapseKeyType.Node)]
    [InlineData(ScynapseKeyType.User)]
    [InlineData(ScynapseKeyType.ComponentType)]
    public void EncodeDecodeSeed_Roundtrip(ScynapseKeyType keyType)
    {
        using var kp = ScynapseKeyPair.Generate(keyType);
        var seed = kp.ExportSeed();

        var encoded = ScynapseKeyEncoding.EncodeSeed(keyType, seed);
        var (decodedType, decodedSeed) = ScynapseKeyEncoding.DecodeSeed(encoded);

        Assert.Equal(keyType, decodedType);
        Assert.Equal(seed, decodedSeed);
    }

    [Fact]
    public void IsSeed_IdentifiesSeedStrings()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var seedEncoded = ScynapseKeyEncoding.EncodeSeed(ScynapseKeyType.Node, kp.ExportSeed());
        var pubEncoded = ScynapseKeyEncoding.EncodePublicKey(ScynapseKeyType.Node, kp.PublicKeyBytes);

        Assert.True(ScynapseKeyEncoding.IsSeed(seedEncoded));
        Assert.False(ScynapseKeyEncoding.IsSeed(pubEncoded));
    }

    [Fact]
    public void DifferentKeyTypes_ProduceDifferentPrefixes()
    {
        var key = new byte[32];
        key[0] = 1;

        var nodeEncoded = ScynapseKeyEncoding.EncodePublicKey(ScynapseKeyType.Node, key);
        var userEncoded = ScynapseKeyEncoding.EncodePublicKey(ScynapseKeyType.User, key);

        Assert.NotEqual(nodeEncoded, userEncoded);
        // First character should differ (different prefix bytes)
        Assert.NotEqual(nodeEncoded[0], userEncoded[0]);
    }

    [Fact]
    public void CorruptedChecksum_Throws()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var encoded = ScynapseKeyEncoding.EncodePublicKey(ScynapseKeyType.Node, kp.PublicKeyBytes);

        // Corrupt a character in the middle
        var chars = encoded.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';
        var corrupted = new string(chars);

        Assert.ThrowsAny<Exception>(() => ScynapseKeyEncoding.DecodePublicKey(corrupted));
    }

    [Fact]
    public void InvalidLength_PublicKey_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => ScynapseKeyEncoding.EncodePublicKey(ScynapseKeyType.Node, new byte[16]));
    }

    [Fact]
    public void InvalidLength_Seed_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => ScynapseKeyEncoding.EncodeSeed(ScynapseKeyType.Node, new byte[16]));
    }

    [Fact]
    public void ScynapseKeyPair_EncodedRoundtrip_PublicKey()
    {
        using var original = ScynapseKeyPair.Generate(ScynapseKeyType.User);
        var encoded = original.ToEncodedPublicKey();

        using var restored = ScynapseKeyPair.FromEncodedPublicKey(encoded);

        Assert.Equal(ScynapseKeyType.User, restored.KeyType);
        Assert.False(restored.CanSign);
        Assert.True(original.PublicKeyBytes.SequenceEqual(restored.PublicKeyBytes));
    }

    [Fact]
    public void ScynapseKeyPair_EncodedRoundtrip_Seed()
    {
        using var original = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var encoded = original.ToEncodedSeed();

        using var restored = ScynapseKeyPair.FromEncodedSeed(encoded);

        Assert.Equal(ScynapseKeyType.Organization, restored.KeyType);
        Assert.True(restored.CanSign);
        Assert.True(original.PublicKeyBytes.SequenceEqual(restored.PublicKeyBytes));

        // Signing with restored key should verify with original
        var data = "cross-verify"u8;
        var sig = restored.Sign(data);
        Assert.True(original.Verify(data, sig));
    }

    [Fact]
    public void VerifyOnly_CannotEncodeSeed_Throws()
    {
        using var full = ScynapseKeyPair.Generate();
        using var verifyOnly = ScynapseKeyPair.FromPublicKey(full.PublicKeyBytes.ToArray());

        Assert.Throws<InvalidOperationException>(() => verifyOnly.ToEncodedSeed());
    }

    [Fact]
    public void EncodedStrings_AreBase32_NoSpecialChars()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var pubEncoded = kp.ToEncodedPublicKey();
        var seedEncoded = kp.ToEncodedSeed();

        // Base32 RFC4648 alphabet: A-Z, 2-7, optional =
        Assert.Matches("^[A-Z2-7=]+$", pubEncoded);
        Assert.Matches("^[A-Z2-7=]+$", seedEncoded);
    }
}
