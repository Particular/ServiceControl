namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EndpointControl;
    using ServiceControl.Infrastructure;

    public class KnownEndpointsPersister
    {
        IDocumentStore store;
        ConcurrentDictionary<Guid, KnownEndpoint> knownEndpoints = new ConcurrentDictionary<Guid, KnownEndpoint>();
     
        public void WarmupMonitoringFromPersistence()
        {
            using (var session = store.OpenSession())
            {
                using (var endpointsEnumerator = session.Advanced.Stream(session.Query<KnownEndpoint, KnownEndpointIndex>()))
                {
                    while (endpointsEnumerator.MoveNext())
                    {
                        var endpoint = endpointsEnumerator.Current.Document;
                        var endpointDetails = endpoint.EndpointDetails;

                        var id = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.HostId.ToString());

                        knownEndpoints.AddOrUpdate(id, endpoint, (x, _) => endpoint);
                    }
                }
            }
        }

        public KnownEndpointsPersister(IDocumentStore store)
        {
            this.store = store;
        }

        public void RegisterEndpoint(EndpointDetails endpointDetails)
        {
            var id = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.HostId.ToString());

            using (var session = store.OpenSession())
            {
                var knownEndpoint = session.Load<KnownEndpoint>(id);
                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = endpointDetails,
                        HostDisplayName = endpointDetails.Host,
                    };

                    knownEndpoints.AddOrUpdate(id, knownEndpoint, (_, __) => knownEndpoint);

                    session.Store(knownEndpoint);
                }

                knownEndpoint.Monitored = false;
                session.SaveChanges();
            }
        }

        public List<KnownEndpointsView> GetKnownEndpoints()
        {
            return knownEndpoints.Values.Select(ke => new KnownEndpointsView
            {
                EndpointDetails = ke.EndpointDetails,
                HostDisplayName = ke.HostDisplayName,
                Id = ke.Id
            }).ToList();
        }
    }
}