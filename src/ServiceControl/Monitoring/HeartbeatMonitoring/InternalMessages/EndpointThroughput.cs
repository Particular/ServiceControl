namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System;
    using System.Collections.Generic;

    class EndpointThroughput
    {
        public string EndpointName { get; set; }
        public Guid HostId { get; set; }
        public string Host { get; set; }
        public Dictionary<DateTime, long> Throughput { get; set; }
    }
}
