namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class EndpointDetected : IDomainEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}