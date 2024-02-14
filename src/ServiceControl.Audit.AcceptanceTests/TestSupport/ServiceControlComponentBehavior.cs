namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.Audit.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IDictionary<string, string>> setStorageConfiguration, Action<IHostApplicationBuilder> hostBuilderCustomization)
        {
            this.customConfiguration = customConfiguration;
            this.persistenceToUse = persistenceToUse;
            this.setSettings = setSettings;
            this.setStorageConfiguration = setStorageConfiguration;
            this.hostBuilderCustomization = hostBuilderCustomization;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerOptions SerializerOptions => runner.SerializerOptions;
        public IServiceProvider ServiceProvider => runner.ServiceProvider;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, persistenceToUse, setSettings, customConfiguration, setStorageConfiguration, hostBuilderCustomization);
            await runner.Initialize(run);
            return runner;
        }

        ITransportIntegration transportIntegration;
        AcceptanceTestStorageConfiguration persistenceToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        ServiceControlComponentRunner runner;
        Action<IDictionary<string, string>> setStorageConfiguration;
        Action<IHostApplicationBuilder> hostBuilderCustomization;
    }
}