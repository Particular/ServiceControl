namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class EndpointFailedToHeartbeat : IEvent
    {
        public string Endpoint { get; set; }
        public Guid HostId { get; set; }
        public DateTime LastReceivedAt { get; set; }
    }
}