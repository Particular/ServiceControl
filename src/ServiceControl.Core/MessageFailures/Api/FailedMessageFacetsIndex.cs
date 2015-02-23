namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class FailedMessageFacetsIndex : AbstractIndexCreationTask<MessageFailureHistory>
    {
        public FailedMessageFacetsIndex()
        {
            Map = failures => from failure in failures
                where failure.Status == FailedMessageStatus.Unresolved
                              let t = failure.ProcessingAttempts.Last().ProcessingEndpoint
                select new
                {
                    t.Name,
                    t.Host,
                    MessageType = failure.ProcessingAttempts.Last().MessageType
                };

            Index("Name", FieldIndexing.NotAnalyzed); //to avoid lower casing
            Index("Host", FieldIndexing.NotAnalyzed); //to avoid lower casing
            Index("MessageType", FieldIndexing.NotAnalyzed); //to avoid lower casing

            DisableInMemoryIndexing = true;
        }
    }
}