namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.IO;
    using System.Reflection;
    using Configuration;
    using CustomChecks;
    using Particular.LicensingComponent.Contracts;

    class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
        {
            static T GetRequiredSetting<T>(SettingsRootNamespace settingsRootNamespace, string key)
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
                ClientCertificatePath = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ClientCertificatePathKey),
                ClientCertificateBase64 = SettingsReader.Read<string>(settingsRootNamespace, RavenBootstrapper.ClientCertificateBase64Key),
                DatabaseName = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseNameKey, RavenPersisterSettings.DatabaseNameDefault),
                DatabasePath = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabasePathKey, DefaultDatabaseLocation()),
                DatabaseMaintenancePort = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.DatabaseMaintenancePortKey, RavenPersisterSettings.DatabaseMaintenancePortDefault),
                ExpirationProcessTimerInSeconds = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.ExpirationProcessTimerInSecondsKey, 600),
                MinimumStorageLeftRequiredForIngestion = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault),
                DataSpaceRemainingThreshold = SettingsReader.Read(settingsRootNamespace, DataSpaceRemainingThresholdKey, CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault),
                ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey),
                EventsRetentionPeriod = SettingsReader.Read(settingsRootNamespace, EventsRetentionPeriodKey, TimeSpan.FromDays(14)),
                AuditRetentionPeriod = SettingsReader.Read(settingsRootNamespace, AuditRetentionPeriodKey, TimeSpan.Zero),
                ExternalIntegrationsDispatchingBatchSize = SettingsReader.Read(settingsRootNamespace, ExternalIntegrationsDispatchingBatchSizeKey, 100),
                MaintenanceMode = SettingsReader.Read(settingsRootNamespace, MaintenanceModeKey, false),
                LogPath = SettingsReader.Read(settingsRootNamespace, RavenBootstrapper.LogsPathKey, DefaultLogLocation()),
                LogsMode = logsMode,
                EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, "EnableFullTextSearchOnBodies", true),
                ThroughputDatabaseName = SettingsReader.Read(ThroughputSettings.SettingsNamespace, ThroughputSettings.DatabaseNameKey, ThroughputSettings.DefaultDatabaseName)
            };

            CheckFreeDiskSpace.Validate(settings);
            CheckMinimumStorageRequiredForIngestion.Validate(settings);
            return settings;
        }

        // SC installer always populates DBPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultDatabaseLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyLocation), ".db");
        }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyLocation), ".logs");
        }

        public IPersistence Create(PersistenceSettings settings)
        {
            var specificSettings = (RavenPersisterSettings)settings;
            return new RavenPersistence(specificSettings);
        }
    }
}