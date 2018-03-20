namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Operations;

    public class HeartbeatingEndpointDetected : IHeartbeatEvent
    {
        public Guid EndpointInstanceId { get; set; }
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}