namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;

    public class EndpointInstanceActivityTracker
    {
        public EndpointInstanceActivityTracker(Settings settings)
        {
            StalenessThreshold = settings.EndpointUptimeGracePeriod;
        }

        public void Record(EndpointInstanceId instanceId, DateTime utcNow)
        {
            endpointsInstances.AddOrUpdate(instanceId, utcNow, (_, __) => utcNow);
        }

        public bool IsStale(EndpointInstanceId endpointInstance)
        {
            DateTime lastActivityTime;
            if (endpointsInstances.TryGetValue(endpointInstance, out lastActivityTime))
            {
                var timeSpan = DateTime.UtcNow - lastActivityTime;
                return timeSpan > StalenessThreshold;
            }

            return false;
        }

        internal readonly TimeSpan StalenessThreshold;
        ConcurrentDictionary<EndpointInstanceId, DateTime> endpointsInstances = new ConcurrentDictionary<EndpointInstanceId, DateTime>();
    }
}