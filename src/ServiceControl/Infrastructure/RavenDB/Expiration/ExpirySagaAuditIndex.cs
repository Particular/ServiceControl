namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.SagaAudit;

    public class ExpirySagaAuditIndex : AbstractMultiMapIndexCreationTask
    {
        public ExpirySagaAuditIndex()
        {
            AddMap<SagaSnapshot>(messages => from message in messages
                select new
                {
                    LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                });

            AddMap<SagaHistory>(sagaHistories => from sagaHistory in sagaHistories
                select new
                {
                    LastModified = MetadataFor(sagaHistory).Value<DateTime>("Last-Modified").Ticks
                });

            DisableInMemoryIndexing = true;
        }
    }
}