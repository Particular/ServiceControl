namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;
    using ServiceControl.Operations;

    public class NewEndpointDetected : IEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}