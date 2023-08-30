using System;

class RavenDBPersisterSettings
{
    public string DatabasePath { get; set; }
    public string HostName { get; set; } = "localhost";
    public int DatabaseMaintenancePort { get; set; } = 55554;
    public bool ExposeRavenDB { get; set; }
    public int ExpirationProcessTimerInSeconds { get; set; }
    public int ExpirationProcessBatchSize { get; set; }
    public bool RunCleanupBundle { get; set; }
    public bool RunInMemory { get; set; }
    public int MinimumStorageLeftRequiredForIngestion { get; set; }
    public int DataSpaceRemainingThreshold { get; set; }
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public TimeSpan EventsRetentionPeriod { get; set; }
    public TimeSpan? AuditRetentionPeriod { get; set; }
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = 100;
    public bool MaintenanceMode { get; set; }
}