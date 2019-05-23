namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class QueueAddressIndex : AbstractIndexCreationTask<FailedMessage, QueueAddress>
    {
        public QueueAddressIndex()
        {
            Map = messages => from message in messages
                let processingAttemptsLast = message.ProcessingAttempts.Last()
                select new
                {
                    PhysicalAddress = processingAttemptsLast.FailureDetails.AddressOfFailingEndpoint,
                    FailedMessageCount = 1
                };

            Reduce = results => from result in results
                group result by result.PhysicalAddress
                into g
                select new
                {
                    PhysicalAddress = g.Key,
                    FailedMessageCount = g.Sum(m => m.FailedMessageCount)
                };

            DisableInMemoryIndexing = true;
        }
    }
}