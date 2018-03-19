namespace Particular.HealthMonitoring.Uptime
{
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.EndpointControl.Contracts;
    using ServiceControl.Infrastructure.DomainEvents;

    public class HeartbeatPersister :
        IDomainHandler<HeartbeatingEndpointDetected>
        IDomainHandler<MonitoringEnabledForEndpoint>,
        IDomainHandler<MonitoringDisabledForEndpoint>
    {
        public void Handle(HeartbeatingEndpointDetected domainEvent)
        {
            throw new System.NotImplementedException();
        }

        public void Handle(MonitoringEnabledForEndpoint domainEvent)
        {
            throw new System.NotImplementedException();
        }

        public void Handle(MonitoringDisabledForEndpoint domainEvent)
        {
            throw new System.NotImplementedException();
        }
    }
}