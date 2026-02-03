namespace ServiceControl.Audit.Persistence.MongoDB.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Persistence.UnitOfWork;

    class MongoAuditIngestionUnitOfWorkFactory(
        IMongoClientProvider clientProvider,
        MongoSettings settings,
        IBodyStorage bodyStorage)
        : IAuditIngestionUnitOfWorkFactory
    {
        public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            var unitOfWork = new MongoAuditIngestionUnitOfWork(
                clientProvider.Client,
                clientProvider.Database,
                clientProvider.ProductCapabilities.SupportsMultiCollectionBulkWrite,
                settings.AuditRetentionPeriod,
                bodyStorage,
                settings.MaxBodySizeToStore);

            return ValueTask.FromResult<IAuditIngestionUnitOfWork>(unitOfWork);
        }

        // TODO: Stage 7 - Implement proper storage state monitoring
        public bool CanIngestMore() => true;
    }
}
