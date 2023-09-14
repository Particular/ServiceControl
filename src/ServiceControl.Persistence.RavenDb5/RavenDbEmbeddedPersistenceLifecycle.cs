﻿namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Persistence;

    class RavenDbEmbeddedPersistenceLifecycle : IPersistenceLifecycle, IDisposable
    {
        public RavenDbEmbeddedPersistenceLifecycle(RavenDBPersisterSettings databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available until the persistence have been started, ensure lifecycle.Start is invoked before IHost.Start/Run ");
            }

            return documentStore;
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration);
            documentStore = await database.Connect(cancellationToken);
        }

        public void Dispose()
        {
            documentStore?.Dispose();
            database?.Dispose();
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;

        readonly RavenDBPersisterSettings databaseConfiguration;
    }
}