namespace ServiceControl.Contracts.Operations
{
    using System;
    using NServiceBus;

    public class EndpointHeartbeatReceived:IEvent
    {
        public DateTime SentAt { get; set; }
        public string Endpoint { get; set; }
    }
}