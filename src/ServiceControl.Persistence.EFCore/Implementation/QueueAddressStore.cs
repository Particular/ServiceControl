namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

public class QueueAddressStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IQueueAddressStore
{
    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (context, token) =>
        {
            var query = context.FailedMessages
                .GroupBy(failure => failure.FailingEndpointAddress)
                .OrderBy(failuresByEndpoint => failuresByEndpoint.Key)
                .Select(failuresByEndpoint => new QueueAddress
                {
                    PhysicalAddress = failuresByEndpoint.Key,
                    FailedMessageCount = failuresByEndpoint.Count()
                });

            var items = await query.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize).ToListAsync(token);
            var addressCount = await query.CountAsync(token);

            // Both fields of every row the body shows, plus the total, gives a reliable data version.
            var version = DataVersion.Compose(
                ("addresses", addressCount),
                ("page", string.Join("|", items.Select(address => $"{address.PhysicalAddress}={address.FailedMessageCount}"))));

            return new QueryResult<IList<QueueAddress>>(items, new QueryStatsInfo(version, addressCount, false));
        }, cancellationToken);
}