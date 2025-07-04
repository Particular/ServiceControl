﻿#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions.Database;
    using ServiceControl.RavenDB;

    sealed class RavenEmbeddedPersistenceLifecycle(RavenPersisterSettings databaseConfiguration, IHostApplicationLifetime lifetime, ILogger<RavenEmbeddedPersistenceLifecycle> logger)
        : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
    {
        public async ValueTask<IDocumentStore> GetDocumentStore(CancellationToken cancellationToken = default)
        {
            if (documentStore != null)
            {
                return documentStore;
            }

            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);
                return documentStore ?? throw new InvalidOperationException("Document store is not available. Ensure `IRavenPersistenceLifecycle.Initialize` is invoked");
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);

                // TODO: See if more can be refactored out of RavenPersisterSettings
                var embeddedConfig = new EmbeddedDatabaseConfiguration(databaseConfiguration.ServerUrl, databaseConfiguration.DatabaseName, databaseConfiguration.DatabasePath, databaseConfiguration.LogPath, databaseConfiguration.LogsMode);

                database = EmbeddedDatabase.Start(embeddedConfig, lifetime);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        documentStore = await database.Connect(cancellationToken);

                        var databaseSetup = new DatabaseSetup(databaseConfiguration, documentStore);
                        await databaseSetup.Execute(cancellationToken);

                        return;
                    }
                    catch (DatabaseLoadTimeoutException e)
                    {
                        logger.LogWarning(e, "Could not connect to database. Retrying in 500ms...");
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            if (database != null)
            {
                await database.Stop(cancellationToken);
            }
        }

        public void Dispose()
        {
            documentStore?.Dispose();
            database?.Dispose();
        }

        IDocumentStore? documentStore;
        EmbeddedDatabase? database;
        readonly SemaphoreSlim initializeSemaphore = new(1, 1);
    }
}