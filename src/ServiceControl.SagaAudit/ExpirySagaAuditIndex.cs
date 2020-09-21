namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using Raven.Client;
    using Raven.Client.Documents.Indexes;
    using SagaAudit;

    public class ExpirySagaAuditIndex : AbstractMultiMapIndexCreationTask
    {
        public ExpirySagaAuditIndex()
        {
            AddMap<SagaSnapshot>(messages => from message in messages
                select new
                {
                    LastModified = MetadataFor(message).Value<DateTime>(Constants.Documents.Metadata.LastModified).Ticks
                });

            AddMap<SagaHistory>(sagaHistories => from sagaHistory in sagaHistories
                select new
                {
                    LastModified = MetadataFor(sagaHistory).Value<DateTime>(Constants.Documents.Metadata.LastModified).Ticks
                });
        }
    }
}