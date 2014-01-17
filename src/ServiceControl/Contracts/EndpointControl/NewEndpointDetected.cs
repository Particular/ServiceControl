namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;

    public class NewEndpointDetected:IEvent
    {
        public DateTime DetectedAt { get; set; }
        public string Endpoint { get; set; }
        public string Machine { get; set; }
    }
}