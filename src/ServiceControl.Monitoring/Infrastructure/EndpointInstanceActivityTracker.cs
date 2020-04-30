﻿namespace ServiceControl.Monitoring.Infrastructure
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
            if (endpointsInstances.TryGetValue(endpointInstance, out var lastActivityTime))
            {
                var age = DateTime.UtcNow - lastActivityTime;
                return age > StalenessThreshold;
            }

            return true;
        }

        internal readonly TimeSpan StalenessThreshold;
        ConcurrentDictionary<EndpointInstanceId, DateTime> endpointsInstances = new ConcurrentDictionary<EndpointInstanceId, DateTime>();
    }
}
