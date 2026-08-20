namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;

    class QueueAddressStore(IRavenSessionProvider sessionProvider) : IQueueAddressStore
    {
        public async Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var addresses = await session
                .Query<QueueAddress, QueueAddressIndex>()
                .Statistics(out var stats)
                .Paging(pagingInfo)
                .ToListAsync(cancellationToken);

            var result = new QueryResult<IList<QueueAddress>>(addresses,
                stats.ToPagedQueryStatsInfo(addresses, address => address.PhysicalAddress, ("page", pagingInfo.Page), ("pageSize", pagingInfo.PageSize)));
            return result;
        }
    }
}