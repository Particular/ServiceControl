namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Threading.Tasks;
    using InMemory;
    using Microsoft.Extensions.DependencyInjection;

    partial class PersistenceTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }

        public Task Configure()
        {
            var serviceCollection = new ServiceCollection();

            var persistence = new InMemoryPersistence();
            persistence.Configure(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }

        public string Name => "InMemory";
    }
}