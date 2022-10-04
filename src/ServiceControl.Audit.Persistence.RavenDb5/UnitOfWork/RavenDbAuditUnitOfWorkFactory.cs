namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {

        public RavenDbAuditIngestionUnitOfWorkFactory(IRavenDbDocumentStoreProvider documentStoreProvider, IRavenDbSessionProvider sessionProvider, PersistenceSettings settings)
        {
            this.documentStoreProvider = documentStoreProvider;
            this.sessionProvider = sessionProvider;

            auditRetentionPeriod = settings.AuditRetentionPeriod;
            settingsMaxBodySizeToStore = settings.MaxBodySizeToStore;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            return new RavenDbAuditIngestionUnitOfWork(
                bulkInsert, auditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, settingsMaxBodySizeToStore)
            );
        }

        readonly TimeSpan auditRetentionPeriod;
        readonly int settingsMaxBodySizeToStore;
        readonly IRavenDbDocumentStoreProvider documentStoreProvider;
        readonly IRavenDbSessionProvider sessionProvider;
    }
}
