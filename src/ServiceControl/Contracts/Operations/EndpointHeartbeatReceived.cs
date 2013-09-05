namespace ServiceControl.Contracts.Operations
{
    using System;

    public class EndpointHeartbeatReceived
    {
        public DateTime SentAt { get; set; }
        public string Endpoint { get; set; }
        public string Machine { get; set; }
    }
}