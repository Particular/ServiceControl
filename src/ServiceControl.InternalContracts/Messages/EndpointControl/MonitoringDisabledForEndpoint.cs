namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class MonitoringDisabledForEndpoint : IEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}