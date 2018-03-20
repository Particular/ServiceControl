namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Operations;

    public class EndpointHeartbeatRestored : IHeartbeatEvent
    {
        public Guid EndpointInstanceId { get; set; }
        public EndpointDetails Endpoint { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}