namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Infrastructure.Settings;

    class RavenDb : PersistenceDataStoreFixture
    {
        public override async Task SetupDataStore()
        {
            var serviceCollection = new ServiceCollection();
            documentStore = await serviceCollection.AddInitializedDocumentStore().ConfigureAwait(false);
            serviceCollection.AddServiceControlPersistence(DataStoreType.RavenDb);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
        }

        public override Task CompleteDBOperation()
        {
            documentStore.WaitForIndexing();
            return base.CompleteDBOperation();
        }

        public override Task CleanupDB()
        {
            documentStore?.Dispose();
            return base.CleanupDB();
        }

        public override string ToString() => "RavenDb";

        EmbeddableDocumentStore documentStore;
    }
}