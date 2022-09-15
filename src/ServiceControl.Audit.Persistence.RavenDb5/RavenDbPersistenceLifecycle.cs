namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Raven.Embedded;
    using RavenDb5;

    class RavenDbPersistenceLifecycle : IPersistenceLifecycle, IAsyncDisposable
    {
        public RavenDbPersistenceLifecycle(Settings settings, DeferredRavenDocumentStore deferredRavenDocumentStore)
        {
            this.settings = settings;
            this.deferredRavenDocumentStore = deferredRavenDocumentStore;
        }

        public async Task Initialize()
        {
            if (ShouldStartServer(settings))
            {
                embeddedRavenDb = EmbeddedDatabase.Start(settings.DbPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            }
            else
            {
                embeddedRavenDb = new EmbeddedDatabase(settings.ExpirationProcessTimerInSeconds, settings.RavenDbConnectionString, settings.RunInMemory);
            }

            var documentStore = await embeddedRavenDb.PrepareDatabase(new AuditDatabaseConfiguration()).ConfigureAwait(false);
            deferredRavenDocumentStore.SetInnerDocumentStore(documentStore);
        }

        public ValueTask DisposeAsync()
        {
            embeddedRavenDb?.Dispose();
            return new ValueTask();
        }

        static bool ShouldStartServer(Settings settings)
        {
            if (settings.RunInMemory)
            {
                // We are probably running in a test context
                try
                {
                    EmbeddedServer.Instance.GetServerUriAsync().Wait();
                    // Embedded server is already running so we don't need to start it
                    return false;
                }
                catch
                {
                    // Embedded Server is not running
                    return true;
                }
            }

            return true;
        }

        Settings settings;
        EmbeddedDatabase embeddedRavenDb;
        DeferredRavenDocumentStore deferredRavenDocumentStore;
    }
}