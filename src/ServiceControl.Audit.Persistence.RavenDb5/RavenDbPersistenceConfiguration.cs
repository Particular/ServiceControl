namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Embedded;
    using Auditing.BodyStorage;
    using Raven.Client.Documents.Indexes;
    using UnitOfWork;
    using System.Collections.Generic;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, IDictionary<string, string> settings, bool maintenanceMode, bool isSetup)
        {
            var runInMemory = false;
            if (settings.TryGetValue("RavenDb/RunInMemory", out var runInMemoryString))
            {
                runInMemory = bool.Parse(runInMemoryString);
            }
            var dbPath = "todo";
            var databaseMaintenanceUrl = "some url";

            // TODO: Is there a more appropriate place to put this?
            if (ShouldStartServer(runInMemory))
            {
                EmbeddedServer.Instance.StartServer(new ServerOptions
                {
                    DataDirectory = dbPath,
                    ServerUrl = databaseMaintenanceUrl,
                    AcceptEula = true
                });
            }

            var documentStore = EmbeddedServer.Instance.GetDocumentStore("ServiceControlAudit");
            // TODO: Set license
            // TODO: Use Run In Memory setting (if still available)

            // TODO: Maybe move this to hosted service
            IndexCreation.CreateIndexes(
                GetType().Assembly, documentStore
            );
            // TODO: Shut down gracefully


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

            //serviceCollection.Configure<RavenStartup>(database =>
            //{
            //    foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
            //    {
            //        database.AddIndexAssembly(indexAssembly);
            //    }
            //});
        }

        static bool ShouldStartServer(bool runInMemory)
        {
            if (runInMemory)
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
