namespace ServiceControl.Persistence.RavenDb
{
    static class RavenBootstrapper
    {
        public const string DatabasePathKey = "DbPath";
        public const string HostNameKey = "HostName";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExposeRavenDBKey = "ExposeRavenDB";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string RunInMemoryKey = "RavenDB35/RunInMemory";
        public const string ConnectionStringKey = "RavenDB5/ConnectionString";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string DatabaseNameKey = "RavenDB5/DatabaseName";
        public const string LogsPathKey = "LogPath";
        public const string LogsModeKey = "LogMode";
    }
}
