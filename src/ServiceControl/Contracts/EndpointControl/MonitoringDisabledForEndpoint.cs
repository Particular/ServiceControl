namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Contracts.Operations;

    public class MonitoringDisabledForEndpoint : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}