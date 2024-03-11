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

    class ServiceControlComponentBehavior(
        ITransportIntegration transportToUse,
        AcceptanceTestStorageConfiguration persistenceToUse,
        Action<Settings> setSettings,
        Action<EndpointConfiguration> customConfiguration,
        Action<IDictionary<string, string>> setStorageConfiguration,
        Action<IHostApplicationBuilder> hostBuilderCustomization)
        : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerOptions SerializerOptions => runner.SerializerOptions;
        public IServiceProvider ServiceProvider => runner.ServiceProvider;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportToUse, persistenceToUse, setSettings, customConfiguration, setStorageConfiguration, hostBuilderCustomization);
            await runner.Initialize(run);
            return runner;
        }

        ServiceControlComponentRunner runner;
    }
}