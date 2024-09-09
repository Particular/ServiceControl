namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using Setting = Settings;

    public class EndpointInstanceActivityTracker
    {
        public EndpointInstanceActivityTracker(Setting settings, TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
            StalenessThreshold = settings.EndpointUptimeGracePeriod;
            ExpiredThreshold = TimeSpan.FromMinutes(HistoryPeriod.LargestHistoryPeriod) + StalenessThreshold;
        }

        public void Record(EndpointInstanceId instanceId, DateTime utcNow)
        {
            endpointsInstances.AddOrUpdate(instanceId, utcNow, (_, __) => utcNow);
        }

        public void Remove(EndpointInstanceId instanceId)
        {
            endpointsInstances.TryRemove(instanceId, out _);
        }

        public bool IsStale(EndpointInstanceId endpointInstance)
        {
            if (endpointsInstances.TryGetValue(endpointInstance, out var lastActivityTime))
            {
                TimeSpan age = timeProvider.GetUtcNow().UtcDateTime - lastActivityTime;
                return age > StalenessThreshold;
            }

            return true;
        }

        public bool IsExpired(EndpointInstanceId endpointInstance)
        {
            if (endpointsInstances.TryGetValue(endpointInstance, out DateTime lastActivityTime))
            {
                TimeSpan age = timeProvider.GetUtcNow().UtcDateTime - lastActivityTime;
                return age > ExpiredThreshold;
            }

            return true;
        }

        internal readonly TimeSpan ExpiredThreshold;
        internal readonly TimeSpan StalenessThreshold;
        readonly TimeProvider timeProvider;
        readonly ConcurrentDictionary<EndpointInstanceId, DateTime> endpointsInstances = new();
    }
}