namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using ServiceControl.Contracts.Operations;

    public class EndpointDetected : IHeartbeatEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public Guid EndpointInstanceId { get; set; }
    }
}