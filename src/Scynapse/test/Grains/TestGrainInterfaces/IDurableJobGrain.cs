using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scynapse.Concurrency;
using Scynapse.DurableJobs;

namespace UnitTests.GrainInterfaces;

public interface IDurableJobGrain : IGrainWithStringKey
{
    Task<DurableJob> ScheduleJobAsync(string jobName, DateTimeOffset scheduledTime, IReadOnlyDictionary<string, string> metadata = null);

    Task<bool> TryCancelJobAsync(DurableJob job);

    Task<bool> HasJobRan(string jobId);

    [AlwaysInterleave]
    Task WaitForJobToRun(string jobId);

    Task<DateTimeOffset> GetJobExecutionTime(string jobId);

    Task<IDurableJobContext> GetJobContext(string jobId);

    Task<bool> WasCancellationTokenCancelled(string jobId);
}
