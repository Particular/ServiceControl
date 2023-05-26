namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using Setting = Monitoring.Settings;

    public class EndpointInstanceActivityTracker
    {
        public EndpointInstanceActivityTracker(Setting settings)
        {
            StalenessThreshold = settings.EndpointUptimeGracePeriod;
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
            return IsStaleSince(endpointInstance, DateTime.UtcNow);
        }

        public bool IsStaleSince(EndpointInstanceId endpointInstance, DateTime since)
        {
            if (endpointsInstances.TryGetValue(endpointInstance, out var lastActivityTime))
            {
                var age = since - lastActivityTime;
                return age > StalenessThreshold;
            }

            return true;
        }

        internal readonly TimeSpan StalenessThreshold;
        ConcurrentDictionary<EndpointInstanceId, DateTime> endpointsInstances = new ConcurrentDictionary<EndpointInstanceId, DateTime>();
    }
}