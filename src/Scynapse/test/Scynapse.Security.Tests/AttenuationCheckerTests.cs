using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Tests;

public class AttenuationCheckerTests
{
    private readonly DefaultAttenuationChecker _checker = new();

    [Theory]
    [InlineData("scynapse:grain/*", "scynapse:grain/MyGrain", true)]
    [InlineData("scynapse:grain/*", "scynapse:other/X", false)]
    [InlineData("*", "anything", true)]
    [InlineData("exact", "exact", true)]
    [InlineData("exact", "different", false)]
    [InlineData("pre*suf", "preXsuf", true)]
    [InlineData("pre*suf", "preXYZsuf", true)]
    [InlineData("pre*suf", "preXsufBAD", false)]
    public void PatternMatching(string pattern, string value, bool expected)
    {
        Assert.Equal(expected, DefaultAttenuationChecker.MatchesPattern(pattern, value));
    }

    [Fact]
    public void SelfSignedIdentityParent_AllowsAnything()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var identity = AssertionBuilder.CreateIdentity(root); // self-signed: issuer == subject
        var capability = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes,
            "scynapse:grain/X", "invoke",
            proofs: new[] { identity.Id.ToArray() });

        Assert.True(_checker.Check(identity, capability));
    }

    [Fact]
    public void DelegatedIdentityParent_CannotDelegate()
    {
        // Non-self-signed identity (issuer != subject) should NOT have blanket authority
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        // Root issues identity FOR node (issuer=root, subject=node — NOT self-signed)
        var nodeIdentity = new AssertionBuilder()
            .SetIssuer(root)
            .SetSubject(node.PublicKeyBytes)
            .SetClaim(ClaimType.Identity, System.Array.Empty<byte>())
            .AddProof(rootIdentity.Id.Span)
            .Build();

        var capability = AssertionBuilder.CreateCapability(
            node, target.PublicKeyBytes,
            "scynapse:grain/X", "invoke",
            proofs: new[] { nodeIdentity.Id.ToArray() });

        Assert.False(_checker.Check(nodeIdentity, capability));
    }

    [Fact]
    public void CapabilityParent_CannotDelegate()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var target = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var cap1 = AssertionBuilder.CreateCapability(
            root, node.PublicKeyBytes,
            "scynapse:grain/X", "invoke");
        var cap2 = AssertionBuilder.CreateCapability(
            node, target.PublicKeyBytes,
            "scynapse:grain/X", "invoke",
            proofs: new[] { cap1.Id.ToArray() });

        Assert.False(_checker.Check(cap1, cap2));
    }

    [Fact]
    public void DelegationNarrowing_ValidSubset()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var domain = ScynapseKeyPair.Generate(ScynapseKeyType.Domain);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var parentDelegation = AssertionBuilder.CreateDelegation(
            root, domain.PublicKeyBytes,
            new[] { ClaimType.Capability, ClaimType.Delegation },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse:grain/*",
            actionPattern: "*",
            maxDepth: 3);

        var childDelegation = AssertionBuilder.CreateDelegation(
            domain, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { parentDelegation.Id.ToArray() },
            resourcePattern: "scynapse:grain/MyGrain",
            actionPattern: "invoke",
            maxDepth: 2);

        Assert.True(_checker.Check(parentDelegation, childDelegation));
    }

    [Fact]
    public void DelegationNarrowing_DepthNotDecremented_Fails()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var domain = ScynapseKeyPair.Generate(ScynapseKeyType.Domain);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var parentDelegation = AssertionBuilder.CreateDelegation(
            root, domain.PublicKeyBytes,
            new[] { ClaimType.Capability, ClaimType.Delegation },
            proofs: new[] { rootIdentity.Id.ToArray() },
            maxDepth: 3);

        var childDelegation = AssertionBuilder.CreateDelegation(
            domain, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { parentDelegation.Id.ToArray() },
            maxDepth: 3); // same — not narrower

        Assert.False(_checker.Check(parentDelegation, childDelegation));
    }
}
