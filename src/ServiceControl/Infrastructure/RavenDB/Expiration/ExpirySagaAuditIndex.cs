namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
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
            Map = sagaHistories => from sagaHistory in sagaHistories
                select new Result
                {
                    LastModified = MetadataFor(sagaHistory).Value<DateTime>("Last-Modified").Ticks
                };

            DisableInMemoryIndexing = true;
        }

    }
}