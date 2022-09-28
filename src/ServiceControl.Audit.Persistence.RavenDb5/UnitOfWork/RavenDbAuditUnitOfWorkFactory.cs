namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;
        TimeSpan auditRetentionPeriod;
        int settingsMaxBodySizeToStore;


        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store, PersistenceSettings settings)
        {
            this.store = store;
            auditRetentionPeriod = settings.AuditRetentionPeriod;
            settingsMaxBodySizeToStore = settings.MaxBodySizeToStore;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var bulkInsert = store.BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            return new RavenDbAuditIngestionUnitOfWork(
                bulkInsert, auditRetentionPeriod, new RavenAttachmentsBodyStorage(store, bulkInsert, settingsMaxBodySizeToStore)
            );
        }
    }
}
