namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    class RavenDb : PersistenceDataStoreFixture
    {
        public override Task SetupDataStore()
        {
            var settings = new Settings
            {
                DataStoreType = DataStoreType.RavenDb,
                RunInMemory = true
            };
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddServiceControlAuditPersistence(settings, false, true);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            return Task.CompletedTask;
        }

        public override Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return base.CompleteDBOperation();
        }

        public override Task CleanupDB()
        {
            DocumentStore?.Dispose();
            return base.CleanupDB();
        }

        public override string ToString() => "RavenDb";

        public IDocumentStore DocumentStore { get; private set; }
    }
}