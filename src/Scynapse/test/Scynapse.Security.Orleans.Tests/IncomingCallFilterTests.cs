using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Orleans;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Orleans.Tests;

public class IncomingCallFilterTests
{
    private readonly ScynapseKeyPair _root;
    private readonly ScynapseKeyPair _caller;
    private readonly SignedAssertion _rootIdentity;
    private readonly SignedAssertion _delegation;
    private readonly InMemoryAssertionStore _store;
    private readonly InMemoryNonceStore _nonceStore;
    private readonly HashSet<ReadOnlyMemory<byte>> _trustedRoots;
    private readonly AttributeBasedPolicyProvider _policyProvider;

    public IncomingCallFilterTests()
    {
        _root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        _caller = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        _rootIdentity = AssertionBuilder.CreateIdentity(_root);
        _delegation = AssertionBuilder.CreateDelegation(
            _root, _caller.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { _rootIdentity.Id.ToArray() },
            resourcePattern: "scynapse.>",
            actionPattern: "*");

        _store = new InMemoryAssertionStore();
        _store.StoreAsync(_rootIdentity).AsTask().Wait();
        _store.StoreAsync(_delegation).AsTask().Wait();

        _nonceStore = new InMemoryNonceStore();
        _trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
            { _root.PublicKeyBytes.ToArray() };
        _policyProvider = new AttributeBasedPolicyProvider();
    }

    private ScynapseIncomingCallFilter CreateFilter()
    {
        return new ScynapseIncomingCallFilter(
            _store, _nonceStore, _trustedRoots, _policyProvider);
    }

    private TestIncomingGrainCallContext CreateContext(Type grainInterface, string methodName)
    {
        var method = grainInterface.GetMethod(methodName)!;
        return new TestIncomingGrainCallContext
        {
            InterfaceName = grainInterface.FullName!,
            MethodName = methodName,
            InterfaceMethod = method,
        };
    }

