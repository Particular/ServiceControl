namespace ServiceControl.EndpointControl
{
    using System;

    public class KnownEndpoint
    {
        public KnownEndpoint()
        {
            MonitorHeartbeat = true;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string HostDisplayName { get; set; }
        public bool MonitorHeartbeat { get; set; }
    }
}