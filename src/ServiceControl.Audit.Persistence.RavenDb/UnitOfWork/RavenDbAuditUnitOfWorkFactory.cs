namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using Persistence.UnitOfWork;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;
        BodyStorageEnricher bodyStorageEnricher;

        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store, IBodyStorage bodyStorage, Settings settings)
        {
            this.store = store;
            bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var bulkInsert = store.BulkInsert(
                options: new BulkInsertOptions
                {
                    OverwriteExisting = true,
                    ChunkedBulkInsertOptions = null,
                    BatchSize = batchSize
                });
            return new RavenDbAuditIngestionUnitOfWork(bulkInsert, bodyStorageEnricher);
        }
    }
}
