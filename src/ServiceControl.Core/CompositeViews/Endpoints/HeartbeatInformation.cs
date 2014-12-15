namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using HeartbeatMonitoring;

    public class HeartbeatInformation
    {
        public DateTime LastReportAt { get; set; }
        public Status ReportedStatus { get; set; }
    }
}