namespace ServiceControl.Persistence.InMemory
{
    using ServiceControl.Persistence;

    class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        InMemoryMonitoringDataStore monitoringDataStore;
        InMemoryCustomCheckDataStore customCheckDataStore;
#pragma warning disable IDE0060 // Remove unused parameter
        public InMemoryPersistenceConfiguration(object[] parameters)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            monitoringDataStore = new InMemoryMonitoringDataStore();
            customCheckDataStore = new InMemoryCustomCheckDataStore();
        }

        public IMonitoringDataStore MonitoringDataStore => monitoringDataStore;
        public ICustomChecksDataStore CustomCheckDataStore => customCheckDataStore;
    }
}
