namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.RavenDb;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure()
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new FakeSettings
            {
                RunInMemory = true
            };

            config.ConfigureServices(serviceCollection, settings, false, true);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
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
            DocumentStore?.Dispose();
            return Task.CompletedTask;
        }

        public override string ToString() => "RavenDb";

        public IDocumentStore DocumentStore { get; private set; }

        class FakeSettings : Settings
        {
            //bypass the public ctor to avoid all mandatory settings
            public FakeSettings() : base()
            {
            }
        }
    }
}