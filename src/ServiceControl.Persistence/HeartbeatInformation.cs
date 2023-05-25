namespace ServiceControl.Persistence
{
    using System;

    public class HeartbeatInformation
    {
        public DateTime LastReportAt { get; set; }
        public Status ReportedStatus { get; set; }
    }
}