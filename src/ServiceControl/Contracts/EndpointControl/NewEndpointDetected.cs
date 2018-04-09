namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class NewEndpointDetected : IDomainEvent, IEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}