namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageQueueIndex : AbstractIndexCreationTask<FailedMessage, FailedMessageQueue>
    {
        public FailedMessageQueueIndex()
        {
            Map = messages => from message in messages
                let processingAttemptsLast = message.ProcessingAttempts.Last()
                select new
                {
                    FailedMessageQueueAddress = processingAttemptsLast.FailureDetails.AddressOfFailingEndpoint.ToLowerInvariant(),
                    FailedMessageQueueDisplayName = processingAttemptsLast.FailureDetails.AddressOfFailingEndpoint,
                    FailedMessageCount = 1
                };

            Reduce = results => from result in results
                group result by result.FailedMessageQueueAddress
                into g
                select new
                {
                    FailedQueueAddress = g.Key,
                    FailedMessageQueueDisplayName = g.First().FailedMessageQueueDisplayName,
                    FailedMessageCount = g.Sum(m => m.FailedMessageCount)
                };

            DisableInMemoryIndexing = true;
        }
    }
}