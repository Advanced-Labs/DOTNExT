using Scynapse.Providers.Streams.Common;

namespace ServiceBus.Tests.MonitorTests
{
    public class CacheMonitorForTesting : ICacheMonitor
    {
        public CacheMonitorCounters CallCounters { get; } = new CacheMonitorCounters();
        
        public void TrackCachePressureMonitorStatusChange(string pressureMonitorType, bool underPressure, double? cachePressureContributionCount, double? currentPressure,
            double? flowControlThreshold)
        {
            Interlocked.Increment(ref CallCounters.TrackCachePressureMonitorStatusChangeCallCounter);
        }

        public void ReportCacheSize(long totalCacheSizeInByte)
        {
            Interlocked.Increment(ref CallCounters.ReportCacheSizeCallCounter);
        }

        public void ReportMessageStatistics(DateTime? oldestMessageEnqueueTimeUtc, DateTime? oldestMessageDequeueTimeUtc, DateTime? newestMessageEnqueueTimeUtc, long totalMessageCount)
        {
            Interlocked.Increment(ref CallCounters.ReportMessageStatisticsCallCounter);
        }

        public void TrackMemoryAllocated(int memoryInByte)
        {
            Interlocked.Increment(ref CallCounters.TrackMemoryAllocatedCallCounter);
        }

        public void TrackMemoryReleased(int memoryInByte)
        {
            Interlocked.Increment(ref CallCounters.TrackMemoryReleasedCallCounter);
        }

        public void TrackMessagesAdded(long mesageAdded)
        {
            Interlocked.Increment(ref CallCounters.TrackMessageAddedCounter);
        }

        public void TrackMessagesPurged(long messagePurged)
        {
            Interlocked.Increment(ref CallCounters.TrackMessagePurgedCounter);
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class CacheMonitorCounters
    {
        [Scynapse.Id(0)]
        public int TrackCachePressureMonitorStatusChangeCallCounter;
        [Scynapse.Id(1)]
        public int ReportCacheSizeCallCounter;
        [Scynapse.Id(2)]
        public int ReportMessageStatisticsCallCounter;
        [Scynapse.Id(3)]
        public int TrackMemoryAllocatedCallCounter;
        [Scynapse.Id(4)]
        public int TrackMemoryReleasedCallCounter;
        [Scynapse.Id(5)]
        public int TrackMessageAddedCounter;
        [Scynapse.Id(6)]
        public int TrackMessagePurgedCounter;
    }
}
