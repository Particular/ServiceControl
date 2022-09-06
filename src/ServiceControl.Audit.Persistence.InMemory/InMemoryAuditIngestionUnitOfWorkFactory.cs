namespace ServiceControl.Audit.Persistence.InMemory
{
    using ServiceControl.Audit.Persistence.UnitOfWork;

    class InMemoryAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public InMemoryAuditIngestionUnitOfWorkFactory(InMemoryAuditDataStore dataStore) => this.dataStore = dataStore;

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            return new InMemoryAuditIngestionUnitOfWork(dataStore);
        }

        InMemoryAuditDataStore dataStore;
    }
}