namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Embedded;
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using Raven.Client.Documents.Indexes;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            // TODO: Is there a more appropriate place to put this?
            EmbeddedServer.Instance.StartServer(new ServerOptions
            {
                DataDirectory = settings.DbPath,
                ServerUrl = settings.DatabaseMaintenanceUrl,
                AcceptEula = true
            });

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
    }
}
