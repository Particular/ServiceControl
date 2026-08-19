namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.Infrastructure;

static class CustomCheckQueries
{
    /// <summary>
    /// Every field of every check the body shows, plus the total. OriginatingEndpoint has no term of its
    /// own and does not need one: Id is a deterministic hash of the endpoint name, its host id and the
    /// check id, so naming Id covers all three, and the host string is written once on insert and never
    /// updated.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<CustomCheck> page, long totalCount) =>
        new(DataVersion.OverRows([("checks", totalCount)], page,
                check => [check.Id, check.CustomCheckId, check.Category, check.Status, check.ReportedAt, check.FailureReason]),
            totalCount,
            false);
}
