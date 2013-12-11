namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Contracts.Operations;
    using MessageAuditing;

    class Heartbeat
    {
        public Guid Id { get; set; }
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