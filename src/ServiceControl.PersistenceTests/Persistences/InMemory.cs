namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Persistence.Tests;
    using ServiceBus.Management.Infrastructure.Settings;

    class InMemory : PersistenceDataStoreFixture
    {
        public override Task SetupDataStore()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();

            return Task.CompletedTask;
        }

        public override string ToString() => "In-Memory";
    }
}