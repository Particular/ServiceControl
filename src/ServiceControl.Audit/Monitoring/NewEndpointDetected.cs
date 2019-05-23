namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Audit.Infrastructure.DomainEvents;
    using Audit.Monitoring;

    public class NewEndpointDetected : IDomainEvent, IBusEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}