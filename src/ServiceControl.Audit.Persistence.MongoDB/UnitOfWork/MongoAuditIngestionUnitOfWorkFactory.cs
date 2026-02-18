namespace ServiceControl.Audit.Persistence.MongoDB.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
    using BodyStorage;
    using Microsoft.Extensions.Logging;
    using Persistence.UnitOfWork;

    class MongoAuditIngestionUnitOfWorkFactory(
        IMongoClientProvider clientProvider,
        MongoSettings settings,
        IBodyWriter bodyWriter,
        MinimumRequiredStorageState storageState,
        ILogger<MongoAuditIngestionUnitOfWork> logger)
        : IAuditIngestionUnitOfWorkFactory
    {
        public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            var unitOfWork = new MongoAuditIngestionUnitOfWork(
                clientProvider.Client,
                clientProvider.Database,
                clientProvider.ProductCapabilities.SupportsMultiCollectionBulkWrite,
                settings.AuditRetentionPeriod,
                settings.MaxBodySizeToStore,
                bodyWriter,
                logger);

            return ValueTask.FromResult<IAuditIngestionUnitOfWork>(unitOfWork);
        }

        public bool CanIngestMore() => storageState.CanIngestMore;
    }
}
