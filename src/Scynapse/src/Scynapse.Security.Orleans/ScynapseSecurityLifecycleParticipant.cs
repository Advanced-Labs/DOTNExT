using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Hooks into ServiceLifecycleStage.First to initialize security infrastructure
/// before networking starts. Loads bootstrap assertions into the store.
/// </summary>
public sealed class ScynapseSecurityLifecycleParticipant : ILifecycleParticipant<ISiloLifecycle>
{
    private readonly ScynapseSecurityOptions _options;
    private readonly IAssertionStore _store;

    public ScynapseSecurityLifecycleParticipant(ScynapseSecurityOptions options, IAssertionStore store)
    {
        _options = options;
        _store = store;
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe<ScynapseSecurityLifecycleParticipant>(
            ServiceLifecycleStage.First,
            OnStart);
    }

    private async Task OnStart(CancellationToken ct)
    {
        // Load bootstrap assertions into the store before networking starts
        foreach (var assertion in _options.BootstrapAssertions)
        {
            await _store.StoreAsync(assertion);
        }
    }
}
