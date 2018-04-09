namespace ServiceControl.Monitoring
{
    using Raven.Client;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.EndpointControl;
    using ServiceControl.EndpointControl.Contracts;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    public class MonitoringDataPersister : IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {

        public void Handle(HeartbeatingEndpointDetected domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());

            using (var session = store.OpenSession())
            {
                var knownEndpoint = new KnownEndpoint
                {
                    Id = id,
                    EndpointDetails = domainEvent.Endpoint,
                    HostDisplayName = domainEvent.Endpoint.Host,
                    Monitored = monitoring.IsMonitored(id)
                };

                session.Store(knownEndpoint);

                session.SaveChanges();
            }
        }

        public void Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());
            using (var session = store.OpenSession())
            {
                var knownEndpoint = session.Load<KnownEndpoint>(id);
                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = domainEvent.Endpoint,
                        HostDisplayName = domainEvent.Endpoint.Host,
                    };
                    session.Store(knownEndpoint);
                }

                knownEndpoint.Monitored = true;
                session.SaveChanges();
            }
        }

        public void Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            var id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString());
            using (var session = store.OpenSession())
            {
                var knownEndpoint = session.Load<KnownEndpoint>(id);
                if (knownEndpoint == null)
                {
                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        EndpointDetails = domainEvent.Endpoint,
                        HostDisplayName = domainEvent.Endpoint.Host,
                    };
                    session.Store(knownEndpoint);
                }

                knownEndpoint.Monitored = false;
                session.SaveChanges();
            }
        }

        public void WarmupMonitoringFromPersistence()
        {
            using (var session = store.OpenSession())
            {
                using (var endpointsEnumerator = session.Advanced.Stream(session.Query<KnownEndpoint, KnownEndpointIndex>()))
                {
                    while (endpointsEnumerator.MoveNext())
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