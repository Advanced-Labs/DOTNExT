using System.Collections.Generic;
using System.Threading.Tasks;
using Scynapse.Concurrency;
using Scynapse.Placement;
using Scynapse.Runtime;
using Scynapse.Dashboard.Model;
using Scynapse.Dashboard.Core;

namespace Scynapse.Dashboard.Implementation.Grains;

[PreferLocalPlacement]
internal sealed class SiloGrainProxy : Grain, ISiloGrainProxy
{
    private readonly ISiloGrainService _siloGrainService;

    public SiloGrainProxy(ISiloGrainClient siloGrainClient)
    {
        _siloGrainService = siloGrainClient.GrainService(
            SiloAddress.FromParsableString(this.GetPrimaryKeyString())
        );
    }

    public Task SetVersion(string scynapse, string host) => _siloGrainService.SetVersion(scynapse, host);

    public Task ReportCounters(Immutable<StatCounter[]> stats) => _siloGrainService.ReportCounters(stats);

    public Task Enable(bool enabled) => _siloGrainService.Enable(enabled);

    public Task<Immutable<Dictionary<string, string>>> GetExtendedProperties() => _siloGrainService.GetExtendedProperties();

    public Task<Immutable<SiloRuntimeStatistics[]>> GetRuntimeStatistics() => _siloGrainService.GetRuntimeStatistics();

    public Task<Immutable<StatCounter[]>> GetCounters() => _siloGrainService.GetCounters();
}
