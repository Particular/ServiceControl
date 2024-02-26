namespace Particular.ThroughputCollector.Persistence.Tests.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Persistence.RavenDb;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;

    partial class PersistenceTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new PersistenceSettings();

            setSettings(settings);

            databaseName = string.Empty;
            //if (!settings.PersisterSpecificSettings.ContainsKey(RavenPersistenceConfiguration.DatabasePathKey))
            //{
            //    var instance = await SharedEmbeddedServer.GetInstance();

            //    settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = instance.ServerUrl;
            //}

            //if (!settings.PersisterSpecificSettings.ContainsKey(RavenPersistenceConfiguration.LogPathKey))
            //{
            //    settings.PersisterSpecificSettings[RavenPersistenceConfiguration.LogPathKey] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs");
            //}

            //if (settings.PersisterSpecificSettings.TryGetValue(RavenPersistenceConfiguration.DatabaseNameKey, out var configuredDatabaseName))
            //{
            //    databaseName = configuredDatabaseName;
            //}
            //else
            //{
            //    databaseName = Guid.NewGuid().ToString();

            //    settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseNameKey] = databaseName;
            //}

            var persistence = config.Create(settings);
            await persistence.CreateInstaller().Install();
            persistenceLifecycle = persistence.Configure(serviceCollection);
            await persistenceLifecycle.Start();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();

            var documentStoreProvider = serviceProvider.GetRequiredService<IRavenDocumentStoreProvider>();
            DocumentStore = documentStoreProvider.GetDocumentStore();
            //var bulkInsert = DocumentStore.BulkInsert(
            //    options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            //var sessionProvider = serviceProvider.GetRequiredService<IRavenSessionProvider>();
        }

        public Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public async Task Cleanup()
        {
            if (DocumentStore != null)
            {
                await DocumentStore.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(
                    new DeleteDatabasesOperation.Parameters() { DatabaseNames = new[] { databaseName }, HardDelete = true }));
            }

            if (persistenceLifecycle != null)
            {
                await persistenceLifecycle.Stop();
            }
        }

        public string Name => "RavenDB";

        public IDocumentStore DocumentStore { get; private set; }

        IPersistenceLifecycle persistenceLifecycle;

        string databaseName;
    }
}