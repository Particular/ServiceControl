namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using Infrastructure.DomainEvents;
    using Operations;
    using Persistence;

    class EndpointInstanceMonitoring : IEndpointInstanceMonitoring
    {
        readonly IDomainEvents domainEvents;
        readonly ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new();
        readonly ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new();
        EndpointMonitoringStats previousStats;

        public EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public void RecordHeartbeat(EndpointInstanceId endpointInstanceId, DateTime timestamp) => heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor()).MarkAlive(timestamp);

        public async Task CheckEndpoints(DateTime threshold)
        {
            foreach (var entry in heartbeats)
            {
                var recordedHeartbeat = entry.Value.MarkDeadIfOlderThan(threshold);

                var endpointInstanceId = entry.Key;
                var monitor = endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, true, domainEvents));
                await monitor.UpdateStatus(recordedHeartbeat.Status, recordedHeartbeat.Timestamp);
            }

            var stats = GetStats();

            await Update(stats);
        }

        public bool IsNewInstance(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();

            return endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents));
        }

        public async Task EndpointDetected(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            if (endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents)))
            {
                await domainEvents.Raise(new EndpointDetected
                {
                    DetectedAt = DateTime.UtcNow,
                    Endpoint = newEndpointDetails
                });
            }
        }

        public async Task DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, true, domainEvents));

            await domainEvents.Raise(new EndpointStarted
            {
                EndpointDetails = newEndpointDetails,
                StartedAt = startedAt
            });
        }

        public void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored, bool supportsHeartbeats)
        {
            var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId, supportsHeartbeats);
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, monitored, domainEvents));
        }

        async Task Update(EndpointMonitoringStats stats)
        {
            var previousActive = previousStats?.Active ?? 0;
            var previousDead = previousStats?.Failing ?? 0;
            if (previousActive != stats.Active || previousDead != stats.Failing)
            {
                await domainEvents.Raise(new HeartbeatsUpdated
                {
                    Active = stats.Active,
                    Failing = stats.Failing,
                    RaisedAt = DateTime.UtcNow
                });
                previousStats = stats;
            }
        }

        public EndpointMonitoringStats GetStats()
        {
            var stats = new EndpointMonitoringStats();
            foreach (var monitor in endpoints.Values)
            {
                monitor.AddTo(stats);
            }

            return stats;
        }

        public Task EnableMonitoring(Guid id) => endpoints[id]?.EnableMonitoring();
        public Task DisableMonitoring(Guid id) => endpoints[id]?.DisableMonitoring();
        public bool IsMonitored(Guid id) => endpoints.ContainsKey(id) && endpoints[id].Monitored;

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

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }

        public void RemoveEndpoint(Guid endpointId)
        {
            var heartbeat = heartbeats.Keys.SingleOrDefault(t => t.UniqueId == endpointId);
            if (heartbeat != null)
            {
                heartbeats.TryRemove(heartbeat, out var _);
            }
            endpoints.TryRemove(endpointId, out _);
        }

        public bool HasEndpoint(Guid endpointId)
        {
            return endpoints.ContainsKey(endpointId);
        }
    }
}