namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class MonitoringDisabledForEndpoint : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}