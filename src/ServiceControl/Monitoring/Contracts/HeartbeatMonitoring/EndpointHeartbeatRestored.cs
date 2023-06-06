namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class EndpointHeartbeatRestored : IDomainEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}