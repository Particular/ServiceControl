namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client.Documents;
    using Infrastructure.Settings;
    using NUnit.Framework;
    using Raven.Client.ServerWide.Operations;
    using RavenDb;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure()
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            Console.WriteLine($"DB Path: {dbPath}");

            var settings = new FakeSettings
            {
                // NOTE: Run in Memory is not an option
                RunInMemory = true,
                DbPath = dbPath
            };

            config.ConfigureServices(serviceCollection, settings, false, true);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
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
                new DeleteDatabasesOperation.Parameters() { DatabaseNames = new[] { "ServiceControlAudit" }, HardDelete = true }));
            DocumentStore?.Dispose();
            return Task.CompletedTask;
        }

        public override string ToString() => "RavenDb5";

        public IDocumentStore DocumentStore { get; private set; }

        class FakeSettings : Settings
        {
            //bypass the public ctor to avoid all mandatory settings
            public FakeSettings() : base()
            {
            }

            // Allow the server to pick it's binding (rather than checking config)
            public override string DatabaseMaintenanceUrl => null;
        }
    }
}