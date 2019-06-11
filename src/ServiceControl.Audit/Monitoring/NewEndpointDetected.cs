namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Audit.Monitoring;
    using NServiceBus;

    public class NewEndpointDetected : IEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}