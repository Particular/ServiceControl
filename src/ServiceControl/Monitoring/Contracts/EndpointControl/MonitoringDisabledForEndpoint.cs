namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class MonitoringDisabledForEndpoint : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}