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
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostApplicationBuilder> hostBuilderCustomization)
        {
            this.customConfiguration = customConfiguration;
            this.persistenceToUse = persistenceToUse;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.setSettings = setSettings;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Settings Settings => runner.Settings;
        public string Port => runner.Port;
        public IDomainEvents DomainEvents => runner.DomainEvents;
        public Func<HttpMessageHandler> HttpMessageHandlerFactory => runner.HttpMessageHandlerFactory;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, persistenceToUse, setSettings, customConfiguration, hostBuilderCustomization);
            await runner.Initialize(run);
            return runner;
        }

        readonly ITransportIntegration transportIntegration;
        readonly AcceptanceTestStorageConfiguration persistenceToUse;
        readonly Action<Settings> setSettings;
        readonly Action<EndpointConfiguration> customConfiguration;
        readonly Action<IHostApplicationBuilder> hostBuilderCustomization;
        ServiceControlComponentRunner runner;
    }
}