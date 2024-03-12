namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Persistence.InMemory;

    partial class ThroughputTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }
        public IThroughputCollector ThroughputCollector { get; protected set; }
        public ThroughputSettings ThroughputSettings { get; protected set; }

        public Task Configure(Action<ThroughputSettings> setThroughputSettings, List<Endpoint> endpointsWithThroughput)
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            var settings = new PersistenceSettings();

            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

            var throughputSettings = new ThroughputSettings(broker: Broker.ServiceControl, transportConnectionString: "", serviceControlAPI: "http://localhost:33333/api", serviceControlQueue: "Particular.ServiceControl", errorQueue: "error", persistenceType: "InMemory", customerName: "TestCustomer", serviceControlVersion: "5.0.1", auditQueue: "audit");
            setThroughputSettings(throughputSettings);
            serviceCollection.AddSingleton(throughputSettings);
            serviceCollection.AddSingleton<IThroughputCollector, ThroughputCollector>();
            //serviceCollection.AddHostedService<AuditThroughputCollectorHostedService>();
            //serviceCollection.AddHostedService<BrokerThroughputCollectorHostedService>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
            ThroughputCollector = serviceProvider.GetRequiredService<IThroughputCollector>();
            ThroughputSettings = serviceProvider.GetRequiredService<ThroughputSettings>();

            endpointsWithThroughput.ForEach(async e =>
            {
                await ThroughputDataStore.RecordEndpointThroughput(e);
            });

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }
    }
}