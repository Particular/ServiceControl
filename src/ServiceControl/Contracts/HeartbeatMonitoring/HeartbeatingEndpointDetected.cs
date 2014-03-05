namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;
    using Operations;

    public class HeartbeatingEndpointDetected : IEvent
    {
        public EndpointDetails EndpointDetails { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}