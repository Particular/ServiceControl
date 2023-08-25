namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Collections.Generic;
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Persistence.RavenDB;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "RavenDB35";

        public IEnumerable<string> ConfigurationKeys => new string[]{
            RavenBootstrapper.DatabasePathKey,
            RavenBootstrapper.HostNameKey,
            RavenBootstrapper.DatabaseMaintenancePortKey,
            RavenBootstrapper.ExposeRavenDBKey,
            RavenBootstrapper.ExpirationProcessTimerInSecondsKey,
            RavenBootstrapper.ExpirationProcessBatchSizeKey,
            RavenBootstrapper.RunCleanupBundleKey,
            RavenBootstrapper.RunInMemoryKey,
            RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey
        };

        public IPersistence Create(PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            var ravenStartup = new RavenStartup();
            ravenStartup.AddIndexAssembly(typeof(RavenBootstrapper).Assembly);

            return new RavenDbPersistence(settings, documentStore, ravenStartup);
        }
    }
}
