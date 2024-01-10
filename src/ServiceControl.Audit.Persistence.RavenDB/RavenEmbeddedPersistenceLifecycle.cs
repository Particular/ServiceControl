﻿namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions.Database;

    class RavenEmbeddedPersistenceLifecycle : IRavenPersistenceLifecycle
    {
        public RavenEmbeddedPersistenceLifecycle(DatabaseConfiguration databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available until the persistence have been started");
            }

            return documentStore;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    documentStore = await database.Connect(cancellationToken);
                    return;
                }
                catch (DatabaseLoadTimeoutException e)
                {
                    Log.Warn("Could not connect to database. Retrying in 500ms...", e);
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();
            database?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;
        static readonly ILog Log = LogManager.GetLogger(typeof(RavenEmbeddedPersistenceLifecycle));
        readonly DatabaseConfiguration databaseConfiguration;
    }
}