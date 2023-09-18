namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;

    class QueueAddressStore : IQueueAddressStore
    {
        readonly IDocumentStore documentStore;

        public QueueAddressStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo)
        {
            using var session = documentStore.OpenAsyncSession();
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
            using var session = documentStore.OpenAsyncSession();
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