namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class RavenMonitoringDataStore(IRavenSessionProvider sessionProvider) : IMonitoringDataStore
    {
        public static string MakeDocumentId(Guid id) => $"{KnownEndpoint.CollectionName}/{id}";

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession();

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId);

            if (knownEndpoint != null)
            {
                return;
            }

            knownEndpoint = new KnownEndpoint
            {
                EndpointDetails = endpoint,
                HostDisplayName = endpoint.Host,
                Monitored = false
            };

            await session.StoreAsync(knownEndpoint, docId);

            await session.SaveChangesAsync();
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession();

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId);

            if (knownEndpoint == null)
            {
                knownEndpoint = new KnownEndpoint
                {
                    EndpointDetails = endpoint,
                    HostDisplayName = endpoint.Host,
                    Monitored = true
                };

                await session.StoreAsync(knownEndpoint, docId);
            }
            else
            {
                knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
            }

            await session.SaveChangesAsync();
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession();

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId);

            if (knownEndpoint != null)
            {
                knownEndpoint.Monitored = isMonitored;

                await session.SaveChangesAsync();
            }
        }

        public async Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            using var session = await sessionProvider.OpenSession();
            await using var endpointsEnumerator = await session.Advanced.StreamAsync(session.Query<KnownEndpoint, KnownEndpointIndex>());

            while (await endpointsEnumerator.MoveNextAsync())
            {
                var endpoint = endpointsEnumerator.Current.Document;

                endpointInstanceMonitoring.DetectEndpointFromPersistentStore(endpoint.EndpointDetails, endpoint.Monitored);
            }
        }

        public async Task Delete(Guid endpointId)
        {
            using var session = await sessionProvider.OpenSession();
            session.Delete(MakeDocumentId(endpointId));
            await session.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints()
        {
            using var session = await sessionProvider.OpenSession();

            var knownEndpoints = await session.Query<KnownEndpoint, KnownEndpointIndex>()
                .ToListAsync();

            return knownEndpoints;
        }
    }
}