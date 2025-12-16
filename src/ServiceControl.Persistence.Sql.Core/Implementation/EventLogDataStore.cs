namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

public class EventLogDataStore : DataStoreBase, IEventLogDataStore
{
    public EventLogDataStore(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public Task Add(EventLogItem logItem)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = new EventLogItemEntity
            {
                Id = SequentialGuidGenerator.NewSequentialGuid(),
                Description = logItem.Description,
                Severity = (int)logItem.Severity,
                RaisedAt = logItem.RaisedAt,
                Category = logItem.Category,
                EventType = logItem.EventType,
                RelatedToJson = logItem.RelatedTo != null ? JsonSerializer.Serialize(logItem.RelatedTo, JsonSerializationOptions.Default) : null
            };

            await dbContext.EventLogItems.AddAsync(entity);
            await dbContext.SaveChangesAsync();
        });
    }

    public Task<(IList<EventLogItem> items, long total, string version)> GetEventLogItems(PagingInfo pagingInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.EventLogItems
                .AsNoTracking()
                .OrderByDescending(e => e.RaisedAt);

            var total = await query.CountAsync();

            var entities = await query
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .ToListAsync();

            var items = entities.Select(entity => new EventLogItem
            {
                Id = entity.Id.ToString(),
                Description = entity.Description,
                Severity = (Severity)entity.Severity,
                RaisedAt = entity.RaisedAt,
                Category = entity.Category,
                EventType = entity.EventType,
                RelatedTo = entity.RelatedToJson != null ? JsonSerializer.Deserialize<List<string>>(entity.RelatedToJson, JsonSerializationOptions.Default) : null
            }).ToList();

            // Version could be based on the latest RaisedAt timestamp but the paging can affect this result, given that the latest may not be retrieved
            var version = entities.Any() ? entities.Max(e => e.RaisedAt).Ticks.ToString() : "0";

            return ((IList<EventLogItem>)items, (long)total, version);
        });
    }
}
