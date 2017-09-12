namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatMonitoringDisabled : IEvent
    {
        public Guid EndpointInstanceId { get; set; }
    }
}