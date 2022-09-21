namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using ServiceControl.Audit.Infrastructure.Settings;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;
        TimeSpan auditRetentionPeriod;

        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store, Settings settings)
        {
            this.store = store;
            auditRetentionPeriod = settings.AuditRetentionPeriod;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
            => new RavenDbAuditIngestionUnitOfWork(
                store.BulkInsert(
                    options: new BulkInsertOptions
                    {
                        SkipOverwriteIfUnchanged = true,
                    }), auditRetentionPeriod);
    }
}
