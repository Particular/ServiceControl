namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;
    using Operations;

    public class NewEndpointDetected : IEvent
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}