using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.Internal;
using Scynapse.Placement.Rebalancing;

namespace Scynapse.Runtime.Placement.Rebalancing;

internal sealed class FailedSessionBackoffProvider(IOptions<ActivationRebalancerOptions> options)
    : FixedBackoff(options.Value.SessionCyclePeriod), IFailedSessionBackoffProvider;