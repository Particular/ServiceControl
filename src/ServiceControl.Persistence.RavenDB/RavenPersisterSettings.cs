using System;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Linq.Indexing;
using ServiceControl.Persistence;
using ServiceControl.Persistence.RavenDB.CustomChecks;
using ServiceControl.RavenDB;

class RavenPersisterSettings : PersistenceSettings, IRavenClientCertificateInfo
{
    public int DatabaseMaintenancePort { get; set; } = DatabaseMaintenancePortDefault;
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
    public string ServerUrl => $"http://localhost:{DatabaseMaintenancePort}";

    /// <summary>
    /// User provided external RavenDB instance connection string
    /// </summary>
    public string ConnectionString { get; set; }
    public string ClientCertificatePath { get; set; }
    public string ClientCertificateBase64 { get; set; }
    public string ClientCertificatePassword { get; set; }
    public bool UseEmbeddedServer => string.IsNullOrWhiteSpace(ConnectionString);
    public string LogPath { get; set; }
    public string LogsMode { get; set; } = LogsModeDefault;
    public string DatabaseName { get; set; } = DatabaseNameDefault;
    public string ThroughputDatabaseName { get; set; } = ThroughputSettings.DefaultDatabaseName;
    public Raven.Client.Documents.Indexes.SearchEngineType SearchEngineType { get; set; } = SearchEngineTypeDefault;

    public static readonly Raven.Client.Documents.Indexes.SearchEngineType SearchEngineTypeDefault = Raven.Client.Documents.Indexes.SearchEngineType.Corax;
    public const string DatabaseNameDefault = "primary";
    public const int DatabaseMaintenancePortDefault = 33334;
    public const int ExpirationProcessTimerInSecondsDefault = 600;
    public const string LogsModeDefault = "Operations";
}