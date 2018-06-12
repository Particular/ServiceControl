namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.EndpointControl;
    using ServiceControl.EndpointControl.Contracts;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    public class MonitoringDataPersister : 
        IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {

        public async Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());

            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = new KnownEndpoint
                {
                    Id = id,
                    EndpointDetails = domainEvent.Endpoint,
                    HostDisplayName = domainEvent.Endpoint.Host,
                    Monitored = monitoring.IsMonitored(id)
                };

                await session.StoreAsync(knownEndpoint)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());
            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id)
                    .ConfigureAwait(false);
                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = domainEvent.Endpoint,
                        HostDisplayName = domainEvent.Endpoint.Host,
                    };
                    await session.StoreAsync(knownEndpoint);
                }

                knownEndpoint.Monitored = true;
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());
            using (var session = store.OpenAsyncSession())
            {
                var knownEndpoint = await session.LoadAsync<KnownEndpoint>(id)
                    .ConfigureAwait(false);
                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = domainEvent.Endpoint,
                        HostDisplayName = domainEvent.Endpoint.Host,
                    };
                    await session.StoreAsync(knownEndpoint)
                        .ConfigureAwait(false);
                }

                knownEndpoint.Monitored = false;
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
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

        private IDocumentStore store;
        private EndpointInstanceMonitoring monitoring;

        public MonitoringDataPersister(IDocumentStore store, EndpointInstanceMonitoring monitoring)
        {
            this.store = store;
            this.monitoring = monitoring;
        }
    }
}