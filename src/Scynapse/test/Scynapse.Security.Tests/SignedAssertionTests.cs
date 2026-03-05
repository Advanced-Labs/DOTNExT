using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Xunit;

namespace Scynapse.Security.Tests;

public class SignedAssertionTests
{
    [Fact]
    public void CreateIdentity_SelfSigned_VerifiesSuccessfully()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.User);
        var assertion = AssertionBuilder.CreateIdentity(kp);

        Assert.Equal(ClaimType.Identity, assertion.ClaimType);
        Assert.Equal(SignedAssertion.CurrentVersion, assertion.Version);
        Assert.True(assertion.Issuer.Span.SequenceEqual(assertion.Subject.Span));
        Assert.True(assertion.VerifySignature());
    }

    [Fact]
    public void CreateIdentity_WithExpiry_HasExpiresAt()
    {
        using var kp = ScynapseKeyPair.Generate();
        long expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var assertion = AssertionBuilder.CreateIdentity(kp, expiresAt: expires);

        Assert.Equal(expires, assertion.ExpiresAt);
        Assert.Null(assertion.NotBefore);
        Assert.True(assertion.VerifySignature());
    }

    [Fact]
    public void CreateCapability_VerifiesSuccessfully()
    {
        using var issuer = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        using var subject = ScynapseKeyPair.Generate(ScynapseKeyType.User);

        var assertion = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes, "grain:MyGrain", "invoke");

        Assert.Equal(ClaimType.Capability, assertion.ClaimType);
        Assert.True(assertion.Issuer.Span.SequenceEqual(issuer.PublicKeyBytes));
        Assert.True(assertion.Subject.Span.SequenceEqual(subject.PublicKeyBytes));
        Assert.True(assertion.VerifySignature());
    }

    [Fact]
    public void CreateCapability_ClaimDataDeserializes()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var assertion = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes, "grain:MyGrain", "read");

        var claim = CapabilityClaim.Deserialize(assertion.ClaimData.Span);
        Assert.Equal("grain:MyGrain", claim.Resource);
        Assert.Equal("read", claim.Action);
        Assert.Null(claim.Constraints);
    }

    [Fact]
    public void CreateDelegation_WithScope_VerifiesSuccessfully()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var assertion = AssertionBuilder.CreateDelegation(
            issuer, subject.PublicKeyBytes,
            [ClaimType.Capability, ClaimType.Relation],
            resourcePattern: "grain:*",
            maxDepth: 3);

        Assert.Equal(ClaimType.Delegation, assertion.ClaimType);
        Assert.True(assertion.VerifySignature());

        var claim = DelegationClaim.Deserialize(assertion.ClaimData.Span);
        Assert.Equal(2, claim.AllowedClaimTypes.Length);
        Assert.Contains(ClaimType.Capability, claim.AllowedClaimTypes);
        Assert.Equal("grain:*", claim.ResourcePattern);
        Assert.Equal((byte)3, claim.MaxDepth);
    }

    [Fact]
    public void CreateRelation_WithMetadata_VerifiesSuccessfully()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var metadata = new Dictionary<string, byte[]>
        {
            ["role"] = "admin"u8.ToArray(),
            ["level"] = new byte[] { 5 }
        };

        var assertion = AssertionBuilder.CreateRelation(
            issuer, subject.PublicKeyBytes, "membership", metadata);

        Assert.Equal(ClaimType.Relation, assertion.ClaimType);
        Assert.True(assertion.VerifySignature());

        var claim = RelationClaim.Deserialize(assertion.ClaimData.Span);
        Assert.Equal("membership", claim.Context);
        Assert.NotNull(claim.Metadata);
        Assert.Equal("admin"u8.ToArray(), claim.Metadata!["role"]);
    }

    [Fact]
    public void SerializationRoundtrip_SignatureStillValid()
    {
        using var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var original = AssertionBuilder.CreateIdentity(kp);
        Assert.True(original.VerifySignature());

        var bytes = original.Serialize();
        var restored = SignedAssertion.Deserialize(bytes);

        Assert.Equal(original.Version, restored.Version);
        Assert.True(original.Id.Span.SequenceEqual(restored.Id.Span));
        Assert.True(original.Issuer.Span.SequenceEqual(restored.Issuer.Span));
        Assert.True(original.Subject.Span.SequenceEqual(restored.Subject.Span));
        Assert.Equal(original.ClaimType, restored.ClaimType);
        Assert.True(original.Signature.Span.SequenceEqual(restored.Signature.Span));
        Assert.True(restored.VerifySignature());
    }

    [Fact]
    public void SerializationRoundtrip_CapabilityWithProofsAndExtensions()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var proofId = new byte[32];
        proofId[0] = 0xAA;
        proofId[31] = 0xBB;

        var assertion = new AssertionBuilder()
            .SetIssuer(issuer)
            .SetSubject(subject.PublicKeyBytes)
            .SetClaim(ClaimType.Capability, new CapabilityClaim("res:test", "write").Serialize())
            .SetScope(notBefore: 1000, expiresAt: 2000, nonce: new byte[] { 1, 2, 3, 4 })
            .AddProof(proofId)
            .AddExtension("channel-binding", new byte[] { 10, 20, 30 })
            .AddExtension("anonymity-shard", new byte[] { 40, 50 })
            .Build();

        Assert.True(assertion.VerifySignature());

        var bytes = assertion.Serialize();
        var restored = SignedAssertion.Deserialize(bytes);

        Assert.Equal(1000, restored.NotBefore);
        Assert.Equal(2000, restored.ExpiresAt);
        Assert.NotNull(restored.Nonce);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, restored.Nonce!.Value.ToArray());
        Assert.Single(restored.Proofs);
        Assert.True(proofId.AsSpan().SequenceEqual(restored.Proofs[0].Span));
        Assert.Equal(2, restored.Extensions.Count);
        Assert.Equal(new byte[] { 10, 20, 30 }, restored.Extensions["channel-binding"].ToArray());
        Assert.Equal(new byte[] { 40, 50 }, restored.Extensions["anonymity-shard"].ToArray());
        Assert.True(restored.VerifySignature());
    }

    [Fact]
    public void ContentHash_ChangesWhenFieldChanges()
    {
        using var kp = ScynapseKeyPair.Generate();

        var a1 = AssertionBuilder.CreateIdentity(kp);
        var a2 = AssertionBuilder.CreateIdentity(kp, expiresAt: 9999);

        // Different content → different Ids
        Assert.False(a1.Id.Span.SequenceEqual(a2.Id.Span));
    }

    [Fact]
    public void ContentHash_DifferentSubject_DifferentId()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var sub1 = ScynapseKeyPair.Generate();
        using var sub2 = ScynapseKeyPair.Generate();

        var a1 = AssertionBuilder.CreateCapability(issuer, sub1.PublicKeyBytes, "res:x", "act");
        var a2 = AssertionBuilder.CreateCapability(issuer, sub2.PublicKeyBytes, "res:x", "act");

        Assert.False(a1.Id.Span.SequenceEqual(a2.Id.Span));
    }

    [Fact]
    public void ContentHash_DifferentClaimData_DifferentId()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var a1 = AssertionBuilder.CreateCapability(issuer, subject.PublicKeyBytes, "res:a", "read");
        var a2 = AssertionBuilder.CreateCapability(issuer, subject.PublicKeyBytes, "res:b", "read");

        Assert.False(a1.Id.Span.SequenceEqual(a2.Id.Span));
    }

    [Fact]
    public void TamperedSubject_InvalidatesSignature()
    {
        using var kp = ScynapseKeyPair.Generate();
        var assertion = AssertionBuilder.CreateIdentity(kp);
        var bytes = assertion.Serialize();

        // Find subject public key bytes in the serialized output and flip one
        var subjectBytes = assertion.Subject.ToArray();
        int idx = FindByteSequence(bytes, subjectBytes);
        Assert.True(idx >= 0, "Subject bytes not found in serialized output");
        bytes[idx + 10] ^= 0xFF;

        var tampered = SignedAssertion.Deserialize(bytes);
        Assert.False(tampered.VerifySignature());
    }

    [Fact]
    public void TamperedSignature_InvalidatesVerification()
    {
        using var kp = ScynapseKeyPair.Generate();
        var assertion = AssertionBuilder.CreateIdentity(kp);
        var bytes = assertion.Serialize();

        // Find signature bytes in the serialized output and flip one
        var sigBytes = assertion.Signature.ToArray();
        int idx = FindByteSequence(bytes, sigBytes);
        Assert.True(idx >= 0, "Signature bytes not found in serialized output");
        bytes[idx + 30] ^= 0xFF;

        var tampered = SignedAssertion.Deserialize(bytes);
        Assert.False(tampered.VerifySignature());
    }

    [Fact]
    public void TamperedId_InvalidatesVerification()
    {
        using var kp = ScynapseKeyPair.Generate();
        var assertion = AssertionBuilder.CreateIdentity(kp);
        var bytes = assertion.Serialize();

        // Find Id bytes in the serialized output and flip one
        var idBytes = assertion.Id.ToArray();
        int idx = FindByteSequence(bytes, idBytes);
        Assert.True(idx >= 0, "Id bytes not found in serialized output");
        bytes[idx + 16] ^= 0xFF;

        var tampered = SignedAssertion.Deserialize(bytes);
        Assert.False(tampered.VerifySignature());
    }

    private static int FindByteSequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }

    [Fact]
    public void ExtensionFields_PreservedThroughSerialization()
    {
        using var kp = ScynapseKeyPair.Generate();
        var assertion = new AssertionBuilder()
            .SetIssuer(kp)
            .SetSubject(kp.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .AddExtension("x-custom-field", new byte[] { 1, 2, 3 })
            .AddExtension("a-sorted-first", new byte[] { 4, 5 })
            .Build();

        var bytes = assertion.Serialize();
        var restored = SignedAssertion.Deserialize(bytes);

        Assert.Equal(2, restored.Extensions.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Extensions["x-custom-field"].ToArray());
        Assert.Equal(new byte[] { 4, 5 }, restored.Extensions["a-sorted-first"].ToArray());
        Assert.True(restored.VerifySignature());
    }

    [Fact]
    public void MultipleProofs_PreservedThroughSerialization()
    {
        using var issuer = ScynapseKeyPair.Generate();
        using var subject = ScynapseKeyPair.Generate();

        var proof1 = new byte[32]; proof1[0] = 1;
        var proof2 = new byte[32]; proof2[0] = 2;
        var proof3 = new byte[32]; proof3[0] = 3;

        var assertion = new AssertionBuilder()
            .SetIssuer(issuer)
            .SetSubject(subject.PublicKeyBytes)
            .SetClaim(ClaimType.Capability, new CapabilityClaim("res:x", "act").Serialize())
            .AddProof(proof1)
            .AddProof(proof2)
            .AddProof(proof3)
            .Build();

        var bytes = assertion.Serialize();
        var restored = SignedAssertion.Deserialize(bytes);

        Assert.Equal(3, restored.Proofs.Count);
        Assert.True(proof1.AsSpan().SequenceEqual(restored.Proofs[0].Span));
        Assert.True(proof2.AsSpan().SequenceEqual(restored.Proofs[1].Span));
        Assert.True(proof3.AsSpan().SequenceEqual(restored.Proofs[2].Span));
        Assert.True(restored.VerifySignature());
    }

    [Fact]
    public void WrongIssuerKey_VerifyFails()
    {
        using var realIssuer = ScynapseKeyPair.Generate();
        var assertion = AssertionBuilder.CreateIdentity(realIssuer);
        Assert.True(assertion.VerifySignature());

        // Find issuer bytes in serialized form and tamper
        var bytes = assertion.Serialize();
        var issuerBytes = assertion.Issuer.ToArray();
        int idx = FindByteSequence(bytes, issuerBytes);
        Assert.True(idx >= 0, "Issuer bytes not found in serialized output");
        bytes[idx + 5] ^= 0xFF;

        var tampered = SignedAssertion.Deserialize(bytes);
        Assert.False(tampered.VerifySignature());
    }

    [Fact]
    public void VerifyOnly_CannotBuildAssertions()
    {
        using var full = ScynapseKeyPair.Generate();
        using var verifyOnly = ScynapseKeyPair.FromPublicKey(full.PublicKeyBytes.ToArray());

        Assert.Throws<InvalidOperationException>(() =>
            AssertionBuilder.CreateIdentity(verifyOnly));
    }

    [Fact]
    public void Builder_MissingIssuer_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AssertionBuilder()
                .SetSubject(new byte[32])
                .SetClaim(ClaimType.Identity, Array.Empty<byte>())
                .Build());
    }

    [Fact]
    public void Builder_MissingSubject_Throws()
    {
        using var kp = ScynapseKeyPair.Generate();
        Assert.Throws<InvalidOperationException>(() =>
            new AssertionBuilder()
                .SetIssuer(kp)
                .SetClaim(ClaimType.Identity, Array.Empty<byte>())
                .Build());
    }

    [Fact]
    public void Builder_MissingClaim_Throws()
    {
        using var kp = ScynapseKeyPair.Generate();
        Assert.Throws<InvalidOperationException>(() =>
            new AssertionBuilder()
                .SetIssuer(kp)
                .SetSubject(kp.PublicKeyBytes)
                .Build());
    }

    [Fact]
    public void DelegationClaim_SerializationRoundtrip()
    {
        var original = new DelegationClaim(
            [ClaimType.Capability, ClaimType.Delegation],
            ResourcePattern: "grain:Foo*",
            ActionPattern: "read|write",
            MaxDepth: 5);

        var bytes = original.Serialize();
        var restored = DelegationClaim.Deserialize(bytes);

        Assert.Equal(original.AllowedClaimTypes, restored.AllowedClaimTypes);
        Assert.Equal(original.ResourcePattern, restored.ResourcePattern);
        Assert.Equal(original.ActionPattern, restored.ActionPattern);
        Assert.Equal(original.MaxDepth, restored.MaxDepth);
    }

    [Fact]
    public void DelegationClaim_NullOptionals_Roundtrip()
    {
        var original = new DelegationClaim([ClaimType.Identity]);

        var bytes = original.Serialize();
        var restored = DelegationClaim.Deserialize(bytes);

        Assert.Single(restored.AllowedClaimTypes);
        Assert.Null(restored.ResourcePattern);
        Assert.Null(restored.ActionPattern);
        Assert.Null(restored.MaxDepth);
    }

    [Fact]
    public void CapabilityClaim_WithConstraints_Roundtrip()
    {
        var constraints = new Dictionary<string, byte[]>
        {
            ["max-rate"] = BitConverter.GetBytes(1000),
            ["allowed-ips"] = "192.168.1.0/24"u8.ToArray()
        };
        var original = new CapabilityClaim("grain:RateLimited", "invoke", constraints);

        var bytes = original.Serialize();
        var restored = CapabilityClaim.Deserialize(bytes);

        Assert.Equal("grain:RateLimited", restored.Resource);
        Assert.Equal("invoke", restored.Action);
        Assert.NotNull(restored.Constraints);
        Assert.Equal(2, restored.Constraints!.Count);
        Assert.Equal(constraints["max-rate"], restored.Constraints["max-rate"]);
        Assert.Equal(constraints["allowed-ips"], restored.Constraints["allowed-ips"]);
    }

    [Fact]
    public void RelationClaim_NullMetadata_Roundtrip()
    {
        var original = new RelationClaim("friendship");

        var bytes = original.Serialize();
        var restored = RelationClaim.Deserialize(bytes);

        Assert.Equal("friendship", restored.Context);
        Assert.Null(restored.Metadata);
    }

    [Fact]
    public void Nonce_PreservedThroughSerialization()
    {
        using var kp = ScynapseKeyPair.Generate();
        var nonce = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };

        var assertion = new AssertionBuilder()
            .SetIssuer(kp)
            .SetSubject(kp.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .SetScope(nonce: nonce)
            .Build();

        var bytes = assertion.Serialize();
        var restored = SignedAssertion.Deserialize(bytes);

        Assert.NotNull(restored.Nonce);
        Assert.Equal(nonce, restored.Nonce!.Value.ToArray());
        Assert.True(restored.VerifySignature());
    }

    [Fact]
    public void InvalidProofLength_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AssertionBuilder().AddProof(new byte[16]));
    }

    [Fact]
    public void SameContent_ProducesSameId()
    {
        // Two assertions with identical content should have the same Id
        var seed = new byte[32]; seed[0] = 42;
        using var kp1 = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.Node);
        using var kp2 = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.Node);

        var a1 = AssertionBuilder.CreateIdentity(kp1);
        var a2 = AssertionBuilder.CreateIdentity(kp2);

        // Same keypair, same claim type, same (empty) claim data, no scope → same content → same Id
        Assert.True(a1.Id.Span.SequenceEqual(a2.Id.Span));
        // And same signature (Ed25519 is deterministic)
        Assert.True(a1.Signature.Span.SequenceEqual(a2.Signature.Span));
    }
}
