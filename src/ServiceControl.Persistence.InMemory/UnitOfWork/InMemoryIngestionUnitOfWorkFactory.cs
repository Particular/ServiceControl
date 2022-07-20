namespace ServiceControl.Persistence.SqlServer
{
    using System.Threading.Tasks;
    using Operations;

    class InMemoryIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWorkFactory(IMonitoringDataStore dataStore) => this.dataStore = dataStore;

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new InMemoryIngestionUnitOfWork(dataStore));
    }
}