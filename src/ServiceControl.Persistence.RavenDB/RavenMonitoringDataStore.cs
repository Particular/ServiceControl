namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class RavenMonitoringDataStore(IRavenSessionProvider sessionProvider) : IMonitoringDataStore
    {
        public static string MakeDocumentId(Guid id) => $"{KnownEndpointsCollectionName}/{id}";

        public async Task CreateIfNotExists(EndpointDetails endpoint, CancellationToken cancellationToken = default)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId, cancellationToken);

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

            await session.StoreAsync(knownEndpoint, docId, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId, cancellationToken);

            if (knownEndpoint == null)
            {
                knownEndpoint = new KnownEndpoint
                {
                    EndpointDetails = endpoint,
                    HostDisplayName = endpoint.Host,
                    Monitored = true
                };

                await session.StoreAsync(knownEndpoint, docId, cancellationToken);
            }
            else
            {
                knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored, CancellationToken cancellationToken = default)
        {
            var id = endpoint.GetDeterministicId();
            var docId = MakeDocumentId(id);

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var knownEndpoint = await session.LoadAsync<KnownEndpoint>(docId, cancellationToken);

            if (knownEndpoint != null)
            {
                knownEndpoint.Monitored = isMonitored;

                await session.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            await using var endpointsEnumerator = await session.Advanced.StreamAsync(session.Query<KnownEndpoint, KnownEndpointIndex>(), cancellationToken);

            while (await endpointsEnumerator.MoveNextAsync())
            {
                var endpoint = endpointsEnumerator.Current.Document;

                endpointInstanceMonitoring.DetectEndpointFromPersistentStore(endpoint.EndpointDetails, endpoint.Monitored);
            }
        }

        public async Task Delete(Guid endpointId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Delete(MakeDocumentId(endpointId));
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var knownEndpoints = await session.Query<KnownEndpoint, KnownEndpointIndex>()
                .ToListAsync(cancellationToken);

            return knownEndpoints;
        }

        public const string KnownEndpointsCollectionName = "KnownEndpoints";
    }
}