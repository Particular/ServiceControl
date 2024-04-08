namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.AuditThroughput;
    using Persistence;
    using Persistence.InMemory;

    partial class ThroughputTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }
        public IThroughputCollector ThroughputCollector { get; protected set; }
        public ThroughputSettings ThroughputSettings { get; protected set; }
        public AuditQuery AuditQuery { get; protected set; }
        public IServiceProvider ServiceProvider { get; protected set; }

        public Task Configure(Action<ThroughputSettings> setThroughputSettings, Action<ServiceCollection> setExtraDependencies)
        {
            var throughputSettings = new ThroughputSettings(broker: Broker.None, serviceControlQueue: "Particular.ServiceControl", errorQueue: "error", persistenceType: "InMemory", transportType: "Learning", customerName: "TestCustomer", serviceControlVersion: "5.0.1");
            setThroughputSettings(throughputSettings);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(throughputSettings);

            serviceCollection.AddLogging();

            var config = new InMemoryPersistenceConfiguration();
            var settings = new PersistenceSettings();
            serviceCollection.AddSingleton(settings);

            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

            setExtraDependencies(serviceCollection);

            serviceCollection.AddSingleton<IThroughputCollector, ThroughputCollector>();
            serviceCollection.AddSingleton<AuditQuery>();
            //serviceCollection.AddHostedService<AuditThroughputCollectorHostedService>();
            //serviceCollection.AddHostedService<BrokerThroughputCollectorHostedService>();

            ServiceProvider = serviceCollection.BuildServiceProvider();

            ThroughputDataStore = ServiceProvider.GetRequiredService<IThroughputDataStore>();
            ThroughputCollector = ServiceProvider.GetRequiredService<IThroughputCollector>();
            ThroughputSettings = ServiceProvider.GetRequiredService<ThroughputSettings>();
            AuditQuery = ServiceProvider.GetRequiredService<AuditQuery>();

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}