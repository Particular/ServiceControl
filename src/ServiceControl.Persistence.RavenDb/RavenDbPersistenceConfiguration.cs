namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using Raven.Client.Embedded;

    class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        const string AuditRetentionPeriodKey = "AuditRetentionPeriod";
        const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
        const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
        const string ExternalIntegrationsDispatchingBatchSizeKey = "ExternalIntegrationsDispatchingBatchSize";
        const string MaintenanceModeKey = "MaintenanceMode";

        public IPersistence Create(Func<string, Type, object> readSetting)
        {
            T GetSetting<T>(string key) => (T)readSetting(key, typeof(T));


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



            var settings = new RavenDBPersisterSettings()
            {
                DatabasePath = GetSetting<string>(RavenBootstrapper.DatabasePathKey),
                HostName = GetSetting<string>(RavenBootstrapper.HostNameKey),
                DatabaseMaintenancePort = GetSetting<int>(RavenBootstrapper.DatabaseMaintenancePortKey),
                ExposeRavenDB = GetSetting<bool>(RavenBootstrapper.ExposeRavenDBKey),
                ExpirationProcessTimerInSeconds = GetSetting<int>(RavenBootstrapper.ExpirationProcessTimerInSecondsKey),
                ExpirationProcessBatchSize = GetSetting<int>(RavenBootstrapper.ExpirationProcessBatchSizeKey),
                RunCleanupBundle = GetSetting<bool>(RavenBootstrapper.RunCleanupBundleKey),
                RunInMemory = GetSetting<bool>(RavenBootstrapper.RunInMemoryKey),
                MinimumStorageLeftRequiredForIngestion = GetSetting<int>(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey),
                DataSpaceRemainingThreshold = GetSetting<int>(DataSpaceRemainingThresholdKey),
                ErrorRetentionPeriod = GetSetting<TimeSpan>(ErrorRetentionPeriodKey),
                EventsRetentionPeriod = GetSetting<TimeSpan>(EventsRetentionPeriodKey),
                AuditRetentionPeriod = GetSetting<TimeSpan>(AuditRetentionPeriodKey),
                ExternalIntegrationsDispatchingBatchSize = GetSetting<int>(ExternalIntegrationsDispatchingBatchSizeKey),
                MaintenanceMode = GetSetting<bool>(MaintenanceModeKey),
            };

            return Create(settings);
        }

        internal IPersistence Create(RavenDBPersisterSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            var ravenStartup = new RavenStartup();

            return new RavenDbPersistence(settings, documentStore, ravenStartup);
        }
    }
}