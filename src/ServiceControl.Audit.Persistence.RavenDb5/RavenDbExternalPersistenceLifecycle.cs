﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;

    class RavenDbExternalPersistenceLifecycle : IRavenDbPersistenceLifecycle
    {
        public RavenDbExternalPersistenceLifecycle(DatabaseConfiguration configuration)
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

        public Task Start(CancellationToken cancellationToken)
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

            store.Initialize();

            documentStore = store;

            return Task.CompletedTask;
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