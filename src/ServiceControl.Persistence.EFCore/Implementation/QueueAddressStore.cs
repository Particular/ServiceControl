namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

public class QueueAddressStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IQueueAddressStore
{
    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo) =>
        ExecuteWithDbContext(async context =>
        {
            var query = context.FailedMessages
                .Select(fm => new { fm.UniqueMessageId, fm.FailingEndpointAddress })
                .Distinct()
                .GroupBy(failure => failure.FailingEndpointAddress)
                .OrderBy(failuresByEndpoint => failuresByEndpoint.Key)
                .Select(failuresByEndpoint => new QueueAddress
                {
                    PhysicalAddress = failuresByEndpoint.Key,
                    FailedMessageCount = failuresByEndpoint.Count()
                });

            var items = await query.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize).ToListAsync();

            return new QueryResult<IList<QueueAddress>>(items, new QueryStatsInfo("", query.Count(), false));
        });
}