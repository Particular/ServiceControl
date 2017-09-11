namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatMonitoringEnabled : IEvent
    {
        public Guid EndpointInstanceId { get; set; }
    }
}