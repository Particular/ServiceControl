namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Auditing.BodyStorage;
    using Persistence.UnitOfWork;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Audit.Persistence.RavenDb.CustomChecks;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        IDocumentStore store;
        CheckMinimumStorageRequiredForAuditIngestion.State customCheckState;
        BodyStorageEnricher bodyStorageEnricher;

        public RavenDbAuditIngestionUnitOfWorkFactory(IDocumentStore store, IBodyStorage bodyStorage, PersistenceSettings settings, CheckMinimumStorageRequiredForAuditIngestion.State customCheckState)
        {
            this.store = store;
            this.customCheckState = customCheckState;
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

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}
