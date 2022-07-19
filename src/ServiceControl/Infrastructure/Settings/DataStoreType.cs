namespace ServiceBus.Management.Infrastructure.Settings
{
    public enum DataStoreType
    {
        InMemory = 1,
        RavenDb = 2,
        SqlDb = 3
    }

    public static class DataStoreConfig
    {
        public static string InMemoryPersistence = "ServiceControl.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Persistence.InMemory";
        public static string SqlServerPersistence = "ServiceControl.Persistence.SqlServer.SqlDbPersistenceConfiguration, ServiceControl.Persistence.SqlServer";
        public static string RavenDbPersistence = "ServiceControl.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Persistence.RavenDb";
    }
}