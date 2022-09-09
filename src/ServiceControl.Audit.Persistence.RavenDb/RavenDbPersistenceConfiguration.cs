namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Infrastructure.Migration;
    using ServiceControl.Audit.Persistence.RavenDB;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            if (settings.IsSetup)
            {
                var ravenOptions = new RavenStartup();
                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    ravenOptions.AddIndexAssembly(indexAssembly);
                }

                var embeddedRaven = new EmbeddedRavenDbHostedService(documentStore, ravenOptions, new IDataMigration[0]);
                embeddedRaven.SetupDatabase().GetAwaiter().GetResult();
            }
            else
            {
                serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
            }

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            serviceCollection.Configure<RavenStartup>(database =>
            {
                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    database.AddIndexAssembly(indexAssembly);
                }
            });

            if (settings.IsSetup)
            {
                serviceCollection.AddTransient<IDataMigration, MigrateKnownEndpoints>();
            }
        }
    }
}
