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
    private readonly ISecurityFlowTraceSink? _traceSink;

    public ScynapseOutgoingCallFilter(
        ScynapseKeyPair nodeKeyPair,
        ICCapWallet wallet,
        ISecurityFlowTraceSink? traceSink = null)
    {
        _nodeKeyPair = nodeKeyPair;
        _wallet = wallet;
        _traceSink = traceSink;
    }

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        var grainInterfaceName = context.InterfaceMethod.DeclaringType?.Name ?? "UnknownInterface";
        var methodName = context.InterfaceMethod.Name;
        _traceSink?.Emit(new SecurityFlowTraceEvent(
            SecurityFlowTraceNames.OutgoingContextStart,
            GrainInterface: grainInterfaceName,
            Method: methodName));

        // Preserve OriginalCallerKey if already present (grain-to-grain call).
        // If not present, this is a client-originated call — the caller key becomes the original caller.
        var existingOriginalCaller = RequestContext.Get(ScynapseSecurityConstants.OriginalCallerKeyKey);
        if (existingOriginalCaller is null)
        {
            // First hop — set the original caller to our identity
            RequestContext.Set(ScynapseSecurityConstants.OriginalCallerKeyKey,
                _nodeKeyPair.PublicKeyBytes.ToArray());
        }
        // If already present, don't overwrite — preserve the end-user's identity through the call chain

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
        _traceSink?.Emit(new SecurityFlowTraceEvent(
            SecurityFlowTraceNames.OutgoingWalletLookup,
            GrainInterface: grainInterfaceName,
            Method: methodName,
            Details: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["resource"] = resource,
                ["action"] = action,
                ["found"] = (ccap is not null).ToString()
            }));

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
            // Attach identity even without a CCap (for node-trusted or anonymous grains)
            RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey,
                _nodeKeyPair.PublicKeyBytes.ToArray());
        }

        _traceSink?.Emit(new SecurityFlowTraceEvent(
            SecurityFlowTraceNames.OutgoingContextAttached,
            GrainInterface: grainInterfaceName,
            Method: methodName,
            Details: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["has_caller_key"] = "true",
                ["has_ccap"] = (ccap is not null).ToString(),
                ["has_bearer_proof"] = (ccap is not null).ToString()
            }));

        await context.Invoke();
    }
}
