namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.EFCore.Entities;

public class EventLogDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEventLogDataStore
{
    public Task Add(EventLogItem logItem, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            dbContext.EventLogItems.Add(new EventLogItemEntity
            {
                Description = logItem.Description,
                Severity = logItem.Severity,
                RaisedAt = logItem.RaisedAt,
                RelatedTo = logItem.RelatedTo ?? [],
                Category = logItem.Category,
                EventType = logItem.EventType
            });

            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);

    public Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
        PagingInfo pagingInfo, string? knownVersion = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var query = dbContext.EventLogItems.AsNoTracking();

            // All three aggregates in one round trip. Grouping on a constant collapses the table to a
            // single row, and an empty table yields no rows at all, hence the null coalescing.
            var stats = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.LongCount(),
                    Newest = g.Max(e => (DateTime?)e.RaisedAt),
                    HighestId = g.Max(e => (long?)e.Id)
                })
                .FirstOrDefaultAsync(token);

            var total = stats?.Total ?? 0;
            var version = Version(total, stats?.Newest, stats?.HighestId);
            var queryStats = new QueryStatsInfo(version, total, isStale: false);

            // The point of knownVersion. Everything above is index work.
            // If the caller already has the latest version, skip the rest of the query.
            // No database round trip is needed. No response body is needed.
            if (knownVersion is not null && knownVersion == version)
            {
                return QueryResult<IList<EventLogItemView>>.Unchanged(queryStats);
            }

            var items = await query
                // The key breaks ties so that items sharing a RaisedAt cannot shuffle between
                // pages. IX_EventLogItems_RaisedAt_Id is declared in exactly this order.
                .OrderByDescending(e => e.RaisedAt)
                .ThenByDescending(e => e.Id)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .Select(e => new EventLogItemView
                {
                    Id = e.Id.ToString(),
                    Description = e.Description,
                    Severity = e.Severity,
                    RaisedAt = e.RaisedAt,
                    RelatedTo = e.RelatedTo,
                    Category = e.Category,
                    EventType = e.EventType
                })
                .ToListAsync(token);

            return new QueryResult<IList<EventLogItemView>>(items, queryStats);
        }, cancellationToken);

    // Synthesised version ID to be used for an ETag. The highest key is the monotonic term: identity
    // values gap but never repeat, so an insert moves the version whatever its RaisedAt says.
    static string Version(long total, DateTime? newest, long? highestId) =>
        DeterministicGuid.MakeId($"{total}|{newest?.Ticks ?? 0}|{highestId ?? 0}").ToString();
}
