namespace ServiceControl.Audit.Persistence.MongoDB.UnitOfWork
{
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Persistence.UnitOfWork;
    using Search;

    class MongoAuditIngestionUnitOfWorkFactory(
        IMongoClientProvider clientProvider,
        MongoSettings settings,
        IBodyStorage bodyStorage,
        MinimumRequiredStorageState storageState,
        Channel<BodyEntry> bodyChannel = null)
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
                settings.MaxBodySizeToStore,
                bodyChannel);

            return ValueTask.FromResult<IAuditIngestionUnitOfWork>(unitOfWork);
        }

        public bool CanIngestMore() => storageState.CanIngestMore;
    }
}
