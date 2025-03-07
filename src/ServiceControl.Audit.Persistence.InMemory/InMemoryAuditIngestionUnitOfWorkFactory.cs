namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    class InMemoryAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public InMemoryAuditIngestionUnitOfWorkFactory(InMemoryAuditDataStore dataStore, IBodyStorage bodyStorage, PersistenceSettings settings)
        {
            this.dataStore = dataStore;
            bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
        }

        public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            //The batchSize argument is ignored: the in-memory storage implementation doesn't support batching.
            return new ValueTask<IAuditIngestionUnitOfWork>(new InMemoryAuditIngestionUnitOfWork(dataStore, bodyStorageEnricher));
        }

        public bool CanIngestMore() => true;

        InMemoryAuditDataStore dataStore;
        BodyStorageEnricher bodyStorageEnricher;
    }
}