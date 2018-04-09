namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointInstanceMonitoring
    {
        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor> heartbeats = new ConcurrentDictionary<EndpointInstanceId, HeartbeatMonitor>();
        EndpointMonitoringStats previousStats;

        public EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public void RecordHeartbeat(EndpointInstanceId endpointInstanceId, DateTime timestamp) => heartbeats.GetOrAdd(endpointInstanceId, id => new HeartbeatMonitor()).MarkAlive(timestamp);

        public void CheckEndpoints(DateTime threshold)
        {
            foreach (var entry in heartbeats)
            {
                var recordedHeartbeat = entry.Value.MarkDeadIfOlderThan(threshold);

                EndpointInstanceId endpointInstanceId = entry.Key;
                var monitor = endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, true, domainEvents));
                monitor.UpdateStatus(recordedHeartbeat.Status, recordedHeartbeat.Timestamp);
            }

            var stats = GetStats();

            Update(stats);
        }

        public void DetectEndpointFromLocalAudit(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            if (endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents)))
            {
                domainEvents.Raise(new NewEndpointDetected { DetectedAt = DateTime.UtcNow, Endpoint = newEndpointDetails });
            }
        }

        public void DetectEndpointFromRemoteAudit(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents));
        }

        public void DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, true, domainEvents));

            domainEvents.Raise(new EndpointStarted
            {
                EndpointDetails = newEndpointDetails,
                StartedAt = startedAt
            });
        }

        public void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored)
        {
            var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, monitored, domainEvents));
        }

        private void Update(EndpointMonitoringStats stats)
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

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }
    }
}