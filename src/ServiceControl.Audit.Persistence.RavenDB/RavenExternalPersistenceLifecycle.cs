﻿namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;

    class RavenExternalPersistenceLifecycle : IRavenPersistenceLifecycle
    {
        public RavenExternalPersistenceLifecycle(DatabaseConfiguration configuration)
        {
            this.configuration = configuration;
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
            var store = new DocumentStore
            {
                Database = configuration.Name,
                Urls = new[] { configuration.ServerConfiguration.ConnectionString },
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            if (configuration.FindClrType != null)
            {
                store.Conventions.FindClrType += configuration.FindClrType;
            }

            documentStore = store.Initialize();

            var databaseSetup = new DatabaseSetup(configuration);
            await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;

        readonly DatabaseConfiguration configuration;
    }
}