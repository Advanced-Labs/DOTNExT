using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// Maps grain calls to appropriate CCaps. Used by the outgoing call filter
/// to select the right capability for each grain invocation.
/// </summary>
public interface ICCapWallet
{
    /// <summary>
    /// Find a valid CCap that authorizes the given action on the given resource.
    /// Returns null if no matching CCap is available.
    /// </summary>
    SignedAssertion? FindCapability(string resource, string action);

    /// <summary>
    /// Store a CCap (received from a grain, peer, or created locally).
    /// </summary>
    void Store(SignedAssertion ccap);

    /// <summary>
    /// Remove expired CCaps.
    /// </summary>
    void Cleanup();
}
