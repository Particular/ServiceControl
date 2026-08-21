namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

static class QueryStatsInfoExtensions
{
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<CustomCheck> items, long totalCount) =>
        new QueryStatsInfo(
            DataVersion.OverRows(
                [("checks", totalCount)],
                items,
                check => [check.Id, check.CustomCheckId, check.Category, check.Status, check.ReportedAt, check.FailureReason]),
            totalCount);

    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<EventLogItemView> items, long totalCount) =>
        new QueryStatsInfo(
            DataVersion.OverRows(
                [("items", totalCount)],
                items,
                item => [item.Id, item.Description, item.Severity, item.RaisedAt, item.Category, item.EventType]),
            totalCount);

    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<FailedMessageEntity> items, long totalCount) =>
        new QueryStatsInfo(
            DataVersion.OverRows(
                [("messages", totalCount)],
                items,
                row => [row.UniqueMessageId, row.LastModified, row.Status, row.NumberOfProcessingAttempts]),
            totalCount);

    // Used in a HEAD with no body. Total-Count is the whole response, so the count is the whole version.
    public static async Task<QueryStatsInfo> ToCountQueryStatsInfo<TEntity>(this IQueryable<TEntity> source, string name, CancellationToken cancellationToken = default)
    {
        var count = await source.LongCountAsync(cancellationToken);

        return new QueryStatsInfo(DataVersion.Compose([(name, count)]), count);
    }

    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<FailureGroupView> groups) =>
        new QueryStatsInfo(
            DataVersion.OverRows(
                [("groups", groups.Count)],
                groups,
                group => [group.Id, group.Title, group.Type, group.Count, group.Comment, group.First, group.Last]),
            groups.Count);

    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<QueueAddress> items, long totalCount) =>
        new QueryStatsInfo(
            DataVersion.OverRows(
                [("addresses", totalCount)],
                items,
                address => [address.PhysicalAddress, address.FailedMessageCount]),
            totalCount);
}
