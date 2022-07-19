namespace ServiceControl.Persistence.RavenDb
{
    using Raven.Client;
    using ServiceControl.Persistence;

    class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        RavenDbMonitoringDataStore monitoringDataStore;
        RavenDbCustomCheckDataStore customCheckDataStore;
        public RavenDbPersistenceConfiguration(object[] parameters)
        {
            if (parameters != null && parameters.Length > 0)
            {
                monitoringDataStore = new RavenDbMonitoringDataStore((IDocumentStore)parameters[0]);
                customCheckDataStore = new RavenDbCustomCheckDataStore((IDocumentStore)parameters[0]);
            }
        }

        public IMonitoringDataStore MonitoringDataStore => monitoringDataStore;
        public ICustomChecksDataStore CustomCheckDataStore => customCheckDataStore;
    }
}
