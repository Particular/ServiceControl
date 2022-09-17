namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client.Documents;
    using NUnit.Framework;
    using Raven.Client.ServerWide.Operations;
    using RavenDb;
    using UnitOfWork;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using System.Collections.Generic;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure()
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            Console.WriteLine($"DB Path: {dbPath}");

            var specificSettings = new Dictionary<string, string>()
            {
                { "RavenDb/RunInMemory",bool.TrueString},
                { "RavenDb/DbPath",dbPath}
            };

            var settings = new PersistenceSettings(specificSettings)
            {
                IsSetup = true
            };

            config.ConfigureServices(serviceCollection, settings);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
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
    }
}