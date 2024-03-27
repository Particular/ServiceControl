namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenIngestionUnitOfWorkFactory(
        IRavenSessionProvider sessionProvider,
        MinimumRequiredStorageState customCheckState,
        ExpirationManager expirationManager,
        RavenPersisterSettings settings)
        : IIngestionUnitOfWorkFactory
    {
        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new(new RavenIngestionUnitOfWork(sessionProvider, expirationManager, settings));

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}