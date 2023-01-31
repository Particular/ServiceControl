﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Embedded;
    using RavenDB;
    using ServiceControl.Audit.Persistence.RavenDb.CustomChecks;
    using UnitOfWork;

    class RavenDbPersistence : IPersistence
    {
        public RavenDbPersistence(PersistenceSettings settings, EmbeddableDocumentStore documentStore, RavenStartup ravenStartup)
        {
            this.settings = settings;
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
            serviceCollection.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

            return new RavenDbPersistenceLifecycle(ravenStartup, documentStore);
        }

        public IPersistenceInstaller CreateInstaller()
        {
            return new RavenDbInstaller(documentStore, ravenStartup);
        }

        readonly PersistenceSettings settings;
        readonly EmbeddableDocumentStore documentStore;
        readonly RavenStartup ravenStartup;
    }
}