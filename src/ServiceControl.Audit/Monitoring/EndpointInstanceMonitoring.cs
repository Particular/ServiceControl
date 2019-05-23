namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;

    class EndpointInstanceMonitoring
    {
        public EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public async Task CheckEndpoints(DateTime threshold)
        {
            var stats = GetStats();

            await Update(stats).ConfigureAwait(false);
        }

        public async Task DetectEndpointFromLocalAudit(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            if (endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents)))
            {
                await domainEvents.Raise(new NewEndpointDetected
                    {
                        DetectedAt = DateTime.UtcNow,
                        Endpoint = newEndpointDetails
                    })
                    .ConfigureAwait(false);
            }
        }

        public void DetectEndpointFromRemoteAudit(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, false, domainEvents));
        }

        public async Task DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId, true, domainEvents));

            await domainEvents.Raise(new EndpointStarted
            {
                EndpointDetails = newEndpointDetails,
                StartedAt = startedAt
            }).ConfigureAwait(false);
        }

        public void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored)
        {
            var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
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
                }).ConfigureAwait(false);
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

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }

        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
        EndpointMonitoringStats previousStats;
    }
}