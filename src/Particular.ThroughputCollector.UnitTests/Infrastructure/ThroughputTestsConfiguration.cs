namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Particular.ThroughputCollector.Infrastructure;
    using Persistence;
    using Persistence.InMemory;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    partial class ThroughputTestsConfiguration
    {
        public IThroughputDataStore ThroughputDataStore { get; protected set; }
        public IThroughputCollector ThroughputCollector { get; protected set; }
        public ThroughputSettings ThroughputSettings { get; protected set; }

        public Task Configure(Action<ThroughputSettings> setThroughputSettings)
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            var settings = new PersistenceSettings();

            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

            var throughputSettings = new ThroughputSettings(broker: Broker.None, transportConnectionString: "", serviceControlAPI: "http://localhost:33333/api", serviceControlQueue: "Particular.ServiceControl", errorQueue: "error", persistenceType: "InMemory", customerName: "TestCustomer", serviceControlVersion: "5.0.1", auditQueue: "audit");
            setThroughputSettings(throughputSettings);
            serviceCollection.AddSingleton(throughputSettings);
            serviceCollection.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            serviceCollection.AddSingleton<IThroughputCollector, ThroughputCollector>();
            //serviceCollection.AddHostedService<AuditThroughputCollectorHostedService>();
            //serviceCollection.AddHostedService<BrokerThroughputCollectorHostedService>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
            ThroughputCollector = serviceProvider.GetRequiredService<IThroughputCollector>();
            ThroughputSettings = serviceProvider.GetRequiredService<ThroughputSettings>();

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }
    }

    class FakeConfigurationApi : IConfigurationApi
    {
        public object GetConfig() => throw new NotImplementedException();

        public Task<object> GetRemoteConfigs() => Task.FromResult<object>("[{ api_uri:\"http://localhost:44444\", status:\"online\", version: \"5.1.0\" }]");

        public RootUrls GetUrls(string baseUrl) => throw new NotImplementedException();
    }
}