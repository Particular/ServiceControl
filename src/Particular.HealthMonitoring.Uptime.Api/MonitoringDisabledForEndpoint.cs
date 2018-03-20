namespace ServiceControl.EndpointControl.Contracts
{
    using System;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;

    public class MonitoringDisabledForEndpoint : IHeartbeatEvent
    {
        public Guid EndpointInstanceId { get; set; }

        public EndpointDetails Endpoint { get; set; }
    }
}