namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;
    using Operations;

    public class HeartbeatingEndpointDetected : IEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}