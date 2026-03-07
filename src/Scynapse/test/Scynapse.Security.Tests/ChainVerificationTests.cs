using System;
using System.Threading.Tasks;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Tests;

public class ChainVerificationTests
{
    private static HashSet<ReadOnlyMemory<byte>> TrustedRoots(params ScynapseKeyPair[] keys)
    {
        var set = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        foreach (var k in keys)
            set.Add(k.PublicKeyBytes.ToArray());
        return set;
    }

    private static (AssertionVerifier verifier, InMemoryAssertionStore store) CreateVerifier(
        HashSet<ReadOnlyMemory<byte>> trustedRoots,
        IAttenuationChecker checker = null,
        int maxDepth = 32)
    {
        var store = new InMemoryAssertionStore();
        var nonceStore = new InMemoryNonceStore();
        var verifier = new AssertionVerifier(
            store, nonceStore, trustedRoots,
            checker ?? new DefaultAttenuationChecker(),
            maxDepth);
        return (verifier, store);
    }

    // ---- Trusted root identity ----

    [Fact]
    public async Task SelfSignedIdentity_TrustedRoot_IsValid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root);
        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(identity);

        var result = await verifier.VerifyAsync(identity);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SelfSignedIdentity_UntrustedRoot_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var other = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root);
        var (verifier, store) = CreateVerifier(TrustedRoots(other));
        await store.StoreAsync(identity);

        var result = await verifier.VerifyAsync(identity);
        Assert.False(result.IsValid);
        Assert.Contains("non-root assertion with no proofs", result.FailureReason);
    }

    // ---- Valid chains ----

    [Fact]
    public async Task ValidChain_1Deep_RootToCapability()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var capability = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes, "scynapse.app.MyGrain", "invoke",
            proofs: new[] { rootIdentity.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidChain_3Deep_RootDelegationDelegationCapability()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var domain = ScynapseKeyPair.Generate(ScynapseKeyType.Domain);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var instance = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);

        var delegationToDomain = AssertionBuilder.CreateDelegation(
            root, domain.PublicKeyBytes,
            new[] { ClaimType.Capability, ClaimType.Delegation },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse.app.>",
            actionPattern: "*");

        var delegationToNode = AssertionBuilder.CreateDelegation(
            domain, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { delegationToDomain.Id.ToArray() },
            resourcePattern: "scynapse.app.>",
            actionPattern: "invoke",
            maxDepth: 1);

        var capability = AssertionBuilder.CreateCapability(
            node, instance.PublicKeyBytes,
            "scynapse.app.MyGrain", "invoke",
            proofs: new[] { delegationToNode.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegationToDomain);
        await store.StoreAsync(delegationToNode);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.True(result.IsValid, result.FailureReason);
    }

    // ---- Chain break ----

    [Fact]
    public async Task BrokenChain_ParentSubjectMismatch_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var domain = ScynapseKeyPair.Generate(ScynapseKeyType.Domain);
        var impostor = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, domain.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() });

        // Impostor issues capability but claims delegation as proof
        var capability = AssertionBuilder.CreateCapability(
            impostor, target.PublicKeyBytes,
            "scynapse.app.X", "invoke",
            proofs: new[] { delegation.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("chain break", result.FailureReason);
    }

    // ---- Temporal scope ----

    [Fact]
    public async Task ExpiredAssertion_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root,
            expiresAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600);

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(identity);

        var result = await verifier.VerifyAsync(identity);
        Assert.False(result.IsValid);
        Assert.Contains("expired", result.FailureReason);
    }

    [Fact]
    public async Task NotYetValid_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = new AssertionBuilder()
            .SetIssuer(root)
            .SetSubject(root.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .SetScope(notBefore: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600)
            .Build();

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(identity);

        var result = await verifier.VerifyAsync(identity);
        Assert.False(result.IsValid);
        Assert.Contains("not yet valid", result.FailureReason);
    }

    // ---- Attenuation ----

    [Fact]
    public async Task Attenuation_CapabilityOutsideDelegationScope_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse.app.Safe.>",
            actionPattern: "read");

        // Node tries to grant "invoke" on "Dangerous" — outside scope
        var capability = AssertionBuilder.CreateCapability(
            node, target.PublicKeyBytes,
            "scynapse.app.Dangerous", "invoke",
            proofs: new[] { delegation.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("attenuation", result.FailureReason);
    }

    [Fact]
    public async Task Attenuation_CapabilityWithinScope_IsValid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse.app.>",
            actionPattern: "*");

        var capability = AssertionBuilder.CreateCapability(
            node, target.PublicKeyBytes,
            "scynapse.app.MyGrain", "invoke",
            proofs: new[] { delegation.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task Attenuation_DelegationNarrowing_WiderChildFails()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var domain = ScynapseKeyPair.Generate(ScynapseKeyType.Domain);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, domain.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse.app.Safe.>",
            actionPattern: "read",
            maxDepth: 2);

        // Domain re-delegates with WIDER scope
        var reDelegation = AssertionBuilder.CreateDelegation(
            domain, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { delegation.Id.ToArray() },
            resourcePattern: "scynapse.app.>", // wider
            actionPattern: "read");

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(reDelegation);

        var result = await verifier.VerifyAsync(reDelegation);
        Assert.False(result.IsValid);
        Assert.Contains("attenuation", result.FailureReason);
    }

    [Fact]
    public async Task Attenuation_TemporalBroader_ChildExpiresAfterParent_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rootIdentity = AssertionBuilder.CreateIdentity(root, expiresAt: now + 3600);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            expiresAt: now + 3600);

        // Capability expires in 2 hours — broader than parent
        var capability = new AssertionBuilder()
            .SetIssuer(node)
            .SetSubject(target.PublicKeyBytes)
            .SetClaim(ClaimType.Capability,
                new CapabilityClaim("scynapse.app.X", "invoke").Serialize())
            .SetScope(expiresAt: now + 7200)
            .AddProof(delegation.Id.Span)
            .Build();

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("attenuation", result.FailureReason);
    }

    // ---- Replay prevention ----

    [Fact]
    public async Task Replay_SameNonceTwice_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var nonce = new byte[16];
        Random.Shared.NextBytes(nonce);

        var identity = new AssertionBuilder()
            .SetIssuer(root)
            .SetSubject(root.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .SetScope(nonce: nonce, expiresAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600)
            .Build();

        var store = new InMemoryAssertionStore();
        var nonceStore = new InMemoryNonceStore();
        var verifier = new AssertionVerifier(
            store, nonceStore, TrustedRoots(root),
            new DefaultAttenuationChecker());
        await store.StoreAsync(identity);

        var result1 = await verifier.VerifyAsync(identity);
        Assert.True(result1.IsValid, result1.FailureReason);

        var result2 = await verifier.VerifyAsync(identity);
        Assert.False(result2.IsValid);
        Assert.Contains("replay", result2.FailureReason);
    }

    // ---- Delegated identity cannot issue capabilities directly ----

    [Fact]
    public async Task DelegatedIdentity_CannotIssueCapability_WithoutDelegation()
    {
        // Root delegates an identity to node (not a delegation assertion — just an identity).
        // Node then tries to use that identity as proof to issue a capability.
        // This should fail: only self-signed root identities have blanket authority.
        // Authority for non-roots flows through explicit Delegation assertions.
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);

        // Root issues an identity assertion FOR node (issuer=root, subject=node)
        var nodeIdentity = new AssertionBuilder()
            .SetIssuer(root)
            .SetSubject(node.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .AddProof(rootIdentity.Id.Span)
            .Build();

        // Node tries to issue a capability using the delegated identity as proof
        var capability = AssertionBuilder.CreateCapability(
            node, target.PublicKeyBytes,
            "scynapse.app.X", "invoke",
            proofs: new[] { nodeIdentity.Id.ToArray() });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(nodeIdentity);
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("attenuation", result.FailureReason);
    }

    // ---- Unresolvable proof ----

    [Fact]
    public async Task UnresolvableProof_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var fakeProofId = new byte[32];
        Random.Shared.NextBytes(fakeProofId);
        var capability = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes,
            "scynapse.app.X", "invoke",
            proofs: new[] { fakeProofId });

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("unresolvable proof", result.FailureReason);
    }

    // ---- Max depth ----

    [Fact]
    public async Task ExcessiveDepth_IsRejected()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var (verifier, store) = CreateVerifier(TrustedRoots(root), maxDepth: 3);
        await store.StoreAsync(rootIdentity);

        var previousId = rootIdentity.Id.ToArray();
        var currentIssuer = root;

        for (int i = 0; i < 5; i++)
        {
            var next = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
            var delegation = AssertionBuilder.CreateDelegation(
                currentIssuer, next.PublicKeyBytes,
                new[] { ClaimType.Capability, ClaimType.Delegation },
                proofs: new[] { previousId });
            await store.StoreAsync(delegation);
            previousId = delegation.Id.ToArray();
            currentIssuer = next;
        }

        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);
        var cap = AssertionBuilder.CreateCapability(
            currentIssuer, target.PublicKeyBytes,
            "scynapse.app.X", "invoke",
            proofs: new[] { previousId });
        await store.StoreAsync(cap);

        var result = await verifier.VerifyAsync(cap);
        Assert.False(result.IsValid);
        Assert.Contains("depth exceeds maximum", result.FailureReason);
    }

    // ---- Revocation ----

    [Fact]
    public async Task RevokedAssertion_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var capability = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes,
            "scynapse.app.X", "invoke",
            proofs: new[] { rootIdentity.Id.ToArray() });

        var store = new InMemoryAssertionStore();
        var nonceStore = new InMemoryNonceStore();
        var verifier = new AssertionVerifier(
            store, nonceStore, TrustedRoots(root),
            new DefaultAttenuationChecker());
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(capability);

        var result1 = await verifier.VerifyAsync(capability);
        Assert.True(result1.IsValid, result1.FailureReason);

        store.Revoke(capability.Id);
        var result2 = await verifier.VerifyAsync(capability);
        Assert.False(result2.IsValid);
        Assert.Contains("revoked", result2.FailureReason);
    }

    // ---- Bad signature ----

    [Fact]
    public async Task TamperedAssertion_BadSignature_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root);

        var bytes = identity.Serialize();
        bytes[bytes.Length - 2] ^= 0xFF;
        var tampered = SignedAssertion.Deserialize(bytes);

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(tampered);

        var result = await verifier.VerifyAsync(tampered);
        Assert.False(result.IsValid);
        Assert.Contains("bad signature", result.FailureReason);
    }

    // ---- VerifyLocal ----

    [Fact]
    public void VerifyLocal_ValidAssertion_Succeeds()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root);
        var (verifier, _) = CreateVerifier(TrustedRoots(root));

        var result = verifier.VerifyLocal(identity);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyLocal_ExpiredAssertion_Fails()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(root,
            expiresAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600);
        var (verifier, _) = CreateVerifier(TrustedRoots(root));

        var result = verifier.VerifyLocal(identity);
        Assert.False(result.IsValid);
        Assert.Contains("expired", result.FailureReason);
    }

    // ---- Non-root without proofs ----

    [Fact]
    public async Task CapabilityWithNoProofs_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var capability = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes,
            "scynapse.app.X", "invoke");

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(capability);

        var result = await verifier.VerifyAsync(capability);
        Assert.False(result.IsValid);
        Assert.Contains("non-root assertion with no proofs", result.FailureReason);
    }

    // ---- Delegation claim type mismatch ----

    [Fact]
    public async Task Delegation_DisallowedClaimType_IsInvalid()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability }, // only Capability allowed
            proofs: new[] { rootIdentity.Id.ToArray() });

        // Node creates a Relation (not authorized by delegation)
        var relationWithProof = new AssertionBuilder()
            .SetIssuer(node)
            .SetSubject(target.PublicKeyBytes)
            .SetClaim(ClaimType.Relation,
                new RelationClaim("member-of").Serialize())
            .AddProof(delegation.Id.Span)
            .Build();

        var (verifier, store) = CreateVerifier(TrustedRoots(root));
        await store.StoreAsync(rootIdentity);
        await store.StoreAsync(delegation);
        await store.StoreAsync(relationWithProof);

        var result = await verifier.VerifyAsync(relationWithProof);
        Assert.False(result.IsValid);
        Assert.Contains("attenuation", result.FailureReason);
    }

    // ---- Full integration chain ----

    [Fact]
    public async Task FullChain_OperatorToNodeToSessionToCapability()
    {
        var operator_ = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var session = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);
        var grain = ScynapseKeyPair.Generate(ScynapseKeyType.ComponentType);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var operatorId = AssertionBuilder.CreateIdentity(operator_, expiresAt: now + 86400);

        var nodeDelegation = AssertionBuilder.CreateDelegation(
            operator_, node.PublicKeyBytes,
            new[] { ClaimType.Capability, ClaimType.Delegation },
            proofs: new[] { operatorId.Id.ToArray() },
            expiresAt: now + 86400,
            resourcePattern: "scynapse.app.>",
            actionPattern: "*");

        var sessionDelegation = AssertionBuilder.CreateDelegation(
            node, session.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { nodeDelegation.Id.ToArray() },
            expiresAt: now + 3600,
            resourcePattern: "scynapse.app.MyGrain",
            actionPattern: "invoke",
            maxDepth: 1);

        var ccap = AssertionBuilder.CreateCapability(
            session, grain.PublicKeyBytes,
            "scynapse.app.MyGrain", "invoke",
            proofs: new[] { sessionDelegation.Id.ToArray() },
            expiresAt: now + 3600);

        var (verifier, store) = CreateVerifier(TrustedRoots(operator_));
        await store.StoreAsync(operatorId);
        await store.StoreAsync(nodeDelegation);
        await store.StoreAsync(sessionDelegation);
        await store.StoreAsync(ccap);

        var result = await verifier.VerifyAsync(ccap);
        Assert.True(result.IsValid, result.FailureReason);
    }
}
