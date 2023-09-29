namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly MinimumRequiredStorageState customCheckState;
        readonly RavenDBPersisterSettings settings;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store, MinimumRequiredStorageState customCheckState, RavenDBPersisterSettings settings)
        {
            this.store = store;
            this.customCheckState = customCheckState;
            this.settings = settings;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(store, settings));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}