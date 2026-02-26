using System.Collections.Generic;
using System.Threading.Tasks;
using Scynapse.Concurrency;
using Scynapse.Runtime;
using Scynapse.Services;
using Scynapse.Dashboard.Model;

namespace Scynapse.Dashboard.Core;

internal interface ISiloGrainService : IGrainService
{
    Task SetVersion(string scynapse, string host);

    [OneWay]
    Task ReportCounters(Immutable<StatCounter[]> stats);

    Task Enable(bool enabled);

    Task<Immutable<Dictionary<string, string>>> GetExtendedProperties();

    Task<Immutable<SiloRuntimeStatistics[]>> GetRuntimeStatistics();

    Task<Immutable<StatCounter[]>> GetCounters();
}
