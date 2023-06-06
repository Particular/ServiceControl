namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class EndpointFailedToHeartbeat : IDomainEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime LastReceivedAt { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}