    private void AttachSecurityContext(
        ScynapseKeyPair callerKey,
        SignedAssertion ccap)
    {
        var ccapBytes = ccap.Serialize();
        var bearerProof = callerKey.Sign(ccap.Id.Span);

        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, callerKey.PublicKeyBytes.ToArray());
        RequestContext.Set(ScynapseSecurityConstants.CCapKey, ccapBytes);
        RequestContext.Set(ScynapseSecurityConstants.BearerProofKey, bearerProof);
    }

    /// <summary>
    /// Creates a CCap issued by _caller (who received the delegation from root).
    /// Chain: rootIdentity → delegation → ccap
    /// Chain continuity: delegation.Subject (_caller) == ccap.Issuer (_caller) ✓
    /// </summary>
    private SignedAssertion CreateCallerCCap(string resource, string action)
    {
        return AssertionBuilder.CreateCapability(
            _caller, _caller.PublicKeyBytes,
            resource, action,
            proofs: new[] { _delegation.Id.ToArray() });
    }

    [Fact]
    public async Task ValidCCap_Succeeds()
    {
        var ccap = CreateCallerCCap("scynapse.app.ISecureTestGrain", "read");
        await _store.StoreAsync(ccap);

        var filter = CreateFilter();
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));
        // Capture verified caller key at the grain execution point (inside Invoke)
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.VerifiedCallerKeyKey);

        AttachSecurityContext(_caller, ccap);
        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);
        var verifiedKey = ctx.CapturedRequestContext[ScynapseSecurityConstants.VerifiedCallerKeyKey] as byte[];
        Assert.NotNull(verifiedKey);
        Assert.True(verifiedKey.AsSpan().SequenceEqual(_caller.PublicKeyBytes));

        RequestContext.Clear();
    }

    [Fact]
    public async Task MissingCCap_Rejected()
    {
        var filter = CreateFilter();
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));

        RequestContext.Clear();

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.False(ctx.Invoked);
        Assert.Contains("Authentication required", ex.Message);

        RequestContext.Clear();
    }

    [Fact]
    public async Task ExpiredCCap_Rejected()
    {
        var expiredCcap = new AssertionBuilder()
            .SetIssuer(_caller)
            .SetSubject(_caller.PublicKeyBytes)
            .SetClaim(ClaimType.Capability,
                new CapabilityClaim("scynapse.app.ISecureTestGrain", "read").Serialize())
            .SetScope(expiresAt: DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds())
            .AddProof(_delegation.Id.Span)
            .Build();
        await _store.StoreAsync(expiredCcap);

        var filter = CreateFilter();
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));
        AttachSecurityContext(_caller, expiredCcap);

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.False(ctx.Invoked);

        RequestContext.Clear();
    }

    [Fact]
    public async Task WrongAction_Rejected()
    {
        // CCap grants "write" but method requires "read"
        var ccap = CreateCallerCCap("scynapse.app.ISecureTestGrain", "write");
        await _store.StoreAsync(ccap);

        var filter = CreateFilter();
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));
        AttachSecurityContext(_caller, ccap);

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.Contains("Insufficient capability", ex.Message);
        Assert.False(ctx.Invoked);

        RequestContext.Clear();
    }

    [Fact]
    public async Task AnonymousGrain_AllowsWithoutCCap()
    {
        var filter = CreateFilter();
        var ctx = CreateContext(typeof(IAnonymousTestGrain), nameof(IAnonymousTestGrain.GetPublicDataAsync));

        RequestContext.Clear();
        await filter.Invoke(ctx);

        Assert.True(ctx.Invoked);

        RequestContext.Clear();
    }

    [Fact]
    public async Task BearerProofFailure_Rejected()
    {
        var ccap = CreateCallerCCap("scynapse.app.ISecureTestGrain", "read");
        await _store.StoreAsync(ccap);

        // Use a different key to sign the bearer proof (wrong key)
        var wrongKey = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);

        var ccapBytes = ccap.Serialize();
        var badBearerProof = wrongKey.Sign(ccap.Id.Span);

        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, _caller.PublicKeyBytes.ToArray());
        RequestContext.Set(ScynapseSecurityConstants.CCapKey, ccapBytes);
        RequestContext.Set(ScynapseSecurityConstants.BearerProofKey, badBearerProof);

        var filter = CreateFilter();
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.Contains("Bearer verification failed", ex.Message);
        Assert.False(ctx.Invoked);

        RequestContext.Clear();
    }

    [Fact]
    public async Task DefaultPolicyGrain_AllowsAnonymous()
    {
        // Grains without [SecurityPolicy] attribute default to anonymous
        // (so internal Orleans system grains work without security context)
        var filter = CreateFilter();
        var ctx = CreateContext(typeof(IDefaultPolicyTestGrain), nameof(IDefaultPolicyTestGrain.DoSomethingAsync));

        RequestContext.Clear();

        await filter.Invoke(ctx);
        Assert.True(ctx.Invoked);

        RequestContext.Clear();
    }

    [Fact]
    public async Task MethodWithNoCapabilityAttr_AuthenticatedCallSucceeds()
    {
        // BasicActionAsync has no [RequireCapability] — any authenticated caller can call it
        var ccap = CreateCallerCCap("scynapse.app.IPartiallySecuredGrain", "anything");
        await _store.StoreAsync(ccap);

        var filter = CreateFilter();
        var ctx = CreateContext(typeof(IPartiallySecuredGrain), nameof(IPartiallySecuredGrain.BasicActionAsync));
        AttachSecurityContext(_caller, ccap);

        await filter.Invoke(ctx);
        Assert.True(ctx.Invoked);

        RequestContext.Clear();
    }

    // ---- Hybrid model tests: Node trust ----

    private ScynapseIncomingCallFilter CreateFilterWithTrustedNodes(params byte[][] nodeKeys)
    {
        var trustedNodes = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        foreach (var key in nodeKeys)
            trustedNodes.Add(key);

        return new ScynapseIncomingCallFilter(
            _store, _nonceStore, _trustedRoots, _policyProvider,
            trustedNodeKeys: trustedNodes);
    }

    [Fact]
    public async Task TrustedNode_AllowedWithoutCCap()
    {
        // A trusted node calling a secured grain should pass without CCap
        var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var filter = CreateFilterWithTrustedNodes(nodeKey.PublicKeyBytes.ToArray());
        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));
        ctx.KeysToCapture.Add(ScynapseSecurityConstants.VerifiedCallerKeyKey);

        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, nodeKey.PublicKeyBytes.ToArray());
        // No CCap, no bearer proof

        await filter.Invoke(ctx);
        Assert.True(ctx.Invoked);

        var verifiedKey = ctx.CapturedRequestContext[ScynapseSecurityConstants.VerifiedCallerKeyKey] as byte[];
        Assert.NotNull(verifiedKey);
        Assert.True(verifiedKey.AsSpan().SequenceEqual(nodeKey.PublicKeyBytes));

        RequestContext.Clear();
    }

    [Fact]
    public async Task UntrustedNode_RequiresCCap()
    {
        // An untrusted caller (not in trusted nodes) with no CCap should be rejected
        var unknownKey = ScynapseKeyPair.Generate(ScynapseKeyType.Instance);
        var filter = CreateFilterWithTrustedNodes(); // no trusted nodes

        var ctx = CreateContext(typeof(ISecureTestGrain), nameof(ISecureTestGrain.GetDataAsync));
        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, unknownKey.PublicKeyBytes.ToArray());
        // No CCap

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.Contains("Authentication required", ex.Message);

        RequestContext.Clear();
    }

    [Fact]
    public async Task StrictGrain_RejectsTrustedNodeWithoutCCap()
    {
        // A grain with RequiresCallerCapability=true should require CCap even from trusted nodes
        var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var filter = CreateFilterWithTrustedNodes(nodeKey.PublicKeyBytes.ToArray());
        var ctx = CreateContext(typeof(IStrictSecureTestGrain), nameof(IStrictSecureTestGrain.TransferAsync));

        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, nodeKey.PublicKeyBytes.ToArray());
        // No CCap

        var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
            () => filter.Invoke(ctx));
        Assert.Contains("Authentication required", ex.Message);

        RequestContext.Clear();
    }
}

// Grain with RequiresCallerCapability = true (strict mode)
[SecurityPolicy(RequiresAuthentication = true, RequiresCallerCapability = true)]
public interface IStrictSecureTestGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "transfer")]
    Task TransferAsync();
}
