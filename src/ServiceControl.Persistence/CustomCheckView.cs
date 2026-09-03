namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    /// <summary>
    /// One custom check as read back and returned by the API. Unlike the stored <see cref="CustomCheck"/>,
    /// it also tells ServicePulse whether the check is one ServiceControl ships itself (primary, audit or
    /// transport check) or one reported by a monitored endpoint. The flag is classified from the check id
    /// at read time and is never persisted.
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
        public bool Internal => InternalCustomCheckClassification.IsInternal(CustomCheckId);

        object?[] IVersionedRow.GetVersionFields() =>
        [
            Id, CustomCheckId, Category, Status, ReportedAt, FailureReason,
            OriginatingEndpoint?.Name, OriginatingEndpoint?.HostId, OriginatingEndpoint?.Host
        ];
    }
}