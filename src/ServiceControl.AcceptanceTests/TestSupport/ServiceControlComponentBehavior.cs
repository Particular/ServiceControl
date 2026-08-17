namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, IAcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostApplicationBuilder> hostBuilderCustomization, Action<IHostApplicationBuilder> hostBuilderCustomizationBeforeServiceControl)
        {
            this.customConfiguration = customConfiguration;
            this.persistenceToUse = persistenceToUse;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.hostBuilderCustomizationBeforeServiceControl = hostBuilderCustomizationBeforeServiceControl;
            this.setSettings = setSettings;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerOptions SerializerOptions => runner.SerializerOptions;
        public Settings Settings => runner.Settings;
        public IDomainEvents DomainEvents => runner.DomainEvents;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, persistenceToUse, setSettings, customConfiguration, hostBuilderCustomization, hostBuilderCustomizationBeforeServiceControl);
            await runner.Initialize(run);
            return runner;
        }

        readonly ITransportIntegration transportIntegration;
        readonly IAcceptanceTestStorageConfiguration persistenceToUse;
        readonly Action<Settings> setSettings;
        readonly Action<EndpointConfiguration> customConfiguration;
        readonly Action<IHostApplicationBuilder> hostBuilderCustomization;
        readonly Action<IHostApplicationBuilder> hostBuilderCustomizationBeforeServiceControl;
        ServiceControlComponentRunner runner;
    }
}