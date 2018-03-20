namespace ServiceControl.Infrastructure
{
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Infrastructure.DomainEvents;
    class Persistence : IDomainHandler<EndpointFailedToHeartbeat>
    {
        
    }
}