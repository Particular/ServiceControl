namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;
    using Operations;

    public class EndpointFailedToHeartbeat : IEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime LastReceivedAt { get; set; }
    }
}