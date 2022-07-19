namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using Infrastructure.DomainEvents;
    using ServiceControl.Persistence;

    class MonitoringDataPersister :
        IDomainHandler<EndpointDetected>,
        IDomainHandler<HeartbeatingEndpointDetected>,
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {
        public MonitoringDataPersister(IMonitoringDataStore monitoringDataStore, EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            _monitoringDataStore = monitoringDataStore;
            _endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public Task Handle(EndpointDetected domainEvent)
        {
            return _monitoringDataStore.CreateIfNotExists(domainEvent.Endpoint);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            return _monitoringDataStore.CreateOrUpdate(domainEvent.Endpoint, _endpointInstanceMonitoring);
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
        EndpointInstanceMonitoring _endpointInstanceMonitoring;
    }
}