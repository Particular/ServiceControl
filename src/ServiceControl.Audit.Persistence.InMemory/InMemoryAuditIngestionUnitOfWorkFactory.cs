namespace ServiceControl.Audit.Persistence.InMemory
{
    using ServiceControl.Audit.Persistence.UnitOfWork;

    class InMemoryAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public InMemoryAuditIngestionUnitOfWorkFactory(InMemoryAuditDataStore dataStore) => this.dataStore = dataStore;

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            //The batchSize argument is ignored: the in-memory storage implementation doesn't support batching.
            return new InMemoryAuditIngestionUnitOfWork(dataStore);
        }

        InMemoryAuditDataStore dataStore;
    }
}