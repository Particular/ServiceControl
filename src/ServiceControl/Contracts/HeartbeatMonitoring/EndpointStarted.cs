namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class EndpointStarted : IEvent
    {
        public Guid HostId { get; set; }
        public string Endpoint { get; set; }
        public DateTime StartedAt { get; set; }
    }
}