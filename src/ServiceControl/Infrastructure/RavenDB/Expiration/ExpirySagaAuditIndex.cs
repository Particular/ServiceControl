namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using Raven.Json.Linq;
    using ServiceControl.SagaAudit;

    public class ExpirySagaAuditIndex : AbstractIndexCreationTask<SagaHistory, ExpirySagaAuditIndex.Result>
    {
        public class Result
        {
            public RavenJToken LastModified { get; set; }
        }

        public ExpirySagaAuditIndex()
        {
            Map = (sagaHistories => from sagaHistory in sagaHistories
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