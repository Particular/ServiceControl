namespace Particular.HealthMonitoring.Uptime.Api
{
    using System;
    using ServiceControl.Contracts.Operations;

    public class EndpointFailedToHeartbeat : IHeartbeatEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime LastReceivedAt { get; set; }
        public DateTime DetectedAt { get; set; }
        public Guid EndpointInstanceId { get; set; }
    }
}