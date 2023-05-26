namespace ServiceControl.Persistence.InMemory
{
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class InMemoryIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWorkFactory(IMonitoringDataStore dataStore) => this.dataStore = dataStore;

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new InMemoryIngestionUnitOfWork(dataStore));

        public bool CanIngestMore() => true;
    }
}