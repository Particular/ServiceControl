namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenIngestionUnitOfWorkFactory(
        IRavenSessionProvider sessionProvider,
        MinimumRequiredStorageState customCheckState,
        ExpirationManager expirationManager,
        RavenPersisterSettings settings)
        : IIngestionUnitOfWorkFactory
    {
        public ValueTask<IIngestionUnitOfWork> StartNew(CancellationToken cancellationToken = default)
            => new(new RavenIngestionUnitOfWork(sessionProvider, expirationManager, settings));

        public bool CanIngestMore() => customCheckState.CanIngestMore;

        // Failed messages are merged by patch scripts that read and rewrite one document, and
        // nothing orders two patches of the same document against each other. Error ingestion on
        // RavenDB has only ever run one batch at a time, and --error-ingestion-only refuses to
        // start on it for the same reason.
        public bool SupportsConcurrentBatches => false;
    }
}