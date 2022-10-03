namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Logging;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Infrastructure.Migration;
    using ServiceControl.Audit.Persistence.RavenDB;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistenceLifecycle ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            var ravenStartup = new RavenStartup();

            foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
            {
                ravenStartup.AddIndexAssembly(indexAssembly);
            }

            return new RavenDbPersistenceLifecycle(ravenStartup, documentStore);
        }

        public async Task Setup(PersistenceSettings settings, CancellationToken cancellationToken)
        {
            using (var documentStore = new EmbeddableDocumentStore())
            {
                RavenBootstrapper.Configure(documentStore, settings);

                var ravenStartup = new RavenStartup();

                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    ravenStartup.AddIndexAssembly(indexAssembly);
                }

                Logger.Info("Database initialization starting");
                documentStore.Initialize();
                Logger.Info("Database initialization complete");

                Logger.Info("Index creation started");
                var indexProvider = ravenStartup.CreateIndexProvider();
                await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                    .ConfigureAwait(false);
                Logger.Info("Index creation complete");

                Logger.Info("Data migrations starting");

                var endpointMigrations = new MigrateKnownEndpoints(documentStore);
                await endpointMigrations.Migrate(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                Logger.Info("Data migrations complete");
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceConfiguration));
    }
}
