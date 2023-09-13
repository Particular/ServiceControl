namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;
    using RavenDb5;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly DocumentStoreProvider storeProvider;
        readonly MinimumRequiredStorageState customCheckState;

        public RavenDbIngestionUnitOfWorkFactory(DocumentStoreProvider storeProvider, MinimumRequiredStorageState customCheckState)
        {
            this.storeProvider = storeProvider;
            this.customCheckState = customCheckState;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(storeProvider));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}