namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;

    public class HeartbeatMonitoringDisabled : HeartbeatStatusChanged
    {
        public Guid EndpointInstanceId { get; set; }
    }
}