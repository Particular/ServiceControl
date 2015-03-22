namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;

    public class HeartbeatMonitoringEnabled : HeartbeatStatusChanged
    {
        public Guid EndpointInstanceId { get; set; } 
    }
}