namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using RavenDb5;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;

    class QueueAddressStore : IQueueAddressStore
    {
        readonly DocumentStoreProvider storeProvider;

        public QueueAddressStore(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
        }

        public async Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo)
        {
            using var session = storeProvider.Store.OpenAsyncSession();
            var addresses = await session
                .Query<QueueAddress, QueueAddressIndex>()
                .Statistics(out var stats)
                .Paging(pagingInfo)
                .ToListAsync();

            var result = new QueryResult<IList<QueueAddress>>(addresses, stats.ToQueryStatsInfo());
            return result;
        }

        public async Task<QueryResult<IList<QueueAddress>>> GetAddressesBySearchTerm(string search, PagingInfo pagingInfo)
        {
            using var session = storeProvider.Store.OpenAsyncSession();
            var failedMessageQueues = await session
                    .Query<QueueAddress, QueueAddressIndex>()
                    .Statistics(out var stats)
                    .Paging(pagingInfo)
                    .Where(q => q.PhysicalAddress.StartsWith(search))
                    .OrderBy(q => q.PhysicalAddress)
                    .ToListAsync();

            var result = new QueryResult<IList<QueueAddress>>(failedMessageQueues, stats.ToQueryStatsInfo());
            return result;
        }
    }
}