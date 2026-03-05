using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Outgoing grain call filter: attaches caller identity, CCap, and bearer proof
/// to RequestContext on every outgoing grain call.
/// </summary>
public sealed class ScynapseOutgoingCallFilter : IOutgoingGrainCallFilter
{
    private readonly ScynapseKeyPair _nodeKeyPair;
    private readonly SignedAssertion _defaultCCap;

    public ScynapseOutgoingCallFilter(ScynapseKeyPair nodeKeyPair, SignedAssertion defaultCCap)
    {
        _nodeKeyPair = nodeKeyPair;
        _defaultCCap = defaultCCap;
    }

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // Attach security context
        RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey, _nodeKeyPair.PublicKeyBytes.ToArray());
        RequestContext.Set(ScynapseSecurityConstants.CCapKey, _defaultCCap.Serialize());
        RequestContext.Set(ScynapseSecurityConstants.BearerProofKey, _nodeKeyPair.Sign(_defaultCCap.Id.Span));

        await context.Invoke();
    }
}
