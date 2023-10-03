using System;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class RavenDBPersisterSettings : PersistenceSettings
{
    public string HostName { get; set; } = HostNameDefault; // TODO: (Ramon) I think thus must be 🔥 I don't think we should ever allow remote access by using a value different than `localhost` as Raven Studio might then be always be accessible!
    public int DatabasePort { get; set; } = DatabasePortDefault;
    public int ExpirationProcessTimerInSeconds { get; set; } = ExpirationProcessTimerInSecondsDefault;
    public int MinimumStorageLeftRequiredForIngestion { get; set; } = CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault;
    public int DataSpaceRemainingThreshold { get; set; } = CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public TimeSpan EventsRetentionPeriod { get; set; }
    public TimeSpan? AuditRetentionPeriod { get; set; }
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = 100;

    /// <summary>
    /// Computed connection string to access embedded RavenDB API and RavenDB Studio
    /// </summary>
    public string ServerUrl => $"http://{HostName}:{DatabasePort}";

    /// <summary>
    /// User provided external RavenDB instance connection string
    /// </summary>
    public string ConnectionString { get; set; }
    public bool UseEmbeddedServer => string.IsNullOrWhiteSpace(ConnectionString);
    public string LogPath { get; set; }
    public string LogsMode { get; set; } = LogsModeDefault;
    public string DatabaseName { get; set; } = DatabaseNameDefault;

    public const string DatabaseNameDefault = "primary";
    public const int DatabasePortDefault = 33334;
    public const int ExpirationProcessTimerInSecondsDefault = 600;
    public const string LogsModeDefault = "Information";
    public const string HostNameDefault = "localhost";
}