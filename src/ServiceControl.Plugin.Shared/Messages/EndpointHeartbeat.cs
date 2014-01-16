namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System;

    class EndpointHeartbeat
    {
        public string HostId { get; set; }
        public string Endpoint { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}