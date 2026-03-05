using System.Text.RegularExpressions;
using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// Default attenuation checker covering:
/// - Identity → anything (root identities delegate freely)
/// - Delegation → Capability (check allowed claim types, resource/action patterns)
/// - Delegation → Delegation (check narrowing: allowed types subset, patterns narrower, depth decremented)
/// - Temporal attenuation (child bounds must be within parent bounds)
/// </summary>
public sealed class DefaultAttenuationChecker : IAttenuationChecker
{
    public bool Check(SignedAssertion parent, SignedAssertion child)
    {
        // Temporal attenuation: child's time bounds must be within parent's
        if (!CheckTemporalAttenuation(parent, child))
            return false;

        // Identity assertions can delegate anything (they are root authority)
        if (parent.ClaimType == ClaimType.Identity)
            return true;

        // Delegation → child: check the delegation's scope authorizes the child
        if (parent.ClaimType == ClaimType.Delegation)
            return CheckDelegationScope(parent, child);

        // Capability assertions cannot delegate (they are leaf grants)
        // Relation/Revocation/Impersonation: no delegation semantics defined yet
        return false;
    }

    private static bool CheckTemporalAttenuation(SignedAssertion parent, SignedAssertion child)
    {
        // Child's not_before must be >= parent's not_before (if parent has one)
        if (parent.NotBefore.HasValue && child.NotBefore.HasValue)
        {
            if (child.NotBefore.Value < parent.NotBefore.Value)
                return false;
        }
        else if (parent.NotBefore.HasValue && !child.NotBefore.HasValue)
        {
            // Parent restricts start time but child doesn't — child is broader
            return false;
        }

        // Child's expires_at must be <= parent's expires_at (if parent has one)
        if (parent.ExpiresAt.HasValue && child.ExpiresAt.HasValue)
        {
            if (child.ExpiresAt.Value > parent.ExpiresAt.Value)
                return false;
        }
        else if (parent.ExpiresAt.HasValue && !child.ExpiresAt.HasValue)
        {
            // Parent expires but child doesn't — child is broader
            return false;
        }

        return true;
    }

    private static bool CheckDelegationScope(SignedAssertion parent, SignedAssertion child)
    {
        var delegation = DelegationClaim.Deserialize(parent.ClaimData.Span);

        // Check that the child's claim type is allowed by the delegation
        if (!delegation.AllowedClaimTypes.Contains(child.ClaimType))
            return false;

        if (child.ClaimType == ClaimType.Capability)
            return CheckCapabilityAttenuation(delegation, child);

        if (child.ClaimType == ClaimType.Delegation)
            return CheckDelegationNarrowing(delegation, child);

        // Other claim types: allowed if listed in AllowedClaimTypes (already checked above)
        return true;
    }

    private static bool CheckCapabilityAttenuation(DelegationClaim delegation, SignedAssertion child)
    {
        var capability = CapabilityClaim.Deserialize(child.ClaimData.Span);

        // Check resource pattern (if delegation restricts it)
        if (delegation.ResourcePattern != null)
        {
            if (!MatchesPattern(delegation.ResourcePattern, capability.Resource))
                return false;
        }

        // Check action pattern (if delegation restricts it)
        if (delegation.ActionPattern != null)
        {
            if (!MatchesPattern(delegation.ActionPattern, capability.Action))
                return false;
        }

        return true;
    }

    private static bool CheckDelegationNarrowing(DelegationClaim parent, SignedAssertion child)
    {
        var childDelegation = DelegationClaim.Deserialize(child.ClaimData.Span);

        // Child's allowed claim types must be a subset of parent's
        foreach (var ct in childDelegation.AllowedClaimTypes)
        {
            if (!parent.AllowedClaimTypes.Contains(ct))
                return false;
        }

        // If parent has resource pattern, child must also have one and it must be narrower or equal
        if (parent.ResourcePattern != null)
        {
            if (childDelegation.ResourcePattern == null)
                return false; // Child is broader (unrestricted)
            // Child pattern must be at least as restrictive (equal or more specific)
            // Simple check: child pattern must match within parent pattern
            if (!MatchesPattern(parent.ResourcePattern, childDelegation.ResourcePattern))
                return false;
        }

        if (parent.ActionPattern != null)
        {
            if (childDelegation.ActionPattern == null)
                return false;
            if (!MatchesPattern(parent.ActionPattern, childDelegation.ActionPattern))
                return false;
        }

        // MaxDepth: child's must be strictly less (or both null)
        if (parent.MaxDepth.HasValue)
        {
            if (!childDelegation.MaxDepth.HasValue)
                return false; // Parent limits depth, child doesn't
            if (childDelegation.MaxDepth.Value >= parent.MaxDepth.Value)
                return false; // Must be strictly less
        }

        return true;
    }

    /// <summary>
    /// Simple glob-style pattern matching. Supports '*' as wildcard.
    /// "scynapse:grain/*" matches "scynapse:grain/MyGrain".
    /// </summary>
    public static bool MatchesPattern(string pattern, string value)
    {
        // Convert glob to regex: escape everything except *, then replace * with .*
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regexPattern);
    }
}
