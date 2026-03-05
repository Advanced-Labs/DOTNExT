using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Orleans;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Orleans.Tests;

public class OutgoingCallFilterTests
{
    [Fact]
    public async Task AttachesCallerIdentity()
    {
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var rootIdentity = AssertionBuilder.CreateIdentity(root);
        var delegation = AssertionBuilder.CreateDelegation(
            root, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse:*",
            actionPattern: "*");

        // CCap issued by node (who received the delegation)
        var ccap = AssertionBuilder.CreateCapability(
            node, node.PublicKeyBytes,
            "scynapse:*", "*",
            proofs: new[] { delegation.Id.ToArray() });

        var filter = new ScynapseOutgoingCallFilter(node, ccap);
        var ctx = new TestOutgoingGrainCallContext
        {
            InterfaceName = "ISecureTestGrain",
            MethodName = "GetDataAsync",
            InterfaceMethod = typeof(ISecureTestGrain).GetMethod(nameof(ISecureTestGrain.GetDataAsync))!,
        };

        // Capture security context at the point the outgoing call happens
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.CallerPublicKeyKey);
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.CCapKey);
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.BearerProofKey);

        RequestContext.Clear();
        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);

        var callerKey = ctx.CapturedRequestContext[ScynapseSecurityConstants.CallerPublicKeyKey] as byte[];
        Assert.NotNull(callerKey);
        Assert.True(callerKey.AsSpan().SequenceEqual(node.PublicKeyBytes));

        var ccapBytes = ctx.CapturedRequestContext[ScynapseSecurityConstants.CCapKey] as byte[];
        Assert.NotNull(ccapBytes);

        var bearerProof = ctx.CapturedRequestContext[ScynapseSecurityConstants.BearerProofKey] as byte[];
        Assert.NotNull(bearerProof);

        // Verify bearer proof is valid
        Assert.True(node.Verify(ccap.Id.Span, bearerProof));

        RequestContext.Clear();
    }
}
