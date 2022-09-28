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
    using Raven.Embedded;
    using RavenDb;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            databaseName = Guid.NewGuid().ToString();
            Console.WriteLine($"DB Path: {dbPath}");

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000)
            {
                IsSetup = true
            };

            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/UseEmbeddedInstance"] = UseEmbeddedInstance.ToString();
            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/DatabaseName"] = databaseName;
            settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"] = dbPath;
            settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"] = FindAvailablePort(33334).ToString();
            settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"] = "localhost";


            setSettings(settings);

            config.ConfigureServices(serviceCollection, settings);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            var bulkInsert = DocumentStore.BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });
            BodyStorage = new RavenAttachmentsBodyStorage(DocumentStore, bulkInsert, settings.MaxBodySizeToStore);
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            return Task.CompletedTask;
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
            DocumentStore?.Dispose();

            if (UseEmbeddedInstance)
            {
                // it's test responsibility to clean up the embedded instance so that each test has a fresh instance available
                // otherwise the server will be running because its lifecycle depends on the test engine process but everything
                // else is disposed at the end of the test execution making so that the next test cannot run.
                EmbeddedServer.Instance.Dispose();
            }

            return Task.CompletedTask;
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

        string databaseName;

        const bool UseEmbeddedInstance = true;
    }
}