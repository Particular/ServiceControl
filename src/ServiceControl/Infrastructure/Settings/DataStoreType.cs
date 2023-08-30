namespace ServiceBus.Management.Infrastructure.Settings
{
    public enum DataStoreType
    {
        InMemory = 1,
        RavenDB35 = 2
    }

    public static class DataStoreConfig
    {
        public static string InMemoryPersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Persistence.InMemory";
        public static string RavenDB35PersistenceTypeFullyQualifiedName = "ServiceControl.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Persistence.RavenDb";
    }
}