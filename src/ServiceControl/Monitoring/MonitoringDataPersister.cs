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
        public MonitoringDataPersister(IMonitoringDataStore monitoringDataStore)
        {
            _monitoringDataStore = monitoringDataStore;
        }

        public Task Handle(EndpointDetected domainEvent)
        {
            return _monitoringDataStore.CreateIfNotExists(domainEvent.Endpoint);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            return _monitoringDataStore.CreateOrUpdate(domainEvent.Endpoint);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            return _monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, true);
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            return _monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, false);
        }

        IMonitoringDataStore _monitoringDataStore;
    }
}