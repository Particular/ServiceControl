namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, DataStoreConfiguration dataStoreToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostBuilder> hostBuilderCustomization)
        {
            this.customConfiguration = customConfiguration;
            this.dataStoreToUse = dataStoreToUse;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.setSettings = setSettings;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Settings Settings => runner.Settings;
        public OwinHttpMessageHandler Handler => runner.Handler;
        public string Port => runner.Port;
        public IDomainEvents DomainEvents => runner.DomainEvents;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, dataStoreToUse, setSettings, customConfiguration, hostBuilderCustomization);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        DataStoreConfiguration dataStoreToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<IHostBuilder> hostBuilderCustomization;
        ServiceControlComponentRunner runner;
    }
}