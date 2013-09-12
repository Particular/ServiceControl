namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatingEndpointDetected : IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}