using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Orleans;
using Xunit;

namespace Scynapse.Security.Orleans.Tests;

public class GrainSecurityExtensionsTests
{
    [Fact]
    public void GetCallerPublicKey_ReturnsKey_WhenSet()
    {
        var key = new byte[] { 1, 2, 3, 4 };
        RequestContext.Set(ScynapseSecurityConstants.VerifiedCallerKeyKey, key);

        var result = GrainSecurityExtensions.GetCallerPublicKey();
        Assert.NotNull(result);
        Assert.Equal(key, result);

        RequestContext.Clear();
    }

    [Fact]
    public void GetCallerPublicKey_ReturnsNull_WhenNotSet()
    {
        RequestContext.Clear();
        var result = GrainSecurityExtensions.GetCallerPublicKey();
        Assert.Null(result);
    }

    [Fact]
    public void GetCallerCapability_ReturnsAssertion_WhenSet()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);
        var ccap = AssertionBuilder.CreateCapability(
            kp, kp.PublicKeyBytes, "test", "read");

        RequestContext.Set(ScynapseSecurityConstants.VerifiedCCapKey, ccap);

        var result = GrainSecurityExtensions.GetCallerCapability();
        Assert.NotNull(result);
        Assert.Equal(ccap.Id.ToArray(), result.Id.ToArray());

        RequestContext.Clear();
    }

    [Fact]
    public void GetCallerCapability_ReturnsNull_WhenNotSet()
    {
        RequestContext.Clear();
        var result = GrainSecurityExtensions.GetCallerCapability();
        Assert.Null(result);
    }
}
