using Scynapse.Providers.Streams.Common;

namespace ServiceBus.Tests.MonitorTests
{
    public class BlockPoolMonitorForTesting : IBlockPoolMonitor
    {
        public ObjectPoolMonitorCounters CallCounters { get; } = new ObjectPoolMonitorCounters();
 
        public void TrackMemoryAllocated(long allocatedMemoryInByte)
        {
            Interlocked.Increment(ref this.CallCounters.TrackObjectAllocatedByCacheCallCounter);
        }

        public void TrackMemoryReleased(long releasedMemoryInByte)
        {
            Interlocked.Increment(ref this.CallCounters.TrackObjectReleasedFromCacheCallCounter);
        }

        public void Report(long totalMemoryInByte, long availableMemoryInByte, long claimedMemoryInByte)
        {
            Interlocked.Increment(ref this.CallCounters.ReportCallCounter);
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class ObjectPoolMonitorCounters
    {
        [Scynapse.Id(0)]
        public int TrackObjectAllocatedByCacheCallCounter;
        [Scynapse.Id(1)]
        public int TrackObjectReleasedFromCacheCallCounter;
        [Scynapse.Id(2)]
        public int ReportCallCounter;
    }
}
