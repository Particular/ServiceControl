namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Embedded;
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using UnitOfWork;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using System.Threading.Tasks;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public async Task ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            EmbeddedDatabase embeddedRavenDb;
            // TODO: Is there a more appropriate place to put this?
            if (ShouldStartServer(settings))
            {
                embeddedRavenDb = EmbeddedDatabase.Start(settings.DbPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            }
            else
            {
                embeddedRavenDb = new EmbeddedDatabase(settings.ExpirationProcessTimerInSeconds, settings.RavenDbConnectionString, settings.RunInMemory);
            }

            var documentStore = await embeddedRavenDb.PrepareDatabase(new AuditDatabaseConfiguration()).ConfigureAwait(false);

            serviceCollection.AddSingleton(documentStore);

            //if (isSetup)
            //{
            //    var ravenOptions = new RavenStartup();
            //    foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
            //    {
            //        ravenOptions.AddIndexAssembly(indexAssembly);
            //    }

            //    var embeddedRaven = new EmbeddedRavenDbHostedService(documentStore, ravenOptions, new IDataMigration[0]);
            //    embeddedRaven.SetupDatabase().GetAwaiter().GetResult();
            //}
            //else
            //{
            //    serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
            //}

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
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
    }
}
