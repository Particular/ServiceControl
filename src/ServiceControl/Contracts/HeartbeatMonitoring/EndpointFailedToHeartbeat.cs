namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Operations;

    public class EndpointFailedToHeartbeat : HeartbeatStatusChanged
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime LastReceivedAt { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}