using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Xunit;

namespace Scynapse.Security.Tests;

public class RevocationClaimTests
{
    [Fact]
    public void SerializationRoundtrip_WithReason()
    {
        var targetId = new byte[32];
        Random.Shared.NextBytes(targetId);

        var claim = new RevocationClaim(targetId, "key compromised");
        var bytes = claim.Serialize();
        var restored = RevocationClaim.Deserialize(bytes);

        Assert.Equal(targetId, restored.Target);
        Assert.Equal("key compromised", restored.Reason);
    }

    [Fact]
    public void SerializationRoundtrip_NullReason()
    {
        var targetId = new byte[32];
        Random.Shared.NextBytes(targetId);

        var claim = new RevocationClaim(targetId, null);
        var bytes = claim.Serialize();
        var restored = RevocationClaim.Deserialize(bytes);

        Assert.Equal(targetId, restored.Target);
        Assert.Null(restored.Reason);
    }

    [Fact]
    public void CreateRevocation_ProducesValidAssertion()
    {
        var issuer = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var targetId = new byte[32];
        Random.Shared.NextBytes(targetId);

        var revocation = AssertionBuilder.CreateRevocation(issuer, targetId, "expired");

        Assert.Equal(ClaimType.Revocation, revocation.ClaimType);
        Assert.True(revocation.VerifySignature());

        // Issuer == subject (self-referential)
        Assert.True(revocation.Issuer.Span.SequenceEqual(revocation.Subject.Span));

        // Claim data deserializes correctly
        var claim = RevocationClaim.Deserialize(revocation.ClaimData.Span);
        Assert.Equal(targetId, claim.Target);
        Assert.Equal("expired", claim.Reason);
    }

    [Fact]
    public void CreateRevocation_WithoutReason()
    {
        var issuer = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var targetId = new byte[32];
        Random.Shared.NextBytes(targetId);

        var revocation = AssertionBuilder.CreateRevocation(issuer, targetId);

        Assert.True(revocation.VerifySignature());
        var claim = RevocationClaim.Deserialize(revocation.ClaimData.Span);
        Assert.Null(claim.Reason);
    }
}
