namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.EFCore.Entities;

public class EventLogDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEventLogDataStore
{
    public Task Add(EventLogItem logItem) =>
        ExecuteWithDbContext(async dbContext =>
        {
            dbContext.EventLogItems.Add(new EventLogItemEntity
            {
                EventLogItemId = logItem.Id,
                Description = logItem.Description,
                Severity = logItem.Severity,
                RaisedAt = logItem.RaisedAt,
                // The column is non-null. The API model allows null and the SignalR broadcast path
                // deliberately empties it, so normalise rather than reject.
                RelatedTo = logItem.RelatedTo ?? [],
                Category = logItem.Category,
                EventType = logItem.EventType
            });

            await dbContext.SaveChangesAsync();
        });

    public Task<(IList<EventLogItem>? items, long total, string version)> GetEventLogItems(
        PagingInfo pagingInfo, string? knownVersion = null) =>
        ExecuteWithDbContext<(IList<EventLogItem>? items, long total, string version)>(async dbContext =>
        {
            var query = dbContext.EventLogItems.AsNoTracking();

            var total = await query.LongCountAsync();
            var newest = await query.MaxAsync(e => (DateTime?)e.RaisedAt);
            var version = Version(total, newest);

            // The point of knownVersion. Everything above is index work.
            // If the caller already has the latest version, skip the rest of the query.
            // No database round trip is needed. No response body is needed.
            if (knownVersion is not null && knownVersion == version)
            {
                return (null, total, version);
            }

            var items = await query
                // The key breaks ties so that items sharing a RaisedAt cannot shuffle between
                // pages. IX_EventLogItems_RaisedAt_Id is declared in exactly this order.
                .OrderByDescending(e => e.RaisedAt)
                .ThenByDescending(e => e.Id)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .Select(e => new EventLogItem
                {
                    Id = e.EventLogItemId,
                    Description = e.Description,
                    Severity = e.Severity,
                    RaisedAt = e.RaisedAt,
                    RelatedTo = e.RelatedTo,
                    Category = e.Category,
                    EventType = e.EventType
                })
                .ToListAsync();

            return (items, total, version);
        });

    // Raven returns the query's ResultEtag but there is no relational equivalent, so it is synthesised
    // using total count and the newest item's RaisedAt timestamp.
    static string Version(long total, DateTime? newest) =>
        DeterministicGuid.MakeId($"{total}|{newest?.Ticks ?? 0}").ToString();
}
