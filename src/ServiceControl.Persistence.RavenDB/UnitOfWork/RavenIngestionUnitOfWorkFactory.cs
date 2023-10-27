namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly MinimumRequiredStorageState customCheckState;
        readonly ExpirationManager expirationManager;
        readonly RavenPersisterSettings settings;

        public RavenIngestionUnitOfWorkFactory(IDocumentStore store, MinimumRequiredStorageState customCheckState, ExpirationManager expirationManager, RavenPersisterSettings settings)
        {
            this.store = store;
            this.customCheckState = customCheckState;
            this.expirationManager = expirationManager;
            this.settings = settings;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenIngestionUnitOfWork(store, expirationManager, settings));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}