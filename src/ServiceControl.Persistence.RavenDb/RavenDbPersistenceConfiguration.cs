namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using Raven.Client.Embedded;

    class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        //TODO: figure out what can be strongly typed
        public const string LogPathKey = "LogPath";
        public const string DbPathKey = "DbPath";
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string AuditRetentionPeriodKey = "AuditRetentionPeriod";


        public string Name => "RavenDB35";

        public IEnumerable<string> ConfigurationKeys => new[]{
            RavenBootstrapper.DatabasePathKey,
            RavenBootstrapper.HostNameKey,
            RavenBootstrapper.DatabaseMaintenancePortKey,
            RavenBootstrapper.ExposeRavenDBKey,
            RavenBootstrapper.ExpirationProcessTimerInSecondsKey,
            RavenBootstrapper.ExpirationProcessBatchSizeKey,
            RavenBootstrapper.RunCleanupBundleKey,
            RavenBootstrapper.RunInMemoryKey,
            RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey,
            DataSpaceRemainingThresholdKey
        };

        public IPersistence Create(PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            var ravenStartup = new RavenStartup();

            return new RavenDbPersistence(settings, documentStore, ravenStartup);
        }
    }
}
