namespace ServiceControl.Audit.Infrastructure.Settings
{
    public enum DataStoreType
    {
        InMemory = 1,
        RavenDb = 2,
        SqlDb = 3,
        RavenDb5 = 4,
    }

    public static class DataStoreConfig
    {
        public static string InMemoryPersistenceTypeFullyQualifiedName = "ServiceControl.Audit.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Audit.Persistence.InMemory";
        public static string SqlServerPersistenceTypeFullyQualifiedName = "ServiceControl.Audit.Persistence.SqlServer.SqlDbPersistenceConfiguration, ServiceControl.Audit.Persistence.SqlServer";
        public static string RavenDbPersistenceTypeFullyQualifiedName = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb";
        public static string RavenDb5PersistenceTypeFullyQualifiedName = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";
    }
}