namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.Infrastructure;

static class CustomCheckQueries
{
    /// <summary>
    /// Every field of every check the body shows, plus the total.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<CustomCheck> page, long totalCount) =>
        new(DataVersion.Compose(
                ("checks", totalCount),
                ("page", string.Join("|", page.Select(check => FormattableString.Invariant(
                    $"{check.Id}.{check.CustomCheckId}.{check.Category}.{check.Status}.{check.ReportedAt.Ticks}.{check.FailureReason}"))))),
            totalCount,
            false);
}
