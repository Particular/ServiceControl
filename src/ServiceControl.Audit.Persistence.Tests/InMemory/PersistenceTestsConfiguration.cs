namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Persistence.InMemory;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public Task Configure()
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            config.ConfigureServices(serviceCollection, null, false, true);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation() => Task.CompletedTask;

        public Task Cleanup() => Task.CompletedTask;
    }
}