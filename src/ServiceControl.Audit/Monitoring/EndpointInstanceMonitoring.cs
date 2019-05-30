namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.Operations;
    using EndpointControl;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Raven.Client;
    using ServiceControl.CompositeViews.Endpoints;

    class EndpointInstanceMonitoring
    {
        public EndpointInstanceMonitoring(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        // TODO: Test this end2end
        public async Task DetectEndpointFromLocalAudit(EndpointDetails newEndpointDetails)
        {
            object previouslyDetected = null;
            var endpointInstanceId = DeterministicGuid.MakeId(newEndpointDetails.Name, newEndpointDetails.HostId.ToString());
            if (endpoints.TryAdd(endpointInstanceId, previouslyDetected))
            {
                using (var session = store.OpenAsyncSession())
                {
                    var knownEndpoint = new KnownEndpoint
                    {
                        Id = endpointInstanceId
                    };

                    await session.StoreAsync(knownEndpoint)
                        .ConfigureAwait(false);

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }

                await domainEvents.Raise(new NewEndpointDetected
                {
                    DetectedAt = DateTime.UtcNow,
                    Endpoint = newEndpointDetails
                })
                    .ConfigureAwait(false);
            }
        }

        public async Task Warmup()
        {
            using (var documentSession = store.OpenAsyncSession())
            {
                using (var endpointsEnumerator = await documentSession.Advanced.StreamAsync(documentSession.Query<KnownEndpoint, KnownEndpointIndex>())
                    .ConfigureAwait(false))
                {
                    while (await endpointsEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var endpoint = endpointsEnumerator.Current.Document;
                        endpoints.GetOrAdd(endpoint.Id, new byte());
                    }
                }
            }
        }


        readonly IDocumentStore store;
        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, object> endpoints = new ConcurrentDictionary<Guid, object>();
    }
}