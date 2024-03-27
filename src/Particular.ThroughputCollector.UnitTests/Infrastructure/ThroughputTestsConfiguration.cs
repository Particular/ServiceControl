namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
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
            var throughputSettings = new ThroughputSettings(broker: Broker.None, serviceControlQueue: "Particular.ServiceControl", errorQueue: "error", persistenceType: "InMemory", transportType: "Learning", customerName: "TestCustomer", serviceControlVersion: "5.0.1");
            setThroughputSettings(throughputSettings);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(throughputSettings);

            var config = new InMemoryPersistenceConfiguration();
            var settings = new PersistenceSettings();
            //TODO is this still needed since we are now storing this on settings and on the auditCommand?
            //throughputSettings.AuditQueues.ForEach(a => settings.PlatformEndpointNames.Add(a));
            //settings.PlatformEndpointNames.Add(throughputSettings.ErrorQueue);
            //settings.PlatformEndpointNames.Add(throughputSettings.ServiceControlQueue);
            serviceCollection.AddSingleton(settings);

            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

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

        public Task Cleanup() => Task.CompletedTask;
    }

    class FakeConfigurationApi : IConfigurationApi
    {
        public object GetConfig() => throw new NotImplementedException();

        public Task<object> GetRemoteConfigs(CancellationToken cancellationToken = default) => Task.FromResult<object>("[{ api_uri:\"http://localhost:44444\", status:\"online\", version: \"5.1.0\" }]");

        public RootUrls GetUrls(string baseUrl) => throw new NotImplementedException();
    }
}