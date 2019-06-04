namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Contracts.Operations;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class FailedMessageFacetsIndex : AbstractIndexCreationTask<FailedMessage>
    {
        public FailedMessageFacetsIndex()
        {
            Map = failures => from failure in failures
                where failure.Status == FailedMessageStatus.Unresolved
                let t = (EndpointDetails)failure.ProcessingAttempts.Last().MessageMetadata["ReceivingEndpoint"]
                select new
                {
                    t.Name,
                    t.Host,
                    MessageType = failure.ProcessingAttempts.Last().MessageMetadata["MessageType"]
                };

            Index("Name", FieldIndexing.NotAnalyzed); //to avoid lower casing
            Index("Host", FieldIndexing.NotAnalyzed); //to avoid lower casing
            Index("MessageType", FieldIndexing.NotAnalyzed); //to avoid lower casing

            DisableInMemoryIndexing = true;
        }
    }
}