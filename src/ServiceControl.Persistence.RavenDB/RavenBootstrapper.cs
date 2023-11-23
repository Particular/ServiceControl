﻿namespace ServiceControl.Persistence.RavenDB
{
    static class RavenBootstrapper
    {
        public const string DatabasePathKey = "DbPath";
        public const string HostNameKey = "HostName";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string ConnectionStringKey = "RavenDB/ConnectionString";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string DatabaseNameKey = "RavenDB/DatabaseName";
        public const string LogsPathKey = "LogPath";
        public const string RavenDbLogLevelKey = "RavenDBLogLevel";
    }
}