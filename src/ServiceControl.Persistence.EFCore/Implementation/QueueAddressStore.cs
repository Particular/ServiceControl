namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

public class QueueAddressStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IQueueAddressStore
{
    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async context =>
        {
            var query = context.FailedMessages
                .GroupBy(failure => failure.FailingEndpointAddress)
                .OrderBy(failuresByEndpoint => failuresByEndpoint.Key)
                .Select(failuresByEndpoint => new QueueAddress
                {
                    PhysicalAddress = failuresByEndpoint.Key,
                    FailedMessageCount = failuresByEndpoint.Count()
                });

            var items = await query.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize).ToListAsync(cancellationToken);
            var eTag = DeterministicGuid.MakeId($"{items.Count}|{string.Join(",", items.Select(x => x.PhysicalAddress))}").ToString();

            return new QueryResult<IList<QueueAddress>>(items, new QueryStatsInfo(eTag, query.Count(), false));
        });
}