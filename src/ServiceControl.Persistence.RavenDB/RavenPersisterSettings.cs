﻿using System;
using Particular.ThroughputCollector.Contracts;
using ServiceControl.Persistence;
using ServiceControl.Persistence.RavenDB.CustomChecks;

class RavenPersisterSettings : PersistenceSettings
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
    public bool UseEmbeddedServer => string.IsNullOrWhiteSpace(ConnectionString);
    public string LogPath { get; set; }
    public string LogsMode { get; set; } = LogsModeDefault;
    public string DatabaseName { get; set; } = DatabaseNameDefault;
    public string ThroughputDatabaseName { get; set; } = ThroughputSettings.DefaultDatabaseName;

    public const string DatabaseNameDefault = "primary";
    public const int DatabaseMaintenancePortDefault = 33334;
    public const int ExpirationProcessTimerInSecondsDefault = 600;
    public const string LogsModeDefault = "Operations";
}