using System.Text;
using Scynapse.Security.Orleans;
using Xunit;

namespace Scynapse.Security.Integration.Tests;

internal enum BridgeMismatchClass
{
    ImplementationMismatch,
    HarnessMismatch,
    ContractAmbiguity
}

internal sealed record BridgeComparisonIssue(
    BridgeMismatchClass Classification,
    string Message,
    string? Token = null);

internal sealed record SecurityTraceComparisonSpec(
    string Name,
    IReadOnlyCollection<string> RequiredTokens,
    IReadOnlyCollection<string>? ForbiddenTokens = null);

internal sealed class SecurityTraceComparisonResult
{
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> ObservedTokens { get; init; }
    public required IReadOnlyCollection<BridgeComparisonIssue> Issues { get; init; }
    public bool IsMatch => Issues.Count == 0;
}

internal static class SecurityTraceBridgeTokens
{
    public const string OutgoingWalletLookupFoundTrue = "OUTGOING.WALLET_LOOKUP.FOUND_TRUE";
    public const string OutgoingWalletLookupFoundFalse = "OUTGOING.WALLET_LOOKUP.FOUND_FALSE";

    public const string IncomingChainVerifySuccess = "INCOMING.CHAIN_VERIFY.SUCCESS";
    public const string IncomingChainVerifyFail = "INCOMING.CHAIN_VERIFY.FAIL";

    public const string IncomingCapabilityMatchSuccess = "INCOMING.CAPABILITY_MATCH.SUCCESS";
    public const string IncomingCapabilityMatchFail = "INCOMING.CAPABILITY_MATCH.FAIL";

    public const string TerminalAllow = "TERMINAL.ALLOW";
    public const string TerminalDeny = "TERMINAL.DENY";

    public static string TerminalDenyCode(SecurityFailureCode code) => $"TERMINAL.DENY.CODE.{code}";
}

internal static class SecurityTraceBridgeComparator
{
    public static SecurityTraceComparisonResult Compare(
        IReadOnlyList<SecurityFlowTraceEvent> trace,
        SecurityTraceComparisonSpec spec)
    {
        var observed = ExtractTokens(trace);
        var issues = new List<BridgeComparisonIssue>();

        foreach (var required in spec.RequiredTokens)
        {
            if (!observed.Contains(required))
            {
                issues.Add(new BridgeComparisonIssue(
                    ClassifyMissingToken(required),
                    $"Missing required token '{required}' for spec '{spec.Name}'.",
                    required));
            }
        }

        if (spec.ForbiddenTokens is not null)
        {
            foreach (var forbidden in spec.ForbiddenTokens)
            {
                if (observed.Contains(forbidden))
                {
                    issues.Add(new BridgeComparisonIssue(
                        BridgeMismatchClass.ImplementationMismatch,
                        $"Observed forbidden token '{forbidden}' for spec '{spec.Name}'.",
                        forbidden));
                }
            }
        }

        if (!observed.Any(token => token.StartsWith("TERMINAL.", StringComparison.Ordinal)))
        {
            issues.Add(new BridgeComparisonIssue(
                BridgeMismatchClass.HarnessMismatch,
                "No terminal token was emitted by the runtime trace.",
                "TERMINAL.*"));
        }

        return new SecurityTraceComparisonResult
        {
            Name = spec.Name,
            ObservedTokens = observed.OrderBy(token => token, StringComparer.Ordinal).ToArray(),
            Issues = issues
        };
    }

    public static void AssertMatch(SecurityTraceComparisonResult result)
    {
        if (result.IsMatch)
        {
            return;
        }

        var buffer = new StringBuilder();
        buffer.AppendLine($"Trace comparison failed for '{result.Name}'.");
        buffer.AppendLine("Issues:");
        foreach (var issue in result.Issues)
        {
            buffer.AppendLine($"- [{issue.Classification}] {issue.Message}");
        }
        buffer.AppendLine("Observed tokens:");
        foreach (var token in result.ObservedTokens)
        {
            buffer.AppendLine($"- {token}");
        }

        Assert.Fail(buffer.ToString());
    }

    private static HashSet<string> ExtractTokens(IReadOnlyList<SecurityFlowTraceEvent> trace)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var evt in trace)
        {
            tokens.Add($"EVENT.{evt.Name}");

            if (string.Equals(evt.Name, SecurityFlowTraceNames.OutgoingWalletLookup, StringComparison.Ordinal))
            {
                if (TryGetDetail(evt, "found", out var found))
                {
                    tokens.Add(found ? SecurityTraceBridgeTokens.OutgoingWalletLookupFoundTrue : SecurityTraceBridgeTokens.OutgoingWalletLookupFoundFalse);
                }
            }

            if (string.Equals(evt.Name, SecurityFlowTraceNames.IncomingChainVerify, StringComparison.Ordinal))
            {
                if (TryGetDetail(evt, "success", out var success))
                {
                    tokens.Add(success ? SecurityTraceBridgeTokens.IncomingChainVerifySuccess : SecurityTraceBridgeTokens.IncomingChainVerifyFail);
                }
            }

            if (string.Equals(evt.Name, SecurityFlowTraceNames.IncomingCapabilityMatch, StringComparison.Ordinal))
            {
                if (TryGetDetail(evt, "success", out var success))
                {
                    tokens.Add(success ? SecurityTraceBridgeTokens.IncomingCapabilityMatchSuccess : SecurityTraceBridgeTokens.IncomingCapabilityMatchFail);
                }
            }

            if (string.Equals(evt.Name, SecurityFlowTraceNames.IncomingTerminal, StringComparison.Ordinal))
            {
                if (TryGetOutcome(evt, out var outcome))
                {
                    if (outcome.Equals("allow", StringComparison.OrdinalIgnoreCase))
                    {
                        tokens.Add(SecurityTraceBridgeTokens.TerminalAllow);
                    }
                    else if (outcome.Equals("deny", StringComparison.OrdinalIgnoreCase))
                    {
                        tokens.Add(SecurityTraceBridgeTokens.TerminalDeny);
                        if (evt.FailureCode.HasValue)
                        {
                            tokens.Add(SecurityTraceBridgeTokens.TerminalDenyCode(evt.FailureCode.Value));
                        }
                    }
                }
            }
        }

        return tokens;
    }

    private static BridgeMismatchClass ClassifyMissingToken(string token)
    {
        if (token.StartsWith("HANDSHAKE.", StringComparison.Ordinal) ||
            token.StartsWith("ROUTE.", StringComparison.Ordinal) ||
            token.StartsWith("MEDIATION.", StringComparison.Ordinal))
        {
            return BridgeMismatchClass.ContractAmbiguity;
        }

        return BridgeMismatchClass.ImplementationMismatch;
    }

    private static bool TryGetDetail(SecurityFlowTraceEvent evt, string key, out bool value)
    {
        value = false;
        if (evt.Details is null || !evt.Details.TryGetValue(key, out var raw))
        {
            return false;
        }

        return bool.TryParse(raw, out value);
    }

    private static bool TryGetOutcome(SecurityFlowTraceEvent evt, out string outcome)
    {
        outcome = string.Empty;
        if (evt.Details is null || !evt.Details.TryGetValue("outcome", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        outcome = raw;
        return true;
    }
}
