namespace ServiceControl.HeartbeatMonitoring
{
    using System;

    public class Heartbeat
    {
        public Guid Id { get; set; }
        public DateTime LastReportAt { get; set; }
        public Guid HostId { get; set; }
        public string Endpoint { get; set; }
        public Status ReportedStatus { get; set; }
        public string KnownEndpointId { get; set; }
    }

    public enum Status
    {
        Beating,
        Dead
    }

}