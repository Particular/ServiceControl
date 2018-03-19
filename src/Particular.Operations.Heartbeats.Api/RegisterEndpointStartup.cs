namespace Particular.Operations.Heartbeats.Api
{
    using System;
    using System.Collections.Generic;

    public class RegisterEndpointStartup
    {
        public Guid HostId { get; set; }
        public string Endpoint { get; set; }
        public DateTime StartedAt { get; set; }
        public Dictionary<string, string> HostProperties { get; set; }
        public string HostDisplayName { get; set; }
        public string Host { get; set; }
    }
}