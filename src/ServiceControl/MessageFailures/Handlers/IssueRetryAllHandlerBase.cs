namespace ServiceBus.Management.MessageFailures.Handlers
{
    using System.Linq;
    using Infrastructure.RavenDB.Indexes;
    using InternalMessages;
    using MessageAuditing;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;

    public abstract class IssueRetryAllHandlerBase
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        protected void ExecuteQuery()
        {
            RavenQueryStatistics stats;
            var skip = 0;
            var numberOfRequestsExecutedSoFar = 0;

            do
            {
                using (var session = Store.OpenSession())
                {
                    var query = session.Query<Messages_Ids.Result, Messages_Ids>()
                        .Statistics(out stats);

                    AddWhere(query);

                    do
                    {
                        var results = query
                            .Skip(skip)
                            .Take(1024)
                            .OfType<Message>()
                            .ToArray();

                        foreach (var result in results)
                        {
                            Bus.SendLocal(new IssueRetry {MessageId = result.Id});
                        }

                        skip += 1024;
                    } while (skip < stats.TotalResults &&
                             ++numberOfRequestsExecutedSoFar < session.Advanced.MaxNumberOfRequestsPerSession);
                }
            } while (skip < stats.TotalResults);
        }

        protected virtual void AddWhere(IRavenQueryable<Messages_Ids.Result> query)
        {
        }
    }
}