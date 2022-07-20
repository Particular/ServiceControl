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
        public static string InMemoryPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Persistence.InMemory";
        public static string SqlServerPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.SqlServer.SqlDbPersistenceConfiguration, ServiceControl.Persistence.SqlServer";
        public static string RavenDbPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Persistence.RavenDb";
    }
}