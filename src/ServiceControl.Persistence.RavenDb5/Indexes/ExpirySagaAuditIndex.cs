namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Indexes;

    class ExpirySagaAuditIndex : AbstractMultiMapIndexCreationTask
    {
        public ExpirySagaAuditIndex()
        {
            AddMap<SagaSnapshot>(messages => from message in messages
                                             select new
                                             {
                                                 LastModified = MetadataFor(message).Value<DateTime>("@last-modified").Ticks
                                             });

            AddMap<SagaHistory>(sagaHistories => from sagaHistory in sagaHistories
                                                 select new
                                                 {
                                                     LastModified = MetadataFor(sagaHistory).Value<DateTime>("@last-modified").Ticks
                                                 });
        }
    }
}