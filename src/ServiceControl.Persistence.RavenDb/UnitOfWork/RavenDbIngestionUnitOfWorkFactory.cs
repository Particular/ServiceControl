namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Operations;
    using Raven.Client;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly CheckMinimumStorageRequiredForIngestion.State customCheckState;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store, CheckMinimumStorageRequiredForIngestion.State customCheckState)
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