namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System;
    using NServiceBus;

    public class EndpointHeartbeat : ICommand
    {
        public DateTime ExecutedAt { get; set; }

        public string EndpointName { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }
    }
}