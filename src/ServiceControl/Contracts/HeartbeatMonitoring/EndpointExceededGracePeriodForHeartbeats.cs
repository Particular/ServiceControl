namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class EndpointExceededGracePeriodForHeartbeats : IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime LastHeartbeatReceivedAt { get; set; }
    }
}