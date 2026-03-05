using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Hooks into ServiceLifecycleStage.First to initialize security infrastructure
/// before networking starts. Loads bootstrap assertions, peer assertions,
/// and bootstrap capabilities.
/// </summary>
public sealed class ScynapseSecurityLifecycleParticipant : ILifecycleParticipant<ISiloLifecycle>
{
    private readonly ScynapseSecurityOptions _options;
    private readonly IAssertionStore _store;
    private readonly ICCapWallet? _wallet;

    public ScynapseSecurityLifecycleParticipant(
        ScynapseSecurityOptions options,
        IAssertionStore store,
        ICCapWallet? wallet = null)
    {
        _options = options;
        _store = store;
        _wallet = wallet;
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe<ScynapseSecurityLifecycleParticipant>(
            ServiceLifecycleStage.First,
            OnStart);
    }

    private async Task OnStart(CancellationToken ct)
    {
        // Load this node's own delegation chain
        foreach (var assertion in _options.BootstrapAssertions)
        {
            await _store.StoreAsync(assertion);
        }

        // Load known peer assertions (so we can verify their TLS certs)
        foreach (var assertion in _options.PeerAssertions)
        {
            await _store.StoreAsync(assertion);
        }

        // Load pre-granted capabilities into the wallet
        if (_wallet != null)
        {
            foreach (var ccap in _options.BootstrapCapabilities)
            {
                _wallet.Store(ccap);
            }
        }
    }
}
