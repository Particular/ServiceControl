namespace ServiceControl.Audit.Persistence.InMemory
{
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    class InMemoryAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public InMemoryAuditIngestionUnitOfWorkFactory(InMemoryAuditDataStore dataStore, IBodyStorage bodyStorage, PersistenceSettings settings)
        {
            this.dataStore = dataStore;
            bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            //The batchSize argument is ignored: the in-memory storage implementation doesn't support batching.
            return new InMemoryAuditIngestionUnitOfWork(dataStore, bodyStorageEnricher);
        }

        InMemoryAuditDataStore dataStore;
        BodyStorageEnricher bodyStorageEnricher;
    }
}