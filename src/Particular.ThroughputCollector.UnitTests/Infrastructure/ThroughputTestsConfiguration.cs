namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.AuditThroughput;
    using Particular.ThroughputCollector.MonitoringThroughput;
    using Persistence;
    using Persistence.InMemory;
    using ServiceControl.Api;
    using ServiceControl.Transports;

    partial class ThroughputTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }
        public IThroughputCollector ThroughputCollector { get; protected set; }
        public ThroughputSettings ThroughputSettings { get; protected set; }
        public IAuditQuery AuditQuery { get; protected set; }
        public MonitoringService MonitoringService { get; protected set; }

        public Task Configure(Action<ThroughputSettings> setThroughputSettings, Action<ServiceCollection> setExtraDependencies)
        {
            var throughputSettings = new ThroughputSettings(serviceControlQueue: "Particular.ServiceControl", errorQueue: "error", transportType: "Learning", customerName: "TestCustomer", serviceControlVersion: "5.0.1");
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

            serviceCollection.AddSingleton<IEndpointsApi, FakeEndpointApi>();
            serviceCollection.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
            serviceCollection.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            serviceCollection.AddSingleton<IThroughputCollector, ThroughputCollector>();
            serviceCollection.AddSingleton<IBrokerThroughputQuery, FakeBrokerThroughputQuery>();
            serviceCollection.AddSingleton<IAuditQuery, AuditQuery>();
            serviceCollection.AddSingleton<MonitoringService>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
            ThroughputCollector = serviceProvider.GetRequiredService<IThroughputCollector>();
            ThroughputSettings = serviceProvider.GetRequiredService<ThroughputSettings>();
            AuditQuery = serviceProvider.GetRequiredService<IAuditQuery>();
            MonitoringService = serviceProvider.GetRequiredService<MonitoringService>();

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}