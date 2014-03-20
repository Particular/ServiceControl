namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System;
    using System.Collections.Generic;

    class EndpointHeartbeat
    {
        public DateTime ExecutedAt { get; set; }

        public string EndpointName { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }
    }

    class RegisterEndpointStartup
    {
        public Guid HostId { get; set; }
        public string Endpoint { get; set; }
        public DateTime StartedAt { get; set; }
        public Dictionary<string, string> HostProperties { get; set; }
        public string HostDisplayName { get; set; }
    }
}