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
        IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>,
        IDomainHandler<EndpointDetected>
    {
        public MonitoringDataPersister(IDocumentStore store, EndpointInstanceMonitoring monitoring)
        {
            this.store = store;
            this.monitoring = monitoring;
        }
        public Task Handle(EndpointDetected domainEvent)
        {
            return StoreEndpoint(domainEvent.Endpoint);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            return StoreEndpoint(domainEvent.Endpoint);
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            return StoreEndpoint(domainEvent.Endpoint, false);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            return StoreEndpoint(domainEvent.Endpoint, true);
        }

        public async Task StoreEndpoint(EndpointDetails endpoint, bool? isMonitored = null)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            if (!isMonitored.HasValue)
            {
                isMonitored = monitoring.IsMonitored(id);
            }

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
                        Monitored = isMonitored.Value
                    };
                    await session.StoreAsync(knownEndpoint).ConfigureAwait(false);

                    return;
                }

                knownEndpoint.Monitored = isMonitored.Value;

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
    }
}