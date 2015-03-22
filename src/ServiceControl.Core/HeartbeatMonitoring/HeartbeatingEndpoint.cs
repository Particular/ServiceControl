namespace ServiceControl.HeartbeatMonitoring
{
    using System;

    class HeartbeatingEndpoint
    {
        public string Name { get; set; }
        public string HostId { get; set; }
        public string Host { get; set; }
        public bool Active { get; set; }
        public bool MonitoringDisabled { get; set; }
        public DateTime? TimeOfLastHeartbeat { get; set; }
    }
}