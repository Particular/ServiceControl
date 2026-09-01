namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    /// <summary>
    /// One custom check as read back and returned by the API. Unlike the stored <see cref="CustomCheck"/>,
    /// it also tells ServicePulse whether the check is one ServiceControl ships itself (primary, audit or
    /// transport check) or one reported by a monitored endpoint, and how severe a failing internal check is
    /// for platform health. Both are classified from the check id at read time and are never persisted.
    /// </summary>
    public class CustomCheckView : IVersionedRow
    {
        public string? Id { get; set; }
        public string? CustomCheckId { get; set; }
        public string? Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string? FailureReason { get; set; }
        public EndpointDetails? OriginatingEndpoint { get; set; }

        /// <summary>
        /// True when this check is one ServiceControl ships itself (primary, audit or transport check),
        /// false when it was reported by a monitored endpoint. Computed from the check id.
        /// </summary>
        public bool Internal => Severity is not null;

        /// <summary>
        /// Platform-health severity for internal checks. Computed from the check id, so it cannot drift
        /// independently of it. Null — and therefore omitted from the response — for endpoint checks,
        /// which have no platform-health semantics.
        /// </summary>
        public CustomCheckSeverity? Severity => InternalCustomCheckClassification.SeverityFor(CustomCheckId);

        object?[] IVersionedRow.GetVersionFields() =>
        [
            Id, CustomCheckId, Category, Status, ReportedAt, FailureReason,
            OriginatingEndpoint?.Name, OriginatingEndpoint?.HostId, OriginatingEndpoint?.Host
        ];
    }
}