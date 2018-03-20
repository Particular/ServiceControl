namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using ServiceControl.Monitoring;

    public class EndpointUptimeInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid HostId { get; set; }
        public bool Monitored { get; set; }
        public HeartbeatStatus Status { get; set; }
    }
}