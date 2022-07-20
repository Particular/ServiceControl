namespace ServiceControl.Persistence.SqlServer
{
    using ServiceControl.Persistence;

    class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        SqlDbMonitoringDataStore monitoringDataStore;
        SqlDbCustomCheckDataStore customCheckDataStore;
        public SqlDbPersistenceConfiguration(object[] parameters)
        {
            if (parameters != null && parameters.Length > 0)
            {
                monitoringDataStore = new SqlDbMonitoringDataStore(parameters[0].ToString());
                customCheckDataStore = new SqlDbCustomCheckDataStore(parameters[0].ToString());
            }
        }

        public IMonitoringDataStore MonitoringDataStore => monitoringDataStore;
        public ICustomChecksDataStore CustomCheckDataStore => customCheckDataStore;
    }
}
