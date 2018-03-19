namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointStarted : IDomainEvent
    {
        public EndpointDetails EndpointDetails { get; set; }
        public DateTime StartedAt { get; set; }
    }
}