namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Persistence.UnitOfWork;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;

        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store) => this.store = store;

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
            => new RavenDbAuditIngestionUnitOfWork(
                store.BulkInsert(
                    options: new BulkInsertOptions
                    {
                        OverwriteExisting = true,
                        ChunkedBulkInsertOptions = null,
                        BatchSize = batchSize
                    }));
    }
}
