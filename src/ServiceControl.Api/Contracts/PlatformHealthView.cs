namespace ServiceControl.Api.Contracts
{
    using System;

    public class PlatformHealthView
    {
        public string Status { get; set; }
        public string Severity { get; set; }
        public PlatformHealthAlert[] Alerts { get; set; }
        public PlatformHealthInstance[] Instances { get; set; } = [];
    }

    public class PlatformHealthAlert
    {
        public Guid Id { get; set; }
        public string InstanceId { get; set; }
        public string CheckId { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public DateTime ReportedAt { get; set; }
        public string InstanceName { get; set; }
        public string Host { get; set; }
        public Guid HostId { get; set; }
    }

#nullable enable
    public sealed record PlatformHealthInstance
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string ApiUrl { get; init; }
        public string Kind { get; init; } = "unknown";
        public string Role { get; init; } = "remote-unknown";
        public string? Version { get; init; }
        public Guid? HostId { get; init; }
        public string Health { get; init; } = "unavailable";
        public DateTimeOffset ObservedAt { get; init; }
        public DateTimeOffset? MetadataObservedAt { get; init; }
        public string HealthSignalsStatus { get; init; } = "unreported";
        public DateTimeOffset? LastReportedAt { get; init; }
        public PlatformHealthAlert[] Issues { get; init; } = [];
        public string? TransportType { get; init; }
        public string? ErrorQueue { get; init; }
        public string? ErrorLogQueue { get; init; }
        public bool? ForwardErrorMessages { get; init; }
        public string? AuditQueue { get; init; }
        public string? AuditLogQueue { get; init; }
        public bool? ForwardAuditMessages { get; init; }
        public TimeSpan? ErrorRetentionPeriod { get; init; }
        public TimeSpan? AuditRetentionPeriod { get; init; }
    }

}