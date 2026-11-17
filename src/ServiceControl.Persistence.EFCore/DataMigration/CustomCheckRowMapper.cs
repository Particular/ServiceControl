namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.EFCore.Entities;

static class CustomCheckRowMapper
{
    public static CustomCheckEntity Map(CustomCheckDetail detail) => new()
    {
        // The key a live report of the same check upserts on, so the first report after the host opens replaces this row rather than adding a second.
        Id = detail.GetDeterministicId(),
        CustomCheckId = detail.CustomCheckId,
        Category = detail.Category,
        Status = detail.HasFailed ? Status.Fail : Status.Pass,
        ReportedAt = detail.ReportedAt,
        FailureReason = detail.FailureReason,
        OriginatingEndpointName = detail.OriginatingEndpoint.Name,
        OriginatingEndpointHostId = detail.OriginatingEndpoint.HostId,
        OriginatingEndpointHost = detail.OriginatingEndpoint.Host
    };
}
