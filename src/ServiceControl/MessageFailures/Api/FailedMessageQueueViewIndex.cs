namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageQueueViewIndex : AbstractIndexCreationTask<FailedMessage, FailedMessageQueueView>
    {
        public FailedMessageQueueViewIndex()
        {
            Map = messages => from message in messages
                let processingAttemptsLast = message.ProcessingAttempts.Last()
                select new
                {
                    FailedQueueAddress = processingAttemptsLast.FailureDetails.AddressOfFailingEndpoint,
                    FailedMessageCount = 1
                };

            Reduce = results => from result in results
                group result by result.FailedQueueAddress
                into g
                select new
                {
                    FailedQueueAddress = g.Key,
                    FailedMessageCount = g.Sum(m => m.FailedMessageCount)
                };

            DisableInMemoryIndexing = true;
        }
    }
}