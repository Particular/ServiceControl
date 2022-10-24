namespace ServiceBus.Management.Infrastructure.Settings
{
    public enum DataStoreType
    {
        InMemory = 1,
        RavenDB35 = 2,
        SqlDb = 3
    }

    public static class DataStoreConfig
    {
        public static string InMemoryPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Persistence.InMemory";
        public static string SqlServerPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.SqlServer.SqlDbPersistenceConfiguration, ServiceControl.Persistence.SqlServer";
        public static string RavenDB35PersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Persistence.RavenDb";
    }
}