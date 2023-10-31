namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using ServiceControl.Operations;

    class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public PersistenceSettings CreateSettings(Func<string, Type, (bool exists, object value)> tryReadSetting)
        {
            T GetRequiredSetting<T>(string key)
            {
                var (exists, value) = tryReadSetting(key, typeof(T));

                if (exists)
                {
                    return (T)value;
                }

                throw new Exception($"Setting {key} of type {typeof(T)} is required");
            }

            T GetSetting<T>(string key, T defaultValue)
            {
                var (exists, value) = tryReadSetting(key, typeof(T));

                if (exists)
                {
                    return (T)value;
                }
                else
                {
                    return defaultValue;
                }
            }

            var settings = new RavenPersisterSettings
            {
                ConnectionString = GetSetting<string>(RavenBootstrapper.ConnectionStringKey, default),
                DatabaseName = GetSetting(RavenBootstrapper.DatabaseNameKey, RavenPersisterSettings.DatabaseNameDefault),
                DatabasePath = GetSetting<string>(RavenBootstrapper.DatabasePathKey, default),
                DatabasePort = GetSetting(RavenBootstrapper.DatabaseMaintenancePortKey, RavenPersisterSettings.DatabasePortDefault),
                ExpirationProcessTimerInSeconds = GetSetting(RavenBootstrapper.ExpirationProcessTimerInSecondsKey, 600),
                MinimumStorageLeftRequiredForIngestion = GetSetting(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault),
                DataSpaceRemainingThreshold = GetSetting(DataSpaceRemainingThresholdKey, CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault),
                ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(ErrorRetentionPeriodKey),
                EventsRetentionPeriod = GetSetting(EventsRetentionPeriodKey, TimeSpan.FromDays(14)),
                AuditRetentionPeriod = GetSetting(AuditRetentionPeriodKey, TimeSpan.Zero),
                ExternalIntegrationsDispatchingBatchSize = GetSetting(ExternalIntegrationsDispatchingBatchSizeKey, 100),
                MaintenanceMode = GetSetting(MaintenanceModeKey, false),
                LogPath = GetRequiredSetting<string>(RavenBootstrapper.LogsPathKey),
                LogsMode = GetSetting(RavenBootstrapper.LogsModeKey, RavenPersisterSettings.LogsModeDefault),
                EnableFullTextSearchOnBodies = GetSetting("EnableFullTextSearchOnBodies", true)
            };

            CheckFreeDiskSpace.Validate(settings);
            CheckMinimumStorageRequiredForIngestion.Validate(settings);
            return settings;
        }

        public IPersistence Create(PersistenceSettings settings)
        {
            var specificSettings = (RavenPersisterSettings)settings;

            //var documentStore = new EmbeddableDocumentStore();
            //RavenBootstrapper.Configure(documentStore, specificSettings);

            //var ravenStartup = new RavenStartup();

            return new RavenPersistence(specificSettings);
        }
    }
}