#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions.Database;
    using ServiceControl.RavenDB;

    sealed class RavenEmbeddedPersistenceLifecycle(DatabaseConfiguration databaseConfiguration, IHostApplicationLifetime lifetime, ILogger<RavenEmbeddedPersistenceLifecycle> logger) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
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

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);

                var serverConfig = databaseConfiguration.ServerConfiguration;

                var embeddedConfig = new EmbeddedDatabaseConfiguration(serverConfig.ServerUrl, databaseConfiguration.Name, serverConfig.DbPath, serverConfig.LogPath, serverConfig.LogsMode)
                {
                    FindClrType = databaseConfiguration.FindClrType
                };

                database = EmbeddedDatabase.Start(embeddedConfig, lifetime);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        documentStore = await database.Connect(cancellationToken);

                        var databaseSetup = new DatabaseSetup(databaseConfiguration);
                        await databaseSetup.Execute(documentStore, cancellationToken);

                        return;
                    }
                    catch (DatabaseLoadTimeoutException e)
                    {
                        logger.LogWarning(e, "Connecting to the embedded RavenDB database timed out. Retrying in 500ms...");
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public Task Stop(CancellationToken cancellationToken = default) => database!.Stop(cancellationToken);

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
