namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class MonitoringEnabledForEndpoint : IEvent
    {
        public Guid EndpointId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}