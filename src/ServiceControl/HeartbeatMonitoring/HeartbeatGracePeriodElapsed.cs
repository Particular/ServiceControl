namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatGracePeriodElapsed : IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public DateTime LastSentAt { get; set; }
    }
}