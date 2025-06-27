namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    class InMemoryAuditIngestionUnitOfWorkFactory(InMemoryAuditDataStore dataStore, BodyStorageEnricher bodyStorageEnricher) : IAuditIngestionUnitOfWorkFactory
    {
        public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            //The batchSize argument is ignored: the in-memory storage implementation doesn't support batching.
            return new ValueTask<IAuditIngestionUnitOfWork>(new InMemoryAuditIngestionUnitOfWork(dataStore, bodyStorageEnricher));
        }

        public bool CanIngestMore() => true;
    }
}