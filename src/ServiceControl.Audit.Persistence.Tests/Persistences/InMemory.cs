namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    class InMemory : PersistenceDataStoreFixture
    {
        public override Task SetupDataStore()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
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