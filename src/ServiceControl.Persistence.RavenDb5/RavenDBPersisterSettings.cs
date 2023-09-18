using System;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class RavenDBPersisterSettings : PersistenceSettings
{
    public string HostName { get; set; } = "localhost";
    public int DatabaseMaintenancePort { get; set; } = DatabaseMaintenancePortDefault;
    public string DatabaseMaintenanceUrl => $"http://{HostName}:{DatabaseMaintenancePort}";
    public bool ExposeRavenDB { get; set; }
    public int ExpirationProcessTimerInSeconds { get; set; } = ExpirationProcessTimerInSecondsDefault;
    public bool RunInMemory { get; set; }
    public int MinimumStorageLeftRequiredForIngestion { get; set; } = CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault;
    public int DataSpaceRemainingThreshold { get; set; } = CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public TimeSpan EventsRetentionPeriod { get; set; }
    public TimeSpan? AuditRetentionPeriod { get; set; }
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = 100;

    //TODO: these are newly added settings, we should remove any duplication
    public string ServerUrl { get; set; }
    public string ConnectionString { get; set; }
    public bool UseEmbeddedServer => string.IsNullOrWhiteSpace(ConnectionString);
    public string LogPath { get; set; }
    public string LogsMode { get; set; } = LogsModeDefault;
    public string DatabaseName { get; set; } = DatabaseNameDefault;

    public const string DatabaseNameDefault = "audit";
    public const int DatabaseMaintenancePortDefault = 33334;
    public const int ExpirationProcessTimerInSecondsDefault = 600;
    public const string LogsModeDefault = "Information";
}