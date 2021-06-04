namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Raven.Client;
    using Raven.Client.Indexes;

    class EmbeddedRavenDbHostedService : IHostedService
    {
        readonly IDocumentStore documentStore;
        readonly IEnumerable<IDataMigration> dataMigrations;
        readonly RavenStartup ravenStartup;

        public EmbeddedRavenDbHostedService(IDocumentStore documentStore, IEnumerable<IDataMigration> dataMigrations, IOptions<RavenStartup> ravenStartup)
        {
            this.documentStore = documentStore;
            this.dataMigrations = dataMigrations;
            this.ravenStartup = ravenStartup.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            documentStore.Initialize();

            var indexProvider = ravenStartup.CreateIndexProvider();
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);

            foreach (var migration in dataMigrations)
            {
                await migration.Migrate(documentStore)
                    .ConfigureAwait(false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }
    }
}