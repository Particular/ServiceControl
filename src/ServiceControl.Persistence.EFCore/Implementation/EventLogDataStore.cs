namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.EFCore.Entities;

public class EventLogDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEventLogDataStore
{
    public Task Add(EventLogItem logItem, Guid eventId) =>
        ExecuteWithDbContext(async dbContext =>
        {
            dbContext.EventLogItems.Add(new EventLogItemEntity
            {
                UniqueEventId = eventId,
                Description = logItem.Description,
                Severity = logItem.Severity,
                RaisedAt = logItem.RaisedAt,
                RelatedTo = logItem.RelatedTo ?? [],
                Category = logItem.Category,
                EventType = logItem.EventType
            });

            await dbContext.SaveChangesAsync();
        });

    public Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
        PagingInfo pagingInfo, string? knownVersion = null) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.EventLogItems.AsNoTracking();

            // Both aggregates in one round trip. Grouping on a constant collapses the table to a
            // single row, and an empty table yields no rows at all, hence the null coalescing.
            var stats = await query
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.LongCount(), Newest = g.Max(e => (DateTime?)e.RaisedAt) })
                .FirstOrDefaultAsync();

            var total = stats?.Total ?? 0;
            var version = Version(total, stats?.Newest);
            var queryStats = new QueryStatsInfo(version, total, isStale: false);

            // The point of knownVersion. Everything above is index work.
            // If the caller already has the latest version, skip the rest of the query.
            // No database round trip is needed. No response body is needed.
            if (knownVersion is not null && knownVersion == version)
            {
                return QueryResult<IList<EventLogItemView>>.Unchanged(queryStats);
            }

            var rows = await query
                // The key breaks ties so that items sharing a RaisedAt cannot shuffle between
                // pages. IX_EventLogItems_RaisedAt_Id is declared in exactly this order.
                .OrderByDescending(e => e.RaisedAt)
                .ThenByDescending(e => e.Id)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .ToListAsync();

            // The id is stringified here, not in the query: SQL Server converts uniqueidentifier
            // to uppercase hex, while Guid.ToString() and PostgreSQL both produce lowercase.
            var items = rows.Select(e => new EventLogItemView
            {
                Id = e.UniqueEventId.ToString(),
                Description = e.Description,
                Severity = e.Severity,
                RaisedAt = e.RaisedAt,
                RelatedTo = e.RelatedTo,
                Category = e.Category,
                EventType = e.EventType
            }).ToList();

            return new QueryResult<IList<EventLogItemView>>(items, queryStats);
        });

    // Synthesised version ID to be used for an ETag, using total count and the newest item's RaisedAt timestamp.
    static string Version(long total, DateTime? newest) =>
        DeterministicGuid.MakeId($"{total}|{newest?.Ticks ?? 0}").ToString();
}
