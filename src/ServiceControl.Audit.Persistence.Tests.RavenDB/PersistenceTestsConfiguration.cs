namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.RavenDB;
    using UnitOfWork;

    class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenPersistenceConfiguration();

            var hostBuilder = Host.CreateApplicationBuilder();

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(persistenceSettings);

            if (!persistenceSettings.PersisterSpecificSettings.ContainsKey(RavenPersistenceConfiguration.DatabasePathKey))
            {
                var instance = await SharedEmbeddedServer.GetInstance();

                persistenceSettings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = instance.ServerUrl;
            }

            if (!persistenceSettings.PersisterSpecificSettings.ContainsKey(RavenPersistenceConfiguration.LogPathKey))
            {
                persistenceSettings.PersisterSpecificSettings[RavenPersistenceConfiguration.LogPathKey] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs");
            }

            if (persistenceSettings.PersisterSpecificSettings.TryGetValue(RavenPersistenceConfiguration.DatabaseNameKey, out var configuredDatabaseName))
            {
                databaseName = configuredDatabaseName;
            }
            else
            {
                databaseName = Guid.NewGuid().ToString();

                persistenceSettings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseNameKey] = databaseName;
            }

            var persistence = config.Create(persistenceSettings);
            persistence.AddPersistence(hostBuilder.Services);
            persistence.AddInstaller(hostBuilder.Services);

            host = hostBuilder.Build();
            await host.StartAsync();

            AuditDataStore = host.Services.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();

            var documentStoreProvider = host.Services.GetRequiredService<IRavenDocumentStoreProvider>();
            DocumentStore = documentStoreProvider.GetDocumentStore();
            var bulkInsert = DocumentStore.BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            var sessionProvider = host.Services.GetRequiredService<IRavenSessionProvider>();

            BodyStorage = new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, persistenceSettings.MaxBodySizeToStore);
            AuditIngestionUnitOfWorkFactory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
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
                    new DeleteDatabasesOperation.Parameters { DatabaseNames = [databaseName], HardDelete = true }));
            }

            await host.StopAsync();
            host.Dispose();
        }

        public string Name => "RavenDB";

        public IDocumentStore DocumentStore { get; private set; }

        string databaseName;
        IHost host;
    }
}