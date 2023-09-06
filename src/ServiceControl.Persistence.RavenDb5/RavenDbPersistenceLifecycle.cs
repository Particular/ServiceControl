﻿namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Embedded;

    class RavenDbPersistenceLifecycle : IPersistenceLifecycle
    {
        public RavenDbPersistenceLifecycle(RavenStartup ravenStartup, EmbeddableDocumentStore documentStore)
        {
            this.ravenStartup = ravenStartup;
            this.documentStore = documentStore;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            Logger.Info("Database initialization starting");
            documentStore.Initialize();
            Logger.Info("Database initialization complete");

            await ravenStartup.CreateIndexesAsync(documentStore);

            Logger.Info("Testing indexes");
            await TestAllIndexesAndResetIfException(documentStore);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }

        static async Task TestAllIndexesAndResetIfException(IDocumentStore store)
        {
            foreach (var index in store.DatabaseCommands.GetStatistics().Indexes)
            {
                try
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        await session.Advanced.AsyncDocumentQuery<object>(index.Name).Take(1).ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"When trying to fetch 1 document from index {index.Name} the following exception was thrown: {ex}");
                    Logger.Warn($"Attempting to reset errored index: [{index.Name}] priority: {index.Priority} is valid: {index.IsInvalidIndex} indexing attempts: {index.IndexingAttempts}, failed indexing attempts:{index.IndexingErrors}");
                    store.DatabaseCommands.ResetIndex(index.Name);
                }
            }
        }

        readonly RavenStartup ravenStartup;
        readonly EmbeddableDocumentStore documentStore;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));
    }
}