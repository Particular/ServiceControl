namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Operations;

    public class EndpointHeartbeatRestored : HeartbeatStatusChanged
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime RestoredAt { get; set; }
    }
}