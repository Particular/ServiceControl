namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatReceived : IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime LastSentAt { get; set; }
    }
}