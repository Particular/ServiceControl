﻿namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDb;
    using ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public RavenDbAuditIngestionUnitOfWorkFactory(IRavenDbDocumentStoreProvider documentStoreProvider, IRavenDbSessionProvider sessionProvider,
            DatabaseConfiguration databaseConfiguration, CheckMinimumStorageRequiredForAuditIngestion.State customCheckState)
        {
            this.documentStoreProvider = documentStoreProvider;
            this.sessionProvider = sessionProvider;
            this.databaseConfiguration = databaseConfiguration;
            this.customCheckState = customCheckState;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            return new RavenDbAuditIngestionUnitOfWork(
                bulkInsert, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }

        readonly IRavenDbDocumentStoreProvider documentStoreProvider;
        readonly IRavenDbSessionProvider sessionProvider;
        readonly DatabaseConfiguration databaseConfiguration;
        readonly CheckMinimumStorageRequiredForAuditIngestion.State customCheckState;
    }
}
