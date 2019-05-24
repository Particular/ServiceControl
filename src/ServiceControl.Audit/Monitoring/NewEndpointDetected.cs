namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Infrastructure.DomainEvents;
    using Operations;

    public class NewEndpointDetected : IDomainEvent, IBusEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}