namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Persistence.UnitOfWork;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;

        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store) => this.store = store;

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
            => new RavenDbAuditIngestionUnitOfWork(
                store.BulkInsert(
                    options: new BulkInsertOptions
                    {
                        SkipOverwriteIfUnchanged = true,
                    }));
    }
}
