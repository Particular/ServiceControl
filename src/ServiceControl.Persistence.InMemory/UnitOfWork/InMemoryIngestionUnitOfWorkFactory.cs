namespace ServiceControl.Persistence.SqlServer
{
    using System.Threading.Tasks;
    using Operations;

    class InMemoryIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWorkFactory(IMonitoringDataStore dataStore) => this.dataStore = dataStore;

        public Task<IIngestionUnitOfWork> StartNew()
            => Task.FromResult<IIngestionUnitOfWork>(new InMemoryIngestionUnitOfWork(dataStore));
    }
}