namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading.Tasks;
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

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();

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
        }

        public async Task Setup(PersistenceSettings settings)
        {
            using (var documentStore = new EmbeddableDocumentStore())
            {
                RavenBootstrapper.Configure(documentStore, settings);

                var ravenStartup = new RavenStartup();

                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    ravenStartup.AddIndexAssembly(indexAssembly);
                }

                var embeddedRaven = new EmbeddedRavenDbHostedService(documentStore, ravenStartup, new[] { new MigrateKnownEndpoints(documentStore) });

                await embeddedRaven.SetupDatabase()
                    .ConfigureAwait(false);
            }
        }
    }
}
