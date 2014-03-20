namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;
    using Operations;

    public class EndpointHeartbeatRestored : IEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}