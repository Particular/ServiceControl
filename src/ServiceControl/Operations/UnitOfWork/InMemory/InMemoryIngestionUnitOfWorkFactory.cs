namespace ServiceControl.Operations
{
    using Monitoring;

    class InMemoryIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWorkFactory(IMonitoringDataStore dataStore) => this.dataStore = dataStore;

        public IIngestionUnitOfWork StartNew() => new InMemoryIngestionUnitOfWork(dataStore);
    }
}