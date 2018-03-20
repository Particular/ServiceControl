namespace Particular.HealthMonitoring.Uptime.Api
{
    using System;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public interface IHeartbeatEvent : IDomainEvent
    {
        Guid EndpointInstanceId { get; }
        EndpointDetails Endpoint { get; }
    }
}