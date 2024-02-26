namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.Persistence.InMemory;

    partial class PersistenceTestsConfiguration
    {
        public IThroughputDataStore AuditDataStore { get; protected set; }

        public Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            var settings = new PersistenceSettings();

            setSettings(settings);
            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation()
        {
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }

        public string Name => "InMemory";
    }
}