namespace ServiceControl.HeartbeatMonitoring
{
    using System;

    public class HeartbeatStatus
    {
        public bool Active { get; set; }

        public string Endpoint { get; set; }

        public string Machine { get; set; }

        public DateTime LastSentAt { get; set; }
    }
}