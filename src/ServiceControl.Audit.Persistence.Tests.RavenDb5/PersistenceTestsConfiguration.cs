namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.ServerWide.Operations;
    using RavenDb;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration : PersistenceTestsConfigurationBase
    {
        public PersistenceTestsConfiguration(string serverUrl) => this.serverUrl = serverUrl;

        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            databaseName = Guid.NewGuid().ToString();

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"] = serverUrl;
            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/DatabaseName"] = databaseName;

            setSettings(settings);

            await config.Setup(settings);
            persistenceLifecycle = config.ConfigureServices(serviceCollection, settings);

            await persistenceLifecycle.Start();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();

            var documentStoreProvider = serviceProvider.GetRequiredService<IRavenDbDocumentStoreProvider>();
            DocumentStore = documentStoreProvider.GetDocumentStore();
            var bulkInsert = DocumentStore.BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            var sessionProvider = serviceProvider.GetRequiredService<IRavenDbSessionProvider>();

            BodyStorage = new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, settings.MaxBodySizeToStore);
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
        }

        public override Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public override Task Cleanup()
        {
            DocumentStore?.Maintenance.Server.Send(new DeleteDatabasesOperation(
                new DeleteDatabasesOperation.Parameters() { DatabaseNames = new[] { databaseName }, HardDelete = true }));

            return persistenceLifecycle?.Stop();
        }

        public string Name => "RavenDb5";

        public IDocumentStore DocumentStore { get; private set; }

        IPersistenceLifecycle persistenceLifecycle;

        string databaseName;
        readonly string serverUrl;
    }
}