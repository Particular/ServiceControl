namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class RavenDbMonitoringDataStore : IMonitoringDataStore
    {
        public RavenDbMonitoringDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = endpoint.GetDeterministicId().ToString();

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id);

                if (knownEndpoint != null)
                {
                    return;
                }

                knownEndpoint = new KnownEndpoint
                {
                    Id = id,
                    EndpointDetails = endpoint,
                    HostDisplayName = endpoint.Host,
                    Monitored = false
                };

                await session.StoreAsync(knownEndpoint);

                await session.SaveChangesAsync();
            }
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            var id = endpoint.GetDeterministicId();

            using (var session = store.OpenAsyncSession())
            {
                var documentId = id.ToString();
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(documentId);

                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = documentId,
                        EndpointDetails = endpoint,
                        HostDisplayName = endpoint.Host,
                        Monitored = true
                    };

                    await session.StoreAsync(knownEndpoint);
                }
                else
                {
                    knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
                }

                await session.SaveChangesAsync();
            }
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = endpoint.GetDeterministicId();

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id);

                if (knownEndpoint != null)
                {
                    knownEndpoint.Monitored = isMonitored;

                    await session.SaveChangesAsync();
                }
            }
        }

        public async Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            using (var session = store.OpenAsyncSession())
            {
                using (var endpointsEnumerator = await session.Advanced.StreamAsync(session.Query<KnownEndpoint, KnownEndpointIndex>())
                    )
                {
                    while (await endpointsEnumerator.MoveNextAsync())
                    {
                        var endpoint = endpointsEnumerator.Current.Document;

                        endpointInstanceMonitoring.DetectEndpointFromPersistentStore(endpoint.EndpointDetails, endpoint.Monitored);
                    }
                }
            }
        }

        public async Task Delete(Guid endpointId)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.Delete<KnownEndpoint>(endpointId);
                await session.SaveChangesAsync();
            }
        }
        public async Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints()
        {
            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoints = await session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .ToListAsync();

                return knownEndpoints.ToArray();
            }
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore store;
    }
}