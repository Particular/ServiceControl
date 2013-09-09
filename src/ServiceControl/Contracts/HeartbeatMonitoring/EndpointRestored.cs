namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class EndpointRestored : IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime At { get; set; }
    }
}