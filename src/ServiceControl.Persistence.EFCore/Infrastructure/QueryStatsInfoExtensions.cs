namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

static class QueryStatsInfoExtensions
{
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

    public static QueryStatsInfo ToQueryStatsInfo(this RetryHistory history) =>
        new QueryStatsInfo(DataVersion.OverRows(
                [("historic", history.HistoricOperations.Count), ("unacknowledged", history.UnacknowledgedOperations.Count)],
                history.HistoricOperations.Concat<IVersionedRow>(history.UnacknowledgedOperations)),
            history.HistoricOperations.Count);
}
