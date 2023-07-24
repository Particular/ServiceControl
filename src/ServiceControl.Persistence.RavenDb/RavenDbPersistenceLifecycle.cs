namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceControl.Infrastructure.RavenDB;

    class RavenDbPersistenceLifecycle : IHostedService
    {
        public RavenDbPersistenceLifecycle(RavenStartup ravenStartup, EmbeddableDocumentStore documentStore, IEnumerable<IDataMigration> dataMigrations)
        {
            this.ravenStartup = ravenStartup;
            this.documentStore = documentStore;
            this.dataMigrations = dataMigrations;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Info("Database initialization starting");
            documentStore.Initialize();
            Logger.Info("Database initialization complete");

            await ravenStartup.CreateIndexesAsync(documentStore);

            Logger.Info("Testing indexes");
            await TestAllIndexesAndResetIfException();

            Logger.Info("Executing data migrations");
            await RunDataMigrations();
        }

        async Task RunDataMigrations()
        {
            foreach (var migration in dataMigrations)
            {
                Logger.InfoFormat("Executing migration {0}", migration.GetType());
                await migration.Migrate(documentStore);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }

        async Task TestAllIndexesAndResetIfException()
        {
            foreach (var index in documentStore.DatabaseCommands.GetStatistics().Indexes)
            {
                try
                {
                    using (var session = documentStore.OpenAsyncSession())
                    {
                        await session.Advanced.AsyncDocumentQuery<object>(index.Name).Take(1).ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"When trying to fetch 1 document from index {index.Name} the following exception was thrown: {ex}");
                    Logger.Warn($"Attempting to reset errored index: [{index.Name}] priority: {index.Priority} is valid: {index.IsInvalidIndex} indexing attempts: {index.IndexingAttempts}, failed indexing attempts:{index.IndexingErrors}");
                    documentStore.DatabaseCommands.ResetIndex(index.Name);
                }
            }
        }

        readonly RavenStartup ravenStartup;
        readonly EmbeddableDocumentStore documentStore;
        readonly IEnumerable<IDataMigration> dataMigrations;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));
    }
}