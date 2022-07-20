namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using Monitoring;

    class InMemoryIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWorkFactory(IMonitoringDataStore dataStore) => this.dataStore = dataStore;

        public Task<IIngestionUnitOfWork> StartNew()
            => Task.FromResult<IIngestionUnitOfWork>(new InMemoryIngestionUnitOfWork(dataStore));
    }
}