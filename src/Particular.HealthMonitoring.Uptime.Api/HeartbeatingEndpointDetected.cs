namespace Particular.HealthMonitoring.Uptime.Api
{
    using System;
    using ServiceControl.Contracts.Operations;

    public class HeartbeatingEndpointDetected : IHeartbeatEvent
    {
        public Guid EndpointInstanceId { get; set; }
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}