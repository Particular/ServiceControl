using System;
using ServiceControl.Infrastructure.RavenDB.Expiration;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class RavenDBPersisterSettings : PersistenceSettings
{
    public string DatabasePath { get; set; }
    public string HostName { get; set; } = "localhost";
    public int DatabaseMaintenancePort { get; set; } = 55554;
    public bool ExposeRavenDB { get; set; }
    public int ExpirationProcessTimerInSeconds { get; set; } = ExpiredDocumentsCleanerBundle.ExpirationProcessTimerInSecondsDefault;
    public int ExpirationProcessBatchSize { get; set; } = ExpiredDocumentsCleanerBundle.ExpirationProcessBatchSizeDefault;
    public bool RunCleanupBundle { get; set; }
    public bool RunInMemory { get; set; }
    public int MinimumStorageLeftRequiredForIngestion { get; set; } = CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault;
    public int DataSpaceRemainingThreshold { get; set; } = CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public TimeSpan EventsRetentionPeriod { get; set; }
    public TimeSpan? AuditRetentionPeriod { get; set; }
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = 100;
}