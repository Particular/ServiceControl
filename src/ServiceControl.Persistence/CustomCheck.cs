namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public class CustomCheck : IVersionedRow
    {
        public string? Id { get; set; }
        public string? CustomCheckId { get; set; }
        public string? Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string? FailureReason { get; set; }
        public EndpointDetails? OriginatingEndpoint { get; set; }
        object?[] IVersionedRow.VersionFields =>
        [
            Id, CustomCheckId, Category, Status, ReportedAt, FailureReason,
            OriginatingEndpoint?.Name, OriginatingEndpoint?.HostId, OriginatingEndpoint?.Host
        ];
    }
}