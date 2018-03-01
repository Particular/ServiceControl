namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointHeartbeatRestored : IDomainEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}