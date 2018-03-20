namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    class EndpointInstanceMonitoring
    {
        IDomainEvents domainEvents;
        IPersistEndpointUptimeInformation persister;

        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor>();
        EndpointMonitoringStats previousStats;


        public async Task InitializeFromPersistence(IPersistEndpointUptimeInformation persister, IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
            this.persister = persister;

            var state = await persister.Load().ConfigureAwait(false);
            foreach (var @event in state)
            {
                var monitor = GetOrCreateMonitor(@event.Endpoint.Name, @event.Endpoint.Host, @event.Endpoint.HostId);
                monitor.Apply(@event);
            }
        }

        public void RecordHeartbeat(string name, string host, Guid hostId, DateTime timestamp)
        {
            var endpointInstanceId = new EndpointInstanceId(name, host, hostId);

            var heartbeatMonitor = heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor());
            heartbeatMonitor.MarkAlive(timestamp);
        }

        public Task StartTrackingEndpoint(string name, string host, Guid hostId)
        {
            var monitor = GetOrCreateMonitor(name, host, hostId);
            var uow = new EventUnitOfWork(domainEvents, persister);
            monitor.StartTrackingEndpoint(uow);

            return uow.Persist();
        }

        public Task CheckEndpoints(DateTime threshold, DateTime currentTime)
        {
            var uow = new EventUnitOfWork(domainEvents, persister);

            foreach (var entry in heartbeats)
            {
                var instanceId = entry.Key;

                var monitor = GetOrCreateMonitor(instanceId.LogicalName, instanceId.HostName, instanceId.HostGuid);

                var newState = entry.Value.MarkDeadIfOlderThan(threshold);

                monitor.UpdateStatus(newState.Status, newState.Timestamp, currentTime, uow);
            }

            var stats = GetStats();
            Update(stats);

            return uow.Persist();
        }

        EndpointInstanceMonitor GetOrCreateMonitor(string name, string host, Guid hostId)
        {
            var endpointInstanceId = new EndpointInstanceId(name, host, hostId);

            return endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId));
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

        public Task EnableMonitoring(Guid id)
        {
            EndpointInstanceMonitor monitor;
            if (!endpoints.TryGetValue(id, out monitor))
            {
                return Task.FromResult(0);
            }
            var uow = new EventUnitOfWork(domainEvents, persister);
            monitor.EnableMonitoring(uow);
            return uow.Persist();
        }

        public Task DisableMonitoring(Guid id)
        {
            EndpointInstanceMonitor monitor;
            if (!endpoints.TryGetValue(id, out monitor))
            {
                return Task.FromResult(0);
            }
            var uow = new EventUnitOfWork(domainEvents, persister);
            monitor.DisableMonitoring(uow);
            return uow.Persist();
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