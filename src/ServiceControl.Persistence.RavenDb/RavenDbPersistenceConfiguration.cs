namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using Raven.Client.Embedded;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.Operations;

    class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public IPersistenceSettings CreateSettings(Func<string, Type, (bool exists, object value)> tryReadSetting)
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

            /*

            <add key="ServiceControl/MaintenanceMode" value="false" />
            <add key="ServiceControl/RavenDB35/MaintenanceMode" value="false" />

             */

            //TODO: In core previously this happened with the settings:

            //    foreach (var keyPair in settings.PersisterSpecificSettings)
            //    {
            //        persistenceSettings.PersisterSpecificSettings[keyPair.Key] = keyPair.Value;
            //    }

            //    foreach (var key in persistenceConfiguration.ConfigurationKeys)
            //    {
            //        var value = SettingsReader.Read<string>("ServiceControl", key, null);
            //        if (!string.IsNullOrWhiteSpace(value))
            //        {
            //            persistenceSettings.PersisterSpecificSettings[key] = value;
            //        }
            //    }

            var settings = new RavenDBPersisterSettings
            {
                DatabasePath = GetSetting<string>(RavenBootstrapper.DatabasePathKey, default),
                HostName = GetSetting(RavenBootstrapper.HostNameKey, "localhost"),
                DatabaseMaintenancePort = GetSetting<int>(RavenBootstrapper.DatabaseMaintenancePortKey, default),
                ExposeRavenDB = GetSetting(RavenBootstrapper.ExposeRavenDBKey, false),
                ExpirationProcessTimerInSeconds = GetSetting(RavenBootstrapper.ExpirationProcessTimerInSecondsKey, ExpiredDocumentsCleanerBundle.ExpirationProcessTimerInSecondsDefault),
                ExpirationProcessBatchSize = GetSetting(RavenBootstrapper.ExpirationProcessBatchSizeKey, ExpiredDocumentsCleanerBundle.ExpirationProcessBatchSizeDefault),
                RunCleanupBundle = GetSetting(RavenBootstrapper.RunCleanupBundleKey, true),
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

        //public IPersistenceSettingstence Create(Func<string, Type, (bool, object)> tryReadSetting)
        //{
        //    var settings = CreateSettings(tryReadSetting);
        //    return Create(settings);
        //}

        public IPersistence Create(IPersistenceSettings settings)
        {
            var specificSettings = (RavenDBPersisterSettings)settings;

            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, specificSettings);

            var ravenStartup = new RavenStartup();
            return new RavenDbPersistence(specificSettings, documentStore, ravenStartup);
        }
    }
}