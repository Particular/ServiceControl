namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.ServerWide.Operations;
    using RavenDb;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            databaseName = Guid.NewGuid().ToString();
            Console.WriteLine($"DB Path: {dbPath}");

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/UseEmbeddedInstance"] = UseEmbeddedInstance.ToString();
            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/DatabaseName"] = databaseName;
            settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"] = dbPath;
            settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"] = FindAvailablePort(33334).ToString();
            settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"] = "localhost";

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

        public Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            DocumentStore?.Maintenance.Server.Send(new DeleteDatabasesOperation(
                new DeleteDatabasesOperation.Parameters() { DatabaseNames = new[] { databaseName }, HardDelete = true }));

            return persistenceLifecycle?.Stop();
        }

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }
        public string Name => "RavenDb5";

        public IDocumentStore DocumentStore { get; private set; }

        IPersistenceLifecycle persistenceLifecycle;

        string databaseName;

        const bool UseEmbeddedInstance = true;
    }
}