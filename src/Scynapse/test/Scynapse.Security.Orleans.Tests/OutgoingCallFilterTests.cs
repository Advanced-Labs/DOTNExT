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
            resourcePattern: "scynapse.>",
            actionPattern: "*");

        // CCap issued by node (who received the delegation)
        var ccap = AssertionBuilder.CreateCapability(
            node, node.PublicKeyBytes,
            "scynapse.>", "*",
            proofs: new[] { delegation.Id.ToArray() });

        // Store CCap in wallet
        var wallet = new InMemoryCCapWallet();
        wallet.Store(ccap);

        var filter = new ScynapseOutgoingCallFilter(node, wallet);
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

    [Fact]
    public async Task NoCCapInWallet_AttachesIdentityOnly()
    {
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var wallet = new InMemoryCCapWallet(); // empty wallet

        var filter = new ScynapseOutgoingCallFilter(node, wallet);
        var ctx = new TestOutgoingGrainCallContext
        {
            InterfaceName = "IOpenTestGrain",
            MethodName = "Hello",
            InterfaceMethod = typeof(IOpenTestGrain).GetMethod(nameof(IOpenTestGrain.Hello))!,
        };

        ctx.KeysToCapture.Add(ScynapseSecurityConstants.CallerPublicKeyKey);
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.CCapKey);

        RequestContext.Clear();
        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);
        var callerKey = ctx.CapturedRequestContext[ScynapseSecurityConstants.CallerPublicKeyKey] as byte[];
        Assert.NotNull(callerKey);
        Assert.Null(ctx.CapturedRequestContext[ScynapseSecurityConstants.CCapKey]); // no CCap

        RequestContext.Clear();
    }
    [Fact]
    public async Task OriginalCallerKey_PreservedOnSecondHop()
    {
        // Simulates grain-to-grain: OriginalCallerKey already set, should not be overwritten
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var wallet = new InMemoryCCapWallet();

        var originalCallerKey = new byte[32];
        Random.Shared.NextBytes(originalCallerKey);

        var filter = new ScynapseOutgoingCallFilter(node, wallet);
        var ctx = new TestOutgoingGrainCallContext
        {
            InterfaceName = "IOpenTestGrain",
            MethodName = "Hello",
            InterfaceMethod = typeof(IOpenTestGrain).GetMethod(nameof(IOpenTestGrain.Hello))!,
        };

        ctx.KeysToCapture.Add(ScynapseSecurityConstants.OriginalCallerKeyKey);

        RequestContext.Clear();
        // Pre-set the original caller (simulating grain-to-grain forwarding)
        RequestContext.Set(ScynapseSecurityConstants.OriginalCallerKeyKey, originalCallerKey);

        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);
        var captured = ctx.CapturedRequestContext[ScynapseSecurityConstants.OriginalCallerKeyKey] as byte[];
        Assert.NotNull(captured);
        Assert.True(captured.AsSpan().SequenceEqual(originalCallerKey));

        RequestContext.Clear();
    }

    [Fact]
    public async Task OriginalCallerKey_SetOnFirstHop()
    {
        // First hop: no OriginalCallerKey — should be set to node identity
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var wallet = new InMemoryCCapWallet();

        var filter = new ScynapseOutgoingCallFilter(node, wallet);
        var ctx = new TestOutgoingGrainCallContext
        {
            InterfaceName = "IOpenTestGrain",
            MethodName = "Hello",
            InterfaceMethod = typeof(IOpenTestGrain).GetMethod(nameof(IOpenTestGrain.Hello))!,
        };

        ctx.KeysToCapture.Add(ScynapseSecurityConstants.OriginalCallerKeyKey);

        RequestContext.Clear();
        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);
        var captured = ctx.CapturedRequestContext[ScynapseSecurityConstants.OriginalCallerKeyKey] as byte[];
        Assert.NotNull(captured);
        Assert.True(captured.AsSpan().SequenceEqual(node.PublicKeyBytes));

        RequestContext.Clear();
    }
}

// Test grain interfaces for outgoing filter tests
[SecurityPolicy(AllowAnonymous = true)]
public interface IOpenTestGrain
{
    Task Hello();
}
