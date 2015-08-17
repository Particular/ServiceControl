namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    class RegisterEndpointStartup : ICommand
    {
        public Guid HostId { get; set; }
        public string Endpoint { get; set; }
        public DateTime StartedAt { get; set; }
        public Dictionary<string, string> HostProperties { get; set; }
        public string HostDisplayName { get; set; }
        public string Host { get; set; }
    }
}