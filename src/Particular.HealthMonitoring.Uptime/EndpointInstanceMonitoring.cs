namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointInstanceMonitoring
    {
        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor>();
        EndpointMonitoringStats previousStats;

        internal EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public void Load(IEnumerable<IHeartbeatEvent> events)
        {
            foreach (var @event in events)
            {
                var monitor = GetOrCreateMonitor(@event.Endpoint.Name, @event.Endpoint.Host, @event.Endpoint.HostId);
                monitor.Apply(@event);
            }
        }

        public void RecordHeartbeat(EndpointHeartbeat message)
        {
            var endpointInstanceId = new EndpointInstanceId(message.EndpointName, message.Host, message.HostId);

            var heartbeatMonitor = heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor());
            heartbeatMonitor.MarkAlive(message.ExecutedAt);
        }

        public void EndpointDetected(string name, string host, Guid hostId)
        {
            var monitor = GetOrCreateMonitor(name, host, hostId);

            monitor.Initialize();
        }

        public void CheckEndpoints(DateTime threshold, DateTime currentTime)
        {
            foreach (var entry in heartbeats)
            {
                var instanceId = entry.Key;

                var monitor = GetOrCreateMonitor(instanceId.LogicalName, instanceId.HostName, instanceId.HostGuid);

                var newState = entry.Value.MarkDeadIfOlderThan(threshold);

                monitor.UpdateStatus(newState.Status, newState.Timestamp, currentTime);
            }

            var stats = GetStats();

            Update(stats);
        }

        EndpointInstanceMonitor GetOrCreateMonitor(string name, string host, Guid hostId)
        {
            var endpointInstanceId = new EndpointInstanceId(name, host, hostId);

            return endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, domainEvents));
        }

        void Update(EndpointMonitoringStats stats)
        {
            var previousActive = previousStats?.Active ?? 0;
            var previousDead = previousStats?.Failing ?? 0;
            if (previousActive != stats.Active || previousDead != stats.Failing)
            {
                domainEvents.Raise(new HeartbeatsUpdated
                {
                    Active = stats.Active,
                    Failing = stats.Failing,
                    RaisedAt = DateTime.UtcNow
                });
                previousStats = stats;
            }
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

        public void EnableMonitoring(Guid id) => endpoints[id]?.EnableMonitoring();
        public void DisableMonitoring(Guid id) => endpoints[id]?.DisableMonitoring();
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