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
        public MonitoringDataPersister(RavenDbMonitoringDataStore ravenDbMonitoringDataStore)
        {
            this._ravenDbMonitoringDataStore = ravenDbMonitoringDataStore;
        }

        public Task Handle(EndpointDetected domainEvent)
        {
            return _ravenDbMonitoringDataStore.CreateIfNotExists(domainEvent.Endpoint);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent)
        {
            return _ravenDbMonitoringDataStore.CreateOrUpdate(domainEvent.Endpoint);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            return _ravenDbMonitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, true);
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            return _ravenDbMonitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, false);
        }

        RavenDbMonitoringDataStore _ravenDbMonitoringDataStore;
    }
}