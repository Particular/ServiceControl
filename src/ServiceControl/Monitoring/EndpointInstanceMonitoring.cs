namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointInstanceMonitoring
    {
        private ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        private ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor>();

        public void RecordHeartbeat(EndpointInstanceId endpointInstanceId, DateTime timestamp) => heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor()).MarkAlive(timestamp);

        public void CheckEndpoints(DateTime threshold)
        {
            foreach (var entry in heartbeats)
            {
                var recordedHeartbeat = entry.Value.MarkDeadIfOlderThan(threshold);

                var monitor = GetOrCreateMonitor(entry.Key, true);

                monitor.UpdateStatus(recordedHeartbeat.Status, recordedHeartbeat.Timestamp);
            }

            var stats = GetStats();

            Update(stats);
        }

        public EndpointInstanceMonitor GetOrCreateMonitor(EndpointInstanceId endpointInstanceId, bool monitorIfNew)
        {
            return endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, monitorIfNew));
        }

        private void Update(EndpointMonitoringStats stats)
        {
            var previousActive = previousStats?.Active ?? 0;
            var previousDead = previousStats?.Failing ?? 0;
            if (previousActive != stats.Active || previousDead != stats.Failing)
            {
                DomainEvents.Raise(new HeartbeatsUpdated
                {
                    Active = stats.Active,
                    Failing = stats.Failing,
                    RaisedAt = DateTime.UtcNow
                });
                previousStats = stats;
            }
        }

        private EndpointMonitoringStats previousStats;

        public EndpointMonitoringStats GetStats()
        {
            var stats = new EndpointMonitoringStats();
            foreach (var monitor in endpoints.Values)
            {
                monitor.AddTo(stats);
            }
            return stats;
        }

        public void EnableMonitoring(Guid id) => endpoints[id]?.EnableMonitoring();
        public void DisableMonitoring(Guid id) => endpoints[id]?.DisableMonitoring();
        public bool IsMonitored(Guid id) => endpoints[id]?.Monitored ?? false;

        public EndpointsView[] GetEndpoints()
        {
            var list = new List<EndpointsView>();

            var heartbeatLookup = heartbeats.ToLookup(x => x.Key, x => x.Value);

            foreach (var endpoint in endpoints.Values)
            {
                var view = endpoint.GetView();
                view.IsSendingHeartbeats = heartbeatLookup[endpoint.Id].Any(x => x.IsSendingHeartbeats());
                list.Add(view);
            }

            return list.ToArray();
        }

        public IList<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }
    }
}