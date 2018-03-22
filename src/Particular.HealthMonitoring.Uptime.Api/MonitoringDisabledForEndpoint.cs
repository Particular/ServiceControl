namespace Particular.HealthMonitoring.Uptime.Api
{
    using System;
    using ServiceControl.Contracts.Operations;

    public class MonitoringDisabledForEndpoint : IHeartbeatEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}