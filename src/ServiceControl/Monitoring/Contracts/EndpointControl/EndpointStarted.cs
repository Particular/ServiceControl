namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class EndpointStarted : IDomainEvent
    {
        public EndpointDetails EndpointDetails { get; set; }
        public DateTime StartedAt { get; set; }
    }
}