﻿namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Raven.Client;
    using Raven.Client.Indexes;

    class EmbeddedRavenDbHostedService : IHostedService
    {
        readonly IDocumentStore documentStore;
        readonly IEnumerable<IDataMigration> dataMigrations;
        readonly ComponentInstallationContext installationContext;

        public EmbeddedRavenDbHostedService(IDocumentStore documentStore, IEnumerable<IDataMigration> dataMigrations, ComponentInstallationContext installationContext)
        {
            this.documentStore = documentStore;
            this.dataMigrations = dataMigrations;
            this.installationContext = installationContext;
        }

        public Task StartAsync(CancellationToken cancellationToken) => SetupDatabase();

        public async Task SetupDatabase()
        {
            documentStore.Initialize();

            var indexProvider = CreateIndexProvider(installationContext.IndexAssemblies);
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

        ExportProvider CreateIndexProvider(List<Assembly> indexAssemblies) =>
            new CompositionContainer(
                new AggregateCatalog(
                    from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
                )
            );
    }
}