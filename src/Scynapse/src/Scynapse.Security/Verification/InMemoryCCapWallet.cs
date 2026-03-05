using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// In-memory CCap wallet. Maps (resource, action) to stored capabilities.
/// Thread-safe. Supports wildcard resource patterns.
/// </summary>
public sealed class InMemoryCCapWallet : ICCapWallet
{
    private readonly List<SignedAssertion> _capabilities = new();
    private readonly object _lock = new();

    public SignedAssertion? FindCapability(string resource, string action)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        lock (_lock)
        {
            foreach (var ccap in _capabilities)
            {
                if (ccap.ClaimType != ClaimType.Capability)
                    continue;

                // Check temporal validity
                if (ccap.ExpiresAt.HasValue && ccap.ExpiresAt.Value < now)
                    continue;
                if (ccap.NotBefore.HasValue && ccap.NotBefore.Value > now)
                    continue;

                var claim = CapabilityClaim.Deserialize(ccap.ClaimData.Span);

                if (ActionMatches(claim.Action, action) &&
                    ResourceMatches(claim.Resource, resource))
                {
                    return ccap;
                }
            }
        }

        return null;
    }

    public void Store(SignedAssertion ccap)
    {
        lock (_lock)
        {
            // Avoid duplicates by content hash
            for (int i = 0; i < _capabilities.Count; i++)
            {
                if (_capabilities[i].Id.Span.SequenceEqual(ccap.Id.Span))
                    return; // already stored
            }
            _capabilities.Add(ccap);
        }
    }

    public void Cleanup()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_lock)
        {
            _capabilities.RemoveAll(ccap =>
                ccap.ExpiresAt.HasValue && ccap.ExpiresAt.Value < now);
        }
    }

    private static bool ActionMatches(string granted, string required)
    {
        if (granted == "*") return true;
        return string.Equals(granted, required, StringComparison.Ordinal);
    }

    private static bool ResourceMatches(string granted, string required)
    {
        if (granted == "*") return true;
        if (granted.EndsWith('*'))
        {
            var prefix = granted[..^1];
            return required.StartsWith(prefix, StringComparison.Ordinal);
        }
        return string.Equals(granted, required, StringComparison.Ordinal);
    }
}
