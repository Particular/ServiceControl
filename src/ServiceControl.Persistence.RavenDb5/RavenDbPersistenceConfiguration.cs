namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using Raven.Client.Embedded;
    using ServiceControl.Operations;

    class RavenDbPersistenceConfiguration : IPersistenceConfiguration
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

            var settings = new RavenDBPersisterSettings
            {
                DatabasePath = GetSetting<string>(RavenBootstrapper.DatabasePathKey, default),
                HostName = GetSetting(RavenBootstrapper.HostNameKey, "localhost"),
                DatabaseMaintenancePort = GetSetting(RavenBootstrapper.DatabaseMaintenancePortKey, RavenDBPersisterSettings.DatabaseMaintenancePortDefault),
                ExposeRavenDB = GetSetting(RavenBootstrapper.ExposeRavenDBKey, false),
                ExpirationProcessTimerInSeconds = GetSetting(RavenBootstrapper.ExpirationProcessTimerInSecondsKey, 600),
                RunInMemory = GetSetting(RavenBootstrapper.RunInMemoryKey, false),
                MinimumStorageLeftRequiredForIngestion = GetSetting(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, CheckMinimumStorageRequiredForIngestion.MinimumStorageLeftRequiredForIngestionDefault),
                DataSpaceRemainingThreshold = GetSetting(DataSpaceRemainingThresholdKey, CheckFreeDiskSpace.DataSpaceRemainingThresholdDefault),
                ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(ErrorRetentionPeriodKey),
                EventsRetentionPeriod = GetSetting(EventsRetentionPeriodKey, TimeSpan.FromDays(14)),
                AuditRetentionPeriod = GetSetting(AuditRetentionPeriodKey, TimeSpan.Zero),
                ExternalIntegrationsDispatchingBatchSize = GetSetting(ExternalIntegrationsDispatchingBatchSizeKey, 100),
                MaintenanceMode = GetSetting(MaintenanceModeKey, false),
            };

            CheckFreeDiskSpace.Validate(settings);
            CheckMinimumStorageRequiredForIngestion.Validate(settings);
            return settings;
        }

        public IPersistence Create(PersistenceSettings settings)
        {
            var specificSettings = (RavenDBPersisterSettings)settings;

            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, specificSettings);

            var ravenStartup = new RavenStartup();
            return new RavenDbPersistence(specificSettings, documentStore, ravenStartup);
        }
    }
}