namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly MinimumRequiredStorageState customCheckState;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store, MinimumRequiredStorageState customCheckState)
        {
            this.store = store;
            this.customCheckState = customCheckState;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(store));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}