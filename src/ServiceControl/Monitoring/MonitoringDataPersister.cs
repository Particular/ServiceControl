namespace ServiceControl.Monitoring
{
    using System.Threading;
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
        public MonitoringDataPersister(IMonitoringDataStore monitoringDataStore, IEndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            _monitoringDataStore = monitoringDataStore;
            _endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public Task Handle(EndpointDetected domainEvent, CancellationToken cancellationToken = default)
        {
            return _monitoringDataStore.CreateIfNotExists(domainEvent.Endpoint, cancellationToken);
        }

        public Task Handle(HeartbeatingEndpointDetected domainEvent, CancellationToken cancellationToken = default)
        {
            return _monitoringDataStore.CreateOrUpdate(domainEvent.Endpoint, _endpointInstanceMonitoring, cancellationToken);
        }

        public Task Handle(MonitoringEnabledForEndpoint domainEvent, CancellationToken cancellationToken = default)
        {
            return _monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, true, cancellationToken);
        }

        public Task Handle(MonitoringDisabledForEndpoint domainEvent, CancellationToken cancellationToken = default)
        {
            return _monitoringDataStore.UpdateEndpointMonitoring(domainEvent.Endpoint, false, cancellationToken);
        }

        IMonitoringDataStore _monitoringDataStore;
        IEndpointInstanceMonitoring _endpointInstanceMonitoring;
    }
}