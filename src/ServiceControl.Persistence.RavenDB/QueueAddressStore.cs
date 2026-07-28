namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;

    class QueueAddressStore(IRavenSessionProvider sessionProvider) : IQueueAddressStore
    {
        public async Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo)
        {
            using var session = await sessionProvider.OpenSession();
            var addresses = await session
                .Query<QueueAddress, QueueAddressIndex>()
                .Statistics(out var stats)
                .Paging(pagingInfo)
                .ToListAsync();

            var result = new QueryResult<IList<QueueAddress>>(addresses, stats.ToQueryStatsInfo());
            return result;
        }
    }
}