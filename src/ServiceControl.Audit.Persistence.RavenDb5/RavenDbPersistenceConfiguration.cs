namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using UnitOfWork;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using Raven.Embedded;
    using Raven.Client.Documents;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            var documentStore = InitializeDatabase(settings, isSetup);

            serviceCollection.AddSingleton(documentStore);

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
        }

        IDocumentStore InitializeDatabase(Settings settings, bool isSetup)
        {
            if (ShouldStartServer(settings))
            {
                embeddedRavenDb = EmbeddedDatabase.Start(settings.DbPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl, settings.EnableFullTextSearchOnBodies);
            }
            else
            {
                embeddedRavenDb = new EmbeddedDatabase(settings.ExpirationProcessTimerInSeconds, settings.RavenDbConnectionString, settings.RunInMemory, settings.EnableFullTextSearchOnBodies);
            }

            return embeddedRavenDb.PrepareDatabase(new AuditDatabaseConfiguration(), isSetup).GetAwaiter().GetResult();
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

        EmbeddedDatabase embeddedRavenDb;
    }
}
