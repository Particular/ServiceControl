namespace ServiceControl.MessageFailures
{
    using System.Threading;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.RavenDB.Indexes;

    class InMemoryErrorMessagesCounterCache
    {
        int total;

        public InMemoryErrorMessagesCounterCache(IDocumentStore store)
        {
            //using (var session = store.OpenSession())
            //{
            //    RavenQueryStatistics stats;
            //    session.Query<Messages_Sort.Result, Messages_Sort>()
            //        .Statistics(out stats)
            //        .Where(m =>
            //            m.Status != MessageStatus.Successful &&
            //            m.Status != MessageStatus.RetryIssued);

            //    total = stats.TotalResults;
            //}
        }

        public int Increment()
        {
            return Interlocked.Increment(ref total);
        }
    }
}