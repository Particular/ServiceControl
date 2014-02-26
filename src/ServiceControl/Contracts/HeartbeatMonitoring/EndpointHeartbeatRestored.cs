namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class EndpointHeartbeatRestored : IEvent
    {
        public string Endpoint { get; set; }
        public Guid HostId { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}