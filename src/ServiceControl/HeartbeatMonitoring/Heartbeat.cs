namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Contracts.Operations;

    class Heartbeat
    {
        public string Id { get; set; }
        public DateTime LastReportAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }

        public Status ReportedStatus { get; set; }
    }

    enum Status
    {
        New,
        Beating,
        Dead
    }

}