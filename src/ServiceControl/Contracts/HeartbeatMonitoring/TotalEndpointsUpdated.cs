namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    class TotalEndpointsUpdated : IEvent
    {
        public int Active { get; set; }
        public int Failing { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}