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
    using EndpointControl;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Raven.Client;

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
            var endpointInstanceId = newEndpointDetails.ToInstanceId();
            if (endpoints.TryAdd(endpointInstanceId.UniqueId, new EndpointInstance(endpointInstanceId)))
            {
                var id = DeterministicGuid.MakeId(newEndpointDetails.Name, newEndpointDetails.HostId.ToString());

                using (var session = store.OpenAsyncSession())
                {
                    var knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = newEndpointDetails,
                        HostDisplayName = newEndpointDetails.Host
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
                        var endpointDetails = endpoint.EndpointDetails;
                        var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
                        endpoints.GetOrAdd(endpointInstanceId.UniqueId, id => new EndpointInstance(endpointInstanceId));
                    }
                }
            }
        }

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return endpoints.Values.Select(endpoint => endpoint.GetKnownView()).ToList();
        }

        private readonly IDocumentStore store;
        IDomainEvents domainEvents;
        ConcurrentDictionary<Guid, EndpointInstance> endpoints = new ConcurrentDictionary<Guid, EndpointInstance>();
    }
}