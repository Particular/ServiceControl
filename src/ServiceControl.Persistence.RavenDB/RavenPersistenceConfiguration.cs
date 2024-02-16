﻿namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using Configuration;
    using ServiceControl.Operations;

    class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public PersistenceSettings CreateSettings(string settingsRootNamespace)
        {
            static T GetRequiredSetting<T>(string settingsRootNamespace, string key)
            {
                if (SettingsReader.TryRead<T>(settingsRootNamespace, key, out var value))
                {
                    return value;
                }

                throw new Exception($"Setting {key} of type {typeof(T)} is required");
            }

            var ravenDbLogLevel = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.RavenDbLogLevelKey, "Warn");
            var logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel);

            var settings = new RavenPersisterSettings
            {
                ConnectionString = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ConnectionStringKey),
                DatabaseName = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseNameKey, RavenPersisterSettings.DatabaseNameDefault),
                DatabasePath = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.DatabasePathKey),
                DatabaseMaintenancePort = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseMaintenancePortKey, RavenPersisterSettings.DatabaseMaintenancePortDefault),
                ExpirationProcessTimerInSeconds = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.ExpirationProcessTimerInSecondsKey, 600),
                MinimumStorageLeftRequiredForIngestion = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault),
                DataSpaceRemainingThreshold = SettingsReader.Read(settingsRootNamespace, DataSpaceRemainingThresholdKey, CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault),
                ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey),
                EventsRetentionPeriod = SettingsReader.Read(settingsRootNamespace, EventsRetentionPeriodKey, TimeSpan.FromDays(14)),
                AuditRetentionPeriod = SettingsReader.Read(settingsRootNamespace, AuditRetentionPeriodKey, TimeSpan.Zero),
                ExternalIntegrationsDispatchingBatchSize = SettingsReader.Read(settingsRootNamespace, ExternalIntegrationsDispatchingBatchSizeKey, 100),
                MaintenanceMode = SettingsReader.Read(settingsRootNamespace, MaintenanceModeKey, false),
                LogPath = GetRequiredSetting<string>(settingsRootNamespace, RavenBootstrapper.LogsPathKey),
                LogsMode = logsMode,
                EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, "EnableFullTextSearchOnBodies", true)
            };

            CheckFreeDiskSpace.Validate(settings);
            CheckMinimumStorageRequiredForIngestion.Validate(settings);
            return settings;
        }

        public IPersistence Create(PersistenceSettings settings)
        {
            var specificSettings = (RavenPersisterSettings)settings;
            return new RavenPersistence(specificSettings);
        }
    }
}