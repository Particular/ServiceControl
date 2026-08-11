namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

public class FailedMessageQueryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageQueryDataStore
{
    public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string? status, string? modified, string? queueAddress, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => dbContext.FailedMessages
            .AsNoTracking()
            .FilterByStatus(status)
            .FilterByLastModifiedRange(modified)
            .FilterByQueueAddress(queueAddress)
            .ToPagedResult(pagingInfo, sortInfo, cancellationToken));

    public Task<QueryStatsInfo> GetFailedMessagesStats(string? status, string? modified, string? queueAddress, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => dbContext.FailedMessages
            .AsNoTracking()
            .FilterByStatus(status)
            .FilterByLastModifiedRange(modified)
            .FilterByQueueAddress(queueAddress)
            .ToQueryStatsInfo(cancellationToken));

    public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string? status, string endpointName, string? modified, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.ReceivingEndpointName == endpointName)
            .FilterByStatus(status)
            .FilterByLastModifiedRange(modified)
            .ToPagedResult(pagingInfo, sortInfo, cancellationToken));

    public Task<IDictionary<string, object>> GetFailedMessagesSummary(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var endpoints = await CountBy(dbContext, message => message.ReceivingEndpointName, cancellationToken);
            var hosts = await CountBy(dbContext, message => message.ReceivingEndpointHost, cancellationToken);
            var messageTypes = await CountBy(dbContext, message => message.MessageType, cancellationToken);

            return (IDictionary<string, object>)new Dictionary<string, object>
            {
                [FailedMessageSummaryKeys.Endpoints] = endpoints,
                [FailedMessageSummaryKeys.Hosts] = hosts,
                [FailedMessageSummaryKeys.MessageTypes] = messageTypes
            };
        });

    public Task<FailedMessageView?> GetLatestFailedMessageView(string failedMessageId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
            {
                return null;
            }

            var entity = await dbContext.FailedMessages
                .AsNoTracking()
                .SingleOrDefaultAsync(message => message.UniqueMessageId == uniqueMessageId, cancellationToken);

            return entity?.ToFailedMessageView();
        });

    public Task<FailedMessage?> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
            {
                return null;
            }

            var entity = await dbContext.FailedMessages
                .AsNoTracking()
                .SingleOrDefaultAsync(message => message.UniqueMessageId == uniqueMessageId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            var groups = await ReadGroups(dbContext, [uniqueMessageId], cancellationToken);

            return entity.ToFailedMessage(groups.GetValueOrDefault(uniqueMessageId, []));
        });

    public Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext<FailedMessage[]>(async dbContext =>
        {
            var entities = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(message => ids.Contains(message.UniqueMessageId))
                .ToListAsync(cancellationToken);

            if (entities.Count == 0)
            {
                return [];
            }

            var groups = await ReadGroups(dbContext, [.. entities.Select(entity => entity.UniqueMessageId)], cancellationToken);

            return [.. entities.Select(entity => entity.ToFailedMessage(groups.GetValueOrDefault(entity.UniqueMessageId, [])))];
        });

    static async Task<Dictionary<string, object>> CountBy(ServiceControlDbContext dbContext, Expression<Func<FailedMessageEntity, string?>> selector, CancellationToken cancellationToken) =>
        await dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.Status == FailedMessageStatus.Unresolved)
            .Select(selector)
            .Where(value => value != null && value != string.Empty)
            .GroupBy(value => value)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Value!, row => (object)row.Count, cancellationToken);

    static async Task<Dictionary<Guid, List<FailedMessageGroupEntity>>> ReadGroups(ServiceControlDbContext dbContext, Guid[] uniqueMessageIds, CancellationToken cancellationToken)
    {
        var groups = await dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => uniqueMessageIds.Contains(group.FailedMessageUniqueId))
            .ToListAsync(cancellationToken);

        return groups
            .GroupBy(group => group.FailedMessageUniqueId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }
}
