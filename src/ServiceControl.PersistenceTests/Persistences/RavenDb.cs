namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Operations;
    using Persistence;
    using Persistence.Tests;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class RavenDb : PersistenceDataStoreFixture
    {
        public override async Task SetupDataStore()
        {
            var serviceCollection = new ServiceCollection();

            documentStore = await serviceCollection.AddInitializedDocumentStore().ConfigureAwait(false);
            serviceCollection.AddServiceControlPersistence(DataStoreType.RavenDb);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
            UnitOfWorkFactory = serviceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();
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