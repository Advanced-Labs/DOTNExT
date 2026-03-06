using Scynapse.Runtime;
using Scynapse.Security;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Outgoing grain call filter: selects the appropriate CCap from the wallet
/// for each grain call, attaches caller identity, CCap, and bearer proof
/// to RequestContext.
/// </summary>
public sealed class ScynapseOutgoingCallFilter : IOutgoingGrainCallFilter
{
    private readonly ScynapseKeyPair _nodeKeyPair;
    private readonly ICCapWallet _wallet;

    public ScynapseOutgoingCallFilter(ScynapseKeyPair nodeKeyPair, ICCapWallet wallet)
    {
        _nodeKeyPair = nodeKeyPair;
        _wallet = wallet;
    }

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // Derive resource from target grain interface
        var grainInterfaceType = context.InterfaceMethod.DeclaringType;
        var resource = grainInterfaceType != null
            ? GrainResourceInference.FromGrainInterface(grainInterfaceType)
            : GrainResourceInference.WildcardAll;

        // Derive action from [RequireCapability] attribute or default to method name
        var reqCapAttr = context.InterfaceMethod
            .GetCustomAttributes(typeof(RequireCapabilityAttribute), inherit: true)
            .OfType<RequireCapabilityAttribute>()
            .FirstOrDefault();
        var action = reqCapAttr?.Action ?? context.InterfaceMethod.Name;

        // Find matching CCap in wallet
        var ccap = _wallet.FindCapability(resource, action);
        if (ccap != null)
        {
            RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey,
                _nodeKeyPair.PublicKeyBytes.ToArray());
            RequestContext.Set(ScynapseSecurityConstants.CCapKey,
                ccap.Serialize());
            RequestContext.Set(ScynapseSecurityConstants.BearerProofKey,
                _nodeKeyPair.Sign(ccap.Id.Span));
        }
        else
        {
            // Attach identity even without a CCap (for anonymous-allowed grains)
            RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey,
                _nodeKeyPair.PublicKeyBytes.ToArray());
        }

        await context.Invoke();
    }
}
