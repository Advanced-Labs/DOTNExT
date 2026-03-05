using Scynapse.Security.Crypto;
using Xunit;

namespace Scynapse.Security.Tests;

public class ScynapseKeyPairTests
{
    [Fact]
    public void Generate_CreatesValidKeyPair()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        Assert.True(kp.CanSign);
        Assert.Equal(ScynapseKeyType.Node, kp.KeyType);
        Assert.Equal(32, kp.PublicKeyBytes.Length);
    }

    [Fact]
    public void Generate_DifferentCalls_ProduceDifferentKeys()
    {
        using var kp1 = ScynapseKeyPair.Generate();
        using var kp2 = ScynapseKeyPair.Generate();

        Assert.False(kp1.PublicKeyBytes.SequenceEqual(kp2.PublicKeyBytes));
    }

    [Fact]
    public void FromSeed_Deterministic_SameKeyFromSameSeed()
    {
        var seed = new byte[32];
        seed[0] = 42;

        using var kp1 = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.User);
        using var kp2 = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.User);

        Assert.True(kp1.PublicKeyBytes.SequenceEqual(kp2.PublicKeyBytes));
    }

    [Fact]
    public void FromSeed_InvalidLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScynapseKeyPair.FromSeed(new byte[16]));
    }

    [Fact]
    public void FromPublicKey_CreatesVerifyOnlyKeyPair()
    {
        using var full = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var pubBytes = full.PublicKeyBytes.ToArray();

        using var verifyOnly = ScynapseKeyPair.FromPublicKey(pubBytes, ScynapseKeyType.Organization);

        Assert.False(verifyOnly.CanSign);
        Assert.Equal(ScynapseKeyType.Organization, verifyOnly.KeyType);
        Assert.True(verifyOnly.PublicKeyBytes.SequenceEqual(pubBytes));
    }

    [Fact]
    public void FromPublicKey_CannotSign_Throws()
    {
        using var full = ScynapseKeyPair.Generate();
        using var verifyOnly = ScynapseKeyPair.FromPublicKey(full.PublicKeyBytes.ToArray());

        Assert.Throws<InvalidOperationException>(() => verifyOnly.Sign(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void FromPublicKey_CannotExportSeed_Throws()
    {
        using var full = ScynapseKeyPair.Generate();
        using var verifyOnly = ScynapseKeyPair.FromPublicKey(full.PublicKeyBytes.ToArray());

        Assert.Throws<InvalidOperationException>(() => verifyOnly.ExportSeed());
    }

    [Fact]
    public void Sign_And_Verify_Roundtrip()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.User);
        var data = "hello scynapse"u8;

        var signature = kp.Sign(data);

        Assert.Equal(64, signature.Length);
        Assert.True(kp.Verify(data, signature));
    }

    [Fact]
    public void Verify_TamperedData_Fails()
    {
        using var kp = ScynapseKeyPair.Generate();
        var data = "original"u8;
        var signature = kp.Sign(data);

        var tampered = "tampered"u8;
        Assert.False(kp.Verify(tampered, signature));
    }

    [Fact]
    public void Verify_WrongKey_Fails()
    {
        using var kp1 = ScynapseKeyPair.Generate();
        using var kp2 = ScynapseKeyPair.Generate();
        var data = "test"u8;

        var signature = kp1.Sign(data);
        Assert.False(kp2.Verify(data, signature));
    }

    [Fact]
    public void Verify_WithVerifyOnlyKeyPair_Works()
    {
        using var full = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var data = "verify me"u8;
        var signature = full.Sign(data);

        using var verifyOnly = ScynapseKeyPair.FromPublicKey(full.PublicKeyBytes.ToArray(), ScynapseKeyType.Node);
        Assert.True(verifyOnly.Verify(data, signature));
    }

    [Fact]
    public void ExportSeed_Roundtrip()
    {
        using var original = ScynapseKeyPair.Generate(ScynapseKeyType.ComponentType);
        var seed = original.ExportSeed();

        using var restored = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.ComponentType);
        Assert.True(original.PublicKeyBytes.SequenceEqual(restored.PublicKeyBytes));
    }

    [Fact]
    public void Dispose_PreventsSubsequentOperations()
    {
        var kp = ScynapseKeyPair.Generate();
        kp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = kp.PublicKeyBytes);
        Assert.Throws<ObjectDisposedException>(() => kp.Sign(new byte[] { 1 }));
        Assert.Throws<ObjectDisposedException>(() => kp.Verify(new byte[] { 1 }, new byte[64]));
    }

    [Theory]
    [InlineData(ScynapseKeyType.Organization)]
    [InlineData(ScynapseKeyType.Domain)]
    [InlineData(ScynapseKeyType.Node)]
    [InlineData(ScynapseKeyType.ComponentType)]
    [InlineData(ScynapseKeyType.Instance)]
    [InlineData(ScynapseKeyType.User)]
    public void AllKeyTypes_GenerateAndSign(ScynapseKeyType keyType)
    {
        using var kp = ScynapseKeyPair.Generate(keyType);
        var data = "type test"u8;
        var sig = kp.Sign(data);

        Assert.Equal(keyType, kp.KeyType);
        Assert.True(kp.Verify(data, sig));
    }
}
