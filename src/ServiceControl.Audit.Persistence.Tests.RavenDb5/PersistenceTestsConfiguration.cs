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
    using RavenDb5;
    using UnitOfWork;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using Raven.Client.Documents.BulkInsert;
    using static Lucene.Net.Documents.Field;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure(Action<Settings> setSettings)
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
            setSettings(settings);

            serviceCollection.AddSingleton<Settings>(settings);

            config.ConfigureServices(serviceCollection, settings, false, true);

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
                new DeleteDatabasesOperation.Parameters() { DatabaseNames = new[] { new AuditDatabaseConfiguration().Name }, HardDelete = true }));
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