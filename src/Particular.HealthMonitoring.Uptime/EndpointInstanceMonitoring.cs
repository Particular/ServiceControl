namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    class EndpointInstanceMonitoring
    {
        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor>();
        EndpointMonitoringStats previousStats;


        public void Initialize(IEnumerable<IHeartbeatEvent> events)
        {
            foreach (var @event in events)
            {
                var monitor = GetOrCreateMonitor(@event.Endpoint.Name, @event.Endpoint.Host, @event.Endpoint.HostId);
                monitor.TryApply(@event);
            }
        }

        public void RecordHeartbeat(string name, string host, Guid hostId, DateTime timestamp)
        {
            var endpointInstanceId = new EndpointInstanceId(name, host, hostId);

            var heartbeatMonitor = heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor());
            heartbeatMonitor.MarkAlive(timestamp);
        }

        public IHeartbeatEvent StartTrackingEndpoint(string name, string host, Guid hostId)
        {
            var monitor = GetOrCreateMonitor(name, host, hostId);
            return monitor.StartTrackingEndpoint();
        }

        public IEnumerable<IDomainEvent> CheckEndpoints(DateTime threshold, DateTime currentTime)
        {
            var events = new List<IDomainEvent>();
            foreach (var entry in heartbeats)
            {
                var instanceId = entry.Key;

                var monitor = GetOrCreateMonitor(instanceId.LogicalName, instanceId.HostName, instanceId.HostGuid);

                var newState = entry.Value.MarkDeadIfOlderThan(threshold);

                var update = monitor.UpdateStatus(newState.Status, newState.Timestamp, currentTime);
                if (update != null)
                {
                    events.Add(update);
                }
            }

            var stats = GetStats();
            var statsUpdate = Update(stats);
            if (statsUpdate != null)
            {
                events.Add(statsUpdate);
            }

            return events;
        }

        EndpointInstanceMonitor GetOrCreateMonitor(string name, string host, Guid hostId)
        {
            var endpointInstanceId = new EndpointInstanceId(name, host, hostId);

            return endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId));
        }

        IDomainEvent Update(EndpointMonitoringStats stats)
        {
            var previousActive = previousStats?.Active ?? 0;
            var previousDead = previousStats?.Failing ?? 0;
            if (previousActive != stats.Active || previousDead != stats.Failing)
            {
                previousStats = stats;
                return new HeartbeatsUpdated
                {
                    Active = stats.Active,
                    Failing = stats.Failing,
                    RaisedAt = DateTime.UtcNow
                };
            }

            return null;
        }

        internal EndpointMonitoringStats GetStats()
        {
            var stats = new EndpointMonitoringStats();
            foreach (var monitor in endpoints.Values)
            {
                monitor.AddTo(stats);
            }
            return stats;
        }

        public IHeartbeatEvent EnableMonitoring(Guid id)
        {
            EndpointInstanceMonitor monitor;
            return !endpoints.TryGetValue(id, out monitor) ? null : monitor.EnableMonitoring();
        }

        public IHeartbeatEvent DisableMonitoring(Guid id)
        {
            EndpointInstanceMonitor monitor;
            return !endpoints.TryGetValue(id, out monitor) ? null : monitor.DisableMonitoring();
        }

        public bool IsMonitored(Guid id) => endpoints[id]?.Monitored ?? false;

        internal EndpointsView[] GetEndpoints()
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

    }
}