namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Contracts.Operations;
    using Raven.Client.Documents.Indexes;

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

            Index("Name", FieldIndexing.Exact); //to avoid lower casing
            Index("Host", FieldIndexing.Exact); //to avoid lower casing
            Index("MessageType", FieldIndexing.Exact); //to avoid lower casing
        }
    }
}