namespace ServiceControl.HeartbeatMonitoring
{
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.EndpointControl.Contracts;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Monitoring;

    public class Persister :
        IDomainHandler<EndpointFailedToHeartbeat>,
        IDomainHandler<EndpointHeartbeatRestored>,
        IDomainHandler<MonitoringDisabledForEndpoint>,
        IDomainHandler<MonitoringEnabledForEndpoint>
    {
        IPersistEndpointUptimeInformation uptimeInformationPersister;

        public Persister(IPersistEndpointUptimeInformation uptimeInformationPersister)
        {
            this.uptimeInformationPersister = uptimeInformationPersister;
        }

        public void Handle(EndpointFailedToHeartbeat domainEvent)
        {
            uptimeInformationPersister.Store(new EndpointUptimeInfo
            {
                Id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString()),
                Status = HeartbeatStatus.Dead,
                Name = domainEvent.Endpoint.Name,
                HostId = domainEvent.Endpoint.HostId,
                Monitored = true
            });
        }

        public void Handle(EndpointHeartbeatRestored domainEvent)
        {
            uptimeInformationPersister.Store(new EndpointUptimeInfo
            {
                Id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString()),
                Status = HeartbeatStatus.Alive,
                Name = domainEvent.Endpoint.Name,
                HostId = domainEvent.Endpoint.HostId,
                Monitored = true
            });
        }

        public void Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            uptimeInformationPersister.Store(new EndpointUptimeInfo
            {
                Id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString()),
                Status = HeartbeatStatus.Unknown,
                Name = domainEvent.Endpoint.Name,
                HostId = domainEvent.Endpoint.HostId,
                Monitored = false
            });
        }

        public void Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            uptimeInformationPersister.Store(new EndpointUptimeInfo
            {
                Id = DeterministicGuid.MakeId(domainEvent.Endpoint.Name, domainEvent.Endpoint.HostId.ToString()),
                Status = HeartbeatStatus.Unknown,
                Name = domainEvent.Endpoint.Name,
                HostId = domainEvent.Endpoint.HostId,
                Monitored = true
            });
        }
    }
}