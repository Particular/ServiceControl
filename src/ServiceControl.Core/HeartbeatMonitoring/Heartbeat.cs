namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Contracts.Operations;

    public class Heartbeat
    {
        public Guid Id { get; set; }
        public DateTime LastReportAt { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public Status ReportedStatus { get; set; }
        public bool Disabled { get; set; }
    }

    public enum Status
    {
        Beating,
        Dead
    }

}