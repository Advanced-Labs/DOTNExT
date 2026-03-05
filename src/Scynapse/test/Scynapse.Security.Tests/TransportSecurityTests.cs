using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Tests;

public class ScynapseCertificateFactoryTests
{
    [Fact]
    public void CreateSelfSigned_ReturnsCertWithPrivateKey()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        using var cert = ScynapseCertificateFactory.CreateSelfSigned(kp);

        Assert.NotNull(cert);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void CreateSelfSigned_CertContainsEd25519Extension()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        using var cert = ScynapseCertificateFactory.CreateSelfSigned(kp);

        var ext = cert.Extensions[ScynapseCertificateFactory.Ed25519ExtensionOid];
        Assert.NotNull(ext);
        Assert.True(ext.Critical);
    }

    [Fact]
    public void ExtractEd25519PublicKey_RoundTrips()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        using var cert = ScynapseCertificateFactory.CreateSelfSigned(kp);

        var extracted = ScynapseCertificateFactory.ExtractEd25519PublicKey(cert);
        Assert.NotNull(extracted);
        Assert.Equal(33, extracted.Length); // 1 byte type + 32 bytes key
        Assert.Equal((byte)ScynapseKeyType.Node, extracted[0]);
        Assert.True(kp.PublicKeyBytes.SequenceEqual(extracted.AsSpan(1)));
    }

    [Fact]
    public void ExtractEd25519PublicKey_NullWhenNoExtension()
    {
        // Create a normal ECDSA cert without our extension
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Test", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(
            System.DateTimeOffset.UtcNow.AddMinutes(-5),
            System.DateTimeOffset.UtcNow.AddYears(1));

        var extracted = ScynapseCertificateFactory.ExtractEd25519PublicKey(cert);
        Assert.Null(extracted);
    }

    [Fact]
    public void CreateSelfSigned_EcdsaKeyFunctional()
    {
        // The ECDSA transport key should actually sign/verify (TLS needs it)
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        using var cert = ScynapseCertificateFactory.CreateSelfSigned(kp);

        using var ecdsa = cert.GetECDsaPrivateKey();
        Assert.NotNull(ecdsa);
        var data = new byte[] { 1, 2, 3 };
        var sig = ecdsa.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA256);
        Assert.True(ecdsa.VerifyData(data, sig, System.Security.Cryptography.HashAlgorithmName.SHA256));
    }

    [Fact]
    public void CreateSelfSigned_DifferentKeys_DifferentExtensions()
    {
        var kp1 = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var kp2 = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        using var cert1 = ScynapseCertificateFactory.CreateSelfSigned(kp1);
        using var cert2 = ScynapseCertificateFactory.CreateSelfSigned(kp2);

        var ext1 = ScynapseCertificateFactory.ExtractEd25519PublicKey(cert1);
        var ext2 = ScynapseCertificateFactory.ExtractEd25519PublicKey(cert2);
        Assert.False(ext1.AsSpan().SequenceEqual(ext2));
    }

    [Fact]
    public void CreateSelfSigned_AllKeyTypes_Work()
    {
        foreach (var keyType in new[] { ScynapseKeyType.Organization, ScynapseKeyType.Domain, ScynapseKeyType.Node, ScynapseKeyType.Instance })
        {
            var kp = ScynapseKeyPair.Generate(keyType);
            using var cert = ScynapseCertificateFactory.CreateSelfSigned(kp);
            var extracted = ScynapseCertificateFactory.ExtractEd25519PublicKey(cert);
            Assert.Equal((byte)keyType, extracted[0]);
        }
    }
}

public class ScynapseRemoteCertificateValidatorTests
{
    [Fact]
    public async Task ValidChain_AcceptsCert()
    {
        // Setup: root identity → delegation → node
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse:*",
            actionPattern: "*");

        var store = new InMemoryAssertionStore();
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);

        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
            { root.PublicKeyBytes.ToArray() };

        var validator = new ScynapseRemoteCertificateValidator(store, new InMemoryNonceStore(), trustedRoots);

        using var cert = ScynapseCertificateFactory.CreateSelfSigned(node);
        Assert.True(validator.Validate(cert));
    }

    [Fact]
    public void UnknownKey_RejectsCert()
    {
        // Node has no assertions in store
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var store = new InMemoryAssertionStore();
        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        var validator = new ScynapseRemoteCertificateValidator(store, new InMemoryNonceStore(), trustedRoots);

        using var cert = ScynapseCertificateFactory.CreateSelfSigned(node);
        Assert.False(validator.Validate(cert));
    }

    [Fact]
    public void NoEd25519Extension_RejectsCert()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Test", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(
            System.DateTimeOffset.UtcNow.AddMinutes(-5),
            System.DateTimeOffset.UtcNow.AddYears(1));

        var store = new InMemoryAssertionStore();
        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        var validator = new ScynapseRemoteCertificateValidator(store, new InMemoryNonceStore(), trustedRoots);

        Assert.False(validator.Validate(cert));
    }

    [Fact]
    public async Task RevokedAssertion_RejectsCert()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() });

        var store = new InMemoryAssertionStore();
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        store.Revoke(delegation.Id); // Revoke the delegation

        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
            { root.PublicKeyBytes.ToArray() };
        var validator = new ScynapseRemoteCertificateValidator(store, new InMemoryNonceStore(), trustedRoots);

        using var cert = ScynapseCertificateFactory.CreateSelfSigned(node);
        Assert.False(validator.Validate(cert));
    }

    [Fact]
    public async Task SelfSignedRoot_AcceptsCert()
    {
        // A trusted root presenting its own cert should be accepted
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var rootIdentity = AssertionBuilder.CreateIdentity(root);

        var store = new InMemoryAssertionStore();
        await store.StoreAsync(rootIdentity);

        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
            { root.PublicKeyBytes.ToArray() };
        var validator = new ScynapseRemoteCertificateValidator(store, new InMemoryNonceStore(), trustedRoots);

        using var cert = ScynapseCertificateFactory.CreateSelfSigned(root);
        Assert.True(validator.Validate(cert));
    }
}
