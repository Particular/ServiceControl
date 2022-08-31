namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    class InMemory : PersistenceTestFixture
    {
        public override Task SetupDataStore()
        {
            var settings = new Settings
            {
                DataStoreType = DataStoreType.InMemory
            };
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddServiceControlAuditPersistence(settings);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            return Task.CompletedTask;
        }

        public override Task CleanupDB()
        {
            return base.CleanupDB();
        }

        public override string ToString() => "In-Memory";
    }
}