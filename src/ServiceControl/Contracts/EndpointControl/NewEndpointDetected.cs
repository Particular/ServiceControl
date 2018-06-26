namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class NewEndpointDetected : IDomainEvent, IBusEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}