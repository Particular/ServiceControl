namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using Raven.Json.Linq;
    using ServiceControl.SagaAudit;

    public class ExpiryProcessedMessageIndex : AbstractMultiMapIndexCreationTask<ExpiryProcessedMessageIndex.Result>
    {
        public class Result
        {
            public RavenJToken LastModified { get; set; }
        }

        public ExpiryProcessedMessageIndex()
        {
            AddMap<ProcessedMessage>(messages => from message in messages
                                                 select new Result
                                                 {
                                                     LastModified = MetadataFor(message)["Last-Modified"],
                                                 });
            AddMap<SagaHistory>(sagaHistories => from sagaHistory in sagaHistories
                                                 select new Result
                                                 {
                                                     LastModified = MetadataFor(sagaHistory)["Last-Modified"],
                                                 });
            DisableInMemoryIndexing = true;

            Sort(result => result.LastModified, SortOptions.String);
            Stores.Add(result => result.LastModified, FieldStorage.Yes);
        }

    }
}