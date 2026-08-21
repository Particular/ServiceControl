namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class EventLogDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEventLogDataStore
{
    public Task Add(EventLogItem logItem, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            dbContext.EventLogItems.Add(new EventLogItemEntity
            {
                Description = logItem.Description ?? string.Empty,
                Severity = logItem.Severity,
                RaisedAt = logItem.RaisedAt,
                RelatedTo = logItem.RelatedTo ?? [],
                Category = logItem.Category,
                EventType = logItem.EventType
            });

            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);

    public Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
        PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var query = dbContext.EventLogItems.AsNoTracking();

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

            var total = await query.LongCountAsync(token);

            return new QueryResult<IList<EventLogItemView>>(items, items.ToQueryStatsInfo(total));
        }, cancellationToken);
}
