namespace ServiceControl.HeartbeatMonitoring
{
    using System;

    public class HeartbeatStatus
    {
        public bool? Failing { get; set; }

        public string Endpoint { get; set; }

        public string Machine { get; set; }

        public DateTime LastHeartbeatSentAt { get; set; }
    }
}