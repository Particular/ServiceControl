namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Audit.Monitoring;
    using Contracts.Operations;
    using Infrastructure;
    using Raven.Client;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;

    class RavenDbMonitoringDataStore : IMonitoringDataStore
    {
        public RavenDbMonitoringDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id)
                    .ConfigureAwait(false);

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

                await session.StoreAsync(knownEndpoint).ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id)
                    .ConfigureAwait(false);

                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = endpoint,
                        HostDisplayName = endpoint.Host,
                        Monitored = true
                    };

                    await session.StoreAsync(knownEndpoint).ConfigureAwait(false);
                }
                else
                {
                    knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id)
                    .ConfigureAwait(false);

                if (knownEndpoint != null)
                {
                    knownEndpoint.Monitored = isMonitored;

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }
            }
        }

        public async Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            using (var session = store.OpenAsyncSession())
            {
                using (var endpointsEnumerator = await session.Advanced.StreamAsync(session.Query<KnownEndpoint, KnownEndpointIndex>())
                    .ConfigureAwait(false))
                {
                    while (await endpointsEnumerator.MoveNextAsync().ConfigureAwait(false))
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
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }
        public async Task<KnownEndpoint[]> GetAllKnownEndpoints()
        {
            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoints = await session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .ToListAsync().ConfigureAwait(false);

                return knownEndpoints.ToArray();
            }
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore store;
    }
}