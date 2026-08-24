namespace ServiceControl.Persistence
{
    using System;
    using ServiceControl.Persistence.Infrastructure;

    public class EndpointsView : IVersionedRow
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public bool MonitorHeartbeat { get; set; }
        public HeartbeatInformation? HeartbeatInformation { get; set; }
        public bool IsSendingHeartbeats { get; set; }
        object?[] IVersionedRow.VersionFields =>
        [
            Id, Name, HostDisplayName, Monitored, MonitorHeartbeat, IsSendingHeartbeats,
            HeartbeatInformation?.LastReportAt, HeartbeatInformation?.ReportedStatus
        ];
    }
}