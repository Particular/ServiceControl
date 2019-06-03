namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl;
    using EndpointControl.Contracts;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Raven.Client;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.Operations;

    class MonitoringDataPersister :
        IDomainHandler<EndpointDetected>,
        IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {
        public MonitoringDataPersister(IDocumentStore store, EndpointInstanceMonitoring monitoring)
        {
            this.store = store;
            this.monitoring = monitoring;
        }

        public async Task Handle(EndpointDetected domainEvent)
        {
            var endpoint = domainEvent.Endpoint;
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

        public async Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            var endpoint = domainEvent.Endpoint;
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
                    knownEndpoint.Monitored = monitoring.IsMonitored(id);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            return UpdateEndpointMonitoring(domainEvent.Endpoint, false);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            return UpdateEndpointMonitoring(domainEvent.Endpoint, true);
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

        public async Task WarmupMonitoringFromPersistence()
        {
            using (var session = store.OpenAsyncSession())
            {
                using (var endpointsEnumerator = await session.Advanced.StreamAsync(session.Query<KnownEndpoint, KnownEndpointIndex>())
                    .ConfigureAwait(false))
                {
                    while (await endpointsEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var endpoint = endpointsEnumerator.Current.Document;

                        monitoring.DetectEndpointFromPersistentStore(endpoint.EndpointDetails, endpoint.Monitored);
                    }
                }
            }
        }

        IDocumentStore store;
        EndpointInstanceMonitoring monitoring;
    }
}