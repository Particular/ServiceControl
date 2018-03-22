namespace Particular.HealthMonitoring.Uptime
{
    using System;

    public class HeartbeatInformation
    {
        public DateTime LastReportAt { get; set; }
        public Status ReportedStatus { get; set; }
    }
}