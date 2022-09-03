﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.RavenDb;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public Task Configure()
        {
            var settings = new Settings
            {
                DataStoreType = DataStoreType.RavenDb,
                RunInMemory = true,
                TransportCustomizationType = "TransportCustomization"
            };

            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            config.ConfigureServices(serviceCollection, settings, false, true);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            DocumentStore?.Dispose();
            return Task.CompletedTask;
        }

        public override string ToString() => "RavenDb";

        public IDocumentStore DocumentStore { get; private set; }
    }
}