namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Indexes;

    class EmbeddedRavenDbHostedService : IHostedService
    {
        readonly IDocumentStore documentStore;
        readonly RavenStartup ravenStartup;
        readonly IEnumerable<IDataMigration> dataMigrations;

        public EmbeddedRavenDbHostedService(IDocumentStore documentStore, IOptions<RavenStartup> ravenStartup, IEnumerable<IDataMigration> dataMigrations)
        : this(documentStore, ravenStartup.Value, dataMigrations)
        {
        }

        public EmbeddedRavenDbHostedService(IDocumentStore documentStore, RavenStartup ravenStartup, IEnumerable<IDataMigration> dataMigrations)
        {
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
            this.dataMigrations = dataMigrations;
        }

        public Task StartAsync(CancellationToken cancellationToken) => SetupDatabase();

        public async Task SetupDatabase()
        {
            Logger.Info("Database initialization starting");
            documentStore.Initialize();
            Logger.Info("Database initialization complete");

            Logger.Info("Index creation started");
            var indexProvider = ravenStartup.CreateIndexProvider();
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);
            Logger.Info("Index creation complete");

            Logger.Info("Data migrations starting");
            foreach (var dataMigration in dataMigrations)
            {
                await dataMigration.Migrate(documentStore)
                    .ConfigureAwait(false);
            }
            Logger.Info("Data migrations complete");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(EmbeddedRavenDbHostedService));
    }
}