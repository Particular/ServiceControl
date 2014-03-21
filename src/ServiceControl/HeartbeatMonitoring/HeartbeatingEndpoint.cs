namespace ServiceControl.HeartbeatMonitoring
{
    using System;

    class HeartbeatingEndpoint
    {
        public string Name { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }
        public bool Active { get; set; }
        public bool MonitoringDisabled { get; set; }
    }
}