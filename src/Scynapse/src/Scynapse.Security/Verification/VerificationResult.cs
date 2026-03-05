using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// Result of assertion verification.
/// </summary>
public sealed record VerificationResult(
    bool IsValid,
    string? FailureReason = null,
    SignedAssertion? FailedAssertion = null)
{
    internal static VerificationResult Valid() => new(true);

    internal static VerificationResult Invalid(string reason, SignedAssertion? failedAssertion = null)
        => new(false, reason, failedAssertion);
}
