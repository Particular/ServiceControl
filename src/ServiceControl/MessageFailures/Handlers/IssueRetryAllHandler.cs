namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;

    public class IssueRetryAllHandler : IHandleMessages<RequestRetryAll>
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public void Handle(RequestRetryAll message)
        {
            RavenQueryStatistics stats;
            var skip = 0;
            var numberOfRequestsExecutedSoFar = 0;

           

            do
            {
                using (var session = Store.OpenSession())
                {
                    IQueryable<FailedMessage> query = session.Query<FailedMessage>()
                        .Statistics(out stats);


                    if (message.Endpoint != null)
                    {
                        query = session.Query<FailedMessage>()
                            .Where(fm => fm.MostRecentAttempt.FailureDetails.AddressOfFailingEndpoint.Queue == message.Endpoint)
                            .Statistics(out stats);
                    }

                    do
                    {
                        var results = query
                            .Skip(skip)
                            .Take(1024)
                            .ToArray();

                        foreach (var result in results)
                        {
                            var retryMessage = new RequestRetry { FailedMessageId = result.Id };
                            message.SetHeader("RequestedAt", Bus.CurrentMessageContext.Headers["RequestedAt"]);

                            Bus.SendLocal(retryMessage);
                        }

                        skip += 1024;
                    } while (skip < stats.TotalResults &&
                             ++numberOfRequestsExecutedSoFar < session.Advanced.MaxNumberOfRequestsPerSession);
                }
            } while (skip < stats.TotalResults);
        }



    }
}