namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using Contracts.EndpointControl;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;

    class EndpointInstanceMonitoring
    {
        public EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public async Task DetectEndpointFromLocalAudit(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            if (endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstanceMonitor(endpointInstanceId)))
            {
                await domainEvents.Raise(new NewEndpointDetected
                    {
                        DetectedAt = DateTime.UtcNow,
                        Endpoint = newEndpointDetails
                    })
                    .ConfigureAwait(false);
            }
        }

        public void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored)
        {
            var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
            endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstanceMonitor(endpointInstanceId));
        }

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }

        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, EndpointInstanceMonitor> endpoints = new ConcurrentDictionary<Guid, EndpointInstanceMonitor>();
    }
}