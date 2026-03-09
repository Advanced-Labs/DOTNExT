namespace Scynapse.Security.Orleans;

/// <summary>
/// Optional test/diagnostic sink for normalized security-flow events.
/// Production behavior must not depend on this.
/// </summary>
public interface ISecurityFlowTraceSink
{
    void Emit(SecurityFlowTraceEvent traceEvent);
}

/// <summary>
/// Normalized event shape used for bridge diagnostics.
/// </summary>
/// <param name="Name">Deterministic event name.</param>
/// <param name="GrainInterface">Target grain interface name when known.</param>
/// <param name="Method">Target method name when known.</param>
/// <param name="Details">Optional key/value details.</param>
/// <param name="FailureCode">Optional failure code for terminal deny events.</param>
/// <param name="FailureReason">Optional free-form reason (for verifier reasons).</param>
public sealed record SecurityFlowTraceEvent(
    string Name,
    string? GrainInterface = null,
    string? Method = null,
    IReadOnlyDictionary<string, string>? Details = null,
    SecurityFailureCode? FailureCode = null,
    string? FailureReason = null);

/// <summary>
/// Canonical normalized event names for B1 diagnostic mapping.
/// </summary>
public static class SecurityFlowTraceNames
{
    public const string OutgoingContextStart = "OutgoingContextStart";
    public const string OutgoingWalletLookup = "OutgoingWalletLookup";
    public const string OutgoingContextAttached = "OutgoingContextAttached";

    public const string IncomingPolicyResolved = "IncomingPolicyResolved";
    public const string IncomingNodeTrustEvaluated = "IncomingNodeTrustEvaluated";
    public const string IncomingCCapDeserialize = "IncomingCCapDeserialize";
    public const string IncomingChainVerify = "IncomingChainVerify";
    public const string IncomingBearerVerify = "IncomingBearerVerify";
    public const string IncomingCapabilityMatch = "IncomingCapabilityMatch";
    public const string IncomingTerminal = "IncomingTerminal";
}
