namespace ServiceControl.CompositeViews.Endpoints
{
    using System;

    public class EndpointsView
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string HostDisplayName { get; set; }

        public bool Monitored { get; set; }

        public bool MonitorHeartbeat { get; set; }
        public string LicenseStatus { get; set; }
        public HeartbeatInformation HeartbeatInformation { get; set; }
        public bool IsSendingHeartbeats { get; set; }
    }
}