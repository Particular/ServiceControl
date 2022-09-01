namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public Task Configure()
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

        public Task CompleteDBOperation() => Task.CompletedTask;

        public Task Cleanup() => Task.CompletedTask;
    }
}