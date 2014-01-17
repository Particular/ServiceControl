namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;

    public class NewMachineDetectedForEndpoint:IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}