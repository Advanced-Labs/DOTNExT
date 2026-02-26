using System;
using System.Threading.Tasks;
using Scynapse.DurableJobs;

namespace UnitTests.GrainInterfaces;

public interface ISchedulerGrain : IGrainWithStringKey
{
    Task<DurableJob> ScheduleJobOnAnotherGrainAsync(string targetGrainKey, string jobName, DateTimeOffset scheduledTime);
}
