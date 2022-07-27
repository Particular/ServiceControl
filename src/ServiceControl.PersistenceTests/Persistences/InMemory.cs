namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Operations;
    using Persistence;
    using Persistence.Tests;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class InMemory : PersistenceDataStoreFixture
    {
        public override async Task SetupDataStore()
        {
            var serviceCollection = new ServiceCollection();
            fallback = await serviceCollection.AddInitializedDocumentStore().ConfigureAwait(false);
            serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
            UnitOfWorkFactory = serviceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();
        }

        public override Task CleanupDB()
        {
            fallback?.Dispose();
            return base.CleanupDB();
        }

        public override string ToString() => "In-Memory";

        EmbeddableDocumentStore fallback;
    }
}