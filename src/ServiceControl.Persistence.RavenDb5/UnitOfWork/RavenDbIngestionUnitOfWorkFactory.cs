namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using RavenDB;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly MinimumRequiredStorageState customCheckState;
        readonly ExpirationManager expirationManager;
        readonly RavenDBPersisterSettings settings;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store, MinimumRequiredStorageState customCheckState, ExpirationManager expirationManager, RavenDBPersisterSettings settings)
        {
            this.store = store;
            this.customCheckState = customCheckState;
            this.expirationManager = expirationManager;
            this.settings = settings;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(store, expirationManager, settings));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}