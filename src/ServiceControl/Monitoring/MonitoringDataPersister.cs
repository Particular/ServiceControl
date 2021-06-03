namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using Infrastructure.DomainEvents;

    class MonitoringDataPersister :
        IDomainHandler<EndpointDetected>,
        IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {
        public MonitoringDataPersister(MonitoringDataStore monitoringDataStore)
        {
            this.monitoringDataStore = monitoringDataStore;
        }

        public Task Handle(EndpointDetected domainEvent)
        {
            return monitoringDataStore.CreateIfNotExists(domainEvent.Endpoint);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            return monitoringDataStore.CreateOrUpdate(domainEvent.Endpoint);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            return monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, true);
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            return monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, false);
        }

        MonitoringDataStore monitoringDataStore;
    }
}