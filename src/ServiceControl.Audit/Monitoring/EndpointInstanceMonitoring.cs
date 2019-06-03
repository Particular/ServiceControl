namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.DomainEvents;

    class EndpointInstanceMonitoring
    {
        public EndpointInstanceMonitoring(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public async Task DetectEndpointFromLocalAudit(EndpointDetails newEndpointDetails)
        {
            object previouslyDetected = null;
            var endpointInstanceId = DeterministicGuid.MakeId(newEndpointDetails.Name, newEndpointDetails.HostId.ToString());

            if (endpoints.TryAdd(endpointInstanceId, previouslyDetected))
            {
                await domainEvents.Raise(new NewEndpointDetected
                {
                    DetectedAt = DateTime.UtcNow,
                    Endpoint = newEndpointDetails
                })
                    .ConfigureAwait(false);
            }
        }

        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, object> endpoints = new ConcurrentDictionary<Guid, object>();
    }
}