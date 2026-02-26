using System.Collections.Generic;
using System.Threading.Tasks;
using Scynapse.Concurrency;
using Scynapse.Runtime;
using Scynapse.Dashboard.Model;
using Scynapse.Dashboard.Model.History;

namespace Scynapse.Dashboard.Core;

internal interface IDashboardClient
{
    Task<Immutable<DashboardCounters>> DashboardCounters();

    Task<Immutable<Dictionary<string, GrainTraceEntry>>> ClusterStats();

    Task<Immutable<ReminderResponse>> GetReminders(int pageNumber, int pageSize);

    Task<Immutable<SiloRuntimeStatistics[]>> HistoricalStats(string siloAddress);

    Task<Immutable<Dictionary<string, string>>> SiloProperties(string siloAddress);

    Task<Immutable<Dictionary<string, GrainTraceEntry>>> SiloStats(string siloAddress);

    Task<Immutable<StatCounter[]>> GetCounters(string siloAddress);

    Task<Immutable<Dictionary<string, Dictionary<string, GrainTraceEntry>>>> GrainStats(string grainName);

    Task<Immutable<Dictionary<string, GrainMethodAggregate[]>>> TopGrainMethods(int take);

    Task<Immutable<string>> GetGrainState(string id, string grainType);

    Task<Immutable<string[]>> GetGrainTypes();
}